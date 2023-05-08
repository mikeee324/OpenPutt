/**
 * Made by TummyTime
 */
using mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PortableMenu : UdonSharpBehaviour
{
    [SerializeField] private KeyCode menuKey = KeyCode.N;
    [SerializeField] private GameObject visibleMenuObject;
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
    private bool checkingLocationAlready = false;

    private bool _isVisible = false;

    void Start()
    {
        visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        visibleMenuObject.transform.localScale = Vector3.zero;

        SendCustomEventDelayedSeconds(nameof(IsUserInVRCheck), 5);
        SendCustomEventDelayedSeconds(nameof(ShouldHideMenu), 5);
    }

    public void IsUserInVRCheck()
    {
        if (Utils.LocalPlayerIsValid())
        {
            userIsInVR = Networking.LocalPlayer.IsUserInVR();

            if (!userIsInVR)
                this.enabled = true;
        }
        else
        {
            SendCustomEventDelayedSeconds(nameof(IsUserInVRCheck), 5);
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

                Vector3 leftPosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand);
                Vector3 rightPosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand);
                float currentDistance = Vector3.Distance(leftPosition, rightPosition);

                bool newIsVisible = false;
                if (currentDistance > (originalHandDistance * openThreshold))
                    newIsVisible = true;
                else if (closeThreshold > 0.0f && currentDistance < (originalHandDistance * closeThreshold))
                    newIsVisible = false;

                Quaternion headRotation = Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.Head);
                Vector3 menuScale = Vector3.one * Vector3.Distance(leftPosition, rightPosition);
                Vector3 menuPosition = ((leftPosition - rightPosition) * 0.5f) + rightPosition;

                if (newIsVisible)
                {
                    visibleMenuObject.transform.SetPositionAndRotation(menuPosition, headRotation);
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
            Vector3 menuScale = Vector3.one;
            Vector3 headPosition = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
            Vector3 menuPosition = headPosition + visibleMenuObject.transform.TransformDirection(desktopHeadOffset);
            Quaternion menuRotation = Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.Head);

            //visibleMenuObject.transform.LookAt(headPosition);
            visibleMenuObject.transform.SetPositionAndRotation(menuPosition, menuRotation);
            visibleMenuObject.transform.localScale = menuScale;
        }
    }

    public void ShouldHideMenu()
    {
        if (!Utils.LocalPlayerIsValid()) return;

        float distanceToMenu = Vector2.Distance(Networking.LocalPlayer.GetPosition(), visibleMenuObject.transform.position);

        bool shouldHideMenu = distanceToMenu > hideDistance;

        if (shouldHideMenu)
        {
            visibleMenuObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            visibleMenuObject.transform.localScale = Vector3.zero;
        }

        SendCustomEventDelayedSeconds(nameof(ShouldHideMenu), 5);
    }

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

                if (originalHandDistance == -1)
                    originalHandDistance = Vector3.Distance(Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand), Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand));
            }
            else
            {
                originalHandDistance = -1;
            }
        }
    }
}