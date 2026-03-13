using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// This is an attempt at trying to work with multiple controller types. <b>* It is NOT finished or fully tested either! *</b>
    /// </summary>
    public enum Controller
    {
        None,
        Xbox,
        DS4
    }

    public enum ShootInputType
    {
        None,
        Keyboard,
        Mouse,
        Controller,
        UI
    }

    public enum ControllerButtons
    {
        None,
        A,
        B,
        X,
        Y,
        LB,
        RB,
        LT,
        RT,
        Select,
        Start,
        LeftStick,
        RightStick
    }

    public enum DevicePlatform
    {
        Desktop,
        PCVR,
        AndroidVR,
        AndroidMobile
    }

    public static class DevicePlatformExtensions
    {
        public static string GetName(this DevicePlatform platform)
        {
            switch (platform)
            {
                case DevicePlatform.AndroidVR:
                    return "AndroidVR";
                case DevicePlatform.AndroidMobile:
                    return "AndroidMobile";
                case DevicePlatform.PCVR:
                    return "PCVR";
                case DevicePlatform.Desktop:
                default:
                    return "Desktop";
            }
        }

        /// <summary>
        /// Returns the current platform for the local player only. Remote players will just return as Desktop as we don't have a way of telling this right now.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static DevicePlatform GetPlatform(this VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return DevicePlatform.Desktop;

#if UNITY_ANDROID || UNITY_IOS
            if (player.IsUserInVR()) return DevicePlatform.AndroidVR;
            else return DevicePlatform.AndroidMobile;
#else
            if (player.IsUserInVR()) return DevicePlatform.PCVR;
            return DevicePlatform.Desktop;
#endif
        }
    }

    public static class ControllerExtensions
    {
        public static string GetName(this Controller platform)
        {
            switch (platform)
            {
                case Controller.None:
                    return "Unknown";
                case Controller.Xbox:
                    return "Xbox";
                case Controller.DS4:
                    return "DS4";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Checks multiple joysticks (up to 4) for a value that's not near 0 and returns the first value it finds
        /// </summary>
        /// <param name="axis">The axis ID to find a value for</param>
        /// <param name="deadzone">The deadzone for values to ignore</param>
        /// <returns></returns>
        public static float GetInputAxis(int axis, int joystickID)
        {
            if (axis < 1 || axis > 10)
            {
                Debug.LogError($"Cannot request joystick {joystickID} axis {axis} as it isn't available in vrc!");
                return 0;
            }

            if (joystickID > 4)
            {
                Debug.LogError($"Cannot request joystick {joystickID} as it isn't available in vrc!");
                return 0;
            }

            return Input.GetAxis($"Joy{joystickID} Axis {axis}");
        }

        public static bool IsKeyPressed(this Controller controller, ControllerButtons button, int joystickID)
        {
            switch (button)
            {
                case ControllerButtons.A:
                    return controller.A(joystickID);
                case ControllerButtons.B:
                    return controller.B(joystickID);
                case ControllerButtons.X:
                    return controller.X(joystickID);
                case ControllerButtons.Y:
                    return controller.Y(joystickID);
                case ControllerButtons.LB:
                    return controller.LeftBumper(joystickID);
                case ControllerButtons.LT:
                    return controller.LeftTriggerButton(joystickID);
                case ControllerButtons.RB:
                    return controller.RightBumper(joystickID);
                case ControllerButtons.RT:
                    return controller.RightTriggerButton(joystickID);
                case ControllerButtons.LeftStick:
                    return controller.LeftStickClick(joystickID);
                case ControllerButtons.RightStick:
                    return controller.RightStickClick(joystickID);
                case ControllerButtons.Select:
                    return controller.Select(joystickID);
                case ControllerButtons.Start:
                    return controller.Select(joystickID);
            }

            return false;
        }

        private static bool IsJoystickKeyPressed(int joystickKeyID, int joystickID)
        {
            var keyCode = (int)KeyCode.Joystick1Button0;
            keyCode += ((joystickID - 1) * 20);
            keyCode += joystickKeyID;
            return Input.GetKey((KeyCode)keyCode);
        }

        public static float MoveHorizontal(this Controller controller, int joystickID)
        {
            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(1, joystickID: joystickID);
                case Controller.DS4:
                    return GetInputAxis(1, joystickID: joystickID);
                default:
                    return 0;
            }
        }

        public static float MoveVertical(this Controller controller, int joystickID)
        {
            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(2, joystickID: joystickID);
                case Controller.DS4:
                    return GetInputAxis(2, joystickID: joystickID);
                default:
                    return 0;
            }
        }

        public static float LookHorizontal(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return GetInputAxis(3, joystickID: joystickID);
#else

            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(4, joystickID: joystickID);
                case Controller.DS4:
                    return GetInputAxis(3, joystickID: joystickID);
                default:
                    return 0;
            }
#endif
        }

        public static float LookVertical(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return GetInputAxis(4, joystickID: joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(5, joystickID: joystickID);
                case Controller.DS4:
                    return GetInputAxis(6, joystickID: joystickID);
                default:
                    return 0;
            }
#endif
        }

        public static float LeftTrigger(this Controller controller, int joystickID, bool rawOutput = false)
        {
#if UNITY_ANDROID || UNITY_IOS
            return GetInputAxis(7, joystickID: joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(9, joystickID: joystickID);
                case Controller.DS4:
                    if (rawOutput)
                        return GetInputAxis(4, joystickID: joystickID);
                    return (GetInputAxis(4, joystickID: joystickID) + 1) / 2; // DS4 Goes -1 to 1
                default:
                    return 0;
            }
#endif
        }

        public static float RightTrigger(this Controller controller, int joystickID, bool rawOutput = false)
        {
#if UNITY_ANDROID || UNITY_IOS
            return GetInputAxis(8, joystickID: joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(10, joystickID: joystickID);
                case Controller.DS4:
                    if (rawOutput)
                        return GetInputAxis(5, joystickID: joystickID);
                    return (GetInputAxis(5, joystickID: joystickID) + 1) / 2; // DS4 Goes -1 to 1
                default:
                    return 0;
            }
#endif
        }

        public static float DpadHorizontal(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return GetInputAxis(5, joystickID: joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(6, joystickID: joystickID);
                case Controller.DS4:
                    return GetInputAxis(7, joystickID: joystickID);
                default:
                    return 0;
            }
#endif
        }

        public static float DpadVertical(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return GetInputAxis(6, joystickID: joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return GetInputAxis(7, joystickID: joystickID);
                case Controller.DS4:
                    return GetInputAxis(8, joystickID: joystickID);
                default:
                    return 0;
            }
        }

        public static bool DpadLeft(this Controller controller, int joystickID)
        {
            return controller.DpadHorizontal(joystickID) < -.5f;
        }

        public static bool DpadRight(this Controller controller, int joystickID)
        {
            return controller.DpadHorizontal(joystickID) > .5f;
        }

        public static bool DpadUp(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return controller.DpadVertical(joystickID) < -.5f;
#endif
            return controller.DpadVertical(joystickID) > .5f;
#endif
        }

        public static bool DpadDown(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return controller.DpadVertical(joystickID) > .5f;
#else
            return controller.DpadVertical(joystickID) < -.5f;
#endif
        }

        public static float Jump(this Controller _) => Input.GetAxis("Jump");

        public static bool A(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(0, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(0, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(1, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool B(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(1, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(1, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(2, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool Y(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(3, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(3, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(3, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool X(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(2, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(2, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(0, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool Select(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(11, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(6, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(8, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool Start(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(10, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(7, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(9, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool LeftBumper(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(4, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(4, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(4, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool RightBumper(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(5, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(5, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(5, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool LeftTriggerButton(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(6, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return controller.LeftTrigger(joystickID) > .5f;
                case Controller.DS4:
                    return IsJoystickKeyPressed(6, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool RightTriggerButton(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(7, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return controller.RightTrigger(joystickID) > .5f;
                case Controller.DS4:
                    return IsJoystickKeyPressed(7, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool TouchpadClick(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return false; // Android thinks the touchpad is a mouse and acts weird
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return false;
                case Controller.DS4:
                    return IsJoystickKeyPressed(13, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool LeftStickClick(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(8, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(8, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(10, joystickID);
                default:
                    return false;
            }
#endif
        }

        public static bool RightStickClick(this Controller controller, int joystickID)
        {
#if UNITY_ANDROID || UNITY_IOS
            return IsJoystickKeyPressed(9, joystickID);
#else
            switch (controller)
            {
                case Controller.Xbox:
                    return IsJoystickKeyPressed(9, joystickID);
                case Controller.DS4:
                    return IsJoystickKeyPressed(11, joystickID);
                default:
                    return false;
            }
#endif
        }
    }
}