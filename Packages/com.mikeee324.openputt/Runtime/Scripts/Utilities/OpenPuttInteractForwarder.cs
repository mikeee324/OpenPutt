using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Forwards an Interact event on this object to a custom event on another UdonBehaviour
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttInteractForwarder : UdonSharpBehaviour
    {
        [OpenPuttDescription("When a player interacts with this object, it will run a public function on another script. Assign the script below and type the name of the function you want it to call.")]
        [OpenPuttFoldoutGroup("Target")]
        [Tooltip("The UdonBehaviour that should receive the event when this object is interacted with")]
        public UdonBehaviour targetBehaviour;

        [OpenPuttFoldoutGroup("Target")]
        [Tooltip("The name of the custom event to send to the target behaviour")]
        public string targetEventName;

        public override void Interact()
        {
            if (Utilities.IsValid(targetBehaviour) && !string.IsNullOrEmpty(targetEventName))
                targetBehaviour.SendCustomEvent(targetEventName);
        }
    }
}
