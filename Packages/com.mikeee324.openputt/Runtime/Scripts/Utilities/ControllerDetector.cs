using UdonSharp;
using UnityEngine;
using UnityEngine.Diagnostics;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ControllerDetector : UdonSharpBehaviour
    {
        [OpenPuttDescription("Experimental (unfinished) helper for desktop users that tries to detect which type of gamepad is plugged in, so other scripts can read the correct button layout.")]
        [OpenPuttFoldoutGroup("Detected State")]
        public Controller LastUsedJoystick = Controller.None;

        [OpenPuttFoldoutGroup("Detected State")]
        public int LastKnownJoystickID = 1;

        public Controller[] Joysticks = new Controller[4] { Controller.None, Controller.None, Controller.None, Controller.None };

        private int[] totalTicks = new int[4] { 0, 0, 0, 0 };
        private int[] ds4Ticks = new int[4] { 0, 0, 0, 0 };

        private Collider[] menuCollider = new Collider[1];

        public VRCInputMethod currentInputMethod {get; private set;} = VRCInputMethod.Keyboard;

        void Start()
        {
            SendCustomEventDelayedSeconds(nameof(DetectControllers), 1);
        }

        public void DetectControllers()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer))
            {
                SendCustomEventDelayedSeconds(nameof(DetectControllers), .25f);
                return;
            }

            if (Networking.LocalPlayer.IsUserInVR())
            {
                Debug.Log("ControllerDetector is only made for non VR users.. stopping.");
                gameObject.SetActive(false);
                return;
            }

#if UNITY_EDITOR
            // Wait for user to close editor pause windows before checking
            var menuOpen = Physics.OverlapSphereNonAlloc(Networking.LocalPlayer.GetPosition(), 9999f, menuCollider, (1 << 19) | (1 << 20) | (1 << 21)) > 0;
            if (menuOpen)
            {
                SendCustomEventDelayedSeconds(nameof(DetectControllers), .25f);
                return;
            }
#endif

            var joystick1CheckResult = CheckType(0);
            var joystick2CheckResult = CheckType(1);
            var joystick3CheckResult = CheckType(2);
            var joystick4CheckResult = CheckType(3);

            if (joystick1CheckResult && joystick2CheckResult && joystick3CheckResult && joystick4CheckResult)
            {
                Debug.Log("Finished controller checks");
                SendCustomEventDelayedSeconds(nameof(CheckCurrentJoystick), .1f);
                return;
            }

            SendCustomEventDelayedFrames(nameof(DetectControllers), 5);
        }

        private void LateUpdate()
        {
            // Force currentInputMethod back to mouse if mouse movement detected
            if (currentInputMethod != VRCInputMethod.Mouse && (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.0001f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.0001f))
            {
                currentInputMethod = VRCInputMethod.Mouse;

                totalTicks = new int[4] { 0, 0, 0, 0 };
                ds4Ticks = new int[4] { 0, 0, 0, 0 };
                SendCustomEventDelayedSeconds(nameof(DetectControllers), 1);
            }
        }

        public void CheckCurrentJoystick()
        {
            SendCustomEventDelayedFrames(nameof(CheckCurrentJoystick), 0);

            var moveHorizontal = Input.GetAxis("Horizontal");
            var moveVertical = Input.GetAxis("Vertical");

            if (moveHorizontal.IsNearZero() && moveVertical.IsNearZero())
                return;

            var prevJoystick = LastKnownJoystickID;

            float joyHorizontal = Joysticks[0].MoveHorizontal(1), joyVertical = -Joysticks[0].MoveVertical(1);
            if (!joyHorizontal.IsNearZero() && !joyVertical.IsNearZero())
                if (moveHorizontal == joyHorizontal && moveVertical == joyVertical)
                    LastKnownJoystickID = 1;

            joyHorizontal = Joysticks[1].MoveHorizontal(2);
            joyVertical = -Joysticks[1].MoveVertical(2);
            if (!joyHorizontal.IsNearZero() && !joyVertical.IsNearZero())
                if (moveHorizontal == joyHorizontal && moveVertical == joyVertical)
                    LastKnownJoystickID = 2;

            joyHorizontal = Joysticks[2].MoveHorizontal(3);
            joyVertical = -Joysticks[2].MoveVertical(3);
            if (!joyHorizontal.IsNearZero() && !joyVertical.IsNearZero())
                if (moveHorizontal == joyHorizontal && moveVertical == joyVertical)
                    LastKnownJoystickID = 3;

            joyHorizontal = Joysticks[3].MoveHorizontal(4);
            joyVertical = -Joysticks[3].MoveVertical(4);
            if (!joyHorizontal.IsNearZero() && !joyVertical.IsNearZero())
                if (moveHorizontal == joyHorizontal && moveVertical == joyVertical)
                    LastKnownJoystickID = 4;

            if (prevJoystick != LastKnownJoystickID)
                LastUsedJoystick = Joysticks[LastKnownJoystickID - 1];
        }

        private bool CheckType(int joystickIndex)
        {
            // Try and figure out if th e player has a controller plugged in, and maybe detect if xbox or ds4
            if (totalTicks[joystickIndex] >= 30)
            {
                // We've used up all time to check controller
                if (ds4Ticks[joystickIndex] == totalTicks[joystickIndex])
                    Joysticks[joystickIndex] = Controller.DS4; // If every frame we checked looks like a DS4 then they're probs using a DS4
                else
                    Joysticks[joystickIndex] = Controller.Xbox; // Otherwise default to xbox

                return true;
            }

            totalTicks[joystickIndex]++;

            // Read LookHorizontal from both controller types
            var LT = ControllerExtensions.GetInputAxis(4, joystickID: joystickIndex + 1);
            var RT = ControllerExtensions.GetInputAxis(5, joystickID: joystickIndex + 1);
            var weirdDS4Thing = LT == -1 && RT == -1;

            // If the xbox mode reads -1 and the ds4 reads near 0, log the tick as a valid DS4 tick
            if (weirdDS4Thing)
                ds4Ticks[joystickIndex]++;

            return false;
        }

        public override void OnInputMethodChanged(VRCInputMethod inputMethod)
        {
            currentInputMethod = inputMethod;

            totalTicks = new int[4] { 0, 0, 0, 0 };
            ds4Ticks = new int[4] { 0, 0, 0, 0 };
            SendCustomEventDelayedSeconds(nameof(DetectControllers), 1);
        }
    }
}