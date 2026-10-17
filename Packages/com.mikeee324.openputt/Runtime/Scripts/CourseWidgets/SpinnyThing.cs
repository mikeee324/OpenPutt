using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public enum SpinnyThingAxis
    {
        X,
        Y,
        Z
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), RequireComponent(typeof(Rigidbody))]
    public class SpinnyThing : UdonSharpBehaviour
    {
        [OpenPuttDescription("A spinning obstacle that knocks the ball about. It spins on the spot around one of its own axes, in time for every player. Select it to see the spin axis and direction in the scene view.")]
        [OpenPuttFoldoutGroup("Spin")]
        [Tooltip("Which of this object's own axes it spins around - the scene view gizmo shows it when selected")]
        public SpinnyThingAxis spinAxis = SpinnyThingAxis.Y;

        [OpenPuttFoldoutGroup("Spin")]
        [Tooltip("Spin the other way")]
        public bool reverseDirection;

        [OpenPuttFoldoutGroup("Spin")]
        [Range(1f, 200f), Tooltip("How fast it spins, in full turns per minute")]
        public float spinSpeedRpm = 30f;

        [OpenPuttFoldoutGroup("Spin")]
        [Range(0f, 360f), Tooltip("Where in its turn it is at the start, in degrees. Give spinners next to each other different values so they don't all line up")]
        public float startAngle;

        [OpenPuttFoldoutGroup("Spin")]
        [Tooltip("Keeps every player's spinner in the same position. Off = each player's copy starts spinning when it loads")]
        public bool syncToServerTime = true;

        [OpenPuttFoldoutGroup("Collision")]
        [Tooltip("Colliders this spinner should pass straight through, e.g. the floor or walls it's set into. Golf clubs are ignored automatically")]
        public Collider[] collidersToIgnore;

        [OpenPuttFoldoutGroup("Advanced")]
        [Tooltip("Heavier spinners get nudged less by the ball between corrections. Rarely needs changing")]
        public float rigidBodymass = 20f;

        [OpenPuttFoldoutGroup("Advanced")]
        public CollisionDetectionMode rigidbodyTargetCollisionMode = CollisionDetectionMode.ContinuousDynamic;

        // Old settings - converted to the ones above by OnValidate, and still read at runtime if that never ran
        [HideInInspector]
        public Vector3 rotationTorque = Vector3.zero;

        [HideInInspector]
        public float maxRotationSpeed;

        /// Errors bigger than this snap instead of being steered out, e.g. after being re-enabled
        private const float SnapAngle = 30f;

        /// Fraction of the remaining error steered out per physics step
        private const float CorrectionRate = 0.5f;

        private Rigidbody rb;
        private bool started;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 localAxis = Vector3.up;
        private float radiansPerSecond;
        private float localStartTime;
        private Collider[] spinnerColliders;

        void Start()
        {
            rb = GetComponent<Rigidbody>();

            originalPosition = rb.position;
            originalRotation = transform.localRotation;
            localStartTime = Time.time;

            if (rotationTorque.sqrMagnitude > 0f)
            {
                localAxis = rotationTorque.normalized;
                radiansPerSecond = maxRotationSpeed;
            }
            else
            {
                localAxis = GetLocalAxis();
                radiansPerSecond = spinSpeedRpm * 2f * Mathf.PI / 60f;
            }

            rb.mass = rigidBodymass;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezePosition;
            rb.centerOfMass = Vector3.zero;
            rb.inertiaTensorRotation = Quaternion.identity;

            spinnerColliders = GetComponentsInChildren<Collider>();

            if (Utilities.IsValid(collidersToIgnore))
            {
                foreach (var spinnerCollider in spinnerColliders)
                {
                    foreach (var toIgnore in collidersToIgnore)
                    {
                        if (Utilities.IsValid(toIgnore))
                            Physics.IgnoreCollision(spinnerCollider, toIgnore, true);
                    }
                }
            }

            started = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Utilities.IsValid(collision) || !Utilities.IsValid(collision.rigidbody) || !Utilities.IsValid(spinnerColliders))
                return;

            // Clubs and spinners both drive their own velocity, so they fight and the blade flips about
            var other = collision.rigidbody.gameObject;
            if (!Utilities.IsValid(other.GetComponent<GolfClubCollider>()) && !Utilities.IsValid(other.GetComponent<GolfClub>()))
                return;

            foreach (var spinnerCollider in spinnerColliders)
                Physics.IgnoreCollision(spinnerCollider, collision.collider, true);
        }

        void FixedUpdate()
        {
            if (!started || rb.isKinematic)
                return;

            var parentRotation = Utilities.IsValid(transform.parent) ? transform.parent.rotation : Quaternion.identity;
            var spinVelocity = parentRotation * originalRotation * localAxis * radiansPerSecond;

            // Where the blade should be now - every client works this out from the same clock, so they agree
            var targetRotation = parentRotation * originalRotation * Quaternion.AngleAxis(GetSpinAngle(), localAxis);
            var error = targetRotation * Quaternion.Inverse(rb.rotation);
            error.ToAngleAxis(out var errorAngle, out var errorAxis);
            if (float.IsNaN(errorAngle))
                errorAngle = 0f;
            else if (errorAngle > 180f)
                errorAngle -= 360f;

            if (Mathf.Abs(errorAngle) > SnapAngle)
            {
                rb.rotation = targetRotation;
                errorAngle = 0f;
            }

            // Velocity rather than a pose, so PhysX still knows how fast the blade is moving when it hits the ball
            var correction = Mathf.Abs(errorAngle) < 0.001f ? Vector3.zero : errorAxis * (errorAngle * Mathf.Deg2Rad * CorrectionRate / Time.fixedDeltaTime);
            rb.maxAngularVelocity = radiansPerSecond * 2f + 10f;
            rb.angularVelocity = spinVelocity + correction;
        }

        private void OnEnable()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.collisionDetectionMode = rigidbodyTargetCollisionMode;
        }

        private void OnDisable()
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Nothing recorded yet if this is switched off before it ever started
            if (started)
            {
                transform.position = originalPosition;
                transform.localRotation = originalRotation;
            }

            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = true;
        }

        /// <summary>
        /// Spin angle in degrees, from the server clock when synced
        /// </summary>
        private float GetSpinAngle()
        {
            var degreesPerSecond = (double)radiansPerSecond * Mathf.Rad2Deg;

            // Milliseconds are kept in a double and wrapped to one turn, so the angle stays precise however long the server has been up
            var seconds = syncToServerTime ? Networking.GetServerTimeInMilliseconds() / 1000.0 : (double)(Time.time - localStartTime);
            return (float)((seconds * degreesPerSecond + startAngle) % 360.0);
        }

        /// <summary>
        /// The chosen axis in this object's own space, flipped when reversed
        /// </summary>
        private Vector3 GetLocalAxis()
        {
            var axis = Vector3.up;
            if (spinAxis == SpinnyThingAxis.X)
                axis = Vector3.right;
            else if (spinAxis == SpinnyThingAxis.Z)
                axis = Vector3.forward;

            return reverseDirection ? -axis : axis;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        // Converts the old torque vector + radians-per-second settings into axis/direction/RPM
        private void OnValidate()
        {
            if (rotationTorque.sqrMagnitude <= 0f)
                return;

            var abs = new Vector3(Mathf.Abs(rotationTorque.x), Mathf.Abs(rotationTorque.y), Mathf.Abs(rotationTorque.z));
            float component;
            if (abs.x >= abs.y && abs.x >= abs.z)
            {
                spinAxis = SpinnyThingAxis.X;
                component = rotationTorque.x;
            }
            else if (abs.y >= abs.z)
            {
                spinAxis = SpinnyThingAxis.Y;
                component = rotationTorque.y;
            }
            else
            {
                spinAxis = SpinnyThingAxis.Z;
                component = rotationTorque.z;
            }

            reverseDirection = component < 0f;
            spinSpeedRpm = Mathf.Clamp(maxRotationSpeed * 60f / (2f * Mathf.PI), 1f, 200f);
            rotationTorque = Vector3.zero;
            maxRotationSpeed = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            var axis = transform.rotation * (rotationTorque.sqrMagnitude > 0f ? rotationTorque.normalized : GetLocalAxis());
            var center = transform.position;

            // Sized to whatever is being spun
            var radius = 0.25f;
            foreach (var childCollider in GetComponentsInChildren<Collider>())
                radius = Mathf.Max(radius, childCollider.bounds.extents.magnitude);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(center - axis * radius * 0.5f, center + axis * radius * 0.5f);

            // Ring around the axis with arrowheads pointing the way it turns
            var radial = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized * radius;
            const int segments = 48;
            for (var i = 0; i < segments; i++)
            {
                var from = Quaternion.AngleAxis(360f * i / segments, axis) * radial;
                var to = Quaternion.AngleAxis(360f * (i + 1) / segments, axis) * radial;
                Gizmos.DrawLine(center + from, center + to);

                if (i % 12 != 0)
                    continue;

                var tangent = (to - from).normalized;
                var head = center + to;
                var size = radius * 0.12f;
                Gizmos.DrawLine(head, head - tangent * size + from.normalized * size * 0.5f);
                Gizmos.DrawLine(head, head - tangent * size - from.normalized * size * 0.5f);
            }
        }
#endif
    }
}
