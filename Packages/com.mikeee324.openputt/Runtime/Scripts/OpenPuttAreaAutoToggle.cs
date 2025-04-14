using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttAreaAutoToggle : UdonSharpBehaviour
    {
        [Header("This component automatically toggles the shoulder pickups and portable menu depending on whether the player is inside this collider")]
        public OpenPutt openPutt;
        
        public Collider collider;

        public bool enabledAtStart = false;
        
        private GameObject localPlayerObject;

        private void Start()
        {
            collider = GetComponent<Collider>();

            if (!Utilities.IsValid(openPutt))
            {
                enabled = false;
                OpenPuttUtils.Log(this, "Cannot set up OpenPuttAreaAutoToggle. A reference to OpenPutt is required.");
                return;
            }
            
            foreach (Transform child in openPutt.transform)
                if (child.name == "LocalPlayer")
                    localPlayerObject = child.gameObject;
            
            localPlayerObject.SetActive(enabledAtStart);
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (Networking.LocalPlayer != player) return;
            localPlayerObject.SetActive(true);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (Networking.LocalPlayer != player) return;
            localPlayerObject.SetActive(false);
        }
    }
}