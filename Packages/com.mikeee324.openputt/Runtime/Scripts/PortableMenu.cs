/**
 * Made by TummyTime
 */
using mikeee324.OpenPutt;
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
                bool wasEnabled = this.enabled;

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
                Vector3 leftHand = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand);
                Vector3 rightHand = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand);
                float currentDistance = Vector3.Distance(leftHand, rightHand);

                bool newIsVisible = false;
                if (currentDistance > (originalHandDistance * openThreshold))
                    newIsVisible = true;
                else if (closeThreshold > 0.0f && currentDistance < (originalHandDistance * closeThreshold))
                    newIsVisible = false;

                Vector3 directionBetweenHands = leftHand - rightHand;
                Vector3 menuScale = Vector3.one * directionBetweenHands.magnitude;
                Vector3 menuPosition = (directionBetweenHands * 0.5f) + rightHand;

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
            Vector3 menuScale = Vector3.one * 1.7f;
            Vector3 headPosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
            Vector3 menuPosition = headPosition + visibleMenuObject.transform.TransformDirection(desktopHeadOffset);
            Quaternion menuRotation = Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.Head);

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

        Vector3 playerPos = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
        Vector3 menuPos = visibleMenuObject.transform.position;

        float distanceToMenu = Vector3.Distance(playerPos, menuPos);

        bool shouldHideMenu = distanceToMenu > hideDistance;

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

                if (originalHandDistance == -1)
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