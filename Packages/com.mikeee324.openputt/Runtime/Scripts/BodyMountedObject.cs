
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(-1)]
    public class BodyMountedObject : UdonSharpBehaviour
    {
        #region Public Settings
        [Header("Object Settings")]
        public Rigidbody rb;
        [SerializeField, Tooltip("The actual object you want the player to see when they grab this body mounted object")]
        private GameObject objectToAttach;
        public GameObject ObjectToAttach
        {
            get => objectToAttach;
            set
            {
                objectToAttach = value;

                ActivateAndTakeOwnership();
            }
        }
        [Tooltip("When the player picks up this object it will send this an event with this name to the attached object")]
        public string pickupEventName = "OnScriptPickup";
        [Tooltip("When the player drops this object it will send this an event with this name to the attached object")]
        public string dropEventName = "OnScriptDrop";
        [Tooltip("When the player drops this object it will send this an event with this name to the attached object")]
        public string currentHandVariableName = "currentOwnerHideOverride";
        public VRCPickup pickup;

        [Header("Mounting Settings")]
        [Tooltip("Defines which bone this pickup gets mounted to on the players avatar")]
        public HumanBodyBones mountToBone = HumanBodyBones.Head;
        public bool mountToPlayerPosition = false;
        public Vector3 mountingOffset = Vector3.zero;
        public KeyCode desktopInputKey = KeyCode.M;
        public KeyCode controllerInputKey = KeyCode.None;
        public VRCPickup.PickupHand pickupHandLimit = VRCPickup.PickupHand.None;
        public bool applyVelocityAfterDrop = false;
        [HideInInspector]
        public bool pickedUpAtLeastOnce = false;
        [HideInInspector]
        public bool forcePickedUp = false;
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

                    if (applyVelocityAfterDrop)
                    {
                        Rigidbody rb = objectToAttach.GetComponent<Rigidbody>();
                        if (rb != null)
                            rb.AddForce(lastFrameVelocity * rb.mass, ForceMode.Impulse);
                    }
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
        private bool firstFrameCheck = false;
        private bool userIsInVR = false;
        #endregion

        void Start()
        {
            if (objectToAttach != null)
                originalRotation = objectToAttach.transform.rotation;
            if (pickup == null)
                pickup = GetComponent<VRCPickup>();
        }

        public override void PostLateUpdate()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer))
                return;

            if (!firstFrameCheck)
            {
                userIsInVR = Networking.LocalPlayer.IsUserInVR();
                firstFrameCheck = true;
            }

            VRCPickup.PickupHand currentHand = VRCPickup.PickupHand.None;
            if (pickup != null)
            {
                // Get VR pickup status
                currentHand = pickup != null ? pickup.currentHand : VRCPickup.PickupHand.None;
                // If player is on Desktop - override this if they press the correct key
                if (!userIsInVR)
                {
                    pickupHandLimit = VRCPickup.PickupHand.None;
                    currentHand = desktopInputKey != KeyCode.None && Input.GetKey(desktopInputKey) ? VRCPickup.PickupHand.Right : VRCPickup.PickupHand.None;
                    if (controllerInputKey != KeyCode.None && Input.GetKey(controllerInputKey))
                        currentHand = VRC_Pickup.PickupHand.Right;
                }

                if (forcePickedUp)
                    currentHand = VRC_Pickup.PickupHand.Right;

                // If it is limited to one hand only and player picked it up with the wrong hand
                if (pickupHandLimit != VRCPickup.PickupHand.None && pickup != null && pickupHandLimit != currentHand)
                {
                    // Drop this object
                    pickup.Drop();
                    currentHand = VRCPickup.PickupHand.None;
                }

                // Trigger event calls if this changed
                heldInHand = currentHand;
            }

            // We either can't tell if it's being held or we know it's not being held
            if (currentHand == VRCPickup.PickupHand.None)
            {
                if (mountToPlayerPosition)
                {
                    gameObject.transform.SetPositionAndRotation(Networking.LocalPlayer.GetPosition(), Networking.LocalPlayer.GetRotation());
                }
                else
                {
                    // Just pin the body object to the bone
                    gameObject.transform.position = Networking.LocalPlayer.GetBonePosition(mountToBone) + transform.TransformDirection(mountingOffset);
                    gameObject.transform.rotation = Networking.LocalPlayer.GetBoneRotation(mountToBone);
                }

                if (pickup != null)
                    pickup.pickupable = true;

                return;
            }

            if (!pickedUpAtLeastOnce)
                pickedUpAtLeastOnce = true;

            if (pickup != null)
                pickup.pickupable = false;

            if (objectToAttach != null)
                objectToAttach.transform.position = gameObject.transform.position;

            if (userIsInVR)
            {
                if (objectToAttach != null)
                    objectToAttach.transform.rotation = gameObject.transform.rotation;
            }
            else
            {
                if (objectToAttach != null)
                    objectToAttach.transform.eulerAngles = new Vector3(-90, 0, gameObject.transform.eulerAngles.z - 90);
                gameObject.transform.position = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head) + transform.TransformDirection(0, 0, 1);
                gameObject.transform.rotation = Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.Head);
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
            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (rb == null)
            {
                lastFrameVelocity = (transform.position - lastFramePosition) / Time.deltaTime;
                lastFramePosition = transform.position;
                return;
            }

            if (rb.isKinematic)
                lastFrameVelocity = (rb.position - lastFramePosition) / Time.deltaTime;
            else
                lastFrameVelocity = rb.velocity;

            lastFramePosition = rb.position;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}