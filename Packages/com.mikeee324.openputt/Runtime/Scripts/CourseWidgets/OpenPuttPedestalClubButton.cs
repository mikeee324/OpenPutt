using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>Moves the local player's club to targetPosition when interacted with.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttPedestalClubButton : UdonSharpBehaviour
    {
        [OpenPuttDescription("A button that, when interacted with, teleports the local player's golf club to a chosen position - handy for fetching a stuck or lost club.")]
        public OpenPutt openPutt;

        [Tooltip("Where the local player's club will be moved to when this button is interacted with")]
        public Transform targetPosition;

        [Tooltip("If enabled the club will have its physics re-enabled after being moved so it can drop instead of staying frozen in place")]
        public bool enableClubPhysicsAfterMove = false;

        public override void Interact()
        {
            FetchItem();
        }

        /// <summary>Moves the local player's club to targetPosition, dropping it first if held.</summary>
        public void FetchItem()
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfClub) || !Utilities.IsValid(targetPosition))
                return;

            // Desktop/mobile players don't physically hold the club - point them at Ball Cam instead
            if (!Networking.LocalPlayer.IsUserInVR())
            {
                ShowBallCamHint();
                return;
            }

            var playerManager = openPutt.LocalPlayerManager;
            var golfClub = playerManager.golfClub;

            if (!Utilities.IsValid(golfClub.openPuttSync))
                return;

            // Can't move the club while it's attached to a hand, so drop it first - the club can be
            // held either directly (golfClub.pickup) or via the shoulder holster (golfClub.shoulderPickup),
            // and only the one actually holding it will respond to Drop()
            if (golfClub.CurrentHand != VRC_Pickup.PickupHand.None)
            {
                if (Utilities.IsValid(golfClub.pickup))
                    golfClub.pickup.Drop();

                var shoulderPickup = golfClub.shoulderPickup;
                if (Utilities.IsValid(shoulderPickup) && Utilities.IsValid(shoulderPickup.pickup))
                    shoulderPickup.pickup.Drop();
            }

            // Make sure the club is active/visible before touching its physics, otherwise the
            // changes below won't take effect properly on a disabled GameObject
            playerManager.ClubVisible = true;

            golfClub.clubRigidbody.isKinematic = true;
            golfClub.clubRigidbody.position = targetPosition.position;
            golfClub.clubRigidbody.rotation = targetPosition.rotation;

            if (enableClubPhysicsAfterMove)
            {
                golfClub.clubRigidbody.isKinematic = false;
                golfClub.clubRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                golfClub.handleCollider.isTrigger = false;
                golfClub.shaftCollider.isTrigger = false;
            }

            playerManager._RequestSync(syncNow: true);

            golfClub.openPuttSync._RequestFastSync(forceSync: true);
        }

        private void ShowBallCamHint()
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.notifications))
                return;

            string hintText;
#if UNITY_ANDROID || UNITY_IOS
            hintText = "Tap the Ball Cam button to aim your shot";
#else
            var controllerDetector = Utilities.IsValid(openPutt.ballCam) && Utilities.IsValid(openPutt.ballCam.inputHandler) ? openPutt.ballCam.inputHandler.controllerDetector : null;
            bool usingGamepad = Utilities.IsValid(controllerDetector) && controllerDetector.currentInputMethod == VRCInputMethod.Controller;
            hintText = usingGamepad ? "Press RB to use Ball Cam" : "Press E to use Ball Cam";
#endif
            openPutt.notifications.InstantiateCalloutBox(hintText);
        }
    }
}
