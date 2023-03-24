
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Engines;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
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
        private Vector3 originalRotation;
        public bool lockRotation = true;

        void Start()
        {
            rb = GetComponent<Rigidbody>();

            originalPosition = rb.position;
            originalRotation = transform.localEulerAngles;

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

            if (lockRotation)
            {
                transform.localEulerAngles = new Vector3(
                    rotationTorque.x == 0.0f ? originalRotation.x : transform.localEulerAngles.x,
                    rotationTorque.y == 0.0f ? originalRotation.y : transform.localEulerAngles.y,
                    rotationTorque.z == 0.0f ? originalRotation.z : transform.localEulerAngles.z
                    );
            }
        }

        void LateUpdate()
        {
            transform.position = originalPosition;
        }
    }
}