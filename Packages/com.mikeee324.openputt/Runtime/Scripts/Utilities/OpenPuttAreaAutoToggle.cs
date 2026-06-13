using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttAreaAutoToggle : OpenPuttEventListener
    {
        [Header("This component automatically toggles the shoulder pickups and portable menu depending on whether the player is inside this collider")]
        public OpenPutt openPutt;

        public bool enabledAtStart = false;

        private GameObject localPlayerObject;
        private PlayerManager localPlayerManager;

        private void Start()
        {
            if (!Utilities.IsValid(openPutt))
            {
                enabled = false;
                OpenPuttUtils.Log(this, "Cannot set up OpenPuttAreaAutoToggle. A reference to OpenPutt is required.");
                return;
            }

            foreach (Transform child in openPutt.transform)
                if (child.name == "LocalPlayer")
                    localPlayerObject = child.gameObject;

            // Register as an event listener so the initial visibility state is applied once the
            // local player's PlayerManager has been set up (it isn't ready during Start())
            openPutt._RegisterEventListener(this);
        }

        public override void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {
            if (!player.isLocal) return;

            localPlayerManager = playerManager;

            SetVisible(enabledAtStart);
            localPlayerManager._RequestSync(syncNow: true);
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (Networking.LocalPlayer != player) return;
            SetVisible(true);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (Networking.LocalPlayer != player) return;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (!Utilities.IsValid(localPlayerManager)) return;

            if (Utilities.IsValid(localPlayerObject))
                localPlayerObject.SetActive(visible);

            localPlayerManager.ClubVisible = visible;
            localPlayerManager.BallVisible = visible;
        }
    }
}
