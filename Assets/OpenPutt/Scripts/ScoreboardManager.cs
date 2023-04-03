
using System;
using Cyan.PlayerObjectPool;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;

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

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ScoreboardManager : UdonSharpBehaviour
    {
        [Header("The scoreboard manager handles refreshing all scoreboards in the scene and makes sure it doesn't happen too often.")]
        public OpenPutt openPutt;

        public Scoreboard[] scoreboards;

        [Space, Header("Settings"), Range(1f, 5f)]
        public float maxRefreshInterval = 1f;
        private float refreshTimer = 0f;
        public bool refreshRequestedDuringRefresh = false;

        /// <summary>
        /// Used to prevent updates running every frame as it's not needed here
        /// </summary>
        private float updateTimer = 0f;

        [Tooltip("Shows timers instead of scores on the scoreboard")]
        public bool speedGolfMode = false;

        [Tooltip("Should the scoreboards show all players in a scrollable list? (This has a noticeable performance impact)")]
        public bool showAllPlayers = false;

        [Tooltip("Toggles whether to hide people who haven't started playing yet (On by default)")]
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

        public int NumberOfPlayersToDisplay
        {
            get
            {
                int maxPlayers = 12;
                foreach (Scoreboard scoreboard in scoreboards)
                {
                    if (scoreboard == null) continue;
                    if (scoreboard.MaxVisibleRowCount > maxPlayers)
                        maxPlayers = scoreboard.MaxVisibleRowCount;
                }
                //if (CurrentPlayerList != null && CurrentPlayerList.Length > 0 && CurrentPlayerList.Length < maxPlayers)
                //  maxPlayers = CurrentPlayerList.Length;

                return maxPlayers;
            }
        }

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

            RequestPlayerListRefresh();
        }

        private void LateUpdate()
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid())
                return;

            updateTimer += Time.deltaTime;

            if (updateTimer > maxRefreshInterval)
            {
                Vector3 localPlayerPos = Networking.LocalPlayer.GetPosition();
                foreach (Scoreboard scoreboard in scoreboards)
                {
                    bool isNowActive = false;
                    bool isPlayerListActive = false;

                    bool playerIsNearby = Vector3.Distance(localPlayerPos, scoreboard.transform.position) < scoreboard.nearbyMaxRadius;

                    switch (scoreboard.scoreboardVisiblility)
                    {
                        case ScoreboardVisibility.AlwaysVisible:
                            isNowActive = true;

                            // Only activate player list is nearby (Rest of scoreboard is always visible)
                            isPlayerListActive = playerIsNearby;
                            break;
                        case ScoreboardVisibility.NearbyOnly:
                            isNowActive = playerIsNearby;

                            // Only activate player list is nearby (Rest of scoreboard is always visible)
                            isPlayerListActive = playerIsNearby;
                            break;
                        case ScoreboardVisibility.NearbyAndCourseFinished:
                            isNowActive = playerIsNearby;

                            // Only activate player list is nearby (Rest of scoreboard is always visible)
                            isPlayerListActive = playerIsNearby;

                            if (isNowActive && scoreboard.attachedToCourse >= 0)
                            {
                                PlayerManager playerManager = openPutt != null ? openPutt.LocalPlayerManager : null;

                                if (playerManager == null || !playerManager.IsReady || playerManager.courseStates[scoreboard.attachedToCourse] != CourseState.Completed)
                                {
                                    isNowActive = false;
                                    isPlayerListActive = false;
                                }
                            }
                            break;
                        case ScoreboardVisibility.Hidden:
                            isNowActive = false;
                            isPlayerListActive = false;
                            break;
                    }

                    // If the state of this scoreboard has changed
                    bool wasActive = scoreboard.myCanvas.enabled;
                    if (wasActive != isNowActive)
                    {
                        scoreboard.myCanvas.enabled = isNowActive;
                        scoreboard.CurrentScoreboardView = ScoreboardView.Scoreboard;
                    }

                    // Make sure we don't render the player list if the scoreboard is showing something else
                    if (isPlayerListActive)
                        isPlayerListActive = scoreboard.CurrentScoreboardView == ScoreboardView.Scoreboard;

                    // If the state of the player list has changed
                    bool wasPlayerListActive = scoreboard.playerListCanvas.enabled;
                    if (wasPlayerListActive != isPlayerListActive)
                        scoreboard.playerListCanvas.enabled = isPlayerListActive;

                    // Toggle raycast if player is nearby
                    bool raycastEnabled = playerIsNearby && isNowActive;
                    if (scoreboard.raycaster.enabled != raycastEnabled)
                    {
                        scoreboard.raycaster.enabled = raycastEnabled;
                    }
                }
                updateTimer = 0f;
            }

            if (refreshTimer != -1f)
            {
                refreshTimer += Time.deltaTime;

                if (refreshTimer >= maxRefreshInterval)
                {
                    refreshTimer = -1f;
                    updateCurrentFieldID = 0;

                    _currentPlayerList = GetPlayerList();
                }
            }

            if (updateCurrentFieldID != -1)
            {
                bool rowIsVisible = UpdateField(updateCurrentFieldID);

                if (rowIsVisible)
                {
                    // If this row is still visible (has a valid player) continue to next column
                    updateCurrentFieldID += 1;

                    // If we reached the end of the update
                    if (updateCurrentFieldID >= _currentPlayerList.Length * NumberOfColumns)
                        updateCurrentFieldID = -1;
                }
                else
                {
                    // If we need to start hiding rows, skip to start of each row and get scoreboards to hide them
                    for (int i = updateCurrentFieldID + NumberOfColumns + 1; i < _currentPlayerList.Length * NumberOfColumns; i += NumberOfColumns + 1)
                        UpdateField(i);

                    // Finish the update
                    updateCurrentFieldID = -1;
                }
            }
            else if (refreshRequestedDuringRefresh)
            {
                refreshTimer = 0;
                refreshRequestedDuringRefresh = false;
            }
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
        /// Queues a refresh of all scoreboards if one isn't already in progress
        /// </summary>
        /// <param name="refreshNow">Forces a refresh to start on the next frame</param>
        public void RequestPlayerListRefresh(bool refreshNow = false)
        {
            // If we are queuing a normal refresh - only start if we aren't currently refreshing or waiting for a refresh
            if (refreshTimer <= 0f && updateCurrentFieldID == -1)
                refreshTimer = 0;
            else
                refreshRequestedDuringRefresh = true;

            // If we are forcing a refresh - start now
            if (refreshNow)
            {
                updateCurrentFieldID = 0;
                refreshTimer = maxRefreshInterval;
                refreshRequestedDuringRefresh = false;
            }
        }

        private PlayerManager[] _currentPlayerList = null;
        /// <summary>
        /// Returns the last known list of players that was pushed out to all scoreboards
        /// </summary>
        public PlayerManager[] CurrentPlayerList => _currentPlayerList;

        public int NumberOfColumns => openPutt != null ? openPutt.courses.Length + 2 : 0;
        private int updateCurrentFieldID = -1;

        private bool UpdateField(int fieldToUpdate)
        {
            if (openPutt == null) return false;

            int row = fieldToUpdate <= 0 ? 0 : fieldToUpdate / NumberOfColumns;
            int col = fieldToUpdate <= 0 ? 0 : fieldToUpdate % NumberOfColumns;

            PlayerManager player = row >= _currentPlayerList.Length ? null : _currentPlayerList[row];

            string columnText = "";
            Color columnTextColor = text;
            Color columnBGColor = nameBackground1;

            if (player != null)
            {
                if (col == 0)
                {
                    columnText = player.Owner.displayName;
                    columnTextColor = text;
                    columnBGColor = row % 2 == 0 ? nameBackground1 : nameBackground2;
                }
                else if (col == NumberOfColumns - 1)
                {
                    // Render the last column - This is usually the "Total" column
                    bool finishedAllCourses = true;
                    foreach (CourseState courseState in player.courseStates)
                    {
                        // TODO: Maybe count skipped courses as completed too?
                        if (courseState != CourseState.Completed)
                        {
                            finishedAllCourses = false;
                            break;
                        }
                    }

                    bool playerIsAbovePar;
                    bool playerIsBelowPar;

                    if (speedGolfMode)
                    {
                        if (player.PlayerTotalTime == 999999)
                        {
                            columnText = "-";
                            playerIsAbovePar = false;
                            playerIsBelowPar = false;
                        }
                        else
                        {
                            columnText = TimeSpan.FromMilliseconds(player.PlayerTotalTime).ToString(@"m\:ss");
                            playerIsAbovePar = player.PlayerTotalTime > 0 && player.PlayerTotalTime > (openPutt.TotalParTime);
                            playerIsBelowPar = finishedAllCourses && player.PlayerTotalTime > 0 && player.PlayerTotalTime < (openPutt.TotalParTime);
                        }
                    }
                    else
                    {
                        if (player.PlayerTotalScore == 999999)
                        {
                            columnText = "-";
                            playerIsAbovePar = false;
                            playerIsBelowPar = false;
                        }
                        else
                        {
                            columnText = $"{player.PlayerTotalScore}";
                            playerIsAbovePar = player.PlayerTotalScore > 0 && player.PlayerTotalScore > openPutt.TotalParScore;
                            playerIsBelowPar = finishedAllCourses && player.PlayerTotalScore < openPutt.TotalParScore;
                        }
                    }

                    if (playerIsAbovePar)
                    {
                        columnTextColor = overParText;
                        columnBGColor = overParBackground;
                    }
                    else if (playerIsBelowPar)
                    {
                        columnTextColor = underParText;
                        columnBGColor = underParBackground;
                    }
                    else
                    {
                        columnTextColor = text;
                        columnBGColor = row % 2 == 0 ? totalBackground1 : totalBackground2;
                    }
                }
                else if (col > 0 || col < NumberOfColumns - 1)
                {
                    if (col - 1 < player.courseStates.Length)
                    {
                        CourseState courseState = player.courseStates[col - 1];
                        int holeScore = player.courseScores[col - 1];

                        columnBGColor = row % 2 == 0 ? scoreBackground1 : scoreBackground2;

                        bool playerIsAbovePar;
                        bool playerIsBelowPar;

                        CourseManager course = openPutt.courses[col - 1];

                        bool courseIsDrivingRange = openPutt != null && course != null && course.drivingRangeMode;


                        if (courseIsDrivingRange)
                        {
                            playerIsAbovePar = false;
                            playerIsBelowPar = false;

                            if (courseState == CourseState.Playing)
                                columnText = "-";
                            else if (courseState == CourseState.Completed)
                                columnText = $"{holeScore}m";
                        }
                        else if (speedGolfMode)
                        {
                            double timeOnThisCourse = player.courseTimes[col - 1];

                            // If the player is playing this course right now, then the stored value is the time when they started the course
                            if (courseState == CourseState.Playing)
                                timeOnThisCourse = Networking.GetServerTimeInMilliseconds() - timeOnThisCourse;

                            columnText = TimeSpan.FromMilliseconds(timeOnThisCourse).ToString(@"m\:ss");
                            playerIsAbovePar = timeOnThisCourse > (course.parTimeMillis);
                            playerIsBelowPar = courseState == CourseState.Completed && player.PlayerTotalTime > 0 && timeOnThisCourse < (course.parTimeMillis);
                        }
                        else
                        {
                            columnText = $"{holeScore}";
                            playerIsAbovePar = holeScore > 0 && holeScore > course.parScore;
                            playerIsBelowPar = courseState == CourseState.Completed && holeScore < course.parScore;
                        }

                        if (courseState == CourseState.Playing)
                        {
                            columnTextColor = currentCourseText;
                            columnBGColor = currentCourseBackground;
                            // If we are in slow update mode we can't display a score as it will not be up to date
                            if (player.Owner != Networking.LocalPlayer && openPutt.playerSyncType != PlayerSyncType.All)
                                columnText = "-";
                        }
                        else if (courseState == CourseState.NotStarted)
                        {
                            columnText = "-";
                        }
                        else if (playerIsAbovePar)
                        {
                            columnTextColor = overParText;
                            columnBGColor = overParBackground;
                        }
                        else if (playerIsBelowPar)
                        {
                            columnTextColor = underParText;
                            columnBGColor = underParBackground;
                        }
                        else
                        {
                            columnTextColor = text;
                        }
                    }
                }
            }

            bool thisRowIsVisible = true;

            foreach (Scoreboard scoreboard in scoreboards)
            {
                if (!scoreboard.HasInitializedUI)
                    continue;
                if (!scoreboard.UpdateField(fieldToUpdate, player, columnText, columnTextColor, columnBGColor))
                    thisRowIsVisible = false;
            }

            return thisRowIsVisible;
        }

        public PlayerManager[] GetPlayerList()
        {
            PlayerManager[] allPlayers = openPutt.GetPlayers(noSort: false, hideInactivePlayers: hideInactivePlayers);

            if (allPlayers == null)
                return new PlayerManager[0];

            if (showAllPlayers)
                return allPlayers;

            int numberOfPlayersToDisplay = NumberOfPlayersToDisplay;

            // Default to the top of the list
            int myPosition = 0;
            for (int i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i].Owner == Networking.LocalPlayer)
                {
                    // Found the local player
                    myPosition = i;
                    break;
                }
            }

            // Work out where we need to slice the array
            int startPos = myPosition - (int)Math.Ceiling(numberOfPlayersToDisplay / 2d);
            int endPos = myPosition + (int)Math.Floor(numberOfPlayersToDisplay / 2d);

            // If the player is near the top or bottom shift the positions so we still fill the board up
            startPos -= endPos >= allPlayers.Length ? endPos - allPlayers.Length : 0;
            endPos += startPos < 0 ? 0 - startPos : 0;

            // Make sure we never go out of bounds
            if (startPos < 0)
                startPos = 0;
            if (endPos >= allPlayers.Length)
                endPos = allPlayers.Length;
            if (endPos < 0)
                endPos = 0;

            allPlayers = allPlayers.GetRange(0, endPos - startPos);

            return allPlayers;
        }
    }
}