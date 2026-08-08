#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Checks the latest OpenPutt version by downloading Ver.txt (the same file the runtime uses)
    /// from GitHub Pages and comparing it against the version in the project. Ver.txt's first line
    /// is the latest version, the rest is the changelog. The automatic check is throttled: it only
    /// hits the network at most once every <see cref="AutoCheckIntervalHours"/> hours (tracked in
    /// EditorPrefs so it survives domain reloads and editor restarts), otherwise it reuses the
    /// cached result. The manual "Check for Updates" button bypasses the throttle.
    /// </summary>
    public static class OpenPuttVersionCheck
    {
        public enum State { Idle, Checking, UpToDate, UpdateAvailable, Failed }

        const string VersionUrl = "https://mikeee324.github.io/OpenPutt/Ver.txt";
        public const string ReleasesPage = "https://github.com/mikeee324/OpenPutt/releases";

        const double AutoCheckIntervalHours = 6;
        const string LastCheckKey = "OpenPutt.VersionCheck.LastCheckUtc";
        const string CachedLatestKey = "OpenPutt.VersionCheck.LatestVersion";
        const string CachedChangelogKey = "OpenPutt.VersionCheck.Changelog";

        public static State CurrentState { get; private set; } = State.Idle;
        public static string LatestVersion { get; private set; } = "";
        public static string Changelog { get; private set; } = "";

        /// <summary>
        /// Resolves the update state without necessarily hitting the network. Uses the cached
        /// result if we checked recently, only fetching when the throttle window has elapsed.
        /// Safe to call every OnGUI frame - it no-ops once a result is resolved for the session.
        /// </summary>
        public static void AutoCheck(string currentVersion, Action onComplete)
        {
            if (CurrentState != State.Idle)
                return; // already checking or resolved this session

            string cachedLatest = EditorPrefs.GetString(CachedLatestKey, "");
            if (!string.IsNullOrEmpty(cachedLatest) && HoursSinceLastCheck() < AutoCheckIntervalHours)
            {
                // Still fresh - reuse the cached result instead of asking GitHub again.
                LatestVersion = cachedLatest;
                Changelog = EditorPrefs.GetString(CachedChangelogKey, "");
                CurrentState = IsNewer(cachedLatest, Normalize(currentVersion)) ? State.UpdateAvailable : State.UpToDate;
                return;
            }

            CheckForUpdates(currentVersion, onComplete);
        }

        private static double HoursSinceLastCheck()
        {
            string raw = EditorPrefs.GetString(LastCheckKey, "");
            if (long.TryParse(raw, out long binary))
                return (DateTime.UtcNow - DateTime.FromBinary(binary)).TotalHours;
            return double.MaxValue;
        }

        public static void CheckForUpdates(string currentVersion, Action onComplete)
        {
            if (CurrentState == State.Checking)
                return;

            CurrentState = State.Checking;
            LatestVersion = "";

            UnityWebRequest request = UnityWebRequest.Get(VersionUrl);

            string current = Normalize(currentVersion);

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        OpenPuttUtils.LogWarning("OpenPutt Update", $"Couldn't check for updates: {request.error}", "orange");
                        CurrentState = State.Failed;
                    }
                    else
                    {
                        // Ver.txt: first line is the latest version, the rest is the changelog.
                        string text = request.downloadHandler.text ?? "";
                        int newline = text.IndexOf('\n');
                        LatestVersion = Normalize(newline >= 0 ? text.Substring(0, newline) : text);
                        Changelog = (newline >= 0 ? text.Substring(newline + 1) : "").Trim();

                        if (string.IsNullOrEmpty(LatestVersion))
                        {
                            CurrentState = State.Failed;
                        }
                        else
                        {
                            // Only record the throttle timestamp on a good response so failures retry next time.
                            EditorPrefs.SetString(LastCheckKey, DateTime.UtcNow.ToBinary().ToString());
                            EditorPrefs.SetString(CachedLatestKey, LatestVersion);
                            EditorPrefs.SetString(CachedChangelogKey, Changelog);

                            if (IsNewer(LatestVersion, current))
                            {
                                CurrentState = State.UpdateAvailable;
                                OpenPuttUtils.Log("OpenPutt Update", $"A new version is available: {LatestVersion} (you have {current})", "cyan");
                            }
                            else
                            {
                                CurrentState = State.UpToDate;
                                OpenPuttUtils.Log("OpenPutt Update", $"You're on the latest version ({current})", "cyan");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    OpenPuttUtils.LogWarning("OpenPutt Update", $"Failed to parse update info: {e.Message}", "orange");
                    CurrentState = State.Failed;
                }
                finally
                {
                    request.Dispose();
                    onComplete?.Invoke();
                }
            };
        }

        private static string Normalize(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "";
            version = version.Trim();
            if (version.Length > 0 && (version[0] == 'v' || version[0] == 'V'))
                version = version.Substring(1);
            return version;
        }

        /// <summary>Numeric, dot-separated comparison. True if latest is higher than current.</summary>
        public static bool IsNewer(string latest, string current)
        {
            string[] a = latest.Split('.');
            string[] b = current.Split('.');
            int len = Mathf.Max(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                int ai = i < a.Length ? ParseLeadingInt(a[i]) : 0;
                int bi = i < b.Length ? ParseLeadingInt(b[i]) : 0;
                if (ai != bi)
                    return ai > bi;
            }
            return false;
        }

        private static int ParseLeadingInt(string s)
        {
            // Only read leading digits so suffixes like "1-beta" don't break the compare.
            int end = 0;
            while (end < s.Length && char.IsDigit(s[end]))
                end++;
            int.TryParse(s.Substring(0, end), out int value);
            return value;
        }
    }
}
#endif
