using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(-1)]
    public class BodyMountedObject : UdonSharpBehaviour
    {
        #region Public Settings

        [Header("Object Settings")]
        public ControllerDetector controllerDetector;

        public Rigidbody rb;

        [SerializeField, Tooltip("The actual object you want the player to see when they grab this body mounted object")]
        private GameObject objectToAttach;

        private Rigidbody rbToAttach;

        public GameObject ObjectToAttach
        {
            get => objectToAttach;
            set
            {
                objectToAttach = value;
                rbToAttach = objectToAttach.GetComponent<Rigidbody>();

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

        [Header("Mounting Settings")] [Tooltip("Defines which bone this pickup gets mounted to on the players avatar")]
        public HumanBodyBones mountToBone = HumanBodyBones.Head;

        public bool mountToPlayerPosition;
        public Vector3 mountingOffset = Vector3.zero;

        [Tooltip("Toggles scaling this offset based onthe local players height (allows you to move it further away from their head if they are taller)")]
        public bool mountingOffsetHeightScaling;

        public AnimationCurve mountingOffsetHeightScale;

        [Tooltip("Allows the VRCPickup proximity to scale with the players height to hopefully make it easier to grab things")]
        public float defaultPickupProximity = 0.2f;

        public KeyCode desktopInputKey = KeyCode.M;
        public ControllerButtons controllerInputKey = ControllerButtons.None;
        public VRC_Pickup.PickupHand pickupHandLimit = VRC_Pickup.PickupHand.None;
        public bool applyVelocityAfterDrop;

        [HideInInspector]
        public bool pickedUpAtLeastOnce;

        [HideInInspector]
        public bool forcePickedUp;

        #endregion

        #region Internal Vars

        private VRC_Pickup.PickupHand _heldInHand = VRC_Pickup.PickupHand.None;

        public VRC_Pickup.PickupHand heldInHand
        {
            get => _heldInHand;
            set
            {
                if (_heldInHand != VRC_Pickup.PickupHand.None && value == VRC_Pickup.PickupHand.None)
                {
                    _heldInHand = value;
                    // Object was just dropped by the player
                    ActivateAndTakeOwnership();

                    if (Utilities.IsValid(objectToAttach))
                    {
                        var listeners = objectToAttach.GetComponents<UdonBehaviour>();
                        foreach (var listener in listeners)
                        {
                            listener.SetProgramVariable("lastHeldFrameVelocity", lastFrameVelocity);
                            listener.SendCustomEvent(dropEventName);
                            listener.SetProgramVariable(currentHandVariableName, (int)_heldInHand);
                        }

                        if (applyVelocityAfterDrop)
                        {
                            var rb = objectToAttach.GetComponent<Rigidbody>();
                            if (Utilities.IsValid(rb))
                                rb.velocity = lastFrameVelocity;
                        }
                    }
                }

                if (_heldInHand == VRC_Pickup.PickupHand.None && value != VRC_Pickup.PickupHand.None)
                {
                    _heldInHand = value;
                    // Object was just pickup up by the player
                    ActivateAndTakeOwnership();
                    if (Utilities.IsValid(objectToAttach))
                    {
                        var listeners = objectToAttach.GetComponents<UdonBehaviour>();
                        foreach (var listener in listeners)
                        {
                            listener.SendCustomEvent(pickupEventName);
                            listener.SetProgramVariable(currentHandVariableName, (int)_heldInHand);
                        }
                    }
                }

                _heldInHand = value;
            }
        }

        private Quaternion originalRotation;

        private Vector3 lastFramePosition = Vector3.zero;
        private Vector3 lastFrameVelocity = Vector3.zero;
        private bool firstFrameCheck;
        private bool userIsInVR;
        private Vector3 currentOffset = Vector3.zero;

        #endregion

        void Start()
        {
            if (Utilities.IsValid(objectToAttach))
                originalRotation = objectToAttach.transform.rotation;
            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();
            if (!Utilities.IsValid(mountingOffsetHeightScale) || mountingOffsetHeightScale.length == 0)
            {
                mountingOffsetHeightScale.AddKey(0f, 0.5f);
                mountingOffsetHeightScale.AddKey(1.5f, 1f);
                mountingOffsetHeightScale.AddKey(5f, 3f);
                mountingOffsetHeightScale.AddKey(100f, 10f); // Not tested!
            }

            currentOffset = mountingOffset;

            SendCustomEventDelayedSeconds(nameof(UpdateObjectOffset), 1f);
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

            var currentHand = VRC_Pickup.PickupHand.None;
            if (Utilities.IsValid(pickup))
            {
                // Get VR pickup status
                currentHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                // If player is on Desktop - override this if they press the correct key
                if (!userIsInVR)
                {
                    pickupHandLimit = VRC_Pickup.PickupHand.None;
                    currentHand = desktopInputKey != KeyCode.None && Input.GetKey(desktopInputKey) ? VRC_Pickup.PickupHand.Right : VRC_Pickup.PickupHand.None;
                    if (controllerInputKey != ControllerButtons.None)
                    {
                        if (Utilities.IsValid(controllerDetector) && controllerDetector.LastUsedJoystick.IsKeyPressed(controllerInputKey, controllerDetector.LastKnownJoystickID))
                            currentHand = VRC_Pickup.PickupHand.Right;
                    }
                }

                if (forcePickedUp)
                    currentHand = VRC_Pickup.PickupHand.Right;

                // If it is limited to one hand only and player picked it up with the wrong hand
                if (pickupHandLimit != VRC_Pickup.PickupHand.None && Utilities.IsValid(pickup) && pickupHandLimit != currentHand)
                {
                    // Drop this object
                    pickup.Drop();
                    currentHand = VRC_Pickup.PickupHand.None;
                }

                // Trigger event calls if this changed
                heldInHand = currentHand;
            }

            // We either can't tell if it's being held or we know it's not being held
            if (currentHand == VRC_Pickup.PickupHand.None)
            {
                if (mountToPlayerPosition)
                {
                    gameObject.transform.SetPositionAndRotation(Networking.LocalPlayer.GetPosition(), Networking.LocalPlayer.GetRotation());
                }
                else
                {
                    // Just pin the body object to the bone
                    gameObject.transform.position = Networking.LocalPlayer.GetBonePosition(mountToBone) + transform.TransformDirection(currentOffset);
                    gameObject.transform.rotation = Networking.LocalPlayer.GetBoneRotation(mountToBone);
                }

                if (Utilities.IsValid(pickup))
                    pickup.pickupable = true;

                lastFramePosition = Vector3.zero;
                lastFrameVelocity = Vector3.zero;

                return;
            }

            if (lastFramePosition == Vector3.zero)
                lastFrameVelocity = Vector3.zero;
            else
                lastFrameVelocity = (transform.position - lastFramePosition) / Time.deltaTime;

            if (lastFrameVelocity.magnitude > 15f)
                lastFrameVelocity = lastFrameVelocity.normalized * 15f;

            lastFramePosition = transform.position;

            if (!pickedUpAtLeastOnce)
                pickedUpAtLeastOnce = true;

            if (Utilities.IsValid(pickup))
                pickup.pickupable = false;

            if (Utilities.IsValid(objectToAttach))
                objectToAttach.transform.position = gameObject.transform.position;

            if (userIsInVR)
            {
                if (Utilities.IsValid(objectToAttach))
                    objectToAttach.transform.rotation = gameObject.transform.rotation;
            }
            else
            {
                if (Utilities.IsValid(objectToAttach))
                    objectToAttach.transform.eulerAngles = new Vector3(-90, 0, gameObject.transform.eulerAngles.z - 90);
                gameObject.transform.position = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head) + transform.TransformDirection(0, 0, 1);
                gameObject.transform.rotation = Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.Head);
            }

            if (Utilities.IsValid(rbToAttach))
            {
                rbToAttach.position = gameObject.transform.position;
                rbToAttach.rotation = gameObject.transform.rotation;
            }
        }

        private void ActivateAndTakeOwnership()
        {
            if (!Utilities.IsValid(objectToAttach)) return;

            if (!objectToAttach.gameObject.activeInHierarchy)
                objectToAttach.gameObject.SetActive(true);

            if (!Networking.LocalPlayer.IsOwner(objectToAttach.gameObject))
                OpenPuttUtils.SetOwner(Networking.LocalPlayer, objectToAttach.gameObject);
        }

        public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
        {
            if (!Utilities.IsValid(player) || !player.isLocal)
                return;

            UpdateObjectOffset();
        }

        /// <summary>
        /// Allows for scaling the position offset based on the players height
        /// </summary>
        private void UpdateObjectOffset()
        {
            if (!mountingOffsetHeightScaling)
            {
                currentOffset = mountingOffset;
                return;
            }

            if (!OpenPuttUtils.LocalPlayerIsValid())
            {
                SendCustomEventDelayedSeconds(nameof(UpdateObjectOffset), 1f);
                return;
            }

            var lastKnownPlayerHeight = Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();

            var scaleFactor = mountingOffsetHeightScale.Evaluate(lastKnownPlayerHeight);

            currentOffset = mountingOffset * scaleFactor;
            pickup.proximity = 0.2f * scaleFactor;
        }
    }
}