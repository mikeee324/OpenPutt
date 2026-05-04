using dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public enum GolfBalLForceZoneType
    {
        /// <summary>
        /// Applies a force in a particular direction
        /// </summary>
        Normal,

        /// <summary>
        /// Changes the gravity direction of the ball, can be used to disable gravity as well (Set forceMagnitude to 0)
        /// </summary>
        Gravity,

        /// <summary>
        /// Pulls the ball towards the position of this game object
        /// </summary>
        CenterPull,

        /// <summary>
        /// Pushes the ball away from the position of this game object
        /// </summary>
        CenterPush,
        
        /// <summary>
        /// Adjusts ball gravity to always go towards a particular capsule collider 
        /// </summary>
        CapsuleGravity
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(-500)]
    public class GolfBallForceZone : UdonSharpBehaviour
    {
        [Tooltip("The magnitude of the force to apply in the forward direction of this object.")]
        public float forceMagnitude = 10f;

        [Tooltip("When ticked the balls rigidbody gravity will be disabled while inside this collider")]
        public GolfBalLForceZoneType forceType = GolfBalLForceZoneType.Normal;

        [Header("Gizmo Settings")] [Tooltip("Color of the gizmo arrows.")]
        public Color gizmoColor = Color.yellow;

        [Range(0.2f, 10f), Tooltip("Desired minimum spacing between arrows along the local X-axis.")]
        public float xArrowSpacing = .3f;

        [Range(0.2f, 10f), Tooltip("Desired minimum spacing between arrows along the local Y-axis.")]
        public float yArrowSpacing = .3f;

        [Range(0.2f, 10f), Tooltip("Desired minimum spacing between arrows along the local Z-axis.")]
        public float zArrowSpacing = .3f;

        [Range(1, 100f), Tooltip("Maximum number of arrows to draw along any single axis")]
        public int maxArrowsPerAxis = 20;

        [Range(0.01f, 1f), Tooltip("Size of the arrowhead.")]
        public float arrowHeadSize = 0.05f;

        [Range(0.01f, 1f), Tooltip("Length of the line segment before the arrowhead.")]
        public float arrowLineLength = 0.1f;

        [FormerlySerializedAs("mainCollider"), Tooltip("Collider that defines the area this effect happens")]
        public Collider forceAreaCollider;
        
        [Tooltip("Certain force types need a reference to the floor collider to work properly. This is only required for CapsuleGravity force types.")]
        public Collider floorCollider;

        private GolfBallController golfBall;

        private void Awake()
        {
            forceAreaCollider = GetComponent<Collider>();

            // Ensure the collider is a trigger for force application
            if (!forceAreaCollider.isTrigger)
                forceAreaCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Utilities.IsValid(other) || !Utilities.IsValid(other.gameObject)) return;

            var ball = other.gameObject.GetComponent<GolfBallController>();

            if (!Utilities.IsValid(ball)) return;
            if (ball.playerManager.Owner != Networking.LocalPlayer) return;

            golfBall = ball;


            switch (forceType)
            {
                case GolfBalLForceZoneType.Gravity:
                    golfBall.gravityDirection = transform.forward;
                    golfBall.gravityMagnitude = forceMagnitude;

                    golfBall._OnBallEnterGravityZone();
                    break;
                case GolfBalLForceZoneType.CenterPull:
                    golfBall.gravityDirection = transform.position - golfBall.transform.position;
                    golfBall.gravityMagnitude = forceMagnitude;

                    golfBall._OnBallEnterGravityZone();
                    break;
                case GolfBalLForceZoneType.CapsuleGravity:
                    if (forceAreaCollider.GetType() == typeof(CapsuleCollider))
                    {
                        golfBall.gravityDirection = ((CapsuleCollider)floorCollider).ClosestPoint(golfBall.transform.position) - golfBall.transform.position;
                        golfBall.gravityMagnitude = forceMagnitude;
                    }

                    golfBall._OnBallEnterGravityZone();
                    break;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!Utilities.IsValid(other)) return;
            if (!Utilities.IsValid(other.gameObject)) return;

            if (!Utilities.IsValid(golfBall)) return;
            if (!Utilities.IsValid(golfBall.gameObject)) return;
            if (other.gameObject != golfBall.gameObject) return;

            switch (forceType)
            {
                case GolfBalLForceZoneType.Gravity:
                case GolfBalLForceZoneType.CenterPull:
                case GolfBalLForceZoneType.CapsuleGravity:
                    golfBall._OnBallExitGravityZone();
                    break;
            }

            golfBall = null;
        }

        /// <summary>
        /// Called when another collider stays within this trigger collider.
        /// </summary>
        /// <param name="other">The other Collider.</param>
        private void OnTriggerStay(Collider other)
        {
            if (forceMagnitude <= 0) return;
            if (!Utilities.IsValid(golfBall) || !golfBall.BallIsMoving) return;

            switch (forceType)
            {
                case GolfBalLForceZoneType.Normal:
                    golfBall.ballRigidbody.AddForce(transform.forward * forceMagnitude, ForceMode.Force);
                    break;
                case GolfBalLForceZoneType.CenterPull:
                    golfBall.gravityDirection = transform.position - golfBall.transform.position;
                    golfBall.gravityMagnitude = forceMagnitude;
                    break;
                case GolfBalLForceZoneType.CenterPush:
                    golfBall.ballRigidbody.AddForce((golfBall.transform.position - transform.position).normalized * forceMagnitude, ForceMode.Acceleration);
                    break;
                case GolfBalLForceZoneType.CapsuleGravity:
                    if (forceAreaCollider.GetType() == typeof(CapsuleCollider))
                    {
                        golfBall.gravityDirection = ((CapsuleCollider)floorCollider).ClosestPoint(golfBall.transform.position) - golfBall.transform.position;
                        golfBall.gravityMagnitude = forceMagnitude;
                    }
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (forceAreaCollider == null) return;
            var originalMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;
            var localCenter = Vector3.zero;
            var localGridSize = Vector3.one;

            var colliderType = forceAreaCollider.GetType();

            BoxCollider box = null;
            SphereCollider sphere = null;
            CapsuleCollider capsule = null;

            if (colliderType == typeof(BoxCollider))
            {
                box = (BoxCollider)forceAreaCollider;
                localCenter = box.center;
                localGridSize = box.size;
            }
            else if (colliderType == typeof(SphereCollider))
            {
                sphere = (SphereCollider)forceAreaCollider; // Explicit cast
                localCenter = sphere.center;
                localGridSize = new Vector3(sphere.radius * 2, sphere.radius * 2, sphere.radius * 2);
            }
            else if (colliderType == typeof(CapsuleCollider))
            {
                capsule = (CapsuleCollider)forceAreaCollider; // Explicit cast
                localCenter = capsule.center;
                // Approximate size for grid bounds based on orientation
                if (capsule.direction == 0) localGridSize = new Vector3(capsule.height, capsule.radius * 2, capsule.radius * 2);
                else if (capsule.direction == 1) localGridSize = new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2);
                else localGridSize = new Vector3(capsule.radius * 2, capsule.radius * 2, capsule.height);
            }
            else
            {
                return;
            }

            Gizmos.color = Color.black;
            Gizmos.DrawSphere(localCenter, .05f);
            Gizmos.DrawWireSphere(localCenter, .05f);
            Gizmos.color = gizmoColor;

            var localGridMin = localCenter - localGridSize / 2f;

            var xArrowCount = 0;
            if (xArrowSpacing > 0 && localGridSize.x > 0)
            {
                var potentialCountFloat = (localGridSize.x / xArrowSpacing) - 1f;
                var potentialCountInt = Mathf.FloorToInt(Mathf.Clamp(potentialCountFloat, -1f, maxArrowsPerAxis));
                xArrowCount = Mathf.Max(0, potentialCountInt);
            }

            var yArrowCount = 0;
            if (yArrowSpacing > 0 && localGridSize.y > 0)
            {
                var potentialCountFloat = (localGridSize.y / yArrowSpacing) - 1f;
                var potentialCountInt = Mathf.FloorToInt(Mathf.Clamp(potentialCountFloat, -1f, maxArrowsPerAxis));
                yArrowCount = Mathf.Max(0, potentialCountInt);
            }

            var zArrowCount = 0;
            if (zArrowSpacing > 0 && localGridSize.z > 0)
            {
                var potentialCountFloat = (localGridSize.z / zArrowSpacing) - 1f;
                var potentialCountInt = Mathf.FloorToInt(Mathf.Clamp(potentialCountFloat, -1f, maxArrowsPerAxis));
                zArrowCount = Mathf.Max(0, potentialCountInt);
            }

            for (var i = 0; i <= xArrowCount; i++)
            {
                for (var j = 0; j <= yArrowCount; j++)
                {
                    for (var k = 0; k <= zArrowCount; k++)
                    {
                        var potentialLocalPoint = Vector3.zero;

                        if (colliderType == typeof(BoxCollider))
                        {
                            potentialLocalPoint.x = Mathf.Lerp(localCenter.x - box.size.x / 2f, localCenter.x + box.size.x / 2f, (i + 0.5f) / (xArrowCount + 1f));
                            potentialLocalPoint.y = Mathf.Lerp(localCenter.y - box.size.y / 2f, localCenter.y + box.size.y / 2f, (j + 0.5f) / (yArrowCount + 1f));
                            potentialLocalPoint.z = Mathf.Lerp(localCenter.z - box.size.z / 2f, localCenter.z + box.size.z / 2f, (k + 0.5f) / (zArrowCount + 1f));
                        }
                        else
                        {
                            var xStep = (xArrowCount > 0) ? localGridSize.x / (xArrowCount + 1) : 0;
                            var yStep = (yArrowCount > 0) ? localGridSize.y / (yArrowCount + 1) : 0;
                            var zStep = (zArrowCount > 0) ? localGridSize.z / (zArrowCount + 1) : 0;

                            potentialLocalPoint = localGridMin + new Vector3(xStep * (i + 0.5f), yStep * (j + 0.5f), zStep * (k + 0.5f));

                            if (xArrowCount == 0) potentialLocalPoint.x = localCenter.x;
                            if (yArrowCount == 0) potentialLocalPoint.y = localCenter.y;
                            if (zArrowCount == 0) potentialLocalPoint.z = localCenter.z;
                        }

                        var isInside = false;
                        if (colliderType == typeof(BoxCollider))
                        {
                            isInside = true;
                        }
                        else if (colliderType == typeof(SphereCollider))
                        {
                            isInside = Vector3.Distance(potentialLocalPoint, localCenter) <= sphere.radius;
                        }
                        else if (colliderType == typeof(CapsuleCollider))
                        {
                            var axisStart = localCenter;
                            var axisEnd = localCenter;
                            var halfHeight = (capsule.height / 2f) - capsule.radius;

                            if (capsule.direction == 0)
                            {
                                axisStart.x -= halfHeight;
                                axisEnd.x += halfHeight;
                            }
                            else if (capsule.direction == 1)
                            {
                                axisStart.y -= halfHeight;
                                axisEnd.y += halfHeight;
                            }
                            else
                            {
                                axisStart.z -= halfHeight;
                                axisEnd.z += halfHeight;
                            }

                            var closestPointOnAxis = ClosestPointOnLineSegment(axisStart, axisEnd, potentialLocalPoint);

                            isInside = Vector3.Distance(potentialLocalPoint, closestPointOnAxis) <= capsule.radius;
                        }

                        if (!isInside)
                            continue;

                        var arrowBaseLocal = potentialLocalPoint;

                        var arrowDirectionLocal = Vector3.forward;
                        if (forceType == GolfBalLForceZoneType.CenterPull)
                            arrowDirectionLocal = (localCenter - arrowBaseLocal).normalized;
                        else if (forceType == GolfBalLForceZoneType.CapsuleGravity)
                            arrowDirectionLocal = (localCenter - arrowBaseLocal).normalized;
                        else if (forceType == GolfBalLForceZoneType.CenterPush)
                            arrowDirectionLocal = (arrowBaseLocal - localCenter).normalized;

                        var arrowTipLocal = arrowBaseLocal + arrowDirectionLocal * arrowLineLength;

                        Gizmos.DrawLine(arrowBaseLocal, arrowTipLocal);

                        var headBaseLocal = arrowTipLocal - arrowDirectionLocal * arrowHeadSize;

                        var localAxis1 = (Mathf.Abs(Vector3.Dot(arrowDirectionLocal, Vector3.up)) > 0.95f) ? Vector3.right : Vector3.up;
                        var localPerp1 = Vector3.Cross(arrowDirectionLocal, localAxis1).normalized;
                        var localPerp2 = Vector3.Cross(arrowDirectionLocal, localPerp1).normalized;

                        var headPoint1Local = headBaseLocal + (localPerp1 * arrowHeadSize * 0.5f);
                        var headPoint2Local = headBaseLocal - (localPerp1 * arrowHeadSize * 0.5f);
                        var headPoint3Local = headBaseLocal + (localPerp2 * arrowHeadSize * 0.5f);
                        var headPoint4Local = headBaseLocal - (localPerp2 * arrowHeadSize * 0.5f);

                        Gizmos.DrawLine(arrowTipLocal, headPoint1Local);
                        Gizmos.DrawLine(arrowTipLocal, headPoint2Local);
                        Gizmos.DrawLine(arrowTipLocal, headPoint3Local);
                        Gizmos.DrawLine(arrowTipLocal, headPoint4Local);

                        Gizmos.DrawLine(headPoint1Local, headPoint3Local);
                        Gizmos.DrawLine(headPoint1Local, headPoint4Local);
                        Gizmos.DrawLine(headPoint2Local, headPoint3Local);
                        Gizmos.DrawLine(headPoint2Local, headPoint4Local);
                    }
                }
            }

            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);

            if (colliderType == typeof(BoxCollider))
            {
                Gizmos.DrawCube(localCenter, box.size);
            }
            else if (colliderType == typeof(SphereCollider))
            {
                Gizmos.DrawSphere(localCenter, sphere.radius);
            }

            Gizmos.matrix = originalMatrix;
        }

        private Vector3 ClosestPointOnLineSegment(Vector3 segmentStart, Vector3 segmentEnd, Vector3 point)
        {
            var line = segmentEnd - segmentStart;
            var lenSq = line.sqrMagnitude;
            if (lenSq == 0.0f) return segmentStart;
            var dot = Vector3.Dot(point - segmentStart, line) / lenSq;
            dot = Mathf.Clamp01(dot);
            return segmentStart + dot * line;
        }
#endif
    }
}