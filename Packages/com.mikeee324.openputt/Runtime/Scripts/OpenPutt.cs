using Cyan.PlayerObjectPool;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OpenPutt : CyanPlayerObjectPoolEventListener
    {
        public string CurrentVersion { get; } = "0.6.4";

        #region References
        [Header("This is the Top Level object for OpenPutt that acts as the main API endpoint and links player prefabs to global objects that don't need syncing.")]
        [Header("Internal References")]
        [Tooltip("This is a reference to Cyans Player Object Pool")]
        public CyanPlayerObjectPool objectPool;
        [Tooltip("This is a reference to Cyans Player Object Pool Assigner")]
        public CyanPlayerObjectAssigner objectAssigner;
        [Tooltip("The PlayerListManager keeps an ordered list of all players for the scoreboards to use")]
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
        [Header("Game Settings")]
        [UdonSynced, Tooltip("Toggles whether players can replay courses (Can be changed at runtime by the instance master)")]
        public bool replayableCourses = false;
        [UdonSynced, Tooltip("Allows balls to travel on the Y axis when hit by a club (Can be changed at runtime by the instance master) (Experimental)")]
        public bool enableVerticalHits = false;
        [Tooltip("Allows players to play courses in any order (Just stops skipped courses showing up red on scoreboards)")]
        public bool coursesCanBePlayedInAnyOrder = false;
        [UdonSynced, Tooltip("Enables dev mode for all players in the instance")]
        public bool enableDevModeForAll = false;
        #endregion

        #region Other Settings
        [Header("Other Settings")]
        [Tooltip("Advanced: Can be used to adjust the ball render queue values (Useful when wanting to make balls render through walls.. you may have to lower the render queue of your world materials for this to work)")]
        public int ballRenderQueueBase = 2000;
        [Tooltip("A list of players that can access the dev mode tab by default")]
        public string[] devModePlayerWhitelist = new string[] { "mikeee324" };
        [Tooltip("Enables logging for everybody in the instance (otherwise only whitelisted players will get logs)")]
        public bool debugMode = false;
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
        /// Shortcut for asking the object pool for the size of the pool.
        /// </summary>
        public int MaxPlayerCount => objectPool != null ? objectPool.poolSize : 0;
        /// <summary>
        /// Returns the current number of players that have started playing at least 1 course
        /// </summary>
        public int CurrentPlayerCount => playerListManager != null && playerListManager.PlayersSortedByScore != null ? playerListManager.PlayersSortedByScore.Length : 0;

        /// <summary>
        /// Keeps a reference to the PlayerManager that is assigned to the local player
        /// </summary>
        [HideInInspector]
        public PlayerManager LocalPlayerManager = null;
        /// <summary>
        /// Is a list of all PlayerManagers from the object pool - Can be used as a shortcut to getting player data without any GetComponent calls etc
        /// </summary>
        [HideInInspector]
        public PlayerManager[] allPlayerManagers = null;

        /// <summary>
        /// Sums up the maximum score a player can get across all courses
        /// </summary>
        public int TotalMaxScore
        {
            get
            {
                int score = 0;
                foreach (CourseManager course in courses)
                {
                    if (course == null || course.drivingRangeMode)
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
                int score = 0;
                foreach (CourseManager course in courses)
                {
                    if (course == null || course.drivingRangeMode)
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
                int score = 0;
                foreach (CourseManager course in courses)
                {
                    if (course == null || course.drivingRangeMode)
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
                int score = 0;
                foreach (CourseManager course in courses)
                {
                    if (course == null || course.drivingRangeMode)
                        continue;
                    score += course.parTime;
                }
                return score;
            }
        }
        #endregion

        void Start()
        {

#if UNITY_EDITOR
            //Force debug mode on in editor
            debugMode = true;
#endif

            if (objectPool == null || objectAssigner == null || courses.Length == 0)
            {
                Utils.LogError(this, "Missing some references! Please check everything is assigned correctly in the inspector. Disabling OpenPutt..");
                gameObject.SetActive(false);
                return;
            }

            foreach (CourseMarker marker in courseMarkers)
                marker.ResetUI();

            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());
        }

        public override void OnDeserialization()
        {
            if (scoreboardManager != null)
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
            if (syncTimeCurve == null || syncTimeCurve.length == 0)
            {
                syncTimeCurve = new AnimationCurve();
                syncTimeCurve.AddKey(0f, 2f);
                syncTimeCurve.AddKey(20f, 5f);
                syncTimeCurve.AddKey(40f, 10f);
                syncTimeCurve.AddKey(82f, 11f);
            }

            maxRefreshInterval = syncTimeCurve.Evaluate(numberOfPlayers);
        }

        public override void _OnLocalPlayerAssigned()
        {
            if (LocalPlayerManager == null)
            {
                Utils.LogError(this, "LocalPlayerManager not found! Something bad happened!");
            }
        }

        public override void _OnPlayerAssigned(VRCPlayerApi player, int poolIndex, UdonBehaviour poolObject)
        {
            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            PlayerManager playerManager = allPlayerManagers[poolIndex];

            playerManager.openPutt = this;

            if (player.isLocal)
            {
                LocalPlayerManager = playerManager;

                if (!debugMode)
                    debugMode = devModePlayerWhitelist.Contains(player.displayName);
            }
        }

        public override void _OnPlayerUnassigned(VRCPlayerApi player, int poolIndex, UdonBehaviour poolObject)
        {
            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            PlayerManager playerManager = allPlayerManagers[poolIndex];

            if (player.isLocal)
                LocalPlayerManager = null;

            playerListManager.OnPlayerUpdate(playerManager);
        }

        public void OnPlayerUpdate(PlayerManager playerManager)
        {
            playerListManager.OnPlayerUpdate(playerManager);
        }
    }
}