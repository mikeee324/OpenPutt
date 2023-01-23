
using Cyan.PlayerObjectPool;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OpenPutt : UdonSharpBehaviour
    {
        #region References
        [Header("This is the Top Level object for OpenPutt that acts as the main API endpoint and links player prefabs to global objects that don't need syncing.")]
        [Header("Internal References")]
        public CyanPlayerObjectPool objectPool;
        public CyanPlayerObjectAssigner objectAssigner;
        public ScoreboardManager scoreboardManager;
        public CourseManager[] courses;
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
        public float maxRefreshInterval { get; private set; }
        [HideInInspector, Tooltip("If enabled all players will receive score updates as they happen. If disabled scores will only be updated as each player finishes a hole.")]
        public PlayerSyncType playerSyncType = PlayerSyncType.All;
        public int MaxPlayerCount => objectPool != null ? objectPool.poolSize : 0;
        public int CurrentPlayerCount => objectPool != null && objectAssigner != null ? objectAssigner._GetActivePoolObjects().Length : 0;
        /// <summary>
        /// Get a list of players with active PlayerManager objects
        /// </summary>
        /// <returns></returns>
        public PlayerManager[] GetPlayers(bool noSort = false, bool hideInactivePlayers = true)
        {

            if (objectPool == null || objectAssigner == null)
                return new PlayerManager[0];

            // Grab a list of active PlayerManager objects
            int totalPlayers = 0;
            Component[] activeObjects = objectAssigner._GetActivePoolObjects();

            if (activeObjects == null)
                return new PlayerManager[0];

            for (int i = 0; i < activeObjects.Length; i++)
            {
                PlayerManager pm = activeObjects[i].GetComponent<PlayerManager>();
                if (pm.IsReady && (!hideInactivePlayers || pm.PlayerTotalScore > 0))
                    totalPlayers++;
            }

            int aPCount = 0;
            PlayerManager[] activePlayers = new PlayerManager[totalPlayers];
            for (int i = 0; i < activeObjects.Length; i++)
            {
                PlayerManager pm = activeObjects[i].GetComponent<PlayerManager>();
                if (pm.IsReady && (!hideInactivePlayers || pm.PlayerTotalScore > 0) && aPCount < totalPlayers)
                {
                    activePlayers[aPCount++] = activeObjects[i].GetComponent<PlayerManager>();
                }
            }

            if (noSort) return activePlayers;

            // Used to filter people who haven't started yet to the bottom
            int maxTotalScore = TotalMaxScore;
            int maxTotalTime = TotalMaxTime;

            // Sort the PlayerManager list by score ascending - TODO: Is there a better way of sorting lists in Udon#? (LinQ and IEnumurators doesn't exist here)
            PlayerManager temp = null;
            if (scoreboardManager.speedGolfMode)
            {
                for (int i = 0; i <= activePlayers.Length - 1; i++)
                {
                    for (int j = i + 1; j < activePlayers.Length; j++)
                    {
                        int score1 = activePlayers[i].PlayerTotalTime;
                        if (score1 <= 0)
                            score1 = maxTotalTime;
                        int score2 = activePlayers[j].PlayerTotalTime;
                        if (score2 <= 0)
                            score2 = maxTotalTime;
                        if (score1 > score2)
                        {
                            temp = activePlayers[i];
                            activePlayers[i] = activePlayers[j];
                            activePlayers[j] = temp;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i <= activePlayers.Length - 1; i++)
                {
                    for (int j = i + 1; j < activePlayers.Length; j++)
                    {
                        int score1 = activePlayers[i].PlayerTotalScore;
                        if (score1 <= 0)
                            score1 = maxTotalScore;
                        int score2 = activePlayers[j].PlayerTotalScore;
                        if (score2 <= 0)
                            score2 = maxTotalScore;
                        if (score1 > score2)
                        {
                            temp = activePlayers[i];
                            activePlayers[i] = activePlayers[j];
                            activePlayers[j] = temp;
                        }
                    }
                }
            }

            return activePlayers;
        }
        /// <summary>
        /// Convenience property to get the local players PlayerManager object (Assigned by Cyan Object Pool)<br/>
        /// <b>Can be null</b> if the pool hasn't been able to assign an object yet.
        /// </summary>
        public PlayerManager LocalPlayerManager
        {
            get
            {
                if (objectPool != null && objectAssigner != null)
                {
                    GameObject playerObject = objectAssigner._GetPlayerPooledObject(Networking.LocalPlayer);
                    if (playerObject != null)
                    {
                        return objectAssigner._GetPlayerPooledObject(Networking.LocalPlayer).GetComponent<PlayerManager>();
                    }
                }
                return null;
            }
        }
        /// <summary>
        /// Sums up the maximum score a player can get across all courses
        /// </summary>
        public int TotalMaxScore
        {
            get
            {
                int score = 0;
                foreach (CourseManager course in courses)
                    score += course.maxScore;
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
                    score += course.maxTimeMillis;
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
                    score += course.parScore;
                return score;
            }
        }
        public int TotalParTime
        {
            get
            {
                int score = 0;
                foreach (CourseManager course in courses)
                    score += course.parTimeMillis;
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
            {
                course.openPutt = this;
            }

            // Set Physics Timestep to 75FPS
            Time.fixedDeltaTime = 1.0f / 75f;

            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());
        }

        public override void OnDeserialization()
        {
            if (scoreboardManager != null)
                scoreboardManager.RequestRefresh();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            Utils.Log(this, "A player joined - sending a sync for local player manager!");
            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            UpdateRefreshSettings(VRCPlayerApi.GetPlayerCount());
        }

        public void UpdateRefreshSettings(int numberOfPlayers)
        {
            if (numberOfPlayers < 20)
                playerSyncType = PlayerSyncType.All;
            else if (numberOfPlayers < 40)
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
    }
}