
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class BodyMountedObject : UdonSharpBehaviour
    {
        #region Public Settings
        [Header("Object Settings")]
        [Tooltip("The actual object you want the player to see when they grab this body mounted object")]
        public GameObject objectToAttach;
        [Tooltip("When the player picks up this object it will send this an event with this name to the attached object")]
        public string pickupEventName = "OnScriptPickup";
        [Tooltip("When the player drops this object it will send this an event with this name to the attached object")]
        public string dropEventName = "OnScriptDrop";
        [Tooltip("When the player drops this object it will send this an event with this name to the attached object")]
        public string currentHandVariableName = "currentOwnerHideOverride";
        public PickupHelper pickupHelper;
        private VRCPickup pickup;

        [Header("Mounting Settings")]
        [Tooltip("Defines which bone this pickup gets mounted to on the players avatar")]
        public HumanBodyBones mountToBone = HumanBodyBones.Head;
        public Vector3 mountingOffset = Vector3.zero;
        public KeyCode desktopInputKey = KeyCode.M;
        public VRCPickup.PickupHand pickupHandLimit = VRCPickup.PickupHand.None;
        #endregion

        #region Internal Vars
        private VRCPickup.PickupHand _heldInHand = VRCPickup.PickupHand.None;
        public VRCPickup.PickupHand heldInHand
        {
            get => _heldInHand;
            set
            {

                if (_heldInHand != VRCPickup.PickupHand.None && value == VRCPickup.PickupHand.None)
                {
                    _heldInHand = value;
                    // Object was just dropped by the player
                    ActivateAndTakeOwnership();

                    UdonBehaviour[] listeners = objectToAttach.GetComponents<UdonBehaviour>();
                    foreach (UdonBehaviour listener in listeners)
                    {
                        listener.SendCustomEvent(dropEventName);
                        listener.SetProgramVariable(currentHandVariableName, (int)_heldInHand);
                    }

                    Rigidbody rb = objectToAttach.GetComponent<Rigidbody>();
                    if (rb != null)
                        rb.AddForce(lastFrameVelocity * 0.06f, ForceMode.Impulse);
                }
                if (_heldInHand == VRCPickup.PickupHand.None && value != VRCPickup.PickupHand.None)
                {
                    _heldInHand = value;
                    // Object was just pickup up by the player
                    ActivateAndTakeOwnership();
                    UdonBehaviour[] listeners = objectToAttach.GetComponents<UdonBehaviour>();
                    foreach (UdonBehaviour listener in listeners)
                    {
                        listener.SendCustomEvent(pickupEventName);
                        listener.SetProgramVariable(currentHandVariableName, (int)_heldInHand);
                    }
                }

                _heldInHand = value;
            }
        }
        private Quaternion originalRotation;

        private Vector3 lastFramePosition = Vector3.zero;
        private Vector3 lastFrameVelocity = Vector3.zero;
        #endregion

        void Start()
        {
            if (objectToAttach != null)
                originalRotation = objectToAttach.transform.rotation;
            if (pickupHelper == null)
                pickupHelper = GetComponent<PickupHelper>();
            if (pickup == null)
                pickup = GetComponent<VRCPickup>();
        }

        private void Update()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer))
                return;

            if (objectToAttach == null)
                return;

            // Get VR pickup status
            VRCPickup.PickupHand currentHand = pickupHelper != null ? pickupHelper.CurrentHand : VRCPickup.PickupHand.None;
            // If player is on Desktop - override this if they press the correct key
            if (!Networking.LocalPlayer.IsUserInVR())
            {
                pickupHandLimit = VRCPickup.PickupHand.None;
                currentHand = Input.GetKey(desktopInputKey) ? VRCPickup.PickupHand.Right : VRCPickup.PickupHand.None;
            }

            // If it is limited to one hand only and player picked it up with the wrong hand
            if (pickupHandLimit != VRCPickup.PickupHand.None && pickupHelper != null && pickupHandLimit != currentHand)
            {
                // Drop this object
                pickupHelper.Drop();
                currentHand = VRCPickup.PickupHand.None;
            }

            // Trigger event calls if this changed
            heldInHand = currentHand;

            // We either can't tell if it's being held or we know it's not being held
            if (pickupHelper == null || currentHand == VRCPickup.PickupHand.None)
            {
                // Just pin the body object to the bone
                gameObject.transform.position = Networking.LocalPlayer.GetBonePosition(mountToBone) + transform.TransformDirection(mountingOffset);
                gameObject.transform.rotation = Networking.LocalPlayer.GetBoneRotation(mountToBone);

                if (pickup != null)
                    pickup.pickupable = true;

                return;
            }

            ActivateAndTakeOwnership();

            if (Networking.LocalPlayer.IsOwner(objectToAttach))
            {
                if (pickup != null)
                    pickup.pickupable = false;

                objectToAttach.transform.position = gameObject.transform.position;
                if (Networking.LocalPlayer.IsUserInVR())
                {
                    objectToAttach.transform.rotation = gameObject.transform.rotation;
                }
                else
                {
                    objectToAttach.transform.eulerAngles = new Vector3(-90, 0, gameObject.transform.eulerAngles.z - 90);
                    gameObject.transform.position = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head) + transform.TransformDirection(0, 0, 1);
                    gameObject.transform.rotation = Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.Head);
                }
            }
        }

        private void ActivateAndTakeOwnership()
        {
            if (objectToAttach == null) return;

            if (!objectToAttach.gameObject.activeInHierarchy)
                objectToAttach.gameObject.SetActive(true);

            if (!Networking.LocalPlayer.IsOwner(objectToAttach.gameObject))
                Utils.SetOwner(Networking.LocalPlayer, objectToAttach.gameObject);
        }

        private void FixedUpdate()
        {
            // Store the velocity of the RigidBody so we can apply it to the object when the player lets go
            Rigidbody rigidbody = GetComponent<Rigidbody>();

            if (rigidbody.isKinematic)
                lastFrameVelocity = (rigidbody.position - lastFramePosition) / Time.deltaTime;
            else
                lastFrameVelocity = rigidbody.velocity;

            lastFramePosition = rigidbody.position;
        }
    }
}