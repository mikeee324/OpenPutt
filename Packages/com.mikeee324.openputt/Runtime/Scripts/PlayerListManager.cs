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
    public class PlayerListManager : OpenPuttTimeSlicer
    {
        [Header("This script keeps track of what positions players are in on the scoreboards - You only need to assign OpenPutt in this script")]
        public OpenPutt openPutt;

        public PlayerManager[] PlayersSortedByScore = new PlayerManager[0]; // { get; private set; }
        public PlayerManager[] PlayersSortedByTime = new PlayerManager[0];  // { get; private set; }


        private PlayerManager[] TempPlayersSortedByScore = new PlayerManager[0];
        private PlayerManager[] TempPlayersSortedByTime = new PlayerManager[0];

        private ScoreboardManager ScoreboardManager => openPutt.scoreboardManager;
        private PlayerManager[] allPlayers;

        private int updatesRequested = 0;

        void Start()
        {
            // We don't want to run on first start - nothing to do anyway
            enabled = false;
        }

        /// <summary>
        /// Called when a players score or state has changed
        /// </summary>
        public void OnPlayerUpdate()
        {
            updatesRequested += 1;
            enabled = true;
        }

        protected override void _StartUpdateFrame()
        {
        }

        protected override void _EndUpdateFrame()
        {
        }

        protected override void _OnUpdateItem(int index)
        {
            var playerManager = allPlayers[index];

            if (!Utilities.IsValid(playerManager)) return;
            if (!Utilities.IsValid(playerManager.Owner)) return;

            // We only add people who have started playing
            if (ScoreboardManager.hideInactivePlayers && !playerManager.PlayerHasStartedPlaying)
                return;

            playerManager.scoreboardRowNeedsUpdating = true;

            // Cache the data we need so we don't repeat this work every time we loop through the players list
            var newPlayerID = playerManager.PlayerID;
            var newPlayerScore = playerManager.PlayerTotalScore;
            var newPlayerTime = playerManager.PlayerTotalTime;

            // Add this player to their correct position in the list sorted by score
            var scorePos = 0;
            var timePos = 0;
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

        protected override void _OnUpdateStarted()
        {
            TempPlayersSortedByScore = new PlayerManager[0];
            TempPlayersSortedByTime = new PlayerManager[0];
            allPlayers = new PlayerManager[openPutt.allPlayerManagers.Length];
            openPutt.allPlayerManagers.CopyTo(allPlayers, 0);
            numberOfObjects = allPlayers.Length;
        }

        protected override void _OnUpdateEnded()
        {
            PlayersSortedByScore = TempPlayersSortedByScore;
            PlayersSortedByTime = TempPlayersSortedByTime;
            
            ScoreboardManager.RequestRefresh();
            
            updatesRequested -= 1;
            if (updatesRequested <= 0)
            {
                updatesRequested = 0;
                enabled = false;
            }
        }
    }
}