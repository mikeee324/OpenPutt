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
            if (Utilities.IsValid(clubLine) && Utilities.IsValid(ControllerTracker))
                clubLine.positionCount = ControllerTracker.bufferSize;
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

            hitDirection.SetPosition(0, ballPos);
            hitDirection.SetPosition(1, ballPos + (velocity.normalized * .7f));

            var offset = new Vector3(0, .01f, 0);
            rawHitDirection.SetPosition(0, ballPos + offset);
            rawHitDirection.SetPosition(1, ballPos + offset + (rawVelocity.normalized * .5f));

            var hand = club.CurrentHand == VRC_Pickup.PickupHand.Right ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand;
            var headOffset = ControllerTracker.CalculateLocalOffsetFromWorldPosition(hand, club.headBoxCollider.transform.TransformPoint(club.headBoxCollider.center));
            var history = ControllerTracker.GetHistoricalPositionsArrayAtOffset(hand, headOffset, 1, ControllerTracker.bufferSize - 1);

            clubData.positionCount = 2;
            clubData.SetPositions(new Vector3[] {history[ControllerTracker.endOffset], history[ControllerTracker.endOffset + ControllerTracker.lookbackFrames]});

            clubLine.positionCount = history.Length;
            clubLine.SetPositions(history);
            clubLine.sharedMaterial.color = gradient.Evaluate(velocity.magnitude / club.ClubType.GetTypicalMaxSpeed());
        }
    }
}