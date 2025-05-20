using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(5)]
    public class DesktopModeCameraController : UdonSharpBehaviour
    {
        #region References

        [Header("References")]
        public OpenPutt openPutt;
        public DesktopModeController desktopModeController;
        public Camera thisCam;
        public Rigidbody target; // The target we are following

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

        [Header("Collision Settings")]
        [Tooltip("The layer mask for objects the camera should collide with.")]
        public LayerMask collisionLayerMask;
        [Tooltip("The radius of the sphere used for collision detection (roughly camera size).")]
        public float cameraSphereRadius = 0.3f;
        [Tooltip("How far the camera should stay from obstacles.")]
        public float collisionOffset = 0.1f;
        [Tooltip("How fast the camera snaps to the collision point.")]
        public float collisionSnapSpeed = 10f;

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
        private float _currentCameraX;
        private float _currentCameraY = 10f;

        private float _inputLookHorizontalDelta;
        private float _inputLookVerticalDelta;
        private float _inputMoveHorizontal;
        private float _inputMoveVertical;

        private float _lastKnownRealDistance;
        private float _currentCollisionDistance; // Stores the distance from the target based on collisions

        private Vector3 _worldUp = Vector3.up;
        private Vector3 _worldUpSmoothed = Vector3.up;
        private GolfBallController _golfBall;

        #endregion

        private void OnEnable()
        {
            if (!Utilities.IsValid(target) || !Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager))
                return;

            _golfBall = openPutt.LocalPlayerManager.golfBall;
            if (!Utilities.IsValid(_golfBall))
                return;

            var localPlayerPos = Networking.LocalPlayer.GetPosition();
            if (Vector3.Distance(localPlayerPos, target.transform.position) < 2f)
                _currentCameraX = GetCameraAngle(localPlayerPos, target.position);

            if (_golfBall.gravityMagnitude > .01f)
                _currentCameraY = Mathf.Clamp(_currentCameraY, cameraMinY, cameraMaxY);
            else
                _currentCameraY = Mathf.Clamp(_currentCameraY, -89.9f, cameraMaxY);

            _lastKnownRealDistance = distance;
            _currentCollisionDistance = distance;

            transform.position = target.transform.position - Quaternion.Euler(_currentCameraY, _currentCameraX, 0f) * (distance * Vector3.forward);
            transform.LookAt(target.transform.position);
        }

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
                _inputMoveHorizontal = value;
            }
        }

        public override void InputMoveVertical(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
            {
                _inputMoveVertical = value;
            }
        }

        public override void InputLookHorizontal(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                _inputLookHorizontalDelta = args.floatValue;
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                _inputLookVerticalDelta = args.floatValue;
        }

        public override void OnInputMethodChanged(VRCInputMethod inputMethod)
        {
            currentInputMethod = inputMethod;
        }

        private void LateUpdate()
        {
            if (!target) return;
            if (!Utilities.IsValid(_golfBall) && Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.LocalPlayerManager))
                _golfBall = openPutt.LocalPlayerManager.golfBall;
            if (!Utilities.IsValid(_golfBall)) return;

            // Handle Keyboard Input for camera movement
            var keyboardVerticalDelta = 0f;
            if (Input.GetKey(KeyCode.UpArrow))
                keyboardVerticalDelta += .8f;
            if (Input.GetKey(KeyCode.DownArrow))
                keyboardVerticalDelta -= .8f;

            var keyboardHorizontalDelta = 0f;
            if (Input.GetKey(KeyCode.RightArrow))
                keyboardHorizontalDelta += .8f;
            if (Input.GetKey(KeyCode.LeftArrow))
                keyboardHorizontalDelta -= .8f;

            // Force currentInputMethod back to mouse if mouse movement detected
            if (currentInputMethod != VRCInputMethod.Mouse && (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.0001f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.0001f))
                currentInputMethod = VRCInputMethod.Mouse;

            // Handle Camera Zoom (Move Vertical or Scroll Wheel)
            if (!_inputMoveVertical.IsNearZero())
            {
                distance = Mathf.Clamp(distance - (_inputMoveVertical * .4f), minDistance, maxDistance);
            }
            else
            {
                var scrollWheel = Input.GetAxis("Mouse ScrollWheel");
                distance = Mathf.Clamp(distance - scrollWheel, minDistance, maxDistance);
            }

            // Scale zoom level depending on target's speed
            var targetScaledDistance = distance * Mathf.Clamp(target.velocity.magnitude / 3f, 1f, distance < 1f ? 5f : 2f);
            _lastKnownRealDistance = Mathf.Lerp(_lastKnownRealDistance, targetScaledDistance, cameraZoomSpeed * Time.deltaTime);

            var horizontalDelta = keyboardHorizontalDelta != 0f ? keyboardHorizontalDelta : _inputLookHorizontalDelta;
            var verticalDelta = keyboardVerticalDelta != 0f ? keyboardVerticalDelta : _inputLookVerticalDelta;

            const float localSpeedModifier = 0.5f;
            _currentCameraX += (cameraXSpeed * localSpeedModifier) * horizontalDelta;
            
            if (!LockCamera)
            {
                _currentCameraY += (cameraYSpeed * localSpeedModifier) * verticalDelta; 
            }

            float clampedRawCameraY; 
            if (_golfBall.gravityMagnitude > .01f)
            {
                _worldUp = -_golfBall.gravityDirection;
                clampedRawCameraY = -Mathf.Clamp(-_currentCameraY, cameraMinY, cameraMaxY);
            }
            else
            {
                _worldUp = Vector3.up;
                clampedRawCameraY = -Mathf.Clamp(-_currentCameraY, -89.9f, cameraMaxY);
            }
            _currentCameraY = clampedRawCameraY;

            // Complicated stuff to account for gravity
            _worldUpSmoothed = Vector3.Lerp(_worldUpSmoothed, _worldUp, Time.deltaTime * 5f);
            var orbitBaseDirection = Vector3.forward;
            if (Vector3.Dot(orbitBaseDirection, _worldUpSmoothed).IsNear(1f) || Vector3.Dot(orbitBaseDirection, _worldUpSmoothed).IsNear(-1f))
                orbitBaseDirection = Vector3.right;
            orbitBaseDirection = Vector3.ProjectOnPlane(orbitBaseDirection, _worldUpSmoothed).normalized;
            var yawRotation = Quaternion.AngleAxis(_currentCameraX, _worldUpSmoothed);
            var rotatedBaseDirection = yawRotation * orbitBaseDirection;
            var horizontalRight = Vector3.Cross(_worldUpSmoothed, rotatedBaseDirection).normalized;
            var pitchRotation = Quaternion.AngleAxis(_currentCameraY, horizontalRight);
            var finalOrbitDirection = pitchRotation * rotatedBaseDirection;

            // Camera collision stuff
            var targetLerpDistance = _lastKnownRealDistance;
            if (Physics.SphereCast(target.transform.position, cameraSphereRadius, finalOrbitDirection, out var hit, _lastKnownRealDistance, collisionLayerMask))
                targetLerpDistance = Mathf.Max(minDistance, hit.distance - cameraSphereRadius - collisionOffset);
            _currentCollisionDistance = Mathf.Lerp(_currentCollisionDistance, targetLerpDistance, collisionSnapSpeed * Time.deltaTime);
            _currentCollisionDistance = Mathf.Min(_currentCollisionDistance, _lastKnownRealDistance);

            var finalCamPos = target.transform.position + finalOrbitDirection * _currentCollisionDistance;
            var desiredCamRot = Quaternion.LookRotation(target.transform.position - finalCamPos, _worldUpSmoothed);
            transform.position = finalCamPos;
            transform.rotation = desiredCamRot;
        }
    }
}