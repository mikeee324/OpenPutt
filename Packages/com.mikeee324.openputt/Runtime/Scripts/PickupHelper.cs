using UdonSharp;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PickupHelper : UdonSharpBehaviour
    {
        #region Public API

        /// <summary>
        /// If there is a VRCPickup registered this will say which hand the local player is using to hold it. (<b>None/Left/Right</b>)<br/>
        /// Note: Desktop users usually report VRCPickup.PickupHand.Right when they are holding an object
        /// </summary>
        public VRC_Pickup.PickupHand CurrentHand => _currentHand;

        public bool LeftUseButtonDown { get; private set; }
        public bool RightUseButtonDown { get; private set; }
        public bool LeftGripButtonDown { get; private set; }
        public bool RightGripButtonDown { get; private set; }
        public bool JumpButtonDown { get; private set; }
        public float moveHorizontalAxis { get; private set; }
        public float moveVerticalAxis { get; private set; }
        public float lookHorizontalAxis { get; private set; }
        public float lookVerticalAxis { get; private set; }

        #endregion

        #region Internal Vars

        private VRCPickup pickup;
        private VRC_Pickup.PickupHand _currentHand = VRC_Pickup.PickupHand.None;

        #endregion

        void Start()
        {
            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();

            moveHorizontalAxis = 0f;
            moveVerticalAxis = 0f;
            lookHorizontalAxis = 0f;
            lookVerticalAxis = 0f;
        }

        public void Drop()
        {
            if (Utilities.IsValid(pickup))
                pickup.Drop();
        }

        public override void OnPickup()
        {
            // Check if Player is still holding item (Scripts could force the object to be dropped)
            if (Utilities.IsValid(pickup) && pickup.IsHeld && pickup.currentPlayer == Networking.LocalPlayer)
                _currentHand = pickup.currentHand;
            else
                _currentHand = VRC_Pickup.PickupHand.None;
        }

        public override void OnDrop()
        {
            _currentHand = VRC_Pickup.PickupHand.None;
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            // Store button state
            if (args.handType == HandType.LEFT)
                LeftGripButtonDown = value;
            else if (args.handType == HandType.RIGHT)
                RightGripButtonDown = value;
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
            // Store button state
            if (args.handType == HandType.LEFT)
                LeftGripButtonDown = value;
            else if (args.handType == HandType.RIGHT)
                RightGripButtonDown = value;
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            // Store button state
            if (args.handType == HandType.LEFT)
                LeftUseButtonDown = value;
            else if (args.handType == HandType.RIGHT)
                RightUseButtonDown = value;
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            JumpButtonDown = value;
        }

        public override void InputLookHorizontal(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                lookHorizontalAxis = value;
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                lookVerticalAxis = value;
        }

        public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                moveHorizontalAxis = value;
        }

        public override void InputMoveVertical(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                moveVerticalAxis = value;
        }
    }
}