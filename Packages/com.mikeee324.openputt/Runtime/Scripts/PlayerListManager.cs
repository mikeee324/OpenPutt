using System.Diagnostics;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Keeps 2 lists of all players - one sorted by score, the other by time - for the scoreboards to use.<br/>
    /// Both lists are always kept fully sorted, so when a players score/time changes we only need to remove them
    /// and binary-search insert them back into their new position rather than re-sorting everybody.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerListManager : UdonSharpBehaviour
    {
        [Header("This script keeps track of what positions players are in on the scoreboards - You only need to assign OpenPutt in this script")]
        public OpenPutt openPutt;

        [Range(0f, 2f), Tooltip("How much time in milliseconds we're allowed to spend repositioning players per frame - if there's more dirty players than we can get through in this time, the rest will be spread over the following frames")]
        public float maxTimePerFrame = 0.3f;

        public PlayerManager[] PlayersSortedByScore = new PlayerManager[0];
        public PlayerManager[] PlayersSortedByTime = new PlayerManager[0];

        /// <summary>
        /// Players that need to be repositioned in the sorted lists above
        /// </summary>
        private PlayerManager[] dirtyPlayers = new PlayerManager[0];

        void Start()
        {
            // We don't want to run on first start - nothing to do anyway
            enabled = false;
        }

        /// <summary>
        /// Called when a player's score or state has changed - queues them up to be repositioned in the sorted lists
        /// </summary>
        public void OnPlayerUpdate(PlayerManager playerManager)
        {
            if (!Utilities.IsValid(playerManager)) return;

            dirtyPlayers = dirtyPlayers.AddUnique(playerManager);

            enabled = true;
        }

        /// <summary>
        /// Called when the known player roster has changed (players joining/leaving) - removes anybody who is no
        /// longer valid from both lists and queues up anybody missing so they get inserted
        /// </summary>
        public void OnPlayerListChanged()
        {
            var allPlayers = openPutt.allPlayerManagers;

            for (var i = PlayersSortedByScore.Length - 1; i >= 0; i--)
                if (!Utilities.IsValid(PlayersSortedByScore[i]) || !allPlayers.Contains(PlayersSortedByScore[i]))
                    PlayersSortedByScore = PlayersSortedByScore.RemoveAt(i);

            for (var i = PlayersSortedByTime.Length - 1; i >= 0; i--)
                if (!Utilities.IsValid(PlayersSortedByTime[i]) || !allPlayers.Contains(PlayersSortedByTime[i]))
                    PlayersSortedByTime = PlayersSortedByTime.RemoveAt(i);

            foreach (var playerManager in allPlayers)
                if (Utilities.IsValid(playerManager) && !PlayersSortedByScore.Contains(playerManager))
                    dirtyPlayers = dirtyPlayers.AddUnique(playerManager);

            StampPositions();

            enabled = true;
        }

        private void LateUpdate()
        {
            if (dirtyPlayers.Length == 0)
            {
                enabled = false;
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var playersProcessed = 0;

            // Keep repositioning players from the queue until we run out of time for this frame - always do at
            // least 1 so we're guaranteed to make progress even if a single reposition blows the budget
            do
            {
                var playerManager = dirtyPlayers[0];
                dirtyPlayers = dirtyPlayers.RemoveAt(0);
                playersProcessed++;

                var shouldBeListed = Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.Owner)
                                      && (!openPutt.scoreboardManager.hideInactivePlayers || playerManager.PlayerHasStartedPlaying);

                if (shouldBeListed)
                {
                    playerManager.scoreboardRowNeedsUpdating = true;
                    PlayersSortedByScore = Reposition(PlayersSortedByScore, playerManager, byTime: false);
                    PlayersSortedByTime = Reposition(PlayersSortedByTime, playerManager, byTime: true);
                }
                else
                {
                    PlayersSortedByScore = PlayersSortedByScore.Remove(playerManager);
                    PlayersSortedByTime = PlayersSortedByTime.Remove(playerManager);
                }
            } while (dirtyPlayers.Length > 0 && stopwatch.Elapsed.TotalMilliseconds < maxTimePerFrame);

            StampPositions();

            openPutt.scoreboardManager.RequestRefresh();

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Repositioned {playersProcessed} player(s) in {stopwatch.Elapsed.TotalMilliseconds}ms");
        }

        /// <summary>
        /// Removes playerManager from the sorted array (if present) and re-inserts it at its correct sorted
        /// position, comparing on either total score or total time
        /// </summary>
        private PlayerManager[] Reposition(PlayerManager[] sorted, PlayerManager playerManager, bool byTime)
        {
            var existingIndex = sorted.IndexOf(playerManager);
            if (existingIndex != -1)
                sorted = sorted.RemoveAt(existingIndex);

            return sorted.Insert(FindInsertIndex(sorted, playerManager, byTime), playerManager);
        }

        /// <summary>
        /// Binary searches a sorted array for the index playerManager should be inserted at to keep it in
        /// ascending score/time order (lowest PlayerID first as a tie breaker, matching the old sort order)
        /// </summary>
        private int FindInsertIndex(PlayerManager[] sorted, PlayerManager playerManager, bool byTime)
        {
            var value = byTime ? playerManager.PlayerTotalTime : playerManager.PlayerTotalScore;
            var playerID = playerManager.PlayerID;

            var low = 0;
            var high = sorted.Length;
            while (low < high)
            {
                var mid = (low + high) / 2;
                var midValue = byTime ? sorted[mid].PlayerTotalTime : sorted[mid].PlayerTotalScore;

                var goesBeforeMid = value < midValue || (value == midValue && playerID < sorted[mid].PlayerID);
                if (goesBeforeMid)
                    high = mid;
                else
                    low = mid + 1;
            }

            return low;
        }

        /// <summary>
        /// Stamps each player with their current index in both sorted lists. Used by the scoreboards to jump to
        /// the local players position when there are more players than can fit on the board at once
        /// </summary>
        private void StampPositions()
        {
            for (var i = 0; i < PlayersSortedByScore.Length; i++)
                if (Utilities.IsValid(PlayersSortedByScore[i]))
                    PlayersSortedByScore[i].ScoreboardPositionByScore = i;

            for (var i = 0; i < PlayersSortedByTime.Length; i++)
                if (Utilities.IsValid(PlayersSortedByTime[i]))
                    PlayersSortedByTime[i].ScoreboardPositionByTime = i;
        }
    }
}
