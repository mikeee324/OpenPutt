
using System.Diagnostics;
using UdonSharp;
using Varneon.VUdon.ArrayExtensions;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerListManager : UdonSharpBehaviour
    {
        public OpenPutt openPutt;
        public PlayerManager[] PlayersSortedByScore = new PlayerManager[0];// { get; private set; }
        public PlayerManager[] PlayersSortedByTime = new PlayerManager[0];// { get; private set; }


        private PlayerManager[] TempPlayersSortedByScore = new PlayerManager[0];
        private PlayerManager[] TempPlayersSortedByTime = new PlayerManager[0];

        private ScoreboardManager scoreboardManager => openPutt.scoreboardManager;

        private PlayerManager[] playerUpdateQueue = new PlayerManager[0];

        void Start()
        {
            // We don't want to run on first start - nothing to do anyway
            this.enabled = false;
        }

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

            // Move to next player in the queue
            CheckForChanges();

            playerUpdateQueue = playerUpdateQueue.RemoveAt(0);
        }

        public void OnPlayerUpdate(PlayerManager playerManager)
        {
            if (playerManager == null) return;

            playerUpdateQueue = playerUpdateQueue.Add(playerManager);

            this.enabled = true;
        }

        /// <summary>
        /// Adds a player to both sorted lists of players. (Only if they have started playing though)
        /// </summary>
        /// <param name="playerManager">The player to add to the list</param>
        private void _AddPlayer(PlayerManager playerManager)
        {
            // We only add people who have started playing
            if (playerManager == null || (scoreboardManager.hideInactivePlayers && !playerManager.PlayerHasStartedPlaying))
                return;

            //Stopwatch stopwatch = Stopwatch.StartNew();

            playerManager.scoreboardRowNeedsUpdating = true;

            // Cache the data we need so we don't repeat this work every time we loop through the players list
            int playerID = playerManager.PlayerID;
            int newScore = playerManager.PlayerTotalScore;
            int newTime = playerManager.PlayerTotalTime;

            // Add this player to their correct position in the list sorted by score
            int scorePos = -1;
            int timePos = 0;
            for (int i = 0; i < TempPlayersSortedByScore.Length; i++)
            {
                // Check if this is where we should place this player
                if (scorePos == -1)
                {
                    int thisScore = TempPlayersSortedByScore[i].PlayerTotalScore;
                    if (thisScore > newScore)
                        scorePos = i;
                    else if (thisScore == newScore && TempPlayersSortedByScore[i].PlayerID > playerID)
                        scorePos = i;
                }

                if (timePos == -1)
                {
                    int thisTime = TempPlayersSortedByTime[i].PlayerTotalTime;
                    if (thisTime > newTime)
                        timePos = i;
                    else if (thisTime == newTime && TempPlayersSortedByTime[i].PlayerID > playerID)
                        timePos = i;
                }
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

        private void CheckForChanges()
        {
            // Update index positions on all PlayerManagers in the list
            for (int sp = 0; sp < TempPlayersSortedByScore.Length; sp++)
            {
                bool requestRefresh = false;

                PlayerManager oldPlayer = sp < PlayersSortedByScore.Length ? (scoreboardManager.SpeedGolfMode ? PlayersSortedByTime[sp] : PlayersSortedByScore[sp]) : null;
                PlayerManager newPlayer = sp < TempPlayersSortedByScore.Length ? (scoreboardManager.SpeedGolfMode ? TempPlayersSortedByTime[sp] : TempPlayersSortedByScore[sp]) : null;

                if (!requestRefresh)
                    requestRefresh = oldPlayer != newPlayer;

                if (!requestRefresh && newPlayer != null)
                    requestRefresh = newPlayer.scoreboardRowNeedsUpdating;

                TempPlayersSortedByScore[sp].ScoreboardPositionByScore = sp;
                TempPlayersSortedByTime[sp].ScoreboardPositionByTime = sp;

                if (requestRefresh)
                    scoreboardManager.RequestRefreshForRow(sp);
            }

            PlayersSortedByScore = TempPlayersSortedByScore;
            PlayersSortedByTime = TempPlayersSortedByTime;
        }
    }

}
