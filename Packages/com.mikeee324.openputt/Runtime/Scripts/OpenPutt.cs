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
        public string CurrentVersion { get; } = "0.8.6";

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

        [UdonSynced, Tooltip("Enables dev mode for all players in the instance")]
        public bool enableDevModeForAll;

        #endregion

        #region Other Settings

        [Header("Other Settings")] [Tooltip("Advanced: Can be used to adjust the ball render queue values (Useful when wanting to make balls render through walls.. you may have to lower the render queue of your world materials for this to work)")]
        public int ballRenderQueueBase = 2000;

        [Tooltip("A list of players that can access the dev mode tab by default")]
        public string[] devModePlayerWhitelist = { "mikeee324", "TummyTime" };

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

        public void OnPlayerUpdate(PlayerManager playerManager)
        {
            if (playerManager.Owner.isLocal)
                LocalPlayerManager = playerManager;

            if (!allPlayerManagers.Contains(playerManager))
                allPlayerManagers = allPlayerManagers.Add(playerManager);

            playerListManager.OnPlayerUpdate();
        }

        /// <summary>
        /// Updates the local players persistant save data
        /// </summary>
        public void SavePersistantData()
        {
            // Save players ball colour
            PlayerData.SetColor("OpenPutt-BallColor", LocalPlayerManager.BallColor);

            // Save volume settings
            PlayerData.SetFloat("OpenPutt-SFXVol", SFXController.Volume);
            PlayerData.SetFloat("OpenPutt-WorldVol", WorldAudioSources.Length > 0 ? WorldAudioSources[0].volume : 1);
            PlayerData.SetFloat("OpenPutt-BGMVol", BGMAudioSources.Length > 0 ? BGMAudioSources[0].volume : 1);
        }

        public void LoadPersistantData()
        {
            var localPlayer = Networking.LocalPlayer;
            if (PlayerData.HasKey(localPlayer, "OpenPutt-BallColor"))
                LocalPlayerManager.BallColor = PlayerData.GetColor(localPlayer, "OpenPutt-BallColor");

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