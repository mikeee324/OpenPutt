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

        [Tooltip("How long the player has to interact again to confirm skipping their current course before the confirmation expires")]
        public float confirmationWindow = 5f;

        private bool awaitingSkipConfirmation;

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

            // Fetching the ball elsewhere abandons whatever course is currently being played, same as
            // pressing Use while holding the ball on the shoulder mount (see GolfBallController._OnScriptUse) -
            // require a second interact within the confirmation window so players don't lose a course by accident
            if (Utilities.IsValid(playerManager.CurrentCourse))
            {
                if (!awaitingSkipConfirmation)
                {
                    awaitingSkipConfirmation = true;
                    SendCustomEventDelayedSeconds(nameof(ClearSkipConfirmation), confirmationWindow);

                    if (Utilities.IsValid(openPutt.notifications))
                        openPutt.notifications.InstantiateCalloutBox("Click again to skip your current course and fetch the ball");
                    return;
                }

                awaitingSkipConfirmation = false;
                playerManager._SkipCurrentCourse();
            }

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

        /// <summary>Called via SendCustomEventDelayedSeconds to expire a pending skip confirmation.</summary>
        public void ClearSkipConfirmation()
        {
            awaitingSkipConfirmation = false;
        }
    }
}
