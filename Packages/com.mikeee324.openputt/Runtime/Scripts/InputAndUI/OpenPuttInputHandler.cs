
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(10)]
    public class OpenPuttInputHandler : UdonSharpBehaviour
    {
        #region Public Variables
        [OpenPuttDescription("Reads player input (keyboard/mouse, VR controllers, gamepad) and turns it into golf actions - charging and taking a shot, opening the ball camera, and cycling clubs.")]
        [Tooltip("Reference to the OpenPutt instance")]
        public OpenPutt openPutt;

        [Tooltip("Reference to the UI Controller that handles the input events")]
        public OpenPuttUIController uiController;

        [Tooltip("Reference to the controller detector for gamepad input")]
        public ControllerDetector controllerDetector;

        [Tooltip("Reference to the ball camera controller")]
        public OpenPuttBallCam ballCam;

        /// <summary>
        /// Whether the ball camera is currently active
        /// </summary>
        public bool ballCameraActive => Utilities.IsValid(ballCam) && ballCam.BallCamActive;

        /// <summary>
        /// Current normalized shot power (0-1)
        /// </summary>
        public float CurrentShotPowerNormalized => currentShotPowerNormalized;

        [OpenPuttFoldoutGroup("Power Settings")]
        [Tooltip("Speed at which power cycles up/down")]
        public float powerCycleSpeed = 50f;

        [OpenPuttFoldoutGroup("Shot Charge Sensitivity")]
        [Tooltip("Sensitivity multiplier for mouse Y movement during shot charging (desktop)"), Range(1f, 15f)]
        public float mouseSensitivity = 5f;
        [OpenPuttFoldoutGroup("Shot Charge Sensitivity")]
        [Tooltip("Sensitivity multiplier for mouse Y movement during shot charging (mobile/UI)"), Range(1f, 15f)]
        public float mouseMobileSensitivity = 3f;
        #endregion

        #region Private Variables
        // Input state tracking
        private bool wasAButtonPressed = false;
        private float currentPower = 0f;
        private float currentShotPowerNormalized = 0f;
        private float powerDirection = 1f;
        private ShootInputType currentShootInput = ShootInputType.None;
        private float accumulatedMouseY = 0f;
        private float lookHorizontalAxis = 0f;
        private float lookVerticalAxis = 0f;
        // For detecting quick joystick flicks to cycle clubs in VR
        private float lastLookVerticalAxis = 0f;
        private const float lookFlickThreshold = 0.75f;
        private const float lookFlickResetThreshold = 0.1f;
        private bool lookFlickReady = true;
        private float moveHorizontalAxis = 0f;
        private float moveVerticalAxis = 0f;
        private float maxPower = 100f;
        private Collider[] _menuColliders = new Collider[3];
        private bool isMenuOpen = false;
        public bool IsMenuOpen => isMenuOpen;

        [Tooltip("The duration of the vibration in seconds.")]
        private float vibrationDuration = 0.2f;

        [Tooltip("The strength of the vibration (0.0 to 1.0).")]
        [Range(0f, 1f)]
        private float vibrationStrength = 1f;

        [Tooltip("The frequency of the vibration (roughly how many pulses per second).")]
        private float vibrationFrequency = 30f;

        public bool IsShooting => currentShootInput != ShootInputType.None;
        #endregion

        #region Private Methods
        private bool CanShoot()
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfBall))
                return false;

            var ball = openPutt.LocalPlayerManager.golfBall;
            var playerIsPlayingACourse = Utilities.IsValid(openPutt.LocalPlayerManager.CurrentCourse);
            if (playerIsPlayingACourse && !ball.allowBallHitWhileMoving && ball.BallIsMoving)
            {
                return false;
            }
            return true;
        }
        #endregion

        void Start()
        {
            SendCustomEventDelayedSeconds(nameof(CheckIfMenuIsOpen), 1f);
        }

        void Update()
        {
            if (isMenuOpen) return;

            // Update shot power if charging
            if (currentShootInput != ShootInputType.None)
            {
                if (currentShootInput == ShootInputType.Keyboard || currentShootInput == ShootInputType.Controller)
                {
                    currentPower += powerCycleSpeed * powerDirection * Time.deltaTime;
                    if (currentPower >= maxPower)
                    {
                        currentPower = maxPower;
                        powerDirection = -1f;
                    }
                    else if (currentPower <= 0f)
                    {
                        currentPower = 0f;
                        powerDirection = 1f;
                    }
                    currentShotPowerNormalized = Mathf.Clamp01(currentPower / maxPower);
                }
                else if (currentShootInput == ShootInputType.Mouse)
                {
#if !(UNITY_ANDROID || UNITY_IOS)
                    float mouseY = Input.GetAxis("Mouse Y");
                    accumulatedMouseY += mouseY * Time.deltaTime * mouseSensitivity * 100f;
                    accumulatedMouseY = Mathf.Min(accumulatedMouseY, 0f);
                    float power = Mathf.Clamp(-accumulatedMouseY / 10f, 0f, maxPower);
                    currentShotPowerNormalized = Mathf.Clamp01(power / maxPower);
#endif
                }
                else if (currentShootInput == ShootInputType.UI)
                {
                    float mouseY = Input.GetAxis("Mouse Y");
                    accumulatedMouseY += mouseY * Time.deltaTime * mouseMobileSensitivity * 100f;
                    accumulatedMouseY = Mathf.Min(accumulatedMouseY, 0f);
                    float power = Mathf.Clamp(-accumulatedMouseY / 10f, 0f, maxPower);
                    currentShotPowerNormalized = Mathf.Clamp01(power / maxPower);
                }
            }
            else
            {
                // Reset power when not charging
                currentShotPowerNormalized = 0f;
            }

            switch (controllerDetector.currentInputMethod)
            {
                case VRCInputMethod.Keyboard:
                case VRCInputMethod.Mouse:
                    HandleKeyboardInput();
                    break;
                case VRCInputMethod.Controller:
                    HandleGamepadInput();
                    break;
            }
        }

        private void HandleKeyboardInput()
        {
            // Fetch Ball - B key - This is already handled by shoulder objects

            // Toggle Camera - E key
            if (Input.GetKeyDown(KeyCode.E))
            {
                ToggleCamera();
            }

            // Cycle Club Next - Period .
            if (Input.GetKeyDown(KeyCode.Period))
            {
                CycleClubNext();
            }

            // Cycle Club Previous - Comma ,
            if (Input.GetKeyDown(KeyCode.Comma))
            {
                CycleClubPrevious();
            }

            // Ball Camera Movement and Zoom (when active)
            if (ballCameraActive)
            {
                // Shoot - Space key (hold)
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (currentShootInput == ShootInputType.None && CanShoot())
                    {
                        currentShootInput = ShootInputType.Keyboard;
                        currentPower = 0f;
                        powerDirection = 1f;
                        uiController.OnShootStart();
                    }
                }
                else if (Input.GetKeyUp(KeyCode.Space))
                {
                    if (currentShootInput == ShootInputType.Keyboard)
                    {
                        ExecuteShot(Mathf.Clamp01(currentPower / maxPower));
                        uiController.OnShootEnd();
                        currentShootInput = ShootInputType.None;
                    }
                }

                // Shoot - Left Mouse Button (hold)
#if !(UNITY_ANDROID || UNITY_IOS)
                if (Input.GetMouseButtonDown(0))
                {
                    if (currentShootInput == ShootInputType.None && CanShoot())
                    {
                        currentShootInput = ShootInputType.Mouse;
                        accumulatedMouseY = 0f;
                        uiController.OnShootStart();
                    }
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (currentShootInput == ShootInputType.Mouse)
                    {
                        float finalPower = Mathf.Clamp(-accumulatedMouseY / 10f, 0f, maxPower);
                        Debug.Log($"Final Shot Power: {finalPower} (Normalized: {finalPower / maxPower})");
                        ExecuteShot(finalPower / maxPower);
                        uiController.OnShootEnd();
                        currentShootInput = ShootInputType.None;
                    }
                }
#endif

                // Zoom - +/- keys
                if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
                {
                    if (Utilities.IsValid(ballCam))
                        ballCam.OnBallCameraZoom(.08f, 0f);
                }
                else if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                {
                    if (Utilities.IsValid(ballCam))
                        ballCam.OnBallCameraZoom(-.08f, 0f);
                }
                else
                {
                    if (Utilities.IsValid(ballCam))
                        ballCam.OnBallCameraZoom(0f, 0f);
                }

                // Movement on horizontal and vertical look
                if (Utilities.IsValid(ballCam))
                {
                    ballCam.OnBallCameraMove(lookHorizontalAxis, lookVerticalAxis);
                }
            }
        }

        private void HandleGamepadInput()
        {
            if (!Utilities.IsValid(controllerDetector) || controllerDetector.LastUsedJoystick == Controller.None)
                return;

            Controller controller = controllerDetector.LastUsedJoystick;
            int joystickID = controllerDetector.LastKnownJoystickID;

            // Fetch Ball - B button
            if (controller.B(joystickID))
            {
                OnUIFetchBall();
            }

            // Toggle Camera - RB button
            if (controller.RightBumper(joystickID))
            {
                ToggleCamera();
            }
            // Cycle Club Next - Y button
            if (controller.Y(joystickID))
            {
                CycleClubNext();
            }

            // Ball Camera Movement and Zoom (when active)
            if (ballCameraActive)
            {
                // Shoot - A button (hold)
                bool aButtonPressed = controller.A(joystickID);
                if (aButtonPressed && !wasAButtonPressed)
                {
                    if (currentShootInput == ShootInputType.None && CanShoot())
                    {
                        currentShootInput = ShootInputType.Controller;
                        currentPower = 0f;
                        powerDirection = 1f;
                        uiController.OnShootStart();
                    }
                }
                else if (!aButtonPressed && wasAButtonPressed)
                {
                    if (currentShootInput == ShootInputType.Controller)
                    {
                        ExecuteShot(Mathf.Clamp01(currentPower / maxPower));
                        uiController.OnShootEnd();
                        currentShootInput = ShootInputType.None;
                    }
                }
                wasAButtonPressed = aButtonPressed;


                // Movement on horizontal and vertical look
                if (Utilities.IsValid(ballCam))
                {
                    ballCam.OnBallCameraMove(lookHorizontalAxis, lookVerticalAxis);
                }

                // Zoom on vertical move
                if (Utilities.IsValid(ballCam))
                {
                    ballCam.OnBallCameraZoom(-moveVerticalAxis, moveHorizontalAxis);
                }
            }
        }

        /// <summary>
        /// Called by UI buttons for fetch ball
        /// </summary>
        public void OnUIFetchBall()
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfBall))
                return;

            // Resolve ball shoulder from handedness mode so toggling left-handed mode stays consistent.
            var shoulder = openPutt.LocalPlayerManager.IsInLeftHandedMode ? openPutt.rightShoulderPickup : openPutt.leftShoulderPickup;

            if (!Utilities.IsValid(shoulder))
                return;

            // Toggle the held state
            shoulder.forcePickedUp = !shoulder.forcePickedUp;
        }

        /// <summary>
        /// Called by UI buttons for toggle camera
        /// </summary>
        public void OnUIToggleCamera()
        {
            ToggleCamera();
        }

        /// <summary>
        /// Called by UI buttons for shoot start
        /// </summary>
        public void OnUIShootStart()
        {
            if (currentShootInput == ShootInputType.None && CanShoot())
            {
                currentShootInput = ShootInputType.UI;
                currentPower = 0f;
                powerDirection = 1f;
                accumulatedMouseY = 0f;
            }
        }

        /// <summary>
        /// Called by UI buttons for shoot end
        /// </summary>
        public void OnUIShootEnd()
        {
            if (currentShootInput == ShootInputType.UI)
            {
                float finalPower = Mathf.Clamp(-accumulatedMouseY / 10f, 0f, maxPower);
                ExecuteShot(finalPower / maxPower);
                currentShootInput = ShootInputType.None;
            }
        }

        /// <summary>
        /// Called by UI buttons for cycle club next
        /// </summary>
        public void OnUICycleClubNext()
        {
            CycleClubNext();
        }

        /// <summary>
        /// Called by UI buttons for cycle club previous
        /// </summary>
        public void OnUICycleClubPrevious()
        {
            CycleClubPrevious();
        }

        /// <summary>
        /// Disables the ball camera
        /// </summary>
        public void DisableBallCam()
        {
            if (Utilities.IsValid(ballCam))
                ballCam.DisableCamera();
        }

        /// <summary>
        /// Toggles the ball camera
        /// </summary>
        public void ToggleCamera()
        {
            // Only allow toggling if the ball has been picked up at least once
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfBall))
                return;

            // If they haven't used the ball yet, can't enable the camera yet
            if (!openPutt.hasUsedGolfBall)
                return;

            // Toggle ball camera
            if (Utilities.IsValid(ballCam))
            {
                if (ballCam.BallCamActive)
                    ballCam.DisableCamera();
                else
                    ballCam.EnableCamera();
            }

            currentShootInput = ShootInputType.None;
        }

        /// <summary>
        /// Cycles to the next club
        /// </summary>
        public void CycleClubNext()
        {
            // Cycle to next club logic
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfClub))
                return;

            var golfClub = openPutt.LocalPlayerManager.golfClub;

            if (golfClub.ClubIsArmed)
                return;

            if (Networking.LocalPlayer.IsUserInVR() && golfClub.CurrentHand == VRC_Pickup.PickupHand.None)
                return;

            if (!Networking.LocalPlayer.IsUserInVR() && !ballCameraActive)
                return;

            // Nothing to cycle to (and nothing to buzz about) if only one club is allowed
            if (!IsMoreThanOneClubAllowed())
                return;

            var nextType = NextAllowedClub(golfClub.ClubType, 1);
            if (nextType == golfClub.ClubType)
                return;
            golfClub.ClubType = nextType;

            // Vibrate on club change if in VR
            if (Networking.LocalPlayer.IsUserInVR())
            {
                var hapticHand = golfClub.CurrentHand;
                if (hapticHand != VRC_Pickup.PickupHand.None)
                    Networking.LocalPlayer.PlayHapticEventInHand(hapticHand, vibrationStrength, vibrationFrequency, vibrationDuration);
            }
        }

        /// <summary>
        /// Cycles to the previous club
        /// </summary>
        public void CycleClubPrevious()
        {
            // Cycle to previous club logic
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfClub))
                return;

            var golfClub = openPutt.LocalPlayerManager.golfClub;

            if (golfClub.ClubIsArmed)
                return;

            if (Networking.LocalPlayer.IsUserInVR() && golfClub.CurrentHand == VRC_Pickup.PickupHand.None)
                return;

            if (!Networking.LocalPlayer.IsUserInVR() && !ballCameraActive)
                return;

            // Nothing to cycle to (and nothing to buzz about) if only one club is allowed
            if (!IsMoreThanOneClubAllowed())
                return;

            var prevType = NextAllowedClub(golfClub.ClubType, -1);
            if (prevType == golfClub.ClubType)
                return;
            golfClub.ClubType = prevType;

            // Vibrate on club change if in VR
            if (Networking.LocalPlayer.IsUserInVR())
            {
                var hapticHand = golfClub.CurrentHand;
                if (hapticHand != VRC_Pickup.PickupHand.None)
                    Networking.LocalPlayer.PlayHapticEventInHand(hapticHand, vibrationStrength, vibrationFrequency, vibrationDuration);
            }
        }

        /// <summary>
        /// Next/previous (direction +1/-1) club type allowed on the current course, skipping disallowed
        /// ones. Returns currentType if nothing else is allowed.
        /// </summary>
        private GolfClubType NextAllowedClub(GolfClubType currentType, int direction)
        {
            var currentCourse = Utilities.IsValid(openPutt.LocalPlayerManager) ? openPutt.LocalPlayerManager.CurrentCourse : null;

            var clubCount = (int)GolfClubType.Hybrid + 1;
            var candidate = (int)currentType;
            for (var i = 0; i < clubCount; i++)
            {
                candidate += direction;
                if (candidate > (int)GolfClubType.Hybrid)
                    candidate = 0;
                else if (candidate < 0)
                    candidate = (int)GolfClubType.Hybrid;

                var candidateType = (GolfClubType)candidate;
                var allowed = Utilities.IsValid(currentCourse)
                    ? currentCourse._IsClubAllowed(candidateType)
                    : (openPutt.allowAnyClubOffCourse || candidateType == GolfClubType.Putter);
                if (allowed)
                    return candidateType;
            }

            return currentType;
        }

        /// <summary>
        /// True if the player currently has more than one club to choose between. Used to avoid buzzing
        /// the controller when cycling can't actually do anything (e.g. putter-only off a course).
        /// </summary>
        private bool IsMoreThanOneClubAllowed()
        {
            var currentCourse = Utilities.IsValid(openPutt.LocalPlayerManager) ? openPutt.LocalPlayerManager.CurrentCourse : null;

            // Off a course: either every club (world setting) or just the putter
            if (!Utilities.IsValid(currentCourse))
                return openPutt.allowAnyClubOffCourse;

            // On a course: count allowed clubs, stop as soon as we know there's more than one
            var allowedCount = 0;
            var clubCount = (int)GolfClubType.Hybrid + 1;
            for (var i = 0; i < clubCount; i++)
            {
                if (currentCourse._IsClubAllowed((GolfClubType)i) && ++allowedCount > 1)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Executes a shot with the given power
        /// </summary>
        /// <param name="power">The power of the shot (0-1)</param>
        public void ExecuteShot(float power)
        {
            // Power of 0 would pass Vector3.zero to HandleBallHit, which falls back to
            // VR controller hand velocity and hits the ball anyway. Bail out instead.
            if (power <= 0f)
                return;

            // Execute shot with current power
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager))
                return;

            var playerManager = openPutt.LocalPlayerManager;
            if (!Utilities.IsValid(playerManager.golfBall) || !Utilities.IsValid(playerManager.golfClubHead))
                return;

            var collider = playerManager.golfClubHead;
            var ball = playerManager.golfBall;
            var club = playerManager.golfClub;

            if (!Utilities.IsValid(ballCam))
                return;

            // Calculate direction from ball camera
            Vector3 direction = ballCam.ballCam.transform.forward;

            // Project on plane perpendicular to gravity if gravity exists
            if (ball.gravityMagnitude > 0)
                direction = Vector3.ProjectOnPlane(direction, -ball.gravityDirection).normalized;

            // Scale hit speed by the current club's typical max speed
            float speed = power * club.ClubType.GetTypicalMaxSpeed();

            // Apply velocity to ball
            collider.HandleBallHit(direction * speed, Vector3.zero);
        }

        public override void InputLookHorizontal(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
                lookHorizontalAxis = args.floatValue;
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.AXIS)
            {
                lookVerticalAxis = args.floatValue;

                // Only treat quick flicks as club cycle input when the user is in VR
                if (Networking.LocalPlayer.IsUserInVR())
                {
                    // Re-arm flick input only after stick returns close to center.
                    if (Mathf.Abs(value) <= lookFlickResetThreshold)
                        lookFlickReady = true;

                    // Detect edge crossing of threshold and require re-arm to prevent rapid repeats.
                    if (lookFlickReady && Mathf.Abs(value) > lookFlickThreshold && Mathf.Abs(lastLookVerticalAxis) <= lookFlickThreshold)
                    {
                        if (value > 0f)
                            CycleClubNext();
                        else
                            CycleClubPrevious();

                        lookFlickReady = false;
                    }

                    lastLookVerticalAxis = value;
                }
                else
                {
                    // Reset stored axis when not in VR to avoid accidental triggers when entering VR
                    lastLookVerticalAxis = 0f;
                    lookFlickReady = true;
                }
            }
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

        public void CheckIfMenuIsOpen()
        {
            if (!OpenPuttUtils.LocalPlayerIsValid() || !gameObject.activeSelf) return;

            // Note: This also registers grabbable personal mirrors - so we check for at least more than 1 collider
            var numberOfColliders = Physics.OverlapSphereNonAlloc(Networking.LocalPlayer.GetPosition(), 10f, _menuColliders, 1 << 19);

#if UNITY_EDITOR
            isMenuOpen = numberOfColliders >= 1;
#else
                        isMenuOpen = numberOfColliders > 1;
#endif

            if (isMenuOpen)
            {
                // If the menu is open and the ball cam is enabled, disable it (easy shortcut to leave the ball cam)
                DisableBallCam();
            }

            // Toggle UI visibility
            if (Utilities.IsValid(uiController))
                uiController.SetUIVisible(!isMenuOpen);

            SendCustomEventDelayedSeconds(nameof(CheckIfMenuIsOpen), .25f);
        }
    }
}
