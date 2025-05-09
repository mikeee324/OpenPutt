﻿/**
 * Made by TummyTime
 */

using dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PortableMenu : UdonSharpBehaviour
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
    
    
    public bool golfClubHeldByPlayer;
    public bool golfBallHeldByPlayer;
    private bool leftUseButtonDown;
    private bool rightUseButtonDown;
    private float originalHandDistance = -1f;
    private bool userIsInVR;
    private bool isCurrentlyVisible = false;
    private VRC_Pickup.PickupHand currentHand = VRC_Pickup.PickupHand.None;

    void Start()
    {
        if (hideOnStart)
        {
            visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            visibleMenuObject.transform.localScale = Vector3.zero;
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

            // Maybe this crashes if an avatar doesn't have finger bones? - No idea
            var leftHand = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftIndexProximal);
            var rightHand = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexProximal);
            var currentDistance = Vector3.Distance(leftHand, rightHand);

            var isVisibleNow = currentDistance > (originalHandDistance * openThreshold);

            if (isCurrentlyVisible != isVisibleNow)
            {
                Networking.LocalPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, vibrationStrength, vibrationFrequency, vibrationDuration);
                Networking.LocalPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, vibrationStrength, vibrationFrequency, vibrationDuration);
            }
            isCurrentlyVisible = isVisibleNow;

            if (!isVisibleNow)
            {
                visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                visibleMenuObject.transform.localScale = Vector3.zero;
                return;
            }

            var directionBetweenHands = leftHand - rightHand;
            var menuScale = Vector3.one * directionBetweenHands.magnitude;
            var menuPosition = (directionBetweenHands * 0.5f) + rightHand;

            if (Utilities.IsValid(rigidBody))
                rigidBody.isKinematic = true;

            visibleMenuObject.transform.SetPositionAndRotation(menuPosition, Quaternion.LookRotation(directionBetweenHands) * Quaternion.Euler(0, 90, 0));
            visibleMenuObject.transform.localScale = menuScale;
        }
        else if (Input.GetKey(menuKey))
        {
            var menuScale = Vector3.one * 1.7f;

            var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            var menuPosition = head.position + visibleMenuObject.transform.TransformDirection(desktopHeadOffset);

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

        var playerPos = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
        var menuPos = visibleMenuObject.transform.position;

        var distanceToMenu = Vector3.Distance(playerPos, menuPos);

        var shouldHideMenu = distanceToMenu > hideDistance;

        if (shouldHideMenu)
        {
            if (Utilities.IsValid(rigidBody))
                rigidBody.isKinematic = true;
            visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            visibleMenuObject.transform.localScale = Vector3.zero;
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
                    originalHandDistance = Vector3.Distance(Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand), Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand));
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