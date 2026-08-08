using dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace com.dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(20000)]
    public class GolfClubColliderVisualiser : UdonSharpBehaviour
    {
        [OpenPuttDescription("A debugging aid that draws lines showing the club head's recent swing path and where the ball was sent after a hit. Not needed for normal gameplay.")]
        [OpenPuttFoldoutGroup("References")]
        public GolfClub club;
        [OpenPuttFoldoutGroup("References")]
        public ControllerTracker ControllerTracker;
        [OpenPuttFoldoutGroup("References")]
        public BoxCollider clubFollow;
        [OpenPuttFoldoutGroup("References")]
        public Transform follower;
        [OpenPuttFoldoutGroup("References")]
        public MeshRenderer meshRenderer;
        [OpenPuttFoldoutGroup("References")]
        public LineRenderer hitDirection;
        [OpenPuttFoldoutGroup("References")]
        public LineRenderer rawHitDirection;
        [OpenPuttFoldoutGroup("References")]
        public LineRenderer clubLine;
        [OpenPuttFoldoutGroup("References")]
        public LineRenderer clubData;
        [OpenPuttFoldoutGroup("References")]
        public Gradient gradient;

        void Start()
        {
            ClearLines();
        }

        /// <summary>
        /// Wipes every debug line. Called on arm so a swing never shows leftover geometry from the
        /// previous hit, and on start so nothing draws at the world origin before the first hit.
        /// </summary>
        public void ClearLines()
        {
            if (Utilities.IsValid(hitDirection))
                hitDirection.positionCount = 0;
            if (Utilities.IsValid(rawHitDirection))
                rawHitDirection.positionCount = 0;
            if (Utilities.IsValid(clubLine))
                clubLine.positionCount = 0;
            if (Utilities.IsValid(clubData))
                clubData.positionCount = 0;
        }

        public override void PostLateUpdate()
        {
            if (follower.gameObject.activeInHierarchy)
            {
                transform.position = follower.transform.TransformPoint(clubFollow.center);
                transform.rotation = follower.transform.rotation;

                hitDirection.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                rawHitDirection.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clubLine.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var scale = new Vector3(clubFollow.size.x, clubFollow.size.y, clubFollow.size.z);
                transform.localScale = scale;
                if (!meshRenderer.enabled)
                    meshRenderer.enabled = true;
            }
            else
            {
                if (meshRenderer.enabled)
                    meshRenderer.enabled = false;
            }
        }

        public void OnBallHit(Vector3 ballPos, Vector3 lastHitPos, Vector3 velocity, Vector3 rawVelocity)
        {
            hitDirection.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rawHitDirection.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clubLine.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            hitDirection.positionCount = 2;
            hitDirection.SetPosition(0, ballPos);
            hitDirection.SetPosition(1, ballPos + (velocity.normalized * .7f));

            var offset = new Vector3(0, .01f, 0);
            rawHitDirection.positionCount = 2;
            rawHitDirection.SetPosition(0, ballPos + offset);
            rawHitDirection.SetPosition(1, ballPos + offset + (rawVelocity.normalized * .5f));

            var hand = club.CurrentHand == VRC_Pickup.PickupHand.Right ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand;
            var clubHeadOffset = ControllerTracker.CalculateLocalOffsetFromWorldPosition(hand, club.headBoxCollider.transform.TransformPoint(club.headBoxCollider.center));
            var history = ControllerTracker.GetHistoricalPositionsArrayAtOffset(hand, clubHeadOffset, 1, ControllerTracker.bufferSize - 1);

            // Draw the window the tracker actually measured over, rather than recomputing frame
            // indices here - the window is resolved from timestamps now, not a fixed frame count
            var window = ControllerTracker.GetVelocityWindowEndpointsAtOffset(hand, clubHeadOffset);
            clubData.positionCount = window.Length;
            if (window.Length > 0)
                clubData.SetPositions(window);

            clubLine.positionCount = history.Length;
            clubLine.SetPositions(history);
            clubLine.sharedMaterial.color = gradient.Evaluate(velocity.magnitude / club.ClubType.GetTypicalMaxSpeed());
        }
    }
}