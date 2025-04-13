using System;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDK3.Persistence;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OpenPutt : UdonSharpBehaviour
    {
        [NonSerialized]
        public readonly string CurrentVersion = "0.8.30";

        #region References

        [Header("This is the Top Level object for OpenPutt that acts as the main API endpoint and links player prefabs to global objects that don't need syncing.")] [Header("Internal References")] [Tooltip("The PlayerListManager keeps an ordered list of all players for the scoreboards to use")]
        public PlayerListManager playerListManager;

        [Tooltip("The ScoreboardManager looks after all scoreboards in the world (Moving them between positions and refreshing them)")]
        public ScoreboardManager scoreboardManager;

        [Tooltip("This array holds a reference to all Courses that you create for your world")]
        public CourseManager[] courses;

        [Tooltip("This array holds a reference to the little canvases that display hole numbers/names/par score")]
        public CourseMarker[] courseMarkers;

        [Tooltip("This curve is used to stop players sending syncs too often depending on the amount of players in the instance (If blank OpenPutt will populate this at runtime)")]
        public AnimationCurve syncTimeCurve;

        [Header("Local Player Objects")]
        public BodyMountedObject leftShoulderPickup;

        public BodyMountedObject rightShoulderPickup;
        public BodyMountedObject footCollider;
        public ControllerTracker controllerTracker;
        public PortableMenu portableScoreboard;
        public SFXController SFXController;
        public AudioSource[] BGMAudioSources;
        public AudioSource[] WorldAudioSources;
        public DesktopModeController desktopModeController;
        public DesktopModeCameraController desktopModeCameraController;

        [Header("External References")]
        public OpenPuttEventListener[] eventListeners;

        #endregion

        #region Game Settings

        [Header("Game Settings")] [UdonSynced, Tooltip("Toggles whether players can replay courses (Can be changed at runtime by the instance master)")]
        public bool replayableCourses;

        [UdonSynced, Tooltip("Allows balls to travel on the Y axis when hit by a club (Can be changed at runtime by the instance master) (Experimental)")]
        public bool enableVerticalHits;

        [Tooltip("Allows players to play courses in any order (Just stops skipped courses showing up red on scoreboards)")]
        public bool coursesCanBePlayedInAnyOrder;

        [Tooltip("Maximum amount of time in seconds that a players game state will be remembered for when using persistence. This only applies to incomplete games. Completed games will not be loaded back in and treated as a fresh start. (-1 = forever)")]
        public int scoreMaxPersistantTimeInSeconds = 900;

        [UdonSynced, Tooltip("Enables dev mode for all players in the instance")]
        public bool enableDevModeForAll;

        #endregion

        #region Other Settings

        [Header("Other Settings")] [Tooltip("Advanced: Can be used to adjust the ball render queue values (Useful when wanting to make balls render through walls.. you may have to lower the render queue of your world materials for this to work)")]
        public int ballRenderQueueBase = 2000;

        [Tooltip("A list of players that can access the dev mode tab by default")]
        public string[] devModePlayerWhitelist = { "mikeee324", "TummyTime", "Narfe" };

        [Tooltip("Enables logging for everybody in the instance (otherwise only whitelisted players will get logs)")]
        public bool debugMode;

        public VRCUrl versionURL;

        #endregion

        #region API

        public PlayerManager[] PlayersSortedByScore => playerListManager.PlayersSortedByScore;
        public PlayerManager[] PlayersSortedByTime => playerListManager.PlayersSortedByTime;
        public float maxRefreshInterval { get; private set; }

        /// <summary>
        /// Determines when PlayerManagers will sync their score data (Used to reduce network traffic when there's lots of players)
        /// </summary>
        [HideInInspector]
        public PlayerSyncType playerSyncType = PlayerSyncType.All;

        /// <summary>
        /// Returns the current number of players that have started playing at least 1 course
        /// </summary>
        public int CurrentPlayerCount => Utilities.IsValid(playerListManager) && Utilities.IsValid(playerListManager.PlayersSortedByScore) ? playerListManager.PlayersSortedByScore.Length : 0;

        /// <summary>
        /// Keeps a reference to the PlayerManager that is assigned to the local player
        /// </summary>
        [HideInInspector]
        public PlayerManager LocalPlayerManager;

        /// <summary>
        /// Is a list of all PlayerManagers from the object pool - Can be used as a shortcut to getting player data without any GetComponent calls etc
        /// </summary>
        [HideInInspector]
        public PlayerManager[] allPlayerManagers = new PlayerManager[0];

        /// <summary>
        /// Sums up the maximum score a player can get across all courses
        /// </summary>
        public int TotalMaxScore
        {
            get
            {
                var score = 0;
                foreach (var course in courses)
                {
                    if (!Utilities.IsValid(course) || course.drivingRangeMode)
                        continue;
                    score += course.maxScore;
                }

                return score;
            }
        }

        /// <summary>
        /// Sums up the maximum time a player can score across all courses
        /// </summary>
        public int TotalMaxTime
        {
            get
            {
                var score = 0;
                foreach (var course in courses)
                {
                    if (!Utilities.IsValid(course) || course.drivingRangeMode)
                        continue;
                    score += course.maxTime;
                }

                return score;
            }
        }

        /// <summary>
        /// Sums up the par on all courses
        /// </summary>
        public int TotalParScore
        {
            get
            {
                var score = 0;
                foreach (var course in courses)
                {
                    if (!Utilities.IsValid(course) || course.drivingRangeMode)
                        continue;
                    score += course.parScore;
                }

                return score;
            }
        }

        public int TotalParTime
        {
            get
            {
                var score = 0;
                foreach (var course in courses)
                {
                    if (!Utilities.IsValid(course) || course.drivingRangeMode)
                        continue;
                    score += course.parTime;
                }

                return score;
            }
        }

        public string latestOpenPuttVer = "";
        public string openPuttChangelog = "";

        #endregion

        void Start()
        {
#if UNITY_EDITOR
            //Force debug mode on in editor
            debugMode = true;
#endif

            if (courses.Length == 0)
            {
                OpenPuttUtils.LogError(this, "Missing some references! Please check everything is assigned correctly in the inspector. Disabling OpenPutt..");
                gameObject.SetActive(false);
                return;
            }

            foreach (var marker in courseMarkers)
                marker.ResetUI();

            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            Physics.bounceThreshold = 0.5f;

            SendCustomEventDelayedSeconds(nameof(CheckForUpdate), 2f);
        }

        public override void OnDeserialization()
        {
            if (Utilities.IsValid(scoreboardManager))
                scoreboardManager.RefreshSettingsIfVisible();
        }

        public void UpdateRefreshSettings(int numberOfPlayers)
        {
            if (numberOfPlayers < 10)
                playerSyncType = PlayerSyncType.All;
            else if (numberOfPlayers < 20)
                playerSyncType = PlayerSyncType.StartAndFinish;
            else
                playerSyncType = PlayerSyncType.FinishOnly;

            // In case the refresh curve has been blanked out in unity, set up the default curve for refresh intervals
            if (!Utilities.IsValid(syncTimeCurve) || syncTimeCurve.length == 0)
            {
                syncTimeCurve = new AnimationCurve();
                syncTimeCurve.AddKey(0f, 2f);
                syncTimeCurve.AddKey(20f, 5f);
                syncTimeCurve.AddKey(40f, 10f);
                syncTimeCurve.AddKey(82f, 11f);
            }

            maxRefreshInterval = syncTimeCurve.Evaluate(numberOfPlayers);
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            SendCustomEventDelayedFrames(nameof(RemoveInvalidPlayerManagers), 2);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            SendCustomEventDelayedFrames(nameof(RemoveInvalidPlayerManagers), 2);
        }

        public void OnLocalPlayerInitialised(PlayerManager playerManager)
        {
            OpenPuttUtils.Log(this, $"Local player init");
            LocalPlayerManager = playerManager;

            // Only call this for the first time we see it
            foreach (var listener in eventListeners)
                listener.OnLocalPlayerInitialised(LocalPlayerManager);
        }

        public void OnPlayerUpdate(PlayerManager playerManager)
        {
            if (!allPlayerManagers.Contains(playerManager))
                allPlayerManagers = allPlayerManagers.Add(playerManager);

            playerListManager.OnPlayerUpdate();

            if (playerManager.Owner.isLocal)
                SavePersistantData();
        }

        /// <summary>
        /// Updates the local players persistant save data
        /// </summary>
        public void SavePersistantData()
        {
            if (!Utilities.IsValid(LocalPlayerManager)) return;

            // Save players ball colour
            PlayerData.SetColor("OpenPutt-BallColor", LocalPlayerManager.BallColor);
            PlayerData.SetBool("OpenPutt-LeftHanded", LocalPlayerManager.IsInLeftHandedMode);
            PlayerData.SetBool("OpenPutt-ThrowEnabled", LocalPlayerManager.golfClub.throwEnabled);

            // Save volume settings
            PlayerData.SetFloat("OpenPutt-SFXVol", SFXController.Volume);
            PlayerData.SetFloat("OpenPutt-WorldVol", WorldAudioSources.Length > 0 ? WorldAudioSources[0].volume : 1);
            PlayerData.SetFloat("OpenPutt-BGMVol", BGMAudioSources.Length > 0 ? BGMAudioSources[0].volume : 1);

            // Save game state
            PlayerData.SetLong("OpenPuttGame-Modified", DateTime.UtcNow.GetUnixTimestamp());
            for (var courseID = 0; courseID < courses.Length; courseID++)
            {
                PlayerData.SetInt($"OpenPuttGame-State-{courseID}", (int)LocalPlayerManager.courseStates[courseID]);
                PlayerData.SetInt($"OpenPuttGame-Score-{courseID}", LocalPlayerManager.courseScores[courseID]);
                PlayerData.SetLong($"OpenPuttGame-Time-{courseID}", LocalPlayerManager.courseTimes[courseID]);
            }

            if (debugMode)
                OpenPuttUtils.Log(this, $"Persistent data saved for {LocalPlayerManager.Owner.displayName}");
        }

        public void LoadPersistantData()
        {
            var localPlayer = Networking.LocalPlayer;
            if (PlayerData.HasKey(localPlayer, "OpenPutt-BallColor"))
                LocalPlayerManager.BallColor = PlayerData.GetColor(localPlayer, "OpenPutt-BallColor");

            if (PlayerData.HasKey(localPlayer, "OpenPutt-ThrowEnabled"))
                LocalPlayerManager.golfClub.throwEnabled = PlayerData.GetBool(localPlayer, "OpenPutt-ThrowEnabled");
            if (PlayerData.HasKey(localPlayer, "OpenPutt-LeftHanded"))
                LocalPlayerManager.IsInLeftHandedMode = PlayerData.GetBool(localPlayer, "OpenPutt-LeftHanded");

            if (PlayerData.HasKey(localPlayer, "OpenPutt-SFXVol"))
                SFXController.Volume = PlayerData.GetFloat(localPlayer, "OpenPutt-SFXVol");

            if (PlayerData.HasKey(localPlayer, "OpenPutt-WorldVol"))
            {
                var vol = PlayerData.GetFloat(localPlayer, "OpenPutt-WorldVol");
                foreach (var worldAudio in WorldAudioSources)
                    worldAudio.volume = vol;
            }

            if (PlayerData.HasKey(localPlayer, "OpenPutt-BGMVol"))
            {
                var vol = PlayerData.GetFloat(localPlayer, "OpenPutt-BGMVol");
                foreach (var bgmAudio in BGMAudioSources)
                    bgmAudio.volume = vol;
            }

            if (PlayerData.HasKey(localPlayer, "OpenPuttGame-Modified"))
            {
                if (DateTime.UtcNow.GetUnixTimestamp() - PlayerData.GetLong(localPlayer, "OpenPuttGame-Modified") < scoreMaxPersistantTimeInSeconds)
                {
                    var atLeastOneIncompleteCourse = false;

                    for (var courseID = 0; courseID < courses.Length; courseID++)
                    {
                        if ((CourseState)PlayerData.GetInt(localPlayer, $"OpenPuttGame-State-{courseID}") != CourseState.Completed)
                        {
                            atLeastOneIncompleteCourse = true;
                            break;
                        }
                    }

                    // If there is at least one course that hasn't been finished yet
                    if (atLeastOneIncompleteCourse)
                    {
                        // Restore player game state
                        for (var courseID = 0; courseID < courses.Length; courseID++)
                        {
                            LocalPlayerManager.courseStates[courseID] = (CourseState)PlayerData.GetInt(localPlayer, $"OpenPuttGame-State-{courseID}");
                            LocalPlayerManager.courseScores[courseID] = PlayerData.GetInt(localPlayer, $"OpenPuttGame-Score-{courseID}");
                            LocalPlayerManager.courseTimes[courseID] = PlayerData.GetLong(localPlayer, $"OpenPuttGame-Time-{courseID}");

                            if (LocalPlayerManager.courseStates[courseID] == CourseState.Playing)
                            {
                                LocalPlayerManager.courseStates[courseID] = CourseState.NotStarted;
                                LocalPlayerManager.courseScores[courseID] = 0;
                                LocalPlayerManager.courseTimes[courseID] = 0;
                            }
                        }

                        if (debugMode)
                            OpenPuttUtils.Log(this, $"Restored player scores - updating scoreboards");

                        scoreboardManager.requestedScoreboardView = ScoreboardView.Scoreboard;
                    }
                    else
                    {
                        if (debugMode)
                            OpenPuttUtils.Log(this, $"Not restoring game state as player completed the entire course last time");
                    }
                }
            }
        }

        public void CheckForUpdate()
        {
            VRCStringDownloader.LoadUrl(versionURL, (IUdonEventReceiver)this);
        }

        public void RemoveInvalidPlayerManagers()
        {
            var newPlayerManagers = new PlayerManager[0];
            for (var i = 0; i < allPlayerManagers.Length; i++)
                if (Utilities.IsValid(allPlayerManagers[i]))
                    newPlayerManagers = newPlayerManagers.Add(allPlayerManagers[i]);

            allPlayerManagers = newPlayerManagers;

            playerListManager.OnPlayerUpdate();
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            openPuttChangelog = result.Result;
            if (Utilities.IsValid(openPuttChangelog) && openPuttChangelog.Length > 0 && openPuttChangelog.Contains("\n"))
                latestOpenPuttVer = openPuttChangelog.Substring(0, openPuttChangelog.IndexOf("\n"));

            if (!Utilities.IsValid(latestOpenPuttVer))
                latestOpenPuttVer = "";

            latestOpenPuttVer = latestOpenPuttVer.Trim();
        }
    }
}