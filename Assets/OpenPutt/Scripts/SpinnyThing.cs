
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Engines;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
    public enum RotationLockType
    {
        EulerAngles,
        Quaternions
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None), RequireComponent(typeof(Rigidbody))]
    public class SpinnyThing : UdonSharpBehaviour
    {
        public Vector3 rotationTorque = Vector3.zero;
        public float rigidBodymass = 20f;
        [Range(0.1f, 20f)]
        public float maxRotationSpeed = 0f;
        private Rigidbody rb;
        public LayerMask layersToIgnore;
        public Collider[] myColliders;
        public Collider[] collidersToIgnore;

        /// <summary>
        /// Stores the starting position of this spinny thing so we can lock the position manually in LateUpdate()
        /// </summary>
        private Vector3 originalPosition;
        /// <summary>
        /// Used when trying to lock the rotation of this object so it only spins on one axis<br/>
        /// Would be better to maybe use Quaternions but I can't figure out how to lock the axises with them.. so eulerAngles will do for now
        /// </summary>
        private Vector3 originalRotationEuler;
        private Quaternion originalRotationQuaternion;

        [Tooltip("Changing this to EulerAngles can be useful if your spinny thing keeps rotating slightly on axises that are supposed to be locked")]
        public RotationLockType lockRotationUsing = RotationLockType.Quaternions;

        void Start()
        {
            rb = GetComponent<Rigidbody>();

            originalPosition = rb.position;
            originalRotationEuler = transform.localEulerAngles;
            originalRotationQuaternion = transform.localRotation;

            rb.mass = rigidBodymass;
            rb.maxAngularVelocity = maxRotationSpeed;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Extrapolate;

            rb.centerOfMass = Vector3.zero;
            rb.inertiaTensorRotation = Quaternion.identity;

            foreach (Collider myCollider in myColliders)
            {
                foreach (Collider toIgnore in collidersToIgnore)
                    Physics.IgnoreCollision(myCollider, toIgnore, true);
            }
        }

        void FixedUpdate()
        {
            rb.maxAngularVelocity = maxRotationSpeed;

            rb.centerOfMass = Vector3.zero;
            rb.inertiaTensorRotation = Quaternion.identity;

            rb.AddRelativeTorque(rotationTorque, ForceMode.Acceleration);

            if (lockRotationUsing == RotationLockType.EulerAngles)
            {
                transform.localEulerAngles = new Vector3(
                    rotationTorque.x == 0.0f ? originalRotationEuler.x : transform.localEulerAngles.x,
                    rotationTorque.y == 0.0f ? originalRotationEuler.y : transform.localEulerAngles.y,
                    rotationTorque.z == 0.0f ? originalRotationEuler.z : transform.localEulerAngles.z
                );
            }
            else if (lockRotationUsing == RotationLockType.Quaternions)
            {
                transform.localRotation = new Quaternion(
                    rotationTorque.x == 0.0f ? originalRotationQuaternion.x : transform.localRotation.x,
                    rotationTorque.y == 0.0f ? originalRotationQuaternion.y : transform.localRotation.y,
                    rotationTorque.z == 0.0f ? originalRotationQuaternion.z : transform.localRotation.z,
                    transform.localRotation.w
                );
            }
        }

        void LateUpdate()
        {
            transform.position = originalPosition;
        }
    }
}