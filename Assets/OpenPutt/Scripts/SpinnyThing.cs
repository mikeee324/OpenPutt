
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

        private Vector3 originalPosition;
        private Quaternion originalRotation;
        public bool lockRotation = true;

        void Start()
        {
            rb = GetComponent<Rigidbody>();

            originalPosition = rb.position;
            originalRotation = transform.localRotation;

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
                transform.localRotation = new Quaternion(
                    rotationTorque.x == 0.0f ? originalRotation.x : transform.localRotation.x,
                    rotationTorque.y == 0.0f ? originalRotation.y : transform.localRotation.y,
                    rotationTorque.z == 0.0f ? originalRotation.z : transform.localRotation.z,
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