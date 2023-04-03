
using mikeee324.OpenPutt;
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
        this.gameObject.layer = 0;
    }

    private void LateUpdate()
    {
        if (Utils.LocalPlayerIsValid())
        {
            Vector3 pos = Networking.LocalPlayer.GetPosition();

            bool enableObjects = colliderToMonitor != null && colliderToMonitor.bounds.Contains(pos);
            foreach (UdonBehaviour obj in scriptsToDisable)
                obj.enabled = enableObjects;
            foreach (GameObject obj in objectsToDisable)
                obj.SetActive(enableObjects);

            this.enabled = false;
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == Networking.LocalPlayer)
        {
            foreach (UdonBehaviour obj in scriptsToDisable)
                obj.enabled = true;
            foreach (GameObject obj in objectsToDisable)
                obj.SetActive(true);
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == Networking.LocalPlayer)
        {
            foreach (UdonBehaviour obj in scriptsToDisable)
                obj.enabled = false;
            foreach (GameObject obj in objectsToDisable)
                obj.SetActive(false);
        }
    }
}
