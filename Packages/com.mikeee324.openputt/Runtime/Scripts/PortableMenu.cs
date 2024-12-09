/**
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
    [SerializeField] private KeyCode menuKey = KeyCode.N;
    [SerializeField] private GameObject visibleMenuObject;
    [SerializeField] private VRCPickup pickup;
    [SerializeField] private Vector3 desktopHeadOffset = Vector3.zero;

    [SerializeField] private float hideDistance = 3f;

    [SerializeField, Range(0f, 1f), Tooltip("How small the menu can get before it is hidden (relative to the initial opening size)")] private float closeThreshold = 0.4f;
    [SerializeField, Range(1f, 5f), Tooltip("How large the menu has to be before it shows initially (relative to the initial opening size)")] private float openThreshold = 2f;

    public bool AllowMenuToOpen
    {
        get => _allowMenuToOpen;
        set
        {
            _allowMenuToOpen = value;
            if (AllowMenuToOpen)
            {
                var wasEnabled = this.enabled;

                this.enabled = leftUseButtonDown && rightUseButtonDown;

                if (this.enabled != wasEnabled)
                {
                    if (this.enabled)
                    {
                        originalHandDistance = Vector3.Distance(Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand), Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand));
                    }
                }
            }
        }
    }

    public bool golfClubHeldByPlayer = false;
    public bool golfBallHeldByPlayer = false;
    private bool _allowMenuToOpen = true;
    private bool leftUseButtonDown = false;
    private bool rightUseButtonDown = false;
    private float originalHandDistance = -1f;
    private bool userIsInVR = false;

    void Start()
    {
        visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        visibleMenuObject.transform.localScale = Vector3.zero;

        SendCustomEventDelayedSeconds(nameof(IsUserInVRCheck), 1);
        SendCustomEventDelayedSeconds(nameof(ShouldHideMenu), 5);
    }

    /// <summary>
    /// Checks if the user is in VR after Start()
    /// </summary>
    public void IsUserInVRCheck()
    {
        if (Utils.LocalPlayerIsValid())
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
            if (originalHandDistance != -1f && leftUseButtonDown && rightUseButtonDown)
            {
                // Maybe this crashes if an avatar doesn't have finger bones? - No idea
                var leftHand = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftIndexProximal);
                var rightHand = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexProximal);
                var currentDistance = Vector3.Distance(leftHand, rightHand);

                var newIsVisible = false;
                if (currentDistance > (originalHandDistance * openThreshold))
                    newIsVisible = true;
                else if (closeThreshold > 0.0f && currentDistance < (originalHandDistance * closeThreshold))
                    newIsVisible = false;

                var directionBetweenHands = leftHand - rightHand;
                var menuScale = Vector3.one * directionBetweenHands.magnitude;
                var menuPosition = (directionBetweenHands * 0.5f) + rightHand;

                if (newIsVisible)
                {
                    visibleMenuObject.transform.SetPositionAndRotation(menuPosition, Quaternion.LookRotation(directionBetweenHands) * Quaternion.Euler(0, 90, 0));
                    visibleMenuObject.transform.localScale = menuScale;
                }
                else
                {
                    visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    visibleMenuObject.transform.localScale = Vector3.zero;
                }
            }
        }
        else if (Input.GetKey(menuKey))
        {
            var menuScale = Vector3.one * 1.7f;
            var headPosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
            var menuPosition = headPosition + visibleMenuObject.transform.TransformDirection(desktopHeadOffset);
            var menuRotation = Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.Head);

            visibleMenuObject.transform.SetPositionAndRotation(menuPosition, menuRotation);
            visibleMenuObject.transform.localScale = menuScale;
        }
    }

    /// <summary>
    /// Checks at a regular interval to see if the player has walked away from the menu and hides it if they did
    /// </summary>
    public void ShouldHideMenu()
    {
        if (!Utils.LocalPlayerIsValid()) return;

        var playerPos = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
        var menuPos = visibleMenuObject.transform.position;

        var distanceToMenu = Vector3.Distance(playerPos, menuPos);

        var shouldHideMenu = distanceToMenu > hideDistance;

        if (shouldHideMenu)
        {
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
}