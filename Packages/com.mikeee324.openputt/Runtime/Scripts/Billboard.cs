using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Billboard : UdonSharpBehaviour
    {
        public GameObject attachToObject;

        void Start()
        {
        }

        void Update()
        {
            if (Networking.LocalPlayer != null && !Networking.LocalPlayer.IsValid())
                return;

            Vector3 headPosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);

            if (headPosition != Vector3.zero)
                transform.LookAt(headPosition);

            if (attachToObject != null)
                transform.position = attachToObject.transform.position + new Vector3(0, 0.1f, 0);
        }
    }
}