using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
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
        public readonly string CurrentVersion = "0.9.1";

        #region References
        [OpenPuttDescription("The central OpenPutt controller for this world - it links all the player prefabs, courses and scoreboards together.")]
        [OpenPuttFoldoutGroup("Internal References")]
        public OpenPuttEventHandler eventHandler;

        [OpenPuttFoldoutGroup("Internal References")]
        public OpenPuttUIController uiController;

        [OpenPuttFoldoutGroup("Internal References")]
        public OpenPuttBallCam ballCam;

        [OpenPuttFoldoutGroup("Internal References")]
        public OpenPuttNotifications notifications;

        [OpenPuttFoldoutGroup("Internal References")]
        [Tooltip("The PlayerListManager keeps an ordered list of all players for the scoreboards to use")]
        public PlayerListManager playerListManager;

        [OpenPuttFoldoutGroup("Internal References")]
        [Tooltip("The ScoreboardManager looks after all scoreboards in the world (Moving them between positions and refreshing them)")]
        public ScoreboardManager scoreboardManager;

        [Tooltip("This array holds a reference to all Courses that you create for your world")]
        public CourseManager[] courses;

        [Tooltip("This array holds a reference to the little canvases that display hole numbers/names/par score")]
        public CourseMarker[] courseMarkers;

        [OpenPuttFoldoutGroup("Internal References")]
        [Tooltip("This curve is used to stop players sending syncs too often depending on the amount of players in the instance (If blank OpenPutt will populate this at runtime)")]
        public AnimationCurve syncTimeCurve;

        [OpenPuttFoldoutGroup("Local Player Objects")]
        public BodyMountedObject leftShoulderPickup;

        [OpenPuttFoldoutGroup("Local Player Objects")]
        public BodyMountedObject rightShoulderPickup;
        [OpenPuttFoldoutGroup("Local Player Objects")]
        public BodyMountedObject footCollider;
        [OpenPuttFoldoutGroup("Local Player Objects")]
        public ControllerTracker controllerTracker;
        [OpenPuttFoldoutGroup("Local Player Objects")]
        [FormerlySerializedAs("portableScoreboard")] public OpenPuttPortableMenu openPuttPortableScoreboard;
        [OpenPuttFoldoutGroup("Local Player Objects")]
        public SFXController sfxController;
        public AudioSource[] bgmAudioSources;
        public AudioSource[] worldAudioSources;

        public OpenPuttEventListener[] eventListeners;

        #endregion

        #region Game Settings

        [OpenPuttFoldoutGroup("Game Settings")]
        [UdonSynced, Tooltip("Toggles whether players can replay courses (Can be changed at runtime by the instance master)")]
        public bool replayableCourses;

        [OpenPuttFoldoutGroup("Game Settings")]
        [Tooltip("Allows balls to travel on the Y axis when hit by a club (Can be changed at runtime by the instance master) (Experimental)")]
        public bool enableVerticalHits;

        [OpenPuttFoldoutGroup("Game Settings")]
        [Tooltip("Allows players to play courses in any order (Just stops skipped courses showing up red on scoreboards)")]
        public bool coursesCanBePlayedInAnyOrder;

        [OpenPuttFoldoutGroup("Game Settings")]
        [Tooltip("When a player isn't playing a course, lets them switch to any golf club instead of being limited to the putter. Courses still control their own allowed clubs.")]
        public bool allowAnyClubOffCourse;

        [OpenPuttFoldoutGroup("Game Settings")]
        [Tooltip("Maximum amount of time in seconds that a players game state will be remembered for when using persistence. This only applies to incomplete games. Completed games will not be loaded back in and treated as a fresh start. (-1 = forever)")]
        public int scoreMaxPersistantTimeInSeconds = 900;

        [OpenPuttFoldoutGroup("Game Settings")]
        [UdonSynced, Tooltip("Enables dev mode for all players in the instance")]
        public bool enableDevModeForAll;

        #endregion

        #region Other Settings

        [OpenPuttFoldoutGroup("Other Settings")]
        [Tooltip("Advanced: Can be used to adjust the ball render queue values (Useful when wanting to make balls render through walls.. you may have to lower the render queue of your world materials for this to work)")]
        public int ballRenderQueueBase = 2000;

        [Tooltip("A list of players that can access the dev mode tab by default")]
        public string[] devModePlayerWhitelist = { "mikeee324", "TummyTime", "Narfe" };

        [OpenPuttFoldoutGroup("Other Settings")]
        [Tooltip("Enables logging for everybody in the instance (otherwise only whitelisted players will get logs)")]
        public bool debugMode;

        [OpenPuttFoldoutGroup("Other Settings")]
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
                    if (!Utilities.IsValid(course) || course.courseType == CourseType.DrivingRangeDistance || course.courseType == CourseType.DrivingRangeWithTargets)
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
                    if (!Utilities.IsValid(course) || course.courseType == CourseType.DrivingRangeDistance || course.courseType == CourseType.DrivingRangeWithTargets)
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
                    if (!Utilities.IsValid(course) || course.courseType == CourseType.DrivingRangeDistance || course.courseType == CourseType.DrivingRangeWithTargets)
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
                    if (!Utilities.IsValid(course) || course.courseType == CourseType.DrivingRangeDistance || course.courseType == CourseType.DrivingRangeWithTargets)
                        continue;
                    score += course.parTime;
                }

                return score;
            }
        }

        public string latestOpenPuttVer = "";
        public string openPuttChangelog = "";

        [HideInInspector]
        public bool hasUsedPortableScoreboard = false, hasUsedGolfClub = false, hasUsedGolfBall = false, hasChangedClubType = false;

        #endregion

        #region API Methods

        /// <summary>
        /// Registers an event listener if it's not already registered
        /// </summary>
        /// <param name="listener">The event listener to register</param>
        public void _RegisterEventListener(OpenPuttEventListener listener)
        {
            if (!eventListeners.Contains(listener))
            {
                eventListeners = eventListeners.Add(listener);

                // If we already have a local player manager, notify the new listener
                if (Utilities.IsValid(LocalPlayerManager))
                    listener.OnPlayerInitialised(Networking.LocalPlayer, LocalPlayerManager);
            }
        }

        /// <summary>
        /// Deregisters an event listener if it's currently registered
        /// </summary>
        /// <param name="listener">The event listener to deregister</param>
        public void _DeregisterEventListener(OpenPuttEventListener listener) => eventListeners = eventListeners.Remove(listener);

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

            _UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            Physics.bounceThreshold = 0.5f;

            SendCustomEventDelayedSeconds(nameof(_CheckForUpdate), 2f);
        }

        public override void OnDeserialization()
        {
            if (Utilities.IsValid(scoreboardManager))
                scoreboardManager.RefreshSettingsIfVisible();
        }

        public void _UpdateRefreshSettings(int numberOfPlayers)
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
            _UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            SendCustomEventDelayedFrames(nameof(_RemoveInvalidPlayerManagers), 2);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            _UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            SendCustomEventDelayedFrames(nameof(_RemoveInvalidPlayerManagers), 2);
        }

        public void _OnLocalPlayerInitialised(PlayerManager playerManager)
        {
            OpenPuttUtils.Log(this, $"Local player init");
            LocalPlayerManager = playerManager;

            if (Utilities.IsValid(eventHandler))
                eventHandler.OnPlayerInitialised(Networking.LocalPlayer, LocalPlayerManager);
        }

        public void _OnPlayerUpdate(PlayerManager playerManager)
        {
            if (!allPlayerManagers.Contains(playerManager))
                allPlayerManagers = allPlayerManagers.Add(playerManager);

            playerListManager.OnPlayerUpdate(playerManager);

            if (playerManager.Owner.isLocal)
            {
                _SavePersistantData();
                if (Utilities.IsValid(uiController))
                    uiController.UpdateDisplay();
            }
        }

        /// <summary>
        /// Updates the local players persistant save data
        /// </summary>
        public void _SavePersistantData()
        {
            if (!Utilities.IsValid(LocalPlayerManager)) return;

            // Save players ball colour
            PlayerData.SetColor("OpenPutt-BallColor", LocalPlayerManager.BallColor);
            PlayerData.SetBool("OpenPutt-LeftHanded", LocalPlayerManager.IsInLeftHandedMode);
            PlayerData.SetBool("OpenPutt-ThrowEnabled", LocalPlayerManager.golfClub.throwEnabled);
            PlayerData.SetBool("OpenPutt-ClubAutoHold", LocalPlayerManager.golfClub.pickup.AutoHold == VRC_Pickup.AutoHoldMode.Yes);

            // Save volume settings
            PlayerData.SetFloat("OpenPutt-SFXVol", sfxController.Volume);
            PlayerData.SetFloat("OpenPutt-WorldVol", worldAudioSources.Length > 0 ? worldAudioSources[0].volume : 1);
            PlayerData.SetFloat("OpenPutt-BGMVol", bgmAudioSources.Length > 0 ? bgmAudioSources[0].volume : 1);

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

        public void _LoadPersistantData()
        {
            var localPlayer = Networking.LocalPlayer;
            if (PlayerData.HasKey(localPlayer, "OpenPutt-BallColor"))
                LocalPlayerManager.BallColor = PlayerData.GetColor(localPlayer, "OpenPutt-BallColor");

            if (PlayerData.HasKey(localPlayer, "OpenPutt-ThrowEnabled"))
                LocalPlayerManager.golfClub.throwEnabled = PlayerData.GetBool(localPlayer, "OpenPutt-ThrowEnabled");
            else LocalPlayerManager.golfClub.throwEnabled = true;
            if (PlayerData.HasKey(localPlayer, "OpenPutt-LeftHanded"))
                LocalPlayerManager.IsInLeftHandedMode = PlayerData.GetBool(localPlayer, "OpenPutt-LeftHanded");
            else LocalPlayerManager.IsInLeftHandedMode = false;
            if (PlayerData.HasKey(localPlayer, "OpenPutt-ClubAutoHold"))
                LocalPlayerManager.golfClub.AutoHoldEnabled = PlayerData.GetBool(localPlayer, "OpenPutt-ClubAutoHold");
            else LocalPlayerManager.golfClub.AutoHoldEnabled = false;

            if (PlayerData.HasKey(localPlayer, "OpenPutt-SFXVol"))
                sfxController.Volume = PlayerData.GetFloat(localPlayer, "OpenPutt-SFXVol");

            if (PlayerData.HasKey(localPlayer, "OpenPutt-WorldVol"))
            {
                var vol = PlayerData.GetFloat(localPlayer, "OpenPutt-WorldVol");
                foreach (var worldAudio in worldAudioSources)
                    worldAudio.volume = vol;
            }

            if (PlayerData.HasKey(localPlayer, "OpenPutt-BGMVol"))
            {
                var vol = PlayerData.GetFloat(localPlayer, "OpenPutt-BGMVol");
                foreach (var bgmAudio in bgmAudioSources)
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

        public void _CheckForUpdate()
        {
#if !UNITY_EDITOR
            VRCStringDownloader.LoadUrl(versionURL, (IUdonEventReceiver)this);
#endif
        }

        public void _RemoveInvalidPlayerManagers()
        {
            var newPlayerManagers = new PlayerManager[0];
            for (var i = 0; i < allPlayerManagers.Length; i++)
                if (Utilities.IsValid(allPlayerManagers[i]))
                    newPlayerManagers = newPlayerManagers.Add(allPlayerManagers[i]);

            allPlayerManagers = newPlayerManagers;

            playerListManager.OnPlayerListChanged();
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