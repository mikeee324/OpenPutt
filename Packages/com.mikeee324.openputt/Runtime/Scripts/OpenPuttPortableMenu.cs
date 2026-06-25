/**
 * Made by TummyTime
 */

using dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttPortableMenu : UdonSharpBehaviour
    {
        [SerializeField]
        public ControllerTracker controllerTracker;

        [Range(0f, 10f), Tooltip("Minimum speed required to initiate a throw (Normalised against a person that is 1.7m tall)")]
        public float minThrowSpeed = 1f;

        [SerializeField]
        private KeyCode menuKey = KeyCode.N;

        [SerializeField]
        private GameObject visibleMenuObject;

        [SerializeField]
        private Rigidbody rigidBody;

        [SerializeField]
        private VRCPickup pickup;

        [SerializeField]
        private Vector3 desktopHeadOffset = Vector3.zero;

        [SerializeField]
        private float hideDistance = 3f;

        [SerializeField, Range(1f, 5f), Tooltip("How large the menu has to be before it shows initially (relative to the initial opening size)")]
        private float openThreshold = 2f;

        public bool hideOnStart = true;

        [Tooltip("The duration of the vibration in seconds.")]
        public float vibrationDuration = 0.1f;

        [Tooltip("The strength of the vibration (0.0 to 1.0).")]
        [Range(0f, 1f)]
        public float vibrationStrength = 0.5f;

        [Tooltip("The frequency of the vibration (roughly how many pulses per second).")]
        public float vibrationFrequency = 30f;

        public UdonBehaviour eventReceiver;
        public string eventToSendOnMenuOpen;
        public string eventToSendOnMenuClose;

        public bool golfClubHeldByPlayer;
        public bool golfBallHeldByPlayer;

        private bool leftUseButtonDown;
        private bool rightUseButtonDown;
        private float originalHandDistance = -1f;
        private bool userIsInVR;
        private bool isCurrentlyVisible = false;
        private VRC_Pickup.PickupHand currentHand = VRC_Pickup.PickupHand.None;
        private Vector3 menuSpawnPosition = Vector3.zero;
        private Quaternion menuSpawnRotation = Quaternion.identity;
        private Vector3 menuSpawnScale = Vector3.one;

        void Start()
        {
            if (hideOnStart)
            {
                visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                visibleMenuObject.transform.localScale = Vector3.zero;
            }
            else
            {
                menuSpawnPosition = visibleMenuObject.transform.position;
                menuSpawnRotation = visibleMenuObject.transform.rotation;
                menuSpawnScale = visibleMenuObject.transform.localScale;
            }

            SendCustomEventDelayedSeconds(nameof(IsUserInVRCheck), 1);
            SendCustomEventDelayedSeconds(nameof(ShouldHideMenu), 5);
        }

        /// <summary>
        /// Checks if the user is in VR after Start()
        /// </summary>
        public void IsUserInVRCheck()
        {
            if (OpenPuttUtils.LocalPlayerIsValid())
            {
                userIsInVR = Networking.LocalPlayer.IsUserInVR();

                if (!userIsInVR)
                {
                    pickup.pickupable = true;
                }
            }
            else
            {
                SendCustomEventDelayedSeconds(nameof(IsUserInVRCheck), 1);
            }
        }

        public override void PostLateUpdate()
        {
            if (userIsInVR)
            {
                if (golfBallHeldByPlayer || golfClubHeldByPlayer)
                {
                    originalHandDistance = -1f;
                    return;
                }

                var bothTriggersHeld = leftUseButtonDown && rightUseButtonDown;

                if (!bothTriggersHeld) return;
                if (originalHandDistance < 0f) return;

                // Use controller tracking positions instead of avatar finger bones
                var leftHand = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
                var rightHand = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
                var currentDistance = Vector3.Distance(leftHand, rightHand);

                var isVisibleNow = currentDistance > (originalHandDistance * openThreshold);

                if (isCurrentlyVisible != isVisibleNow)
                {
                    Networking.LocalPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, vibrationStrength, vibrationFrequency, vibrationDuration);
                    Networking.LocalPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, vibrationStrength, vibrationFrequency, vibrationDuration);
                    
                    if (Utilities.IsValid(eventReceiver))
                    {
                        if (isVisibleNow)
                            eventReceiver.SendCustomEvent(eventToSendOnMenuOpen);
                        else if (!isVisibleNow)
                            eventReceiver.SendCustomEvent(eventToSendOnMenuClose);
                    }
                }

                isCurrentlyVisible = isVisibleNow;

                if (!isVisibleNow)
                {
                    if (hideOnStart)
                    {
                        visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        visibleMenuObject.transform.localScale = Vector3.zero;
                    }
                    else
                    {
                        visibleMenuObject.transform.SetPositionAndRotation(menuSpawnPosition, menuSpawnRotation);
                        visibleMenuObject.transform.localScale = menuSpawnScale;
                    }
                    return;
                }

                var directionBetweenHands = leftHand - rightHand;
                var menuScale = Vector3.one * directionBetweenHands.magnitude;
                var menuPosition = (directionBetweenHands * 0.5f) + rightHand;

                if (Utilities.IsValid(rigidBody))
                    rigidBody.isKinematic = true;

                // Orient the menu so it spans between the hands (menu right axis follows hand direction),
                // but pitch it up/down so it faces the player's head instead of being flat to world Y.
                var headPos = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

                Quaternion finalRot;
                // Fallback to the original simple rotation if vectors are degenerate
                if (directionBetweenHands.sqrMagnitude < 1e-6f || (headPos - menuPosition).sqrMagnitude < 1e-6f)
                {
                    finalRot = Quaternion.LookRotation(directionBetweenHands) * Quaternion.Euler(0, 90, 0);
                }
                else
                {
                        // Compute a forward vector that looks at the head but is orthogonal to the
                        // hand direction (so the menu plane spans the hands while facing the head).
                        var desiredRight = directionBetweenHands.normalized;
                        var toHead = headPos - menuPosition;

                        // Project the head direction onto the plane orthogonal to the hand axis
                        var forward = toHead - Vector3.Project(toHead, desiredRight);

                        // If projection is degenerate (hands line up with head), pick a fallback forward
                        if (forward.sqrMagnitude < 1e-6f)
                        {
                            forward = Vector3.Cross(Vector3.up, desiredRight);
                            if (forward.sqrMagnitude < 1e-6f)
                                forward = Vector3.Cross(Vector3.forward, desiredRight);
                        }

                        forward.Normalize();

                        // Ensure the forward is pointing toward the head (not away)
                        if (Vector3.Dot(forward, toHead) < 0f)
                            forward = -forward;

                        var up = Vector3.Cross(forward, desiredRight).normalized;
                        finalRot = Quaternion.LookRotation(forward, up);
                }

                // Flip the menu by 180 degrees so it faces the player correctly
                visibleMenuObject.transform.SetPositionAndRotation(menuPosition, finalRot * Quaternion.Euler(0f, 180f, 0f));
                visibleMenuObject.transform.localScale = menuScale * 1.2f;
            }
            else if (Input.GetKey(menuKey))
            {
                var menuScale = Vector3.one * 1.7f;

                var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                // Scale the offset by player height (1.7m reference) so the menu doesn't float too far away when scaled down
                var heightScale = Mathf.Clamp(Networking.LocalPlayer.GetAvatarEyeHeightAsMeters(), 0.2f, 5f) / 1.7f;
                var menuPosition = head.position + head.rotation * (desktopHeadOffset * heightScale);

                if (Utilities.IsValid(rigidBody))
                    rigidBody.isKinematic = true;

                visibleMenuObject.transform.SetPositionAndRotation(menuPosition, head.rotation);
                visibleMenuObject.transform.localScale = menuScale;
            }
        }

        /// <summary>
        /// Checks at a regular interval to see if the player has walked away from the menu and hides it if they did
        /// </summary>
        public void ShouldHideMenu()
        {
            if (!OpenPuttUtils.LocalPlayerIsValid()) return;

            var playerPos = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            var menuPos = visibleMenuObject.transform.position;

            var distanceToMenu = Vector3.Distance(playerPos, menuPos);

            var shouldHideMenu = hideDistance > 0f && distanceToMenu > hideDistance;

            if (shouldHideMenu)
            {
                if (Utilities.IsValid(rigidBody))
                    rigidBody.isKinematic = true;
                    
                if (hideOnStart)
                {
                    visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    visibleMenuObject.transform.localScale = Vector3.zero;
                }
                else
                {
                    visibleMenuObject.transform.SetPositionAndRotation(menuSpawnPosition, menuSpawnRotation);
                    visibleMenuObject.transform.localScale = menuSpawnScale;
                }
            }

            SendCustomEventDelayedSeconds(nameof(ShouldHideMenu), 5);
        }

        /// <summary>
        /// Used to monitor inputs of VR players
        /// </summary>
        /// <param name="value"></param>
        /// <param name="args"></param>
        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!userIsInVR)
                return;

            if (args.eventType == UdonInputEventType.BUTTON)
            {
                switch (args.handType)
                {
                    case HandType.RIGHT:
                        rightUseButtonDown = value;
                        break;
                    case HandType.LEFT:
                        leftUseButtonDown = value;
                        break;
                }

                if (leftUseButtonDown && rightUseButtonDown)
                {
                    if (golfBallHeldByPlayer || golfClubHeldByPlayer)
                        return;

                    pickup.pickupable = false;

                    if (originalHandDistance < 0)
                        originalHandDistance = Vector3.Distance(Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position, Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position);
                }
                else
                {
                    pickup.pickupable = true;
                    originalHandDistance = -1;
                }
            }
        }

        public override void OnPickup()
        {
            if (Utilities.IsValid(pickup))
                currentHand = pickup.currentHand;
        }

        public override void OnDrop()
        {
            if (!Utilities.IsValid(controllerTracker)) return;
            if (!Utilities.IsValid(rigidBody)) return;

            var hand = currentHand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;
            var offset = controllerTracker.CalculateLocalOffsetFromWorldPosition(hand, rigidBody.worldCenterOfMass);
            var controllerVel = controllerTracker.GetVelocityAtOffset(hand, offset);

            // Get raw velocity without player velocity
            var rawSpeed = controllerVel.magnitude - Networking.LocalPlayer.GetVelocity().magnitude;

            // Normalize speed based on player height (Using 1.7m as reference height)
            var heightRatio = 1.7f / Mathf.Clamp(Networking.LocalPlayer.GetAvatarEyeHeightAsMeters(), 0.2f, 5f);
            var normalizedSpeed = rawSpeed * heightRatio;

            if (normalizedSpeed < minThrowSpeed)
            {
                rigidBody.isKinematic = true;
                return;
            }

            rigidBody.isKinematic = false;

            // Get the linear velocity of the Rigidbody's center of mass using the stored offset
            var throwLinearVelocity = controllerTracker.GetVelocityAtOffset(hand, offset);

            // Get the angular velocity of the hand. This is the angular velocity of the rigid body.
            var throwAngularVelocity = controllerTracker.GetAngularVelocity(hand, 5);

            // Apply the calculated velocities to the rigidbody
            rigidBody.velocity = throwLinearVelocity * pickup.ThrowVelocityBoostScale;
            rigidBody.angularVelocity = throwAngularVelocity * Mathf.Deg2Rad; // Convert degrees/sec to radians/sec for Rigidbody.angularVelocity


            if (Utilities.IsValid(pickup))
                currentHand = pickup.currentHand;
        }
    }
}