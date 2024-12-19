using System;
using System.Diagnostics;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
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
    public class ScoreboardManager : UdonSharpBehaviour
    {
        [Header("The scoreboard manager handles refreshing all scoreboards in the scene and makes sure it doesn't happen too often.")]
        public OpenPutt openPutt;

        public Scoreboard[] scoreboards;
        public Scoreboard[] staticScoreboards;

        /// <summary>
        /// Used to avoid having to build a joined array of scoreboards+staticScoreboards after start
        /// </summary>
        private Scoreboard[] allScoreboards;

        public ScoreboardPositioner[] scoreboardPositions;

        [Space, Header("Settings")] [Range(0f, 10f), Tooltip("Amount of time in seconds that must pass before a refresh will begin")]
        public float maxRefreshInterval = .25f;

        [Range(1f, 5f), Tooltip("Amount of time in seconds between scoreboard position updates")]
        public float scoreboardPositionUpdateInterval = 1f;

        [Range(1, 82), Tooltip("The total number of players that the scoreboards can display at once (Large numbers can cause VERY long build times and maybe performance issues too - haven't tested it above 12)")]
        public int numberOfPlayersToDisplay = 12;

        [Tooltip("This should stay enabled unless you're debugging the scoreboard player lists")]
        public bool hideInactivePlayers = true;

        [Header("Background Colours")]
        public Color nameBackground1 = Color.black;

        public Color scoreBackground1 = Color.black;
        public Color totalBackground1 = Color.black;

        [Space]
        public Color nameBackground2 = Color.black;

        public Color scoreBackground2 = Color.black;
        public Color totalBackground2 = Color.black;

        [Space]
        public Color currentCourseBackground = Color.black;

        public Color underParBackground = Color.green;
        public Color onParBackground = Color.green;
        public Color overParBackground = Color.red;

        [Header("Text Colours")]
        public Color text = Color.white;

        public Color currentCourseText = Color.white;
        public Color underParText = Color.white;
        public Color onParText = Color.white;
        public Color overParText = Color.white;

        [Header("Prefab References")]
        public GameObject rowPrefab;

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
                    RequestRefreshForRow(-2, true);
                    CheckPlayerListForChanges(forceUpdate: true);
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

        private int progressiveUpdateCurrentScoreboardID;
        private int[] progressiveRowUpdateQueue = new int[0];
        private bool allScoreboardsInitialised;

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

            // Update positions of all scoreboards a bit quicker at startup
            SendCustomEventDelayedSeconds(nameof(UpdateScoreboardVisibility), .5f);
        }

        /// <summary>
        /// Syncs up all the scoreboards so they are displaying the same thing<br/>
        /// Might need to be expanded to sync up scroll positions on larger lists later.<br/>
        /// This can take 1+ms to perform so don't call this too often. (Might need to split this up into separate frames so it's less laggy)
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
                    CheckPlayerListForChanges();
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

            var localPlayerPos = Networking.LocalPlayer.GetPosition();

            for (var i = 0; i < scoreboardPositions.Length; i++)
            {
                var position = scoreboardPositions[i];
                var isVisibleHere = position.ShouldBeVisible(localPlayerPos);

                // We ran out of scoreboards in the pool so just show the positioner canvases if needed
                if (currentVisibleScoreboardID >= scoreboards.Length)
                    isVisibleHere = false;

                if (isVisibleHere)
                {
                    // Hide the background of the positioner
                    position.backgroundCanvas.enabled = false;

                    // Move this scoreboard to the correct position
                    var scoreboard = scoreboards[currentVisibleScoreboardID];
                    scoreboard.transform.SetPositionAndRotation(position.transform.position, position.transform.rotation);
                    scoreboard.transform.localScale = position.transform.lossyScale;

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
        /// Updates the settings page on all scoreboards if the settings are currently visible.<br/>
        /// If the settings aren't visible then we don't need to do anything as they get refreshed when players click the settings cog
        /// </summary>
        public void RefreshSettingsIfVisible()
        {
            if (requestedScoreboardView != ScoreboardView.Settings)
                return;

            foreach (var scoreboard in scoreboards)
                scoreboard.RefreshSettingsMenu();
        }

        public void RequestRefreshForRow(int rowID, bool startNextFrameIfPossible = false)
        {
            // If queue is empty start a new refresh
            if (progressiveRowUpdateQueue.Length == 0)
            {
                progressiveRowUpdateQueue = progressiveRowUpdateQueue.Add(rowID);

                // Empty the player list before the update - Progressive update will fetch a new list before it starts
                CurrentPlayerList = new PlayerManager[0];

                if (startNextFrameIfPossible)
                    SendCustomEventDelayedFrames(nameof(ProgressiveScoreboardRowUpdate), 0);
                else
                    SendCustomEventDelayedSeconds(nameof(ProgressiveScoreboardRowUpdate), maxRefreshInterval);
            }
            else
            {
                progressiveRowUpdateQueue = progressiveRowUpdateQueue.Add(rowID);
            }
        }

        public void RequestRefreshForRows(int[] rowIDs, bool startNextFrameIfPossible = false)
        {
            // If queue is empty start a new refresh
            if (progressiveRowUpdateQueue.Length == 0)
            {
                progressiveRowUpdateQueue = progressiveRowUpdateQueue.AddRange(rowIDs);

                // Empty the player list before the update - Progressive update will fetch a new list before it starts
                CurrentPlayerList = new PlayerManager[0];

                if (startNextFrameIfPossible)
                    SendCustomEventDelayedFrames(nameof(ProgressiveScoreboardRowUpdate), 0);
                else
                    SendCustomEventDelayedSeconds(nameof(ProgressiveScoreboardRowUpdate), maxRefreshInterval);
            }
            else
            {
                progressiveRowUpdateQueue = progressiveRowUpdateQueue.AddRange(rowIDs);
            }
        }

        /// <summary>
        /// Performs a check on all player rows and checks if they need to be updated. If they do a request for a refresh will be added to the queue.
        /// </summary>
        /// <param name="forceUpdate">True forces an update on all rows</param>
        public void CheckPlayerListForChanges(bool forceUpdate = false)
        {
            var newList = GetPlayerList();

            if (!Utilities.IsValid(CurrentPlayerList))
            {
                CurrentPlayerList = new PlayerManager[0];
                forceUpdate = true;
            }

            var stopwatch = Stopwatch.StartNew();

            var rowsToUpdate = new int[0];

            // We need to check which rows have changed and request a refresh
            for (var i = 0; i < numberOfPlayersToDisplay; i++)
            {
                // Find the current player in this row
                PlayerManager thisRowPlayer = null;
                if (i < CurrentPlayerList.Length)
                    thisRowPlayer = CurrentPlayerList[i];

                // Find the player that will be in this row after the update
                PlayerManager thisRowNewPlayer = null;
                if (i < newList.Length)
                    thisRowNewPlayer = newList[i];

                // If this row has no player and hasn't changed - we don't need to update this row
                if (!Utilities.IsValid(thisRowPlayer) && !Utilities.IsValid(thisRowNewPlayer))
                    continue;

                // Check if an update is being forced (for when player toggles between timer/normal mode)
                if (forceUpdate)
                {
                    rowsToUpdate = rowsToUpdate.Add(i);
                    continue;
                }

                var requestRefresh = false;

                // Check if player in this row has changed
                if (!requestRefresh)
                    requestRefresh = thisRowNewPlayer != thisRowPlayer;

                // Check if this row has been marked as dirty and needs refreshing
                if (!requestRefresh)
                    requestRefresh = Utilities.IsValid(thisRowNewPlayer) && thisRowNewPlayer.scoreboardRowNeedsUpdating;

                // Add to the list to refresh
                if (requestRefresh)
                    rowsToUpdate = rowsToUpdate.Add(i);
            }

            // Bulk add all rows to update at once
            RequestRefreshForRows(rowsToUpdate, true);

            stopwatch.Stop();
            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"CheckPlayerListForChanges({stopwatch.Elapsed.TotalMilliseconds}ms)");
        }

        public void ProgressiveScoreboardRowUpdate()
        {
            if (!allScoreboardsInitialised)
            {
                // Wait for a second until all the scoreboards have initialised
                SendCustomEventDelayedSeconds(nameof(ProgressiveScoreboardRowUpdate), 1);
                return;
            }

            if (progressiveUpdateCurrentScoreboardID == 0 && (!Utilities.IsValid(CurrentPlayerList) || CurrentPlayerList.Length == 0))
            {
                CurrentPlayerList = GetPlayerList();
            }

            if (progressiveUpdateCurrentScoreboardID >= scoreboards.Length + staticScoreboards.Length)
            {
                // We updated all scoreboards for the first player on the queue, reset ID and remove from the queue
                progressiveUpdateCurrentScoreboardID = 0;

                if (progressiveRowUpdateQueue.Length > 0)
                    progressiveRowUpdateQueue = progressiveRowUpdateQueue.RemoveAt(0);
            }

            // Queue is empty
            if (progressiveRowUpdateQueue.Length == 0)
            {
                // Just check if players are not in the correct order and move them if needed
                //SortPlayerUI();
                return;
            }

            // Update the row on this scoreboard
            Scoreboard scoreboard = null;

            if (progressiveUpdateCurrentScoreboardID < scoreboards.Length)
                scoreboard = scoreboards[progressiveUpdateCurrentScoreboardID];
            else
                scoreboard = staticScoreboards[progressiveUpdateCurrentScoreboardID - scoreboards.Length];

            progressiveUpdateCurrentScoreboardID += 1;

            var rowIDToUpdate = progressiveRowUpdateQueue[0];

            // Update the row for this player if we found one
            if (Utilities.IsValid(CurrentPlayerList))
            {
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

            // Loop again next frame to process the queue until it's empty
            //SendCustomEventDelayedFrames(nameof(ProgressiveScoreboardRowUpdate), 0);
            SendCustomEventDelayedSeconds(nameof(ProgressiveScoreboardRowUpdate), 0.05f);
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