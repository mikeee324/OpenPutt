
using System;
using System.Diagnostics;
using Cyan.PlayerObjectPool;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
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
        public ScoreboardPositioner[] scoreboardPositions;

        [Space, Header("Settings")]
        [Range(1f, 5f), Tooltip("Amount of time in seconds that must pass before a refresh will begin")]
        public float maxRefreshInterval = 1f;
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
        public Color overParBackground = Color.red;
        [Header("Text Colours")]
        public Color text = Color.white;
        public Color currentCourseText = Color.white;
        public Color underParText = Color.white;
        public Color overParText = Color.white;

        [Header("Prefab References")]
        public GameObject rowPrefab;
        public GameObject colPrefab;

        #region Internal Vars
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
                    CheckPlayerListForChanges(forceUpdate: true);
                }
                _speedGolfMode = value;
            }
        }
        private bool _speedGolfMode = false;
        /// <summary>
        /// Returns the last known list of players that was pushed out to all scoreboards
        /// </summary>
        public PlayerManager[] CurrentPlayerList { get; private set; }

        public int NumberOfColumns => openPutt != null ? openPutt.courses.Length + 2 : 0;
        [HideInInspector]
        public ScoreboardView requestedScoreboardView = ScoreboardView.Info;

        private int progressiveUpdateCurrentScoreboardID = 0;
        private int[] progressiveRowUpdateQueue = new int[0];
        private bool allScoreboardsInitialised = false;
        #endregion

        void Start()
        {
            if (openPutt == null)
            {
                Utils.LogError(this, "Missing OpenPutt Reference! Disabling Scoreboards");
                gameObject.SetActive(false);
                foreach (Scoreboard scoreboard in scoreboards)
                    scoreboard.gameObject.SetActive(false);
                return;
            }

            foreach (Scoreboard scoreboard in scoreboards)
                scoreboard.manager = this;

            SendCustomEventDelayedSeconds(nameof(UpdateScoreboardVisibility), maxRefreshInterval);
        }

        public void UpdateScoreboardVisibility()
        {
            // Schedule the next update before anything else
            SendCustomEventDelayedSeconds(nameof(UpdateScoreboardVisibility), maxRefreshInterval);

            // Don't do anything if we can't find the local player
            if (!Utils.LocalPlayerIsValid())
                return;

            // After all scoreboard have initialised - queue a full refresh
            if (!allScoreboardsInitialised)
            {
                bool queueBigRefresh = true;
                foreach (Scoreboard scoreboard in scoreboards)
                    if (!scoreboard.HasInitializedUI)
                        queueBigRefresh = false;

                if (queueBigRefresh)
                {
                    allScoreboardsInitialised = true;
                    CheckPlayerListForChanges();
                }
            }
            int currentVisibleScoreboardID = 0;

            Vector3 localPlayerPos = Networking.LocalPlayer.GetPosition();
            ScoreboardPositioner[] scoreboardPositionsByDistance = this.scoreboardPositions.SortByDistance(localPlayerPos);

            foreach (ScoreboardPositioner position in scoreboardPositionsByDistance)
            {
                bool isVisibleHere = position.ShouldBeVisible(localPlayerPos);

                if (currentVisibleScoreboardID >= scoreboards.Length)
                    isVisibleHere = false;

                if (isVisibleHere)
                {
                    // Hide the background of the positioner
                    position.backgroundCanvas.enabled = false;

                    // Move this scoreboard to the correct position
                    Scoreboard scoreboard = this.scoreboards[currentVisibleScoreboardID];
                    scoreboard.transform.SetPositionAndRotation(position.transform.position, position.transform.rotation);
                    scoreboard.transform.localScale = position.transform.lossyScale;

                    // Make it so we use the next scoreboard in the list next loop
                    currentVisibleScoreboardID += 1;
                }
                else
                {
                    // Re-enable the background if it was on by default
                    if (position.CanvasWasEnabledAtStart)
                        position.backgroundCanvas.enabled = true;
                }
            }

            while (currentVisibleScoreboardID < scoreboards.Length)
            {
                // Hide the scoreboard far down
                scoreboards[currentVisibleScoreboardID].transform.position = new Vector3(0, -100, 0);

                currentVisibleScoreboardID += 1;
            }

            // Swap all scoreboard views to be the same
            if (requestedScoreboardView != ScoreboardView.Settings && requestedScoreboardView != ScoreboardView.DevMode)
            {
                foreach (Scoreboard scoreboard in scoreboards)
                {
                    if (scoreboard.HasInitializedUI)
                        scoreboard.CurrentScoreboardView = requestedScoreboardView;
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

            foreach (Scoreboard scoreboard in scoreboards)
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
            PlayerManager[] newList = GetPlayerList();

            if (CurrentPlayerList == null)
            {
                CurrentPlayerList = new PlayerManager[0];
                forceUpdate = true;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            int[] rowsToUpdate = new int[0];

            // We need to check which rows have changed and request a refresh
            for (int i = 0; i < numberOfPlayersToDisplay; i++)
            {
                // Find the current player in this row
                PlayerManager thisRowPlayer = null;
                if (i < CurrentPlayerList.Length)
                    thisRowPlayer = CurrentPlayerList[i];

                // Find the player that will be in this row after the update
                PlayerManager thisRowNewPlayer = null;
                if (i < newList.Length)
                    thisRowNewPlayer = newList[i];

                // If this row has no player and hasn't changed - we don't need to check any more rows as the rest should be the same
                if (thisRowPlayer == null && thisRowNewPlayer == null)
                    break;

                // Check if an update is being force (for when player toggles between timer/normal mode)
                bool requestRefresh = forceUpdate;

                // Check if player in this row has changed
                if (!requestRefresh)
                    requestRefresh = thisRowNewPlayer != thisRowPlayer;

                // Check if this row has been marked as dirty and needs refreshing
                if (!requestRefresh)
                    requestRefresh = thisRowNewPlayer != null && thisRowNewPlayer.scoreboardRowNeedsUpdating;

                // Add to the list to refresh
                if (requestRefresh)
                    rowsToUpdate = rowsToUpdate.Add(i);
            }

            // Bulk add all rows to update at once
            RequestRefreshForRows(rowsToUpdate, true);

            stopwatch.Stop();
            Utils.Log(this, $"CheckPlayerListForChanges({stopwatch.Elapsed.TotalMilliseconds}ms)");
        }

        public void ProgressiveScoreboardRowUpdate()
        {
            if (!allScoreboardsInitialised)
            {
                // Wait for a second until all the scoreboards have initialised
                SendCustomEventDelayedSeconds(nameof(ProgressiveScoreboardRowUpdate), 1);
                return;
            }

            if (progressiveUpdateCurrentScoreboardID == 0 && (CurrentPlayerList == null || CurrentPlayerList.Length == 0))
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

            int rowIDToUpdate = progressiveRowUpdateQueue[0];

            // Update the row for this player if we found one
            if (CurrentPlayerList != null && rowIDToUpdate >= 0 && rowIDToUpdate < scoreboard.scoreboardRows.Length)
            {
                scoreboard.scoreboardRows[rowIDToUpdate].Refresh(rowIDToUpdate < CurrentPlayerList.Length ? CurrentPlayerList[rowIDToUpdate] : null);
            }

            // Loop again next frame to process the queue until it's empty
            //SendCustomEventDelayedFrames(nameof(ProgressiveScoreboardRowUpdate), 0);
            SendCustomEventDelayedSeconds(nameof(ProgressiveScoreboardRowUpdate), 0.1f);
        }

        /// <summary>
        /// Called when the player opens the settings tab on a scoreboard. (Currently just disables the settings tab on all other scoreboards so we don't have to keep them up to date)
        /// </summary>
        /// <param name="scoreboard">The scoreboard that is now displaying the settings tab</param>
        public void OnPlayerOpenSettings(Scoreboard scoreboard)
        {
            if (scoreboard == null) return;

            foreach (Scoreboard thisScoreboard in scoreboards)
            {
                if (thisScoreboard == null) continue;
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
            Stopwatch stopwatch = Stopwatch.StartNew();

            PlayerManager[] allPlayers = SpeedGolfMode ? openPutt.PlayersSortedByTime : openPutt.PlayersSortedByScore;

            if (allPlayers == null)
                return new PlayerManager[0];

            if (allPlayers.Length <= numberOfPlayersToDisplay)
            {
                stopwatch.Stop();
                Utils.Log(this, $"GetPlayerList({stopwatch.Elapsed.TotalMilliseconds}ms) - Can fit all players inside available rows. No array slicing required.");
                return allPlayers;
            }

            // Warning, this can be slow!

            // Find current players position in the list
            int myPosition = SpeedGolfMode ? openPutt.LocalPlayerManager.ScoreboardPositionByTime : openPutt.LocalPlayerManager.ScoreboardPositionByScore;
            myPosition = Mathf.Clamp(myPosition, 0, allPlayers.Length);

            // Work out where we need to slice the array
            int startPos = myPosition - (int)Math.Ceiling(numberOfPlayersToDisplay / 2d);
            int endPos = myPosition + (int)Math.Floor(numberOfPlayersToDisplay / 2d);

            // If the player is near the top or bottom shift the positions so we still fill the board up
            startPos -= endPos >= allPlayers.Length ? endPos - allPlayers.Length : 0;
            endPos += startPos < 0 ? 0 - startPos : 0;

            // Make sure we never go out of bounds
            startPos = Mathf.Clamp(startPos, 0, allPlayers.Length);
            endPos = Mathf.Clamp(endPos, 0, allPlayers.Length);

            // Take a slice of the array that should hopefully contain the local player
            allPlayers = allPlayers.GetRange(startPos, endPos - startPos);

            stopwatch.Stop();

            Utils.Log(this, $"GetPlayerList({stopwatch.Elapsed.TotalMilliseconds}ms) - Could not fit all players inside available rows. Array slicing was required. Returning {allPlayers.Length} players for the scoreboards.");

            return allPlayers;
        }
    }
}