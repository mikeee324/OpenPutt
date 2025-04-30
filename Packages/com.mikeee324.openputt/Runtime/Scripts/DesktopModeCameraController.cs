using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(98)]
    public class DesktopModeCameraController : UdonSharpBehaviour
    {
        #region References

        [Header("References")]
        public OpenPutt openPutt;

        public DesktopModeController desktopModeController;

        public Camera thisCam;

        // The target we are following
        public Rigidbody target;

        #endregion

        #region Public Settings

        [Header("Settings"), Range(.2f, 10f), Tooltip("The default distance to the object we are following")]
        public float distance = 10.0f;

        [Range(.2f, 10f)]
        public float minDistance = .5f;

        [Range(.2f, 10f)]
        public float maxDistance = 5f;

        [Range(1, 5), Tooltip("How fast the camera smoothly zooms")]
        public float cameraZoomSpeed = 2f;

        [Range(0, 5), Tooltip("How fast the camera moves horizontally")]
        public float cameraXSpeed = 1f;

        [Range(0, 5), Tooltip("How fast the camera moves vertically")]
        public float cameraYSpeed = 1f;

        [Range(-89.9f, 89.9f)]
        public float cameraMinY = -40f;

        [Range(-89.9f, 89.9f)]
        public float cameraMaxY = 89.9f;

        [Header("Camera Masks")]
        public LayerMask defaultCullingMask;

        public LayerMask noPlayersCullingMask;

        #endregion

        #region Properties

        public bool ShowPlayersInCamera
        {
            get => thisCam.cullingMask == defaultCullingMask;
            set => thisCam.cullingMask = value ? defaultCullingMask : noPlayersCullingMask;
        }

        /// <summary>
        /// Locks the Y axis mouse movement on the camera while lining up a shot
        /// </summary>
        public bool LockCamera;

        #endregion

        #region Private Vars

        private VRCInputMethod currentInputMethod = VRCInputMethod.Mouse;

        /// <summary>
        /// Current rotation angle for horizontal orbit around the target
        /// </summary>
        private float currentCameraX;

        /// <summary>
        /// Current rotation angle for vertical orbit around the target
        /// </summary>
        private float currentCameraY = 10;

        private float currentHorizontalDelta;
        private float currentVerticalDelta;
        private float currentKeyboardHorizontalDelta;
        private float currentKeyboardVerticalDelta;

        private float currentMoveVertical;
        private float currentMoveHorizontal;

        /// <summary>
        /// Stores the cameras distance from the target from the last frame, used to lerping the camera distance
        /// </summary>
        private float lastKnownRealDistance;

        #endregion

        private void OnEnable()
        {
            if (!Utilities.IsValid(target))
                return;

            // Set initial camera angle to look away from the player - as long as they are stood near the ball (otherwise just re-use last rotation)
            var localPlayerPos = Networking.LocalPlayer.GetPosition();
            if (Vector3.Distance(localPlayerPos, target.transform.position) < 2)
                currentCameraX = GetCameraAngle(localPlayerPos, target.position);

            currentCameraY = Mathf.Clamp(currentCameraY, cameraMinY, cameraMaxY);

            lastKnownRealDistance = distance;

            transform.position = target.transform.position - Quaternion.Euler(currentCameraY, currentCameraX, 0f) * (distance * Vector3.forward);
            transform.LookAt(target.transform.position);
        }

        /// <summary>
        /// Works out which way to point the camera so it faces in the direction we want
        /// </summary>
        /// <param name="from">The starting point (player or ball pos)</param>
        /// <param name="to">A poisiton that we want to look at</param>
        /// <returns>The camera angle that should point the camera in the correct direction to face the target</returns>
        private float GetCameraAngle(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            var angle = Vector3.Angle(Vector3.forward, direction);
            var sign = Mathf.Sign(Vector3.Dot(Vector3.up, Vector3.Cross(Vector3.forward, direction)));
            return angle * sign;
        }

        public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
            {
                currentMoveHorizontal = value;
            }
        }

        public override void InputMoveVertical(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
            {
                currentMoveVertical = value;
            }
        }

        public override void InputLookHorizontal(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                currentHorizontalDelta = args.floatValue;
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                currentVerticalDelta = args.floatValue;
        }

        public override void OnInputMethodChanged(VRCInputMethod inputMethod)
        {
            // Used to toggle camera inversion for controllers
            // This event is not called just by moving a mouse! - Player has to click a button for it to count
            currentInputMethod = inputMethod;
        }

        /// <summary>
        /// Using FixedUpdate for tracking the ball because it is a rigidbody
        /// </summary>
        public override void PostLateUpdate()
        {
            // Early out if we don't have a target
            if (!target) return;

            currentKeyboardVerticalDelta = 0;
            if (Input.GetKey(KeyCode.UpArrow))
                currentKeyboardVerticalDelta += .8f;
            if (Input.GetKey(KeyCode.DownArrow))
                currentKeyboardVerticalDelta -= .8f;

            currentKeyboardHorizontalDelta = 0;
            if (Input.GetKey(KeyCode.RightArrow))
                currentKeyboardHorizontalDelta += .8f;
            if (Input.GetKey(KeyCode.LeftArrow))
                currentKeyboardHorizontalDelta -= .8f;

            // Force currentInputMethod back to mouse - OnInputMethodChanged is not called just by moving mouse (Only changes when a click happens)
            if (currentInputMethod != VRCInputMethod.Mouse && (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.0001f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.0001f))
                currentInputMethod = VRCInputMethod.Mouse;
            
            // if (desktopModeController.localPlayerPlatform == DevicePlatform.AndroidMobile)
            // {
            //     // Workaround for InputLookHorizontal/InputlookVertical doing nothing in the mobile alpha
            //     //currentVerticalDelta = -currentMoveVertical;
            //     //currentHorizontalDelta = currentMoveHorizontal;
            // }
            // else
            if (!currentMoveVertical.IsNearZero())
            {
                distance = Mathf.Clamp(distance - (currentMoveVertical * .4f), minDistance, maxDistance);
            }
            else
            {
                // Scroll wheel zoom
                var scrollWheel = Input.GetAxis("Mouse ScrollWheel");
                distance = Mathf.Clamp(distance - scrollWheel, minDistance, maxDistance);
            }

            // Scale zoom level depending on targets speed
            var targetScaledDistance = distance * Mathf.Clamp(target.velocity.magnitude / 3, 1, distance < 1 ? 5 : 2);
            lastKnownRealDistance = Mathf.Lerp(lastKnownRealDistance, targetScaledDistance, cameraZoomSpeed * Time.deltaTime);

            var horizontalDelta = currentKeyboardHorizontalDelta != 0 ? currentKeyboardHorizontalDelta : currentHorizontalDelta;
            var verticalDelta = currentKeyboardVerticalDelta != 0 ? currentKeyboardVerticalDelta : currentVerticalDelta;

            // Unity editor reports mouse movements higher so lower the speed to make it usable in editor
#if UNITY_EDITOR
            var localSpeedModifier = 0.5f;
#else
            float localSpeedModifier = 0.5f;
#endif

            // Player can always rotate camera horizontally
            currentCameraX += (cameraXSpeed * localSpeedModifier) * horizontalDelta;
            if (!LockCamera)
                currentCameraY -= (cameraYSpeed * localSpeedModifier) * verticalDelta;

            currentCameraY = Mathf.Clamp(currentCameraY, cameraMinY, cameraMaxY);

            transform.position = target.transform.position - Quaternion.Euler(currentCameraY, currentCameraX, 0f) * (lastKnownRealDistance * Vector3.forward);
            transform.LookAt(target.transform.position);
        }
    }
}