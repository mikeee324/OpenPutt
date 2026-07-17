using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>Moves the local player's club to targetPosition when interacted with.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttPedestalClubButton : UdonSharpBehaviour
    {
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

            var playerManager = openPutt.LocalPlayerManager;
            var golfClub = playerManager.golfClub;

            // Can't move the club while it's attached to a hand, so drop it first
            if (golfClub.CurrentHand != VRC_Pickup.PickupHand.None)
                golfClub.pickup.Drop();

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
        }
    }
}
