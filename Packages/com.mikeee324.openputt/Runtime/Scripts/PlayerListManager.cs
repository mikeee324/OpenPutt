using System.Diagnostics;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// This script keeps track of what position each player is on the scoreboards.<br/>
    /// There are 2 lists. The first is sorted by score the second is sorted by time.<br/>
    /// TODO: There seems to be an issue somewhere when there are lots of players that stop this from functioning correctly (I couldn't spot a crash so very odd)
    /// </summary>
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

            var playerManager = playerUpdateQueue[0];

            // TODO: Check if the player will even be moved first - no need to do anything if they aren't moving around the array

            var stopwatch = Stopwatch.StartNew();

            // If the player is in the list - remove them from it
            if (playerManager.ScoreboardPositionByScore >= 0 || playerManager.ScoreboardPositionByTime >= 0)
            {
                _RemovePlayer(playerManager);

                if (openPutt.debugMode)
                {
                    stopwatch.Stop();
                    Utils.Log(this, $"RemovePlayer({stopwatch.Elapsed.TotalMilliseconds}ms)");
                    stopwatch.Restart();
                }
            }

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.Owner))
            {
                // Add the player back into the list but in their new position (Should also tell scoreboard manager to update the rows that have changed)
                _AddPlayer(playerManager, out var scorePos, out var timePos);

                if (openPutt.debugMode)
                {
                    stopwatch.Stop();
                    Utils.Log(this, $"AddPlayer({stopwatch.Elapsed.TotalMilliseconds}ms) - Added {(Utilities.IsValid(playerManager.Owner) ? playerManager.Owner.displayName : "NA")}({playerManager.PlayerID}) to lists. (S={scorePos}/T={timePos}) PC(S={TempPlayersSortedByScore.Length}/T={TempPlayersSortedByTime.Length})");
                    stopwatch.Restart();
                }
            }

            // Run a full check on all rows and update player indexes
            _CheckForChanges();

            if (openPutt.debugMode)
            {
                stopwatch.Stop();
                Utils.Log(this, $"_CheckForChanges({stopwatch.Elapsed.TotalMilliseconds}ms)");
                stopwatch.Restart();
            }

            // Move to next player in the queue
            playerUpdateQueue = playerUpdateQueue.RemoveAt(0);
        }

        /// <summary>
        /// Called when a players score or state has changed
        /// </summary>
        /// <param name="playerManager">The player that triggered an update</param>
        public void OnPlayerUpdate(PlayerManager playerManager)
        {
            if (!Utilities.IsValid(playerManager)) return;

            // Add player to the update queue
            playerUpdateQueue = playerUpdateQueue.Add(playerManager);

            // Start processing in LateUpdate()
            this.enabled = true;
        }

        /// <summary>
        /// Adds a player to both sorted lists of players. (Only if they have started playing though)
        /// </summary>
        /// <param name="playerManager">The player to add to the list</param>
        private void _AddPlayer(PlayerManager playerManager, out int scorePos, out int timePos)
        {
            // Init the position variables that we pass back out
            scorePos = -1;
            timePos = -1;

            // PlayerManager is null, return before we die
            if (!Utilities.IsValid(playerManager))
                return;

            // We only add people who have started playing
            if (ScoreboardManager.hideInactivePlayers && !playerManager.PlayerHasStartedPlaying)
                return;

            playerManager.scoreboardRowNeedsUpdating = true;

            // Cache the data we need so we don't repeat this work every time we loop through the players list
            var newPlayerID = playerManager.PlayerID;
            var newPlayerScore = playerManager.PlayerTotalScore;
            var newPlayerTime = playerManager.PlayerTotalTime;

            // Add this player to their correct position in the list sorted by score
            scorePos = 0;
            timePos = 0;
            for (var i = 0; i < TempPlayersSortedByScore.Length; i++)
            {
                // Check if this is where we should place this player in the score list
                var thisScore = TempPlayersSortedByScore[i].PlayerTotalScore;
                if (newPlayerScore > thisScore)
                    scorePos = i + 1;
                else if (thisScore == newPlayerScore && TempPlayersSortedByScore[i].PlayerID < newPlayerID)
                    scorePos = i + 1;

                // Check if this is where we should place this player in the timer list
                var thisTime = TempPlayersSortedByTime[i].PlayerTotalTime;
                if (newPlayerTime > thisTime)
                    timePos = i + 1;
                else if (thisTime == newPlayerTime && TempPlayersSortedByTime[i].PlayerID < newPlayerID)
                    timePos = i + 1;
            }

            // Put the player into both lists where they should be
            TempPlayersSortedByScore = TempPlayersSortedByScore.Insert(scorePos, playerManager);
            TempPlayersSortedByTime = TempPlayersSortedByTime.Insert(timePos, playerManager);
        }

        /// <summary>
        /// Removes a player from both sorted lists of players
        /// </summary>
        /// <param name="playerManager">The player to remove</param>
        private void _RemovePlayer(PlayerManager playerManager)
        {
            if (!Utilities.IsValid(playerManager)) return;

            var originalScorePos = playerManager.ScoreboardPositionByScore;
            var originalTimePos = playerManager.ScoreboardPositionByTime;

            // Remove this player from the list
            if (originalScorePos >= 0)
                TempPlayersSortedByScore = PlayersSortedByScore.RemoveAt(originalScorePos);
            if (originalTimePos >= 0)
                TempPlayersSortedByTime = PlayersSortedByTime.RemoveAt(originalTimePos);

            // Blank out the positions for this PlayerManager
            playerManager.ScoreboardPositionByScore = -1;
            playerManager.ScoreboardPositionByTime = -1;
        }

        private void _CheckForChanges()
        {
            // Update index positions on all PlayerManagers in the list
            for (var sp = 0; sp < openPutt.scoreboardManager.numberOfPlayersToDisplay; sp++)
            {
                var requestRefresh = false;

                var oldPlayerByScore = sp < PlayersSortedByScore.Length ? PlayersSortedByScore[sp] : null;
                var newPlayerByScore = sp < TempPlayersSortedByScore.Length ? TempPlayersSortedByScore[sp] : null;

                var oldPlayerByTime = sp < PlayersSortedByTime.Length ? PlayersSortedByTime[sp] : null;
                var newPlayerByTime = sp < TempPlayersSortedByTime.Length ? TempPlayersSortedByTime[sp] : null;

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
                        requestRefresh = Utilities.IsValid(newPlayerByTime) && newPlayerByTime.scoreboardRowNeedsUpdating;
                    else
                        requestRefresh = Utilities.IsValid(newPlayerByScore) && newPlayerByScore.scoreboardRowNeedsUpdating;
                }

                if (sp < TempPlayersSortedByScore.Length && TempPlayersSortedByScore[sp].ScoreboardPositionByScore != sp)
                {
                    TempPlayersSortedByScore[sp].ScoreboardPositionByScore = sp;
                    if (!ScoreboardManager.SpeedGolfMode)
                        requestRefresh = true;
                }

                if (sp < TempPlayersSortedByTime.Length && TempPlayersSortedByTime[sp].ScoreboardPositionByTime != sp)
                {
                    TempPlayersSortedByTime[sp].ScoreboardPositionByTime = sp;
                    if (ScoreboardManager.SpeedGolfMode)
                        requestRefresh = true;
                }

                if (requestRefresh)
                    ScoreboardManager.RequestRefreshForRow(sp);
            }

            PlayersSortedByScore = TempPlayersSortedByScore;
            PlayersSortedByTime = TempPlayersSortedByTime;
        }
    }

}
