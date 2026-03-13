using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
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
            if (Utilities.IsValid(Networking.LocalPlayer) && !Networking.LocalPlayer.IsValid())
                return;

            var headPosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);

            if (headPosition != Vector3.zero)
                transform.LookAt(headPosition);

            if (Utilities.IsValid(attachToObject))
                transform.position = attachToObject.transform.position + new Vector3(0, 0.1f, 0);
        }
    }
}