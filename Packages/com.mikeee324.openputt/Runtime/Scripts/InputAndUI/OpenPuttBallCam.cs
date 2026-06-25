
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(999)]
    public class OpenPuttBallCam : OpenPuttEventListener
    {
        [Header("References")]
        [Tooltip("Reference to the OpenPutt instance")]
        public OpenPutt openPutt;
        [Tooltip("Reference to the ball camera")]
        public Camera ballCam;
        [Tooltip("Reference to the aim line renderer")]
        public LineRenderer aimLineRenderer;
        [Tooltip("Reference to the input handler")]
        public OpenPuttInputHandler inputHandler;
        [Tooltip("Reference to the UI controller")]
        public OpenPuttUIController uiController;

        [Header("Camera Settings")]
        [Tooltip("Initial distance from the ball")]
        public float initialDistance = 1f;
        [Tooltip("Minimum distance")]
        public float minDistance = 1f;
        [Tooltip("Maximum distance")]
        public float maxDistance = 20f;
        [Header("Ball Camera Sensitivity")]
        [Tooltip("Sensitivity multiplier for ball camera movement (desktop)"), Range(0.5f, 15f)]
        public float moveSensitivity = 5f;
        [Tooltip("Sensitivity multiplier for ball camera zoom (desktop)"), Range(0.1f, 15f)]
        public float zoomSensitivity = 1f;
        [Tooltip("Sensitivity multiplier for ball camera movement (mobile)"), Range(10f, 100f)]
        public float mobileMoveSensitivity = 25f;
        [Tooltip("Sensitivity multiplier for ball camera zoom (mobile)"), Range(0.01f, 1f)]
        public float mobileZoomSensitivity = 0.5f;
        [Header("Camera Settings")]
        [Tooltip("Smoothing speed for camera movement"), Range(0.1f, 10f)]
        public float smoothingSpeed = 5f;
        [Tooltip("Sphere cast radius for collision detection")]
        public float sphereCastRadius = 0.1f;
        [Tooltip("Layer mask for sphere cast (what to collide with)")]
        public LayerMask collisionMask = -1;

        public LayerMask cameraCullMask = -1;
        public LayerMask cameraCullPlayersMask = -1;

        [Header("Aim Line Settings")]
        [Tooltip("Minimum length of the aim line")]
        public float lineMinLength = 0.1f;
        [Tooltip("Maximum length of the aim line")]
        public float lineMaxLength = 1f;
        [Tooltip("Angle to aim the line up when no gravity (degrees)")]
        public float aimUpAngle = 10f;
        [Tooltip("Smoothing speed for UI elements")]
        public float uiSmoothSpeed = 5f;
        [Tooltip("Gradient for power bar color")]
        public Gradient powerBarGradient;
        [Tooltip("Offset for the start of the aim line")]
        public float aimLineStartOffset = 0.1f;

        private PlayerManager localPlayerManager;
        private GolfBallController ball;
        private float currentDistance;

        private float cameraZoomSpeed = 5f;
        private float _lastKnownRealDistance;
        private float cameraMinY = -80f;
        private float cameraMaxY = 80f;
        private Vector3 _worldUp;
        private Vector3 _worldUpSmoothed;
        private float _currentCameraX = 0f;
        private float _currentCameraY = 0f;
        private float collisionOffset = 0f;
        private float collisionSnapSpeed = 5f;
        private float _currentCollisionDistance;
        private float _inputLookHorizontalDelta;
        private float _inputLookVerticalDelta;
        private float _inputMoveVertical;
        private float _inputMoveHorizontal;
        private Vector3[] directionLinePoints = new Vector3[3];

        public bool BallCamActive
        {
            get => ballCam.enabled;
            set
            {
                if (value)
                    EnableCamera();
                else
                    DisableCamera();
            }
        }

        void Start()
        {
            currentDistance = initialDistance;
            _lastKnownRealDistance = initialDistance;
            _currentCollisionDistance = initialDistance;
            _worldUpSmoothed = Vector3.up;

            if (Utilities.IsValid(openPutt))
                openPutt._RegisterEventListener(this);

            ballCam.enabled = false;
        }

        void LateUpdate()
        {
            if (!Utilities.IsValid(ball) || !Utilities.IsValid(localPlayerManager))
                return;

            if (localPlayerManager.ownerIsInVR)
                return;

            Rigidbody target = ball.GetComponent<Rigidbody>();
            // Handle Camera Zoom (Move Vertical)
            if (!_inputMoveVertical.IsNearZero())
                currentDistance = Mathf.Clamp(currentDistance - (_inputMoveVertical * .4f), minDistance, maxDistance);

            // Scale zoom level depending on target's speed
            var targetScaledDistance = currentDistance * Mathf.Clamp(target.velocity.magnitude / 3f, 1f, currentDistance < 1f ? 5f : 2f);
            _lastKnownRealDistance = Mathf.Lerp(_lastKnownRealDistance, targetScaledDistance, cameraZoomSpeed * Time.deltaTime);

#if UNITY_EDITOR
            if (!_inputLookHorizontalDelta.IsNearZero())
                _inputLookHorizontalDelta = _inputLookHorizontalDelta * .5f;
            if (!_inputLookVerticalDelta.IsNearZero())
                _inputLookVerticalDelta = _inputLookVerticalDelta * .5f;
#endif

            bool isShooting = Utilities.IsValid(inputHandler) && inputHandler.IsShooting;

#if UNITY_ANDROID || UNITY_IOS
            // InputLook on mobile keeps going to 0 while moving and make the camera move really badly
            if (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.0001f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.0001f)
            {
                // Only move camera around if user isn't trying to use movement joystick on screen
                if (Mathf.Abs(_inputMoveHorizontal) < 0.0001f && Mathf.Abs(_inputMoveVertical) < 0.0001f)
                {
                    _inputLookHorizontalDelta = Input.GetAxis("Mouse X") * GetMoveSensitivity() * Time.deltaTime * 50f;
                    _inputLookVerticalDelta = Input.GetAxis("Mouse Y") * GetMoveSensitivity() * Time.deltaTime * 50f;
                }

                if (!isShooting)
                    _currentCameraX += _inputLookHorizontalDelta;
            }
#else
            _currentCameraX += _inputLookHorizontalDelta;
#endif

            if (!isShooting)
                _currentCameraY += _inputLookVerticalDelta;

            float clampedRawCameraY;
            if (ball.gravityMagnitude > 0)
            {
                _worldUp = -ball.gravityDirection;
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
            var orbitBaseDirection = Vector3.Cross(_worldUpSmoothed, Vector3.forward);
            orbitBaseDirection = orbitBaseDirection.normalized;
            var yawRotation = Quaternion.AngleAxis(_currentCameraX, _worldUpSmoothed);
            var rotatedBaseDirection = yawRotation * orbitBaseDirection;
            var horizontalRight = Vector3.Cross(_worldUpSmoothed, rotatedBaseDirection).normalized;
            var pitchRotation = Quaternion.AngleAxis(_currentCameraY, horizontalRight);
            var finalOrbitDirection = pitchRotation * rotatedBaseDirection;

            // Camera collision stuff
            var targetLerpDistance = _lastKnownRealDistance;
            if (Physics.SphereCast(ball.transform.position, sphereCastRadius, finalOrbitDirection, out var hit, _lastKnownRealDistance, collisionMask))
                targetLerpDistance = Mathf.Max(minDistance, hit.distance - sphereCastRadius - collisionOffset);
            _currentCollisionDistance = Mathf.Lerp(_currentCollisionDistance, targetLerpDistance, collisionSnapSpeed * Time.deltaTime);
            _currentCollisionDistance = Mathf.Min(_currentCollisionDistance, _lastKnownRealDistance);
            var finalCamPos = ball.transform.position + finalOrbitDirection * _currentCollisionDistance;
            var desiredCamRot = Quaternion.LookRotation(ball.transform.position - finalCamPos, _worldUpSmoothed);
            ballCam.transform.position = finalCamPos;
            ballCam.transform.rotation = desiredCamRot;

            // Update aim line renderer
            UpdateAimLine();
        }

        /// <summary>
        /// Called when OpenPutt has assigned and finished setting up a PlayerManager for a player
        /// </summary>
        /// <param name="player">The player that was initialized</param>
        /// <param name="playerManager">The PlayerManager that was assigned</param>
        public override void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {
            if (!player.isLocal)
                return;

            localPlayerManager = playerManager;
            ball = localPlayerManager.golfBall;
            UpdateLocalBallLabelLookTarget();
        }

        private void UpdateLocalBallLabelLookTarget()
        {
            if (!Utilities.IsValid(localPlayerManager) || !Utilities.IsValid(localPlayerManager.playerLabel) || !Utilities.IsValid(ballCam))
                return;

            if (BallCamActive)
                localPlayerManager.playerLabel.lookAtTarget = ballCam.gameObject;
            else if (localPlayerManager.playerLabel.lookAtTarget == ballCam.gameObject)
                localPlayerManager.playerLabel.lookAtTarget = null;
        }

        /// <summary>
        /// Called when ball camera movement input is detected
        /// </summary>
        /// <param name="horizontal">Horizontal movement input</param>
        /// <param name="vertical">Vertical movement input</param>
        public void OnBallCameraMove(float horizontal, float vertical)
        {
            _inputLookHorizontalDelta = horizontal * GetMoveSensitivity();
            _inputLookVerticalDelta = vertical * GetMoveSensitivity();
        }

        /// <summary>
        /// Called when ball camera zoom input is detected
        /// </summary>
        /// <param name="zoomDelta">Zoom delta (positive for zoom in, negative for zoom out)</param>
        /// <param name="horizontal">Horizontal movement input</param>
        public void OnBallCameraZoom(float zoomDelta, float horizontal)
        {
            _inputMoveVertical = zoomDelta * GetZoomSensitivity();
            _inputMoveHorizontal = horizontal * GetZoomSensitivity();
        }

        private float GetMoveSensitivity()
        {
#if UNITY_ANDROID || UNITY_IOS
            return mobileMoveSensitivity;
#else
            return moveSensitivity;
#endif
        }

        private float GetZoomSensitivity()
        {
#if UNITY_ANDROID || UNITY_IOS
            return mobileZoomSensitivity;
#else
            return zoomSensitivity;
#endif
        }

        /// <summary>
        /// Called to toggle the ball camera on or off
        /// </summary>
        public void ToggleCamera()
        {
            if (BallCamActive)
                DisableCamera();
            else
                EnableCamera();
        }

        /// <summary>
        /// Enables the ball camera object
        /// </summary>
        public void EnableCamera()
        {
            ClearCameraInputState();

            ballCam.enabled = true;
            ballCam.cullingMask = cameraCullMask;
            UpdateLocalBallLabelLookTarget();

            // Stop the ball shoulder pickup from listening for the pickup key while aiming with the cam
            SetBallPickupKeyIgnored(true);

            if (Utilities.IsValid(uiController))
                uiController.OnBallCameraToggled();

            // This can be overridden on the playermanager
            if (Utilities.IsValid(localPlayerManager))
                localPlayerManager.PlayerIsCurrentlyFrozen = true;
        }

        /// <summary>
        /// Disables the ball camera object
        /// </summary>
        public void DisableCamera()
        {
            ClearCameraInputState();

            ballCam.enabled = false;
            ballCam.cullingMask = cameraCullMask;
            UpdateLocalBallLabelLookTarget();

            // Re-enable the ball shoulder pickup key now the cam is off
            SetBallPickupKeyIgnored(false);

            if (Utilities.IsValid(uiController))
                uiController.OnBallCameraToggled();

            // This can be overridden on the playermanager
            if (Utilities.IsValid(localPlayerManager))
                localPlayerManager.PlayerIsCurrentlyFrozen = false;
        }

        /// <summary>
        /// Toggles whether the local player's ball shoulder pickup listens for the desktop/controller pickup key
        /// </summary>
        /// <param name="ignored">True to stop listening for the pickup key</param>
        private void SetBallPickupKeyIgnored(bool ignored)
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(localPlayerManager))
                return;

            // Ball lives on the opposite shoulder to the club depending on handedness
            var ballPickup = localPlayerManager.IsInLeftHandedMode ? openPutt.rightShoulderPickup : openPutt.leftShoulderPickup;
            if (Utilities.IsValid(ballPickup))
                ballPickup.ignorePickupKey = ignored;
        }

        private void ClearCameraInputState()
        {
            _inputLookHorizontalDelta = 0f;
            _inputLookVerticalDelta = 0f;
            _inputMoveVertical = 0f;
            _inputMoveHorizontal = 0f;
        }

        public override void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar, int totalHits)
        {
            if (player.isLocal && BallCamActive)
            {
                inputHandler.SendCustomEventDelayedSeconds(nameof(OpenPuttInputHandler.ToggleCamera), .1f);
            }
        }

        /// <summary>
        /// Updates the aim line renderer to show the direction the ball will go
        /// </summary>
        private void UpdateAimLine()
        {
            if (!Utilities.IsValid(aimLineRenderer) || !Utilities.IsValid(ball))
                return;

            // Calculate the direction the ball will go (same as in ExecuteShot)
            Vector3 direction = ballCam.transform.forward;

            // Project on plane perpendicular to gravity if gravity exists
            if (ball.gravityMagnitude > 0)
            {
                direction = Vector3.ProjectOnPlane(direction, -ball.gravityDirection).normalized;
            }
            else
            {
                // Aim up a bit when no gravity to make the line visible
                direction = Quaternion.AngleAxis(aimUpAngle, ballCam.transform.right) * direction;
            }

            var currentUISpeed = Utilities.IsValid(inputHandler) ? inputHandler.CurrentShotPowerNormalized : 0f;

            // Scale line length based on current shot power
            float currentLineLength = Mathf.Lerp(lineMinLength, lineMaxLength, currentUISpeed);

            // Set up the line renderer positions
            Vector3 startPos = ball.transform.position;
            Vector3 endPos = startPos + direction * currentLineLength; // scaled length based on hit speed

            directionLinePoints[0] = ball.transform.position + direction * aimLineStartOffset;
            directionLinePoints[1] = Vector3.Lerp(directionLinePoints[0], endPos, .97f);
            directionLinePoints[2] = endPos;

            aimLineRenderer.SetPositions(directionLinePoints);

            var newColor = powerBarGradient.Evaluate(currentUISpeed);
            aimLineRenderer.sharedMaterial.color = aimLineRenderer.startColor = aimLineRenderer.endColor = newColor;

            // Enable the line renderer
            if (!aimLineRenderer.enabled)
                aimLineRenderer.enabled = true;
        }
    }
}
