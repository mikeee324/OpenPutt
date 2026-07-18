using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(-999)]
    public class BodyMountedObject : UdonSharpBehaviour
    {
        #region Public Settings

        [OpenPuttDescription("A pickup object that gets mounted onto a bone on the players body (like a shoulder) so it follows them around and can be grabbed later.")]
        [OpenPuttFoldoutGroup("Object Settings")]
        public ControllerDetector controllerDetector;

        [OpenPuttFoldoutGroup("Object Settings")]
        public Rigidbody rb;

        [OpenPuttFoldoutGroup("Object Settings")]
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

        [OpenPuttFoldoutGroup("Object Settings")]
        [Tooltip("When the player picks up this object it will send this an event with this name to the attached object")]
        public string pickupEventName = "_OnScriptPickup";

        [OpenPuttFoldoutGroup("Object Settings")]
        [Tooltip("When the player drops this object it will send this an event with this name to the attached object")]
        public string dropEventName = "_OnScriptDrop";

        [OpenPuttFoldoutGroup("Object Settings")]
        [Tooltip("When the player presses Use while holding this object it will send this event with this name to the attached object")]
        public string useEventName = "_OnScriptUse";

        [OpenPuttFoldoutGroup("Object Settings")]
        [Tooltip("When the player drops this object it will send this an event with this name to the attached object")]
        public string currentHandVariableName = "currentOwnerHideOverride";

        [OpenPuttFoldoutGroup("Object Settings")]
        public VRCPickup pickup;

        [OpenPuttFoldoutGroup("Mounting Settings")]
        [Tooltip("Defines which bone this pickup gets mounted to on the players avatar")]
        public HumanBodyBones mountToBone = HumanBodyBones.Head;

        [OpenPuttFoldoutGroup("Mounting Settings")]
        public bool mountToPlayerPosition;
        [OpenPuttFoldoutGroup("Mounting Settings")]
        public Vector3 mountingOffset = Vector3.zero;

        [OpenPuttFoldoutGroup("Mounting Settings")]
        [Tooltip("Toggles scaling this offset based onthe local players height (allows you to move it further away from their head if they are taller)")]
        public bool mountingOffsetHeightScaling;

        [OpenPuttFoldoutGroup("Mounting Settings")]
        public AnimationCurve mountingOffsetHeightScale;

        [OpenPuttFoldoutGroup("Mounting Settings")]
        [Tooltip("Allows the VRCPickup proximity to scale with the players height to hopefully make it easier to grab things")]
        public float defaultPickupProximity = 0.2f;

        [OpenPuttFoldoutGroup("Mounting Settings")]
        public KeyCode desktopInputKey = KeyCode.M;
        [OpenPuttFoldoutGroup("Mounting Settings")]
        public ControllerButtons controllerInputKey = ControllerButtons.None;
        [OpenPuttFoldoutGroup("Mounting Settings")]
        public VRC_Pickup.PickupHand pickupHandLimit = VRC_Pickup.PickupHand.None;

        [OpenPuttFoldoutGroup("Mounting Settings")]
        public Vector3 desktopHeadOffset = new Vector3(0, 0, 1);
        [OpenPuttFoldoutGroup("Mounting Settings")]
        public Vector3 desktopRotationOffset = new Vector3(-90, 0, 0);

        [HideInInspector]
        public bool pickedUpAtLeastOnce;

        [HideInInspector]
        public bool forcePickedUp;

        [HideInInspector]
        public bool tempDisableAttachment = false;

        [HideInInspector, Tooltip("When true the desktop/controller pickup key is ignored (e.g. while the ball cam is active)")]
        public bool ignorePickupKey = false;

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

            SendCustomEventDelayedSeconds(nameof(_UpdateObjectOffset), 1f);
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
                    currentHand = !ignorePickupKey && desktopInputKey != KeyCode.None && Input.GetKey(desktopInputKey) ? VRC_Pickup.PickupHand.Right : VRC_Pickup.PickupHand.None;
                    if (!ignorePickupKey && controllerInputKey != ControllerButtons.None)
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
                    // GetPosition() only tracks the playspace origin (X/Z), so it doesn't move when the
                    // player crouches or their playspace is raised above the floor. Use the real tracked
                    // head height for Y instead of a fixed eye-height offset so it follows both cases.
                    var rootPos = localPlayer.GetPosition();
                    rootPos.y = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position.y;
                    var rootRot = localPlayer.GetRotation();
                    gameObject.transform.SetPositionAndRotation(rootPos + rootRot * currentOffset, rootRot);
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
                currPos = head.position + head.rotation * GetScaledDesktopHeadOffset();
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

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!value || heldInHand == VRC_Pickup.PickupHand.None)
                return;

            // Only fire from the hand actually holding this object, otherwise pressing Use with the
            // other hand (e.g. while it's holding something else) would incorrectly trigger this too.
            bool useFromHeldHand = (heldInHand == VRC_Pickup.PickupHand.Left && args.handType == HandType.LEFT) ||
                                    (heldInHand == VRC_Pickup.PickupHand.Right && args.handType == HandType.RIGHT);
            if (!useFromHeldHand)
                return;

            if (string.IsNullOrEmpty(useEventName) || !Utilities.IsValid(objectToAttach))
                return;

            var listeners = objectToAttach.GetComponents<UdonBehaviour>();
            foreach (var listener in listeners)
                listener.SendCustomEvent(useEventName);
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

            _UpdateObjectOffset();
        }

        /// <summary>
        /// Returns the desktop head offset scaled to the local player's height.
        /// </summary>
        public Vector3 GetScaledDesktopHeadOffset()
        {
            var eyeHeight = OpenPuttUtils.LocalPlayerIsValid() ? Networking.LocalPlayer.GetAvatarEyeHeightAsMeters() : 1.7f;
            return desktopHeadOffset * (Mathf.Clamp(eyeHeight, 0.2f, 5f) / 1.7f);
        }

        /// <summary>
        /// Allows for scaling the position offset based on the players height
        /// </summary>
        public void _UpdateObjectOffset()
        {
            if (!mountingOffsetHeightScaling)
            {
                currentOffset = mountingOffset;
                return;
            }

            if (!OpenPuttUtils.LocalPlayerIsValid())
            {
                SendCustomEventDelayedSeconds(nameof(_UpdateObjectOffset), 1f);
                return;
            }

            var lastKnownPlayerHeight = Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();

            var scaleFactor = mountingOffsetHeightScale.Evaluate(lastKnownPlayerHeight);

            currentOffset = mountingOffset * scaleFactor;
            if (Utilities.IsValid(pickup))
                pickup.proximity = defaultPickupProximity * scaleFactor;
        }

        void OnDisable()
        {
            if (!Utilities.IsValid(pickup)) return;
            pickup.Drop();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, pickup.proximity);
        }
    }
}