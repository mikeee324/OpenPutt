﻿using UdonSharp;
using UnityEngine.XR;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
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
            if (!Utilities.IsValid(openPutt))
                openPutt = scoreboard.manager.openPutt;

            if (Utilities.IsValid(openPutt) && !Utilities.IsValid(localPlayerManager))
                localPlayerManager = openPutt.LocalPlayerManager;

            if (!Utilities.IsValid(localPlayerManager))
                return;

            scoreboard.devModeLastClubHitSpeed.text = $"{localPlayerManager.golfClubHead.LastKnownHitVelocity:F2} {localPlayerManager.golfClubHead.LastKnownHitType}";
            scoreboard.devModeLastClubHitDirBias.text = $"{localPlayerManager.golfClubHead.LastKnownHitDirBias * 100f:F0}%";
            scoreboard.devModeBallSpeed.text = $"{localPlayerManager.golfBall.BallCurrentSpeed:F2}";


            var clubHand = localPlayerManager.golfClub.CurrentHand;
            if (clubHand == VRC_Pickup.PickupHand.None)
            {
                scoreboard.devModeClubSpeed.text = $"Not Holding";
            }
            else
            {
                var hand = localPlayerManager.golfClub.CurrentHand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;
                var offset = openPutt.controllerTracker.CalculateLocalOffsetFromWorldPosition(hand, localPlayerManager.golfClubHead.transform.position);
                var headVelocity = openPutt.controllerTracker.GetVelocityAtOffset(hand, offset);
                scoreboard.devModeClubSpeed.text = $"{headVelocity.magnitude:F2}";
            }
        }
    }
}