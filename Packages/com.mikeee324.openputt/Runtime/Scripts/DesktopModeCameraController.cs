
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace mikeee324.OpenPutt
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

        public bool invertCameraX = false;
        public bool invertCameraY = false;

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
        public bool LockCamera = false;
        #endregion

        #region Private Vars
        /// <summary>
        /// Current rotation angle for horizontal orbit around the target
        /// </summary>
        private float currentCameraX = 0;
        /// <summary>
        /// Current rotation angle for vertical orbit around the target
        /// </summary>
        private float currentCameraY = 10;

        private float currentHorizontalDelta = 0;
        private float currentVerticalDelta = 0;
        private float currentKeyboardHorizontalDelta = 0;
        private float currentKeyboardVerticalDelta = 0;

        private float currentMoveVertical = 0;
        private float currentMoveHorizontal = 0;
        /// <summary>
        /// Stores the cameras distance from the target from the last frame, used to lerping the camera distance
        /// </summary>
        private float lastKnownRealDistance = 0;
        #endregion

        private void OnEnable()
        {
            if (target == null)
                return;

            // Set initial camera angle to look away from the player - as long as they are stood near the ball (otherwise just re-use last rotation)
            Vector3 localPlayerPos = Networking.LocalPlayer.GetPosition();
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
            Vector3 direction = to - from;
            float angle = Vector3.Angle(Vector3.forward, direction);
            float sign = Mathf.Sign(Vector3.Dot(Vector3.up, Vector3.Cross(Vector3.forward, direction)));
            return angle * sign;
        }

        void Start()
        {

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

        /// <summary>
        /// Using FixedUpdate for tracking the ball because it is a rigidbody
        /// </summary>
        void FixedUpdate()
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

            if (desktopModeController.localPlayerPlatform == DevicePlatform.AndroidMobile)
            {
                // Workaround for InputLookHorizontal/InputlookVertical doing nothing in the mobile alpha
                currentVerticalDelta = -currentMoveVertical;
                currentHorizontalDelta = currentMoveHorizontal;
            }
            else if (!currentMoveVertical.IsNearZero())
            {
                distance = Mathf.Clamp(distance - (currentMoveVertical * .4f), minDistance, maxDistance);
            }
            else
            {
                // Scroll wheel zoom
                float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
                distance = Mathf.Clamp(distance - scrollWheel, minDistance, maxDistance);
            }

            // Scale zoom level depending on targets speed
            float targetScaledDistance = distance * Mathf.Clamp(target.velocity.magnitude / 3, 1, distance < 1 ? 5 : 2);
            lastKnownRealDistance = Mathf.Lerp(lastKnownRealDistance, targetScaledDistance, cameraZoomSpeed * Time.deltaTime);

            float horizontalDelta = currentKeyboardHorizontalDelta != 0 ? currentKeyboardHorizontalDelta : currentHorizontalDelta;
            float verticalDelta = currentKeyboardVerticalDelta != 0 ? currentKeyboardVerticalDelta : currentVerticalDelta;

            if (invertCameraX)
                horizontalDelta *= -1f;
            if (invertCameraY)
                verticalDelta *= -1f;

            // Unity editor reports mouse movements higher so lower the speed to make it usable in editor
#if UNITY_EDITOR
            float localSpeedModifier = 0.5f;
#else
            float localSpeedModifier = 0.5f;
#endif

            // Player can always rotate camera horizontally
            currentCameraX += (cameraXSpeed * localSpeedModifier) * horizontalDelta;
            if (!LockCamera)
                currentCameraY -= (cameraYSpeed * localSpeedModifier) * verticalDelta;

            currentCameraY = Mathf.Clamp(currentCameraY, cameraMinY, cameraMaxY);
        }

        public override void PostLateUpdate()
        {
            transform.position = target.transform.position - Quaternion.Euler(currentCameraY, currentCameraX, 0f) * (lastKnownRealDistance * Vector3.forward);
            transform.LookAt(target.transform.position);
        }
    }
}