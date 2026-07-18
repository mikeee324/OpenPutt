using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>Moves the local player's ball to targetPosition when interacted with.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttPedestalBallButton : UdonSharpBehaviour
    {
        [OpenPuttDescription("A button that, when interacted with, teleports the local player's golf ball to a chosen position - handy for fetching a stuck or lost ball.")]
        public OpenPutt openPutt;

        [Tooltip("Where the local player's ball will be moved to when this button is interacted with")]
        public Transform targetPosition;

        [Tooltip("If enabled the ball will have BallIsMoving set to true after being moved so it can drop/roll instead of staying frozen in place")]
        public bool enableBallMovingAfterMove = false;

        public override void Interact()
        {
            FetchItem();
        }

        /// <summary>Moves the local player's ball to targetPosition.</summary>
        public void FetchItem()
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfBall) || !Utilities.IsValid(targetPosition))
                return;

            var playerManager = openPutt.LocalPlayerManager;
            var golfBall = playerManager.golfBall;

            // Make sure the ball is active/visible before touching its physics, otherwise the
            // changes below won't take effect properly on a disabled GameObject
            playerManager.BallVisible = true;

            golfBall.BallIsMoving = false;

            golfBall._SetPosition(targetPosition.position);
            golfBall._SetRespawnPosition(targetPosition.position);

            if (enableBallMovingAfterMove)
                golfBall.BallIsMoving = true;

            playerManager._RequestSync(syncNow: true);
        }
    }
}
