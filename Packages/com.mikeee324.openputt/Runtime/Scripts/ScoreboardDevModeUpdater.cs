﻿using UdonSharp;

namespace mikeee324.OpenPutt
{
    public class ScoreboardDevModeUpdater : UdonSharpBehaviour
    {
        public Scoreboard scoreboard;
        private OpenPutt openPutt;
        private PlayerManager localPlayerManager;

        void Start()
        {

        }

        private void Update()
        {
            if (openPutt == null)
                openPutt = scoreboard.manager.openPutt;

            if (openPutt != null && localPlayerManager == null)
                localPlayerManager = openPutt.LocalPlayerManager;

            if (localPlayerManager == null)
                return;

            scoreboard.devModeLastClubHitSpeed.text = string.Format("{0:F2}", localPlayerManager.golfClubHead.LastKnownHitVelocity);
            scoreboard.devModeBallSpeed.text = string.Format("{0:F2}", localPlayerManager.golfBall.BallCurrentSpeed);
        }
    }
}