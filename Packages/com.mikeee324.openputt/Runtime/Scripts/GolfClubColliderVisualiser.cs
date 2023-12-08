
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace com.mikeee324.OpenPutt
{
    [DefaultExecutionOrder(20000)]
    public class GolfClubColliderVisualiser : UdonSharpBehaviour
    {
        public BoxCollider clubFollow;
        public Transform follower;
        public MeshRenderer meshRenderer;


        void Start()
        {

        }

        void FixedUpdate()
        {
            if (follower.gameObject.activeInHierarchy)
            {
                transform.position = follower.transform.TransformPoint(clubFollow.center);
                transform.rotation = follower.transform.rotation;

                Vector3 scale = new Vector3(clubFollow.size.x, clubFollow.size.y, clubFollow.size.z);
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
    }
}