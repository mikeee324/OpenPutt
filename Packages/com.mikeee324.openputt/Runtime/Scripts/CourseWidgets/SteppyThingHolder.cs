using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SteppyThingHolder : UdonSharpBehaviour
    {
        [OpenPuttDescription("A small ledge that holds the ball in place at the edge of a step until something collides with it, then lets the ball roll past.")]
        [OpenPuttFoldoutGroup("References")]
        public Collider myCollider;

        [OpenPuttFoldoutGroup("References")]
        public MeshRenderer myRenderer;

        void Start()
        {
            if (!Utilities.IsValid(myCollider))
                myCollider = GetComponent<Collider>();
            if (!Utilities.IsValid(myRenderer))
                myRenderer = GetComponent<MeshRenderer>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            myCollider.isTrigger = false;
            myRenderer.enabled = true;
        }

        private void OnCollisionExit(Collision collision)
        {
            myCollider.isTrigger = true;
            myRenderer.enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            myCollider.isTrigger = false;
            myRenderer.enabled = true;
        }

        void OnTriggerExit(Collider other)
        {
            myCollider.isTrigger = true;
            myRenderer.enabled = false;
        }
    }
}