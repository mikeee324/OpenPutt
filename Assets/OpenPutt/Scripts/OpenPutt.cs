﻿using Cyan.PlayerObjectPool;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OpenPutt : CyanPlayerObjectPoolEventListener
    {
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
        public SFXController SFXController;
        public AudioSource[] BGMAudioSources;
        public AudioSource[] WorldAudioSources;
        [Header("External References")]
        public OpenPuttEventListener[] eventListeners;
        #endregion

        #region Synced Settings
        [Header("Synced Settings")]
        [UdonSynced, Tooltip("Toggles whether players can replay courses (Can be changed at runtime by the instance master)")]
        public bool replayableCourses = false;
        [UdonSynced, Tooltip("Allows balls to travel on the Y axis when hit by a club (Experimental)")]
        public bool enableVerticalHits = false;
        #endregion

        #region Other Settings
        [Header("Other Settings")]
        [Tooltip("Advanced: Can be used to adjust the ball render queue values (Useful when wanting to make balls render through walls.. you may have to lower the render queue of your world materials for this to work)")]
        public int ballRenderQueueBase = 2000;
        #endregion

        #region API
        public PlayerManager[] PlayersSortedByScore => playerListManager.PlayersSortedByScore;
        public PlayerManager[] PlayersSortedByTime => playerListManager.PlayersSortedByTime;
        public float maxRefreshInterval { get; private set; }
        [HideInInspector, Tooltip("If enabled all players will receive score updates as they happen. If disabled scores will only be updated as each player finishes a hole.")]
        public PlayerSyncType playerSyncType = PlayerSyncType.All;
        public int MaxPlayerCount => objectPool != null ? objectPool.poolSize : 0;
        public int CurrentPlayerCount => objectPool != null && objectAssigner != null ? objectAssigner._GetActivePoolObjects().Length : 0;

        /// <summary>
        /// Keeps a reference to the PlayerManager that is assigned to the local player
        /// </summary>
        [HideInInspector]
        public PlayerManager LocalPlayerManager = null;

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
                    score += course.maxTimeMillis;
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
                    score += course.parTimeMillis;
                }
                return score;
            }
        }
        #endregion

        void Start()
        {
            if (objectPool == null || objectAssigner == null || courses.Length == 0)
            {
                Utils.LogError(this, "Missing some references! Please check everything is assigned correctly in the inspector. Disabling OpenPutt..");
                gameObject.SetActive(false);
                return;
            }

            // Automatically assign course numbers based on their position in the array so we don't get mixed up
            for (int i = 0; i < courses.Length; i++)
                courses[i].holeNumber = i;

            foreach (CourseMarker marker in courseMarkers)
                marker.ResetUI();

            // Allow the player managers access to the whole OpenPutt system (For updating scoreboards etc)
            for (int i = 0; i < MaxPlayerCount; i++)
            {
                PlayerManager pm = objectAssigner.transform.GetChild(i).GetComponent<PlayerManager>();
                if (pm != null)
                {
                    // Register the OpenPutt manager script with each player object
                    pm.openPutt = this;
                }
            }

            foreach (CourseManager course in courses)
                course.openPutt = this;

            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());
        }

        public override void OnDeserialization()
        {
            if (scoreboardManager != null)
                scoreboardManager.RefreshSettingsIfVisible();
        }

        public void UpdateRefreshSettings(int numberOfPlayers)
        {
            if (numberOfPlayers < 5)
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

            if (player.isLocal)
                LocalPlayerManager = poolObject.GetComponent<PlayerManager>();
        }

        public override void _OnPlayerUnassigned(VRCPlayerApi player, int poolIndex, UdonBehaviour poolObject)
        {
            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());

            PlayerManager playerManager = poolObject.GetComponent<PlayerManager>();

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