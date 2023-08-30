using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace mikeee324.OpenPutt
{
    public enum DesktopModeAimType
    {
        Nothing, Mouse, Jump, Controller, UI
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(99)]
    public class DesktopModeController : OpenPuttEventListener
    {
        #region References
        [Header("References")]
        public OpenPutt openPutt;
        public Camera desktopCamera;
        public ControllerDetector controllerDetector;
        public DesktopModeCameraController desktopCameraController;
        public GameObject desktopUI;

        public GameObject mobileUI;
        public GameObject mobileUIBallButton;
        public GameObject mobileUIShootButton;
        public GameObject mobileUICameraButton;
        public GameObject mobileUIShootButtonText;
        public GameObject mobileUIShootPowerStuff;

        public LineRenderer directionLine;
        public Image powerBar;
        public Image powerBarMobile;
        public Gradient powerBarGradient;

        public TextMeshProUGUI courseNameLabel;

        public TextMeshProUGUI courseParLabel;
        public TextMeshProUGUI courseParValueLabel;
        public TextMeshProUGUI courseHitsLabel;
        public TextMeshProUGUI courseHitsValueLabel;

        public TextMeshProUGUI powerLabel;

        #endregion

        #region Public Settings
        [Header("Settings")]
        public LayerMask directionLineMask;
        public float lineMaxLength = 1.5f;
        [Tooltip("Defines which keyboard key will swap the player to the ball camera")]
        public KeyCode desktopCameraKey = KeyCode.Q;
        [Tooltip("Defines which keyboard key will toggle player models from being hidden on the camera")]
        public KeyCode togglePlayersKey = KeyCode.P;
        [Range(0f, 20f), Tooltip("How fast the smoothing is for UI elements")]
        public float uiSmoothSpeed = 10f;
        #endregion

        #region OpenPutt reference cache
        [HideInInspector]
        /// Contains a reference to the local player manager object
        public PlayerManager playerManager;
        [HideInInspector]
        /// Contains a reference to the local players golf ball
        public GolfBallController golfBall;
        #endregion

        #region Properties
        /// <summary>
        /// Checks whether or not the player can currently lock the camera and start lining up their shot
        /// </summary>
        private bool CanAimBallNow
        {
            get
            {
                // If camera is off just say no
                if (!IsBallCamEnabled || playerManager == null || golfBall == null)
                    return false;

                bool playerIsPlayingACourse = playerManager.CurrentCourse != null;

                // Discard any hits while the ball is already moving and the player is playing a course (allows them to hit the ball as much as they want otherwise)
                if (playerIsPlayingACourse && !golfBall.allowBallHitWhileMoving && golfBall.BallIsMoving)
                    return false;

                return true;
            }
        }

        public bool IsBallCamEnabled
        {
            get => desktopCamVisible;
            set
            {
                desktopCamVisible = value;

                if (desktopCamera != null)
                    desktopCamera.gameObject.SetActive(value);

                if (desktopCamera != null)
                    desktopCamera.enabled = value;

                if (openPutt != null && openPutt.LocalPlayerManager != null)
                {
                    // Lock the players position while the camera is open
                    openPutt.LocalPlayerManager.canFreezeDesktopPlayers = value;
                    openPutt.LocalPlayerManager.PlayerIsCurrentlyFrozen = value;

                    // Override the name labels target to be the desktop camera while it is enabled
                    openPutt.LocalPlayerManager.playerLabel.lookAtTarget = value ? desktopCamera.gameObject : null;
                }

                mobileUIBallButton.SetActive(!value);
                mobileUIShootButton.SetActive(value);

                if (!value)
                {
                    playerIsCurrentlyAimingWith = DesktopModeAimType.Nothing;
                    IsPlayerAiming = false;
                }
            }
        }
        /// <summary>
        /// Defines the maximum speed that the player can hit the ball with. The UI will scale the power bar to match this value.
        /// </summary>
        public float CurrentMaxSpeed
        {
            get
            {
                if (golfBall == null)
                    return 15;

                if (playerManager != null && playerManager.CurrentCourse != null && playerManager.CurrentCourse.drivingRangeMode)
                    return golfBall.BallMaxSpeed * 3;

                return golfBall.BallMaxSpeed;
            }
        }
        public float CurrentShotSpeed => currentShotSpeed;
        public float CurrentShotSpeedNormalised => currentShotSpeed / CurrentMaxSpeed;
        public bool IsPlayerAiming
        {
            get => isAimingShot;
            set
            {
                if (!IsBallCamEnabled)
                {
                    isAimingShot = false;
                    return;
                }

                if (isAimingShot != value)
                {
                    // Store the last shot speed
                    if (!value && currentShotSpeed > 0.1f)
                        lastShotSpeed = currentShotSpeed;

                    // Toggle UI bits on mobile UI
                    if (localPlayerPlatform == DevicePlatform.AndroidMobile)
                    {
                        mobileUIShootButtonText.SetActive(!value);
                        mobileUIShootPowerStuff.SetActive(value);
                    }

                    // If player stopped aiming and there is a speed, hit the ball with it
                    if (!value && currentShotSpeed > 0.1f)
                    {
                        Vector3 ballDirection = desktopCamera.transform.position.GetDirectionTowards(golfBall.transform.position, ignoreHeight: true);
                        if (!openPutt.enableVerticalHits)
                            ballDirection.y = 0;

                        golfBall.OnBallHit(ballDirection * currentShotSpeed);

                        UpdateUI(CurrentShotSpeedNormalised, noSmooth: true);
                        UpdateBallLineRenderer(CurrentShotSpeedNormalised, noSmooth: true);
                    }

                    // Reset speed to 0
                    currentShotSpeed = 0;
                }
                isAimingShot = value;

            }
        }

        private Controller CurrentJoystick => controllerDetector.LastUsedJoystick;
        private int CurrentJoystickID => controllerDetector.LastKnownJoystickID;
        #endregion

        #region Private Vars
        private bool desktopCamVisible = false;
        private bool isAimingShot = false;
        private bool initialized = false;

        private Vector3[] directionLinePoints = new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };

        [HideInInspector]
        public DevicePlatform localPlayerPlatform = DevicePlatform.Desktop;

        private Collider[] _menuColliders = new Collider[1];

        private DesktopModeAimType playerIsCurrentlyAimingWith = DesktopModeAimType.Nothing;

        private bool playerIsHoldingMouseAimButton = false;
        private bool playerIsHoldingControllerAimButton = false;
        private bool playerIsHoldingJump = false;
        private bool playerIsHoldingUIAimButton = false;

        private bool currentShotIsChargingUp = false;
        private float currentShotSpeed = 0f;

        private bool playerIsHoldingControllerCamButton = false;
        private bool playerIsHoldingControllerTogglePlayersButton = false;

        private float lastShotSpeed = 0f;
        #endregion


        void Start()
        {
            // Disable camera by default
            IsBallCamEnabled = false;

            mobileUICameraButton.SetActive(false);

            SendCustomEventDelayedSeconds(nameof(InitializeCamera), .25f);

            SendCustomEventDelayedSeconds(nameof(CheckIfMenuIsOpen), .25f);
        }

        public void CheckIfMenuIsOpen()
        {
            if (!Utils.LocalPlayerIsValid() || !gameObject.activeSelf) return;

            int numberOfColliders = Physics.OverlapSphereNonAlloc(Networking.LocalPlayer.GetPosition(), 9999f, _menuColliders, (1 << 19) | (1 << 20) | (1 << 21));
            bool menuMightBeOpen = numberOfColliders > 0;

            if (menuMightBeOpen)
            {
                // If the menu is open and the ball cam is enabled, disable it (easy shortcut to leave the ball cam)
                if (IsBallCamEnabled)
                    IsBallCamEnabled = false;
            }

            // Hide all mobile UI when the menus are open
            if (localPlayerPlatform == DevicePlatform.AndroidMobile)
                mobileUI.SetActive(!menuMightBeOpen);

            SendCustomEventDelayedSeconds(nameof(CheckIfMenuIsOpen), .25f);
        }

        public void CheckIfPlayerPickedUpBall()
        {
            BodyMountedObject ballObject = openPutt.leftShoulderPickup.ObjectToAttach == playerManager.golfBall.gameObject ? openPutt.leftShoulderPickup : openPutt.rightShoulderPickup;

            if (ballObject.pickedUpAtLeastOnce)
            {
                mobileUICameraButton.SetActive(true);
                return;
            }

            SendCustomEventDelayedSeconds(nameof(CheckIfPlayerPickedUpBall), .25f);
        }

        public void InitializeCamera()
        {
            // Wait until the local player is valid
            if (!Utils.LocalPlayerIsValid())
            {
                SendCustomEventDelayedSeconds(nameof(InitializeCamera), .25f);
                return;
            }

            // If player is in VR, disable this behaviour
            if (Networking.LocalPlayer.IsUserInVR())
            {
                gameObject.SetActive(false);
                desktopUI.SetActive(false);
                Utils.LogError(this, "Player is in VR disabling desktop camera");
                return;
            }

            // Wait until we can have been assigned a player manager
            if (openPutt == null || openPutt.LocalPlayerManager == null)
            {
                SendCustomEventDelayedSeconds(nameof(InitializeCamera), .25f);
                return;
            }

            if (desktopCamera == null || desktopCameraController == null)
            {
                gameObject.SetActive(false);
                return;
            }

            playerManager = openPutt.LocalPlayerManager;
            golfBall = playerManager.golfBall;

            desktopCameraController.target = golfBall.GetComponent<Rigidbody>();

            localPlayerPlatform = Networking.LocalPlayer.GetPlatform();

            BodyMountedObject b = openPutt.rightShoulderPickup.ObjectToAttach == playerManager.golfBall.gameObject ? openPutt.rightShoulderPickup : openPutt.leftShoulderPickup;
            if (b == null || (!b.pickedUpAtLeastOnce && localPlayerPlatform != DevicePlatform.AndroidMobile))
            {
                SendCustomEventDelayedSeconds(nameof(InitializeCamera), .25f);
                return;
            }

            switch (localPlayerPlatform)
            {
                case DevicePlatform.Desktop:
                    mobileUI.SetActive(false);
                    desktopUI.SetActive(true);
                    break;
                case DevicePlatform.AndroidMobile:
                    mobileUI.SetActive(true);
                    desktopUI.SetActive(false);

                    SendCustomEventDelayedSeconds(nameof(CheckIfPlayerPickedUpBall), .25f);
                    break;
                default:
                    desktopUI.SetActive(false);
                    mobileUI.SetActive(false);
                    break;
            }

            initialized = true;
        }


        private void LateUpdate()
        {
            if (!initialized)
                return;

            CheckInputs();

            if (IsBallCamEnabled)
            {
                desktopCameraController.LockCamera = CanAimBallNow && IsPlayerAiming;

                if (CanAimBallNow)
                {
                    // Check if there is an input we need to start listening to
                    if (playerIsCurrentlyAimingWith == DesktopModeAimType.Nothing && playerIsHoldingUIAimButton)
                        playerIsCurrentlyAimingWith = DesktopModeAimType.UI;
                    if (playerIsCurrentlyAimingWith == DesktopModeAimType.Nothing && playerIsHoldingJump)
                        playerIsCurrentlyAimingWith = DesktopModeAimType.Jump;
                    if (playerIsCurrentlyAimingWith == DesktopModeAimType.Nothing && playerIsHoldingMouseAimButton)
                        playerIsCurrentlyAimingWith = DesktopModeAimType.Mouse;
                    if (playerIsCurrentlyAimingWith == DesktopModeAimType.Nothing && playerIsHoldingControllerAimButton)
                        playerIsCurrentlyAimingWith = DesktopModeAimType.Controller;

                    switch (playerIsCurrentlyAimingWith)
                    {
                        case DesktopModeAimType.Mouse:
                            float moveVal = Input.GetAxis("Mouse Y") * .5f;

                            currentShotSpeed = Mathf.Clamp(currentShotSpeed -= moveVal, 0, CurrentMaxSpeed);

                            if (!playerIsHoldingMouseAimButton)
                                playerIsCurrentlyAimingWith = DesktopModeAimType.Nothing;
                            break;
                        case DesktopModeAimType.Controller:
                            currentShotSpeed = CurrentMaxSpeed * CurrentJoystick.RightTrigger(CurrentJoystickID);

                            if (!playerIsHoldingControllerAimButton)
                                playerIsCurrentlyAimingWith = DesktopModeAimType.Nothing;
                            break;
                        case DesktopModeAimType.Jump:
                            {
                                float speedNormalised = CurrentShotSpeedNormalised;
                                if (speedNormalised <= 0 || speedNormalised >= 1)
                                    currentShotIsChargingUp = !currentShotIsChargingUp;

                                currentShotSpeed = Mathf.MoveTowards(currentShotSpeed, currentShotIsChargingUp ? CurrentMaxSpeed : 0, 10 * Time.deltaTime);

                                speedNormalised = CurrentShotSpeedNormalised;
                                UpdateUI(speedNormalised, noSmooth: true);
                                UpdateBallLineRenderer(speedNormalised);

                                if (!playerIsHoldingJump)
                                    playerIsCurrentlyAimingWith = DesktopModeAimType.Nothing;
                                break;
                            }
                        case DesktopModeAimType.UI:
                            {
                                float speedNormalised = CurrentShotSpeedNormalised;
                                if (speedNormalised <= 0 || speedNormalised >= 1)
                                    currentShotIsChargingUp = !currentShotIsChargingUp;

                                currentShotSpeed = Mathf.MoveTowards(currentShotSpeed, currentShotIsChargingUp ? CurrentMaxSpeed : 0, 10 * Time.deltaTime);

                                speedNormalised = CurrentShotSpeedNormalised;
                                UpdateUI(speedNormalised, noSmooth: true);
                                UpdateBallLineRenderer(speedNormalised);

                                if (!playerIsHoldingUIAimButton)
                                    playerIsCurrentlyAimingWith = DesktopModeAimType.Nothing;
                                break;
                            }
                    }

                    IsPlayerAiming = playerIsCurrentlyAimingWith != DesktopModeAimType.Nothing;

                    if (playerIsCurrentlyAimingWith != DesktopModeAimType.Nothing)
                    {
                        float speedNormalised = CurrentShotSpeedNormalised;

                        UpdateUI(speedNormalised);
                        UpdateBallLineRenderer(speedNormalised);
                    }
                    else
                    {
                        // Animate UI back to 0 speed
                        UpdateUI(0);
                        UpdateBallLineRenderer(0);
                    }
                }
                else
                {
                    // Hide the line renderer
                    UpdateUI(lastShotSpeed / CurrentMaxSpeed, noSmooth: true);
                    UpdateBallLineRenderer(0, noSmooth: true);
                }
            }
        }

        public void CheckInputs()
        {
            if (Input.GetKeyDown(desktopCameraKey))
            {
                IsBallCamEnabled = !IsBallCamEnabled;
                return;
            }

            bool playerHoldingXButton = CurrentJoystick.X(CurrentJoystickID);
            if (playerHoldingXButton && !playerIsHoldingControllerCamButton)
            {
                playerIsHoldingControllerCamButton = true;
                IsBallCamEnabled = !IsBallCamEnabled;
            }
            else if (playerIsHoldingControllerCamButton && !playerHoldingXButton)
            {
                playerIsHoldingControllerCamButton = false;
            }

            // Keyboard players can toggle player models with a keypress
            if (Input.GetKeyDown(togglePlayersKey))
                desktopCameraController.ShowPlayersInCamera = !desktopCameraController.ShowPlayersInCamera;

            // Controller players can toggle with left bumper
            bool playerHoldingLBButton = CurrentJoystick.LeftBumper(CurrentJoystickID);
            if (playerHoldingLBButton && !playerIsHoldingControllerTogglePlayersButton)
            {
                playerIsHoldingControllerTogglePlayersButton = true;
                desktopCameraController.ShowPlayersInCamera = !desktopCameraController.ShowPlayersInCamera;
            }
            else if (playerIsHoldingControllerTogglePlayersButton && !playerHoldingLBButton)
            {
                playerIsHoldingControllerTogglePlayersButton = false;
            }

            playerIsHoldingControllerAimButton = CurrentJoystick.LeftTriggerButton(CurrentJoystickID);

#if UNITY_STANDALONE_WIN
            playerIsHoldingMouseAimButton = Input.GetKey(KeyCode.Mouse0); // Technically this does work on mobile - maybe trigger it on a UI button first though
#endif
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.BUTTON)
                playerIsHoldingJump = value;
        }

        private void UpdateUI(float speedNormalised, bool noSmooth = false)
        {
            float currentUISpeed = uiSmoothSpeed == 0 || noSmooth ? speedNormalised : Mathf.Lerp(powerBar.transform.localScale.x, speedNormalised, Time.deltaTime * uiSmoothSpeed);

            powerBar.color = powerBarGradient.Evaluate(currentUISpeed);
            powerBarMobile.color = powerBar.color;

            Vector3 targetScale = new Vector3(currentUISpeed, powerBar.transform.localScale.y, powerBar.transform.localScale.z);

            powerBar.transform.localScale = targetScale;
            powerBarMobile.transform.localScale = targetScale;
        }

        /// <summary>
        /// Updates the line renderer to show the current direction the ball will travel and how much power the player will hit the ball with if they let go.
        /// </summary>
        /// <param name="speedNormalised">Current shot speed between 0-1</param>
        /// <param name="noSmooth">Should the length of the line be smoothed?</param>
        private void UpdateBallLineRenderer(float speedNormalised, bool noSmooth = false)
        {
            float currentUISpeed = uiSmoothSpeed == 0 || noSmooth ? speedNormalised : Mathf.Lerp(powerBar.transform.localScale.x, speedNormalised, Time.deltaTime * uiSmoothSpeed);

            // Toggle state on/off
            bool newLineState = CanAimBallNow;
            if (directionLine.gameObject.activeInHierarchy != newLineState)
                directionLine.gameObject.SetActive(newLineState);

            if (newLineState)
            {
                Vector3 ballDirection = golfBall.transform.position - desktopCamera.transform.position;
                if (!openPutt.enableVerticalHits)
                    ballDirection.y = 0;

                float distance = (lineMaxLength * currentUISpeed) + 0.1f;

                Ray r = new Ray(origin: golfBall.transform.position, direction: ballDirection);

                Vector3 endPoint;
                if (Physics.Raycast(r, out RaycastHit endHit, distance, directionLineMask))
                    endPoint = endHit.point;
                else
                    endPoint = r.GetPoint(distance);

                directionLinePoints[0] = golfBall.transform.position;
                directionLinePoints[1] = Vector3.Lerp(directionLinePoints[0], endPoint, .9f);
                directionLinePoints[1] = Vector3.Lerp(directionLinePoints[0], endPoint, .95f);
                directionLinePoints[2] = endPoint;

                directionLine.SetPositions(directionLinePoints);
            }


            if (currentUISpeed > 0.01f)
            {
                Color newColor = powerBarGradient.Evaluate(currentUISpeed);
                directionLine.material.color = directionLine.startColor = directionLine.endColor = newColor;
            }
            else
            {
                directionLine.material.color = directionLine.startColor = directionLine.endColor = Color.white;
            }
        }

        public void OnPlayerRequestBall()
        {
            BodyMountedObject ballObject = openPutt.leftShoulderPickup.ObjectToAttach == playerManager.golfBall.gameObject ? openPutt.leftShoulderPickup : openPutt.rightShoulderPickup;

            ballObject.forcePickedUp = true;

            if (!ballObject.pickedUpAtLeastOnce)
            {
                // Just to try and catch the player pressing the button too quick the first time
                ballObject.ObjectToAttach.transform.position = Networking.LocalPlayer.GetPosition() + new Vector3(0, 1, 0);

                ballObject.pickedUpAtLeastOnce = true;
            }

            mobileUICameraButton.SetActive(true);
        }

        public void OnPlayerDropBall()
        {
            BodyMountedObject ballObject = openPutt.leftShoulderPickup.ObjectToAttach == playerManager.golfBall.gameObject ? openPutt.leftShoulderPickup : openPutt.rightShoulderPickup;

            ballObject.forcePickedUp = false;
        }

        public void OnToggleBallCamera()
        {
            BodyMountedObject b = openPutt.rightShoulderPickup.ObjectToAttach == playerManager.golfBall.gameObject ? openPutt.rightShoulderPickup : openPutt.leftShoulderPickup;
            if (b == null || b.pickedUpAtLeastOnce)
                IsBallCamEnabled = !IsBallCamEnabled;
        }

        public void OnPlayerStartShot()
        {
            playerIsHoldingUIAimButton = true;
        }

        public void OnPlayerEndShot()
        {
            playerIsHoldingUIAimButton = false;
        }

        public override void OnRemotePlayerHoleInOne(CourseManager course, CourseHole hole)
        {

        }

        public override void OnRemotePlayerBallEnterHole(CourseManager course, CourseHole hole)
        {

        }

        public override void OnLocalPlayerHoleInOne(CourseManager course, CourseHole hole)
        {

        }

        public override void OnLocalPlayerBallEnterHole(CourseManager course, CourseHole hole)
        {
            // Auto disable the camera after the local players ball enters a hole
            SendCustomEventDelayedSeconds(nameof(DisableCam), 2f);
        }

        public override void OnLocalPlayerBallHit()
        {
            SendCustomEventDelayedSeconds(nameof(UpdateUIText), .5f);
        }

        public void DisableCam()
        {
            IsBallCamEnabled = false;

            SendCustomEventDelayedSeconds(nameof(UpdateUIText), .5f);
        }

        public void UpdateUIText()
        {
            if (openPutt == null)
                return;

            PlayerManager playerManager = openPutt.LocalPlayerManager;

            if (playerManager == null)
                return;

            CourseManager currentCourse = playerManager.CurrentCourse;

            if (currentCourse == null)
            {
                courseNameLabel.text = "-";
                courseParValueLabel.text = "-";
                courseHitsValueLabel.text = "-";
            }
            else
            {
                if (currentCourse.scoreboardLongName != null && currentCourse.scoreboardLongName.Length > 0)
                    courseNameLabel.text = $"{currentCourse.scoreboardLongName} ({currentCourse.holeNumber})";
                else if (currentCourse.scoreboardShortName != null && currentCourse.scoreboardShortName.Length > 0)
                    courseNameLabel.text = $"{currentCourse.scoreboardShortName} ({currentCourse.holeNumber})";
                else
                    courseNameLabel.text = $"{currentCourse.holeNumber}";

                if (currentCourse.drivingRangeMode)
                {
                    courseParLabel.text = "BEST";
                    courseHitsLabel.text = "CUR";
                    courseParValueLabel.text = $"{playerManager.courseScores[currentCourse.holeNumber]}m";

                    int distance = Mathf.FloorToInt(Vector3.Distance(golfBall.transform.position, golfBall.respawnPosition));
                    courseHitsValueLabel.text = $"{distance}m";
                }
                else
                {
                    courseParLabel.text = "PAR";
                    courseHitsLabel.text = "HITS";
                    courseParValueLabel.text = $"{currentCourse.parScore}";
                    courseHitsValueLabel.text = $"{playerManager.courseScores[currentCourse.holeNumber]}";
                }
            }
        }
    }

}