using System;
using System.Diagnostics;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public enum PlayerSyncType
    {
        /// <summary>
        /// All score updates will be displayed as soon as the refresh interval allows
        /// </summary>
        All,

        /// <summary>
        /// Only player course start/end updates will be synced
        /// </summary>
        StartAndFinish,

        /// <summary>
        /// Only player course finish events will be synced
        /// </summary>
        FinishOnly
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(99)]
    public class ScoreboardManager : OpenPuttTimeSlicer
    {
        [OpenPuttDescription("Handles refreshing all scoreboards in the scene and makes sure it doesn't happen too often.")]
        public OpenPutt openPutt;

        public Scoreboard[] scoreboards;
        public Scoreboard[] staticScoreboards;

        /// <summary>
        /// Used to avoid having to build a joined array of scoreboards+staticScoreboards after start
        /// </summary>
        private Scoreboard[] allScoreboards;

        public ScoreboardPositioner[] scoreboardPositions;

        [Space, OpenPuttFoldoutGroup("Settings")] [Range(0f, 10f), Tooltip("Amount of time in seconds that must pass before a refresh will begin")]
        public float maxRefreshInterval = .25f;

        [OpenPuttFoldoutGroup("Settings")]
        [Range(1f, 5f), Tooltip("Amount of time in seconds between scoreboard position updates")]
        public float scoreboardPositionUpdateInterval = 1f;

        [OpenPuttFoldoutGroup("Settings")]
        [Range(1, 82), Tooltip("The total number of players that the scoreboards can display at once (Large numbers can cause VERY long build times and maybe performance issues too - haven't tested it above 12)")]
        public int numberOfPlayersToDisplay = 12;

        [OpenPuttFoldoutGroup("Settings")]
        [Tooltip("This should stay enabled unless you're debugging the scoreboard player lists")]
        public bool hideInactivePlayers = true;

        [OpenPuttFoldoutGroup("Settings")]
        [Tooltip("Extra text that gets appended to the end of the credits text on every scoreboard")]
        [TextArea]
        public string extraCreditsText = "";

        [OpenPuttFoldoutGroup("Background Colours")]
        public Color nameBackground1 = Color.black;

        [OpenPuttFoldoutGroup("Background Colours")]
        public Color scoreBackground1 = Color.black;
        [OpenPuttFoldoutGroup("Background Colours")]
        public Color totalBackground1 = Color.black;

        [Space, OpenPuttFoldoutGroup("Background Colours")]
        public Color nameBackground2 = Color.black;

        [OpenPuttFoldoutGroup("Background Colours")]
        public Color scoreBackground2 = Color.black;
        [OpenPuttFoldoutGroup("Background Colours")]
        public Color totalBackground2 = Color.black;

        [Space, OpenPuttFoldoutGroup("Background Colours")]
        public Color currentCourseBackground = Color.black;

        [OpenPuttFoldoutGroup("Background Colours")]
        public Color underParBackground = Color.green;
        [OpenPuttFoldoutGroup("Background Colours")]
        public Color onParBackground = Color.green;
        [OpenPuttFoldoutGroup("Background Colours")]
        public Color overParBackground = Color.red;

        [OpenPuttFoldoutGroup("Text Colours")]
        public Color text = Color.white;

        [OpenPuttFoldoutGroup("Text Colours")]
        public Color currentCourseText = Color.white;
        [OpenPuttFoldoutGroup("Text Colours")]
        public Color underParText = Color.white;
        [OpenPuttFoldoutGroup("Text Colours")]
        public Color onParText = Color.white;
        [OpenPuttFoldoutGroup("Text Colours")]
        public Color overParText = Color.white;

        [OpenPuttFoldoutGroup("Prefab References")]
        public GameObject rowPrefab;

        [OpenPuttFoldoutGroup("Prefab References")]
        public GameObject colPrefab;

        #region Internal Vars

        /// <summary>
        /// Determines whether or not the local player currently has access to the dev mode tab
        /// </summary>
        public bool LocalPlayerCanAccessDevMode
        {
            get
            {
                if (openPutt.enableDevModeForAll)
                    return true;

                var localPlayerName = OpenPuttUtils.LocalPlayerIsValid() ? Networking.LocalPlayer.displayName : null;

                if (Utilities.IsValid(localPlayerName) && openPutt.devModePlayerWhitelist.Contains(localPlayerName))
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Determines whether or not the local player currently has access to the dev mode tab
        /// </summary>
        public bool LocalPlayerCanAccessToolbox
        {
            get
            {
                if (openPutt.enableDevModeForAll)
                    return true;

                var localPlayerName = OpenPuttUtils.LocalPlayerIsValid() ? Networking.LocalPlayer.displayName : null;

                if (Utilities.IsValid(localPlayerName) && openPutt.devModePlayerWhitelist.Contains(localPlayerName))
                    return true;

                if (devModeTaps >= 30)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Whether or not the scoreboard is showing course times instead of scores
        /// </summary>
        public bool SpeedGolfMode
        {
            get => _speedGolfMode;
            set
            {
                if (value != _speedGolfMode)
                {
                    RequestRefresh();
                }

                _speedGolfMode = value;
            }
        }

        private bool _speedGolfMode;

        /// <summary>
        /// Returns the last known list of players that was pushed out to all scoreboards
        /// </summary>
        public PlayerManager[] CurrentPlayerList { get; private set; }

        public int NumberOfColumns => Utilities.IsValid(openPutt) ? openPutt.courses.Length + 2 : 0;

        [HideInInspector]
        public ScoreboardView requestedScoreboardView = ScoreboardView.Info;

        private bool allScoreboardsInitialised;
        private int updatesRequested = 0;

        [HideInInspector]
        public int devModeTaps;

        #endregion

        void Start()
        {
            if (!Utilities.IsValid(openPutt))
            {
                OpenPuttUtils.LogError(this, "Missing OpenPutt Reference! Disabling Scoreboards");
                gameObject.SetActive(false);
                foreach (var scoreboard in scoreboards)
                    scoreboard.gameObject.SetActive(false);
                return;
            }

            foreach (var scoreboard in scoreboards)
                scoreboard.manager = this;

            foreach (var scoreboard in staticScoreboards)
                scoreboard.manager = this;

            allScoreboards = scoreboards.AddRange(staticScoreboards);

            numberOfObjects = numberOfPlayersToDisplay;
            enabled = false;

            // Update positions of all scoreboards a bit quicker at startup
            SendCustomEventDelayedSeconds(nameof(UpdateScoreboardVisibility), .5f);
        }

        public void RequestRefresh()
        {
            numberOfObjects = numberOfPlayersToDisplay;
            updatesRequested += 1;
            enabled = true;
        }

        protected override void _StartUpdateFrame()
        {
        }

        protected override void _EndUpdateFrame()
        {
        }

        protected override void _OnUpdateItem(int rowIDToUpdate)
        {
            if (!Utilities.IsValid(CurrentPlayerList)) return;
            // Update the row on this scoreboard
            Scoreboard scoreboard;

            for (var scoreboardID = 0; scoreboardID < allScoreboards.Length; scoreboardID++)
            {
                scoreboard = allScoreboards[scoreboardID];

                // Update the row for this player if we found one
                if (rowIDToUpdate == -1)
                {
                    if (scoreboard.topRowPanel.transform.childCount > 0)
                        scoreboard.topRowPanel.GetChild(0).GetComponent<ScoreboardPlayerRow>().Refresh();
                }
                else if (rowIDToUpdate == -2)
                {
                    if (scoreboard.parRowPanel.transform.childCount > 0)
                        scoreboard.parRowPanel.GetChild(0).GetComponent<ScoreboardPlayerRow>().Refresh();
                }
                else if (rowIDToUpdate >= 0 && rowIDToUpdate < scoreboard.scoreboardRows.Length)
                {
                    scoreboard.scoreboardRows[rowIDToUpdate].Refresh(rowIDToUpdate < CurrentPlayerList.Length ? CurrentPlayerList[rowIDToUpdate] : null);
                }
            }
        }

        protected override void _OnUpdateStarted()
        {
            CurrentPlayerList = GetPlayerList();
        }

        protected override void _OnUpdateEnded()
        {
            updatesRequested -= 1;

            if (updatesRequested <= 0)
            {
                updatesRequested = 0;
                enabled = false;
            }
        }

        /// <summary>
        /// Syncs up all the scoreboards so they display the same thing. Can take 1+ms, so don't call too often.
        /// </summary>
        public void UpdateScoreboardVisibility()
        {
            // Schedule the next update before anything else
            SendCustomEventDelayedSeconds(nameof(UpdateScoreboardVisibility), scoreboardPositionUpdateInterval);

            // Don't do anything if we can't find the local player
            if (!OpenPuttUtils.LocalPlayerIsValid())
                return;

            // After all scoreboard have initialised - queue a full refresh
            if (!allScoreboardsInitialised)
            {
                var queueBigRefresh = true;
                foreach (var scoreboard in scoreboards)
                {
                    if (!scoreboard.HasInitializedUI)
                    {
                        queueBigRefresh = false;
                        break;
                    }
                }

                if (queueBigRefresh)
                {
                    allScoreboardsInitialised = true;
                    enabled = true;
                }
                else
                {
                    return;
                }
            }

            var devModeEnabled = LocalPlayerCanAccessDevMode || LocalPlayerCanAccessToolbox;

            if (!devModeEnabled && requestedScoreboardView == ScoreboardView.DevMode)
                requestedScoreboardView = ScoreboardView.Scoreboard;

            for (var i = 0; i < allScoreboards.Length; i++)
            {
                if (i < scoreboards.Length)
                {
                    // Hide all managed scoreboards far down away from player (They will be moved to the correct position furhter down if they are visible)
                    allScoreboards[i].transform.SetPositionAndRotation(new Vector3(0, -100, 0), Quaternion.identity);
                    allScoreboards[i].transform.localScale = Vector3.zero;
                }

                // Sync up the current tab the player is looking at
                switch (requestedScoreboardView)
                {
                    case ScoreboardView.Settings:
                    case ScoreboardView.DevMode:
                        break;
                    default:
                        // Kick player out of dev mode if they don't have access anymore
                        if (!devModeEnabled && allScoreboards[i].CurrentScoreboardView == ScoreboardView.DevMode)
                            allScoreboards[i].CurrentScoreboardView = ScoreboardView.Scoreboard;

                        allScoreboards[i].CurrentScoreboardView = requestedScoreboardView;
                        break;
                }
            }

            var currentVisibleScoreboardID = 0;

            // Head tracking is always available and is what governs physical proximity (interaction range)
            var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            var headPosition = head.position;
            var headForward = head.rotation * Vector3.forward;
            const float headFieldOfView = 60f;

            // Camera the player might be looking through instead of/as well as their head (handheld photo cam takes priority over the desktop/screen cam)
            var viewCamera = VRCCameraSettings.ScreenCamera;
            var photoCamera = VRCCameraSettings.PhotoCamera;
            if (Utilities.IsValid(photoCamera) && photoCamera.Active)
                viewCamera = photoCamera;

            var cameraIsValid = Utilities.IsValid(viewCamera);
            var cameraPosition = cameraIsValid ? viewCamera.Position : default;
            var cameraForward = cameraIsValid ? viewCamera.Rotation * Vector3.forward : default;
            var cameraFieldOfView = cameraIsValid ? viewCamera.FieldOfView : 0f;

            // A positioner qualifies if either the player's head or their camera is looking at it. If they all fit in the pool there's no contest, so skip the distance ranking below
            var claimed = new bool[scoreboardPositions.Length];
            var distance = new float[scoreboardPositions.Length];
            var qualifyingCount = 0;
            for (var i = 0; i < scoreboardPositions.Length; i++)
            {
                var visibleFromHead = scoreboardPositions[i].ShouldBeVisible(headPosition, headForward, headFieldOfView, out var headDistance);

                var visibleFromCamera = false;
                var cameraDistance = headDistance;
                if (cameraIsValid)
                    visibleFromCamera = scoreboardPositions[i].ShouldBeVisible(cameraPosition, cameraForward, cameraFieldOfView, out cameraDistance);

                claimed[i] = visibleFromHead || visibleFromCamera;
                distance[i] = Mathf.Min(headDistance, cameraDistance);
                if (claimed[i])
                    qualifyingCount++;
            }

            // Pool is oversubscribed - drop the farthest positioners until what's left fits
            if (qualifyingCount > scoreboards.Length)
            {
                for (var excess = qualifyingCount - scoreboards.Length; excess > 0; excess--)
                {
                    var farthestIndex = -1;
                    for (var i = 0; i < claimed.Length; i++)
                    {
                        if (claimed[i] && (farthestIndex == -1 || distance[i] > distance[farthestIndex]))
                            farthestIndex = i;
                    }

                    claimed[farthestIndex] = false;
                }
            }

            for (var i = 0; i < scoreboardPositions.Length; i++)
            {
                var position = scoreboardPositions[i];
                var isVisibleHere = claimed[i];

                if (isVisibleHere)
                {
                    // Hide the background of the positioner
                    position.backgroundCanvas.enabled = false;

                    // Move this scoreboard to the correct position (pooled scoreboard prefabs face the opposite way to the positioner's forward)
                    var scoreboard = scoreboards[currentVisibleScoreboardID];
                    scoreboard.transform.SetPositionAndRotation(position.transform.position, position.transform.rotation * Quaternion.Euler(0f, 180f, 0f));

                    // Convert the positioner's world scale into a local scale relative to the scoreboard's parent
                    var targetScale = position.transform.lossyScale;
                    var scoreboardParent = scoreboard.transform.parent;
                    if (Utilities.IsValid(scoreboardParent))
                    {
                        var parentScale = scoreboardParent.lossyScale;
                        targetScale = new Vector3(
                            parentScale.x != 0f ? targetScale.x / parentScale.x : targetScale.x,
                            parentScale.y != 0f ? targetScale.y / parentScale.y : targetScale.y,
                            parentScale.z != 0f ? targetScale.z / parentScale.z : targetScale.z);
                    }

                    scoreboard.transform.localScale = targetScale;

                    // Keep the board visible but stop accepting clicks once the player gets too far away
                    scoreboard.SetInteractable(position.ShouldBeInteractable(headPosition));

                    // Make it so we use the next scoreboard in the list next loop
                    currentVisibleScoreboardID += 1;
                }
                else if (position.CanvasWasEnabledAtStart)
                {
                    // Re-enable the background if it was on by default
                    position.backgroundCanvas.enabled = true;
                }
            }
        }

        /// <summary>
        /// Updates the settings page on all scoreboards if the settings tab is currently visible
        /// </summary>
        public void RefreshSettingsIfVisible()
        {
            if (requestedScoreboardView != ScoreboardView.Settings)
                return;

            foreach (var scoreboard in scoreboards)
                scoreboard.RefreshSettingsMenu();
        }

        /// <summary>
        /// Called when the player opens the settings tab on a scoreboard. (Currently just disables the settings tab on all other scoreboards so we don't have to keep them up to date)
        /// </summary>
        /// <param name="scoreboard">The scoreboard that is now displaying the settings tab</param>
        public void OnPlayerOpenSettings(Scoreboard scoreboard)
        {
            if (!Utilities.IsValid(scoreboard)) return;

            foreach (var thisScoreboard in scoreboards)
            {
                if (!Utilities.IsValid(thisScoreboard)) continue;
                if (thisScoreboard.gameObject != scoreboard.gameObject)
                {
                    thisScoreboard.CurrentScoreboardView = ScoreboardView.Scoreboard;
                }
            }
        }

        /// <summary>
        /// Gets a sorted list of players for the scoreboards to display
        /// </summary>
        /// <returns></returns>
        public PlayerManager[] GetPlayerList()
        {
            var stopwatch = Stopwatch.StartNew();

            var allPlayers = SpeedGolfMode ? openPutt.PlayersSortedByTime : openPutt.PlayersSortedByScore;

            if (!Utilities.IsValid(allPlayers)) return new PlayerManager[0];

            if (allPlayers.Length <= numberOfPlayersToDisplay)
            {
                stopwatch.Stop();
                if (openPutt.debugMode)
                    OpenPuttUtils.Log(this, $"GetPlayerList({stopwatch.Elapsed.TotalMilliseconds}ms) - Can fit all players inside available rows. No array slicing required.");
                return allPlayers;
            }

            // Warning, this can be slow!

            // Find current players position in the list
            var myPosition = SpeedGolfMode ? openPutt.LocalPlayerManager.ScoreboardPositionByTime : openPutt.LocalPlayerManager.ScoreboardPositionByScore;
            myPosition = Mathf.Clamp(myPosition, 0, allPlayers.Length);

            // Work out where we need to slice the array
            var startPos = myPosition - (int)Math.Ceiling(numberOfPlayersToDisplay / 2d);
            var endPos = myPosition + (int)Math.Floor(numberOfPlayersToDisplay / 2d);

            // If the player is near the top or bottom shift the positions so we still fill the board up
            startPos -= endPos >= allPlayers.Length ? endPos - allPlayers.Length : 0;
            endPos += startPos < 0 ? 0 - startPos : 0;

            // Make sure we never go out of bounds
            startPos = Mathf.Clamp(startPos, 0, allPlayers.Length);
            endPos = Mathf.Clamp(endPos, 0, allPlayers.Length);

            // Take a slice of the array that should hopefully contain the local player
            allPlayers = allPlayers.GetRange(startPos, endPos - startPos);

            stopwatch.Stop();

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"GetPlayerList({stopwatch.Elapsed.TotalMilliseconds}ms) - Could not fit all players inside available rows. Array slicing was required. Returning {allPlayers.Length} players for the scoreboards.");

            return allPlayers;
        }
    }
}