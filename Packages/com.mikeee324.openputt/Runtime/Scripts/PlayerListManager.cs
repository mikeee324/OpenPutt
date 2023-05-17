
using System.Diagnostics;
using UdonSharp;
using UnityEngine;
using UnityEngine.UIElements;
using Varneon.VUdon.ArrayExtensions;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerListManager : UdonSharpBehaviour
    {
        [Header("This script keeps track of what positions players are in on the scoreboards - You only need to assign OpenPutt in this script")]
        public OpenPutt openPutt;
        public PlayerManager[] PlayersSortedByScore = new PlayerManager[0];// { get; private set; }
        public PlayerManager[] PlayersSortedByTime = new PlayerManager[0];// { get; private set; }


        private PlayerManager[] TempPlayersSortedByScore = new PlayerManager[0];
        private PlayerManager[] TempPlayersSortedByTime = new PlayerManager[0];

        private ScoreboardManager ScoreboardManager => openPutt.scoreboardManager;

        private PlayerManager[] playerUpdateQueue = new PlayerManager[0];

        void Start()
        {
            // We don't want to run on first start - nothing to do anyway
            this.enabled = false;
        }

        /// <summary>
        /// Processes one player in the queue per frame.<br/>
        /// It works by removing the player from the arrays and then looking to see where they should be inserted back into the array (if applicable)<br/>
        /// This is faster than sorting the whole array as we just do up to 2 array copy operations per list rather than looping both arrays and sorting them.
        /// </summary>
        private void LateUpdate()
        {
            if (playerUpdateQueue.Length == 0)
            {
                this.enabled = false;
                return;
            }

            PlayerManager playerManager = playerUpdateQueue[0];

            // TODO: Check if the player will even be moved first - no need to do anything if they aren't moving around the array

            // If the player is in the list - remove them from it
            if (playerManager.ScoreboardPositionByScore >= 0 || playerManager.ScoreboardPositionByTime >= 0)
                _RemovePlayer(playerManager);

            // Add the player back into the list but in their new position (Should also tell scoreboard manager to update the rows that have changed)
            _AddPlayer(playerManager);

            // Run a full check on all rows and update player indexes
            _CheckForChanges();

            // Move to next player in the queue
            playerUpdateQueue = playerUpdateQueue.RemoveAt(0);
        }

        public void OnPlayerUpdate(PlayerManager playerManager)
        {
            if (playerManager == null) return;

            // Add player to the update queue
            playerUpdateQueue = playerUpdateQueue.Add(playerManager);

            // Start processing in LateUpdate()
            this.enabled = true;
        }

        /// <summary>
        /// Adds a player to both sorted lists of players. (Only if they have started playing though)
        /// </summary>
        /// <param name="playerManager">The player to add to the list</param>
        private void _AddPlayer(PlayerManager playerManager)
        {
            // We only add people who have started playing
            if (playerManager == null || (ScoreboardManager.hideInactivePlayers && !playerManager.PlayerHasStartedPlaying))
                return;

            //Stopwatch stopwatch = Stopwatch.StartNew();

            playerManager.scoreboardRowNeedsUpdating = true;

            // Cache the data we need so we don't repeat this work every time we loop through the players list
            int newPlayerID = playerManager.PlayerID;
            int newPlayerScore = playerManager.PlayerTotalScore;
            int newPlayerTime = playerManager.PlayerTotalTime;

            // Add this player to their correct position in the list sorted by score
            int scorePos = 0;
            int timePos = 0;
            for (int i = 0; i < TempPlayersSortedByScore.Length; i++)
            {
                // Check if this is where we should place this player in the score list
                int thisScore = TempPlayersSortedByScore[i].PlayerTotalScore;
                if (newPlayerScore > thisScore)
                    scorePos = i + 1;
                else if (thisScore == newPlayerScore && TempPlayersSortedByScore[i].PlayerID < newPlayerID)
                    scorePos = i + 1;

                // Check if this is where we should place this player in the timer list
                int thisTime = TempPlayersSortedByTime[i].PlayerTotalTime;
                if (newPlayerTime > thisTime)
                    timePos = i + 1;
                else if (thisTime == newPlayerTime && TempPlayersSortedByTime[i].PlayerID < newPlayerID)
                    timePos = i + 1;
            }

            TempPlayersSortedByScore = TempPlayersSortedByScore.Insert(scorePos, playerManager);
            TempPlayersSortedByTime = TempPlayersSortedByTime.Insert(timePos, playerManager);

            //stopwatch.Stop();
            //Utils.Log(this, $"AddPlayer({stopwatch.Elapsed.TotalMilliseconds}ms) - Added {(playerManager.Owner != null ? playerManager.Owner.displayName : "NA")}({playerManager.PlayerID}) to lists. (S={scorePos}/T={timePos}) PC(S={TempPlayersSortedByScore.Length}/T={TempPlayersSortedByTime.Length})");
        }

        /// <summary>
        /// Removes a player from both sorted lists of players
        /// </summary>
        /// <param name="playerManager">The player to remove</param>
        private void _RemovePlayer(PlayerManager playerManager)
        {
            if (playerManager == null) return;
            //Stopwatch stopwatch = Stopwatch.StartNew();

            int originalScorePos = playerManager.ScoreboardPositionByScore;
            int originalTimePos = playerManager.ScoreboardPositionByTime;

            // Remove this player from the list
            if (originalScorePos >= 0)
                TempPlayersSortedByScore = PlayersSortedByScore.RemoveAt(originalScorePos);
            if (originalTimePos >= 0)
                TempPlayersSortedByTime = PlayersSortedByTime.RemoveAt(originalTimePos);

            // Blank out the positions for this PlayerManager
            playerManager.ScoreboardPositionByScore = -1;
            playerManager.ScoreboardPositionByTime = -1;

            //stopwatch.Stop();
            //Utils.Log(this, $"RemovePlayer({stopwatch.Elapsed.TotalMilliseconds}ms) - Added {(playerManager.Owner != null ? playerManager.Owner.displayName : "NA")}({playerManager.PlayerID}) to lists. PlayerPosition(S={originalScorePos}/T={originalTimePos}) PlayerCount(S={TempPlayersSortedByScore.Length}/T={TempPlayersSortedByTime.Length})");
        }

        private void _CheckForChanges()
        {
            // Update index positions on all PlayerManagers in the list
            for (int sp = 0; sp < TempPlayersSortedByScore.Length; sp++)
            {
                bool requestRefresh = false;

                PlayerManager oldPlayerByScore = sp < PlayersSortedByScore.Length ? PlayersSortedByScore[sp] : null;
                PlayerManager newPlayerByScore = sp < TempPlayersSortedByScore.Length ? TempPlayersSortedByScore[sp] : null;

                PlayerManager oldPlayerByTime = sp < PlayersSortedByTime.Length ? PlayersSortedByTime[sp] : null;
                PlayerManager newPlayerByTime = sp < TempPlayersSortedByTime.Length ? TempPlayersSortedByTime[sp] : null;

                if (!requestRefresh)
                {
                    if (ScoreboardManager.SpeedGolfMode)
                        requestRefresh = oldPlayerByTime != newPlayerByTime;
                    else
                        requestRefresh = oldPlayerByScore != newPlayerByScore;
                }

                if (!requestRefresh)
                {
                    if (ScoreboardManager.SpeedGolfMode)
                        requestRefresh = newPlayerByTime != null && newPlayerByTime.scoreboardRowNeedsUpdating;
                    else
                        requestRefresh = newPlayerByScore != null && newPlayerByScore.scoreboardRowNeedsUpdating;
                }

                if (TempPlayersSortedByScore[sp].ScoreboardPositionByScore != sp)
                {
                    TempPlayersSortedByScore[sp].ScoreboardPositionByScore = sp;
                    if (!ScoreboardManager.SpeedGolfMode)
                        requestRefresh = true;
                }

                if (TempPlayersSortedByTime[sp].ScoreboardPositionByTime != sp)
                {
                    TempPlayersSortedByTime[sp].ScoreboardPositionByTime = sp;
                    if (ScoreboardManager.SpeedGolfMode)
                        requestRefresh = true;
                }

                if (requestRefresh)
                    ScoreboardManager.RequestRefreshForRow(sp, true);
            }

            PlayersSortedByScore = TempPlayersSortedByScore;
            PlayersSortedByTime = TempPlayersSortedByTime;
        }
    }

}
