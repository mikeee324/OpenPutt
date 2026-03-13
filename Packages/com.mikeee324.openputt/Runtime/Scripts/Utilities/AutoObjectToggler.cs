using dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AutoObjectToggler : UdonSharpBehaviour
{
    public UdonBehaviour[] scriptsToDisable;
    public GameObject[] objectsToDisable;
    public BoxCollider colliderToMonitor;

    void Start()
    {
        gameObject.layer = 0;
    }

    private void LateUpdate()
    {
        if (OpenPuttUtils.LocalPlayerIsValid())
        {
            var pos = Networking.LocalPlayer.GetPosition();

            var enableObjects = Utilities.IsValid(colliderToMonitor) && colliderToMonitor.bounds.Contains(pos);
            foreach (var obj in scriptsToDisable)
                obj.enabled = enableObjects;
            foreach (var obj in objectsToDisable)
                obj.SetActive(enableObjects);

            enabled = false;
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == Networking.LocalPlayer)
        {
            foreach (var obj in scriptsToDisable)
                obj.enabled = true;
            foreach (var obj in objectsToDisable)
                obj.SetActive(true);
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == Networking.LocalPlayer)
        {
            foreach (var obj in scriptsToDisable)
                obj.enabled = false;
            foreach (var obj in objectsToDisable)
                obj.SetActive(false);
        }
    }
}