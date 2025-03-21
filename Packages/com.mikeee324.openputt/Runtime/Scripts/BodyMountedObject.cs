using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(-999)]
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

        public Vector3 desktopHeadOffset = new Vector3(0, 0, 1);
        public Vector3 desktopRotationOffset = new Vector3(-90, 0, 0);

        [HideInInspector]
        public bool pickedUpAtLeastOnce;

        [HideInInspector]
        public bool forcePickedUp;

        [HideInInspector]
        public bool tempDisableAttachment = false;

        #endregion

        #region Internal Vars

        private VRC_Pickup.PickupHand _heldInHand = VRC_Pickup.PickupHand.None;

        public VRC_Pickup.PickupHand heldInHand
        {
            get => _heldInHand;
            private set
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
                            listener.SendCustomEvent(dropEventName);
                            listener.SetProgramVariable(currentHandVariableName, (int)_heldInHand);
                        }
                    }
                }

                if (_heldInHand == VRC_Pickup.PickupHand.None && value != VRC_Pickup.PickupHand.None)
                {
                    tempDisableAttachment = false;
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

        private bool firstFrameCheck;
        private bool userIsInVR;
        private Vector3 currentOffset = Vector3.zero;

        #endregion

        void Start()
        {
            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();

            if (!Utilities.IsValid(mountingOffsetHeightScale) || mountingOffsetHeightScale.length == 0)
            {
                mountingOffsetHeightScale.AddKey(0.2f, 0.1f);
                mountingOffsetHeightScale.AddKey(1.5f, 1f);
                mountingOffsetHeightScale.AddKey(5f, 3f);
            }

            currentOffset = mountingOffset;

            SendCustomEventDelayedSeconds(nameof(UpdateObjectOffset), 1f);
        }

        public override void PostLateUpdate()
        {
            var localPlayer = Networking.LocalPlayer;

            if (!Utilities.IsValid(localPlayer))
                return;

            if (!firstFrameCheck)
            {
                userIsInVR = localPlayer.IsUserInVR();
                firstFrameCheck = true;
            }

            var wasHeldLastFrame = heldInHand;

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

                heldInHand = currentHand;
            }

            // We either can't tell if it's being held or we know it's not being held
            if (currentHand == VRC_Pickup.PickupHand.None)
            {
                if (mountToPlayerPosition)
                {
                    gameObject.transform.SetPositionAndRotation(localPlayer.GetPosition(), localPlayer.GetRotation());
                }
                else
                {
                    // Just pin the body object to the bone
                    gameObject.transform.position = localPlayer.GetBonePosition(mountToBone) + transform.TransformDirection(currentOffset);
                    gameObject.transform.rotation = localPlayer.GetBoneRotation(mountToBone);
                }

                if (Utilities.IsValid(pickup))
                    pickup.pickupable = true;

                return;
            }

            if (!pickedUpAtLeastOnce)
                pickedUpAtLeastOnce = true;

            if (Utilities.IsValid(pickup))
                pickup.pickupable = false;

            if (tempDisableAttachment)
                return;

            var currPos = transform.position;
            var currRot = transform.rotation;

            // Desktop users always get the object floating in front of their head
            if (!userIsInVR)
            {
                var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                currPos = head.position + transform.TransformDirection(desktopHeadOffset);
                currRot = head.rotation;
            }

            // Move mounted object to the correct position
            transform.SetPositionAndRotation(currPos, currRot);

            // Apply the desktop offset
            if (!userIsInVR)
                currRot *= Quaternion.Euler(desktopRotationOffset);
            
            // Move attached object to the position
            if (Utilities.IsValid(rbToAttach) && !rbToAttach.isKinematic)
            {
                rbToAttach.position = currPos;
                rbToAttach.rotation = currRot;
            }
            else if (Utilities.IsValid(objectToAttach))
            {
                objectToAttach.transform.SetPositionAndRotation(currPos, currRot);
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
        public void UpdateObjectOffset()
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
            pickup.proximity = defaultPickupProximity * scaleFactor;
        }

        void OnDisable()
        {
            pickup.Drop();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, pickup.proximity);
        }
    }
}