using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(99)]
    public class DesktopModeController : OpenPuttEventListener
    {
        #region References
        [Header("References")]
        public OpenPutt openPutt;
        public Camera desktopCamera;
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
                    IsPlayerAiming = false;
                    playerIsAimingOnController = false;
                    playerIsAimingWithUI = false;
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
        #endregion

        #region Private Vars
        private bool desktopCamVisible = false;
        private bool isAimingShot = false;
        private float currentShotSpeed = 0f;
        private float lastShotSpeed = 0f;
        private bool playerIsAimingOnController = false;
        private bool playerIsAimingWithUI = false;
        private bool initialized = false;
        private int controllerCheckTotalTicks = 0;
        private int controllerCheckDS4Ticks = 0;
        private bool playerIsHoldingJump = false;
        private bool shotIsChargingUp = true;
        private Vector3[] directionPoints = new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
        [HideInInspector]
        public DevicePlatform localPlayerPlatform = DevicePlatform.Desktop;
        private Collider[] _menuColliders = new Collider[1];
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

            // Try and figure out if th e player has a controller plugged in, and maybe detect if xbox or ds4
            controllerCheckTotalTicks++;
            if (controllerCheckTotalTicks >= 30)
            {
                // We've used up all time to check controller
                if (controllerCheckDS4Ticks == controllerCheckTotalTicks)
                    openPutt.playerControllerType = Controller.DS4; // If every frame we checked looks like a DS4 then they're probs using a DS4
                else
                    openPutt.playerControllerType = Controller.Xbox; // Otherwise default to xbox
            }
            else
            {
                // Read LookHorizontal from both controller types
                float xboxLookHorizontal = Controller.Xbox.LookHorizontal();
                float ds4LookHorizontal = Controller.DS4.LookHorizontal();

                // If the xbox mode reads -1 and the ds4 reads near 0, log the tick as a valid DS4 tick
                if (xboxLookHorizontal == -1 && ds4LookHorizontal.IsNearZero())
                    controllerCheckDS4Ticks++;

                SendCustomEventDelayedFrames(nameof(InitializeCamera), 0);
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

                if (playerManager != null && playerManager.CurrentCourse != null)
                {
                    CourseManager currentCourse = playerManager.CurrentCourse;
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

                if (CanAimBallNow)
                {
                    if (playerIsHoldingJump || playerIsAimingWithUI)
                    {
                        float speedNormalised = CurrentShotSpeedNormalised;
                        if (speedNormalised <= 0 || speedNormalised >= 1)
                            shotIsChargingUp = !shotIsChargingUp;

                        currentShotSpeed = Mathf.MoveTowards(currentShotSpeed, shotIsChargingUp ? CurrentMaxSpeed : 0, 10 * Time.deltaTime);

                        speedNormalised = CurrentShotSpeedNormalised;
                        UpdateUI(speedNormalised, noSmooth: true);
                        UpdateBallLineRenderer(speedNormalised);
                    }
                    else if (IsPlayerAiming)
                    {
                        if (openPutt.playerControllerType.LeftTrigger() > 0)
                        {
                            // LT is down
                            IsPlayerAiming = true;

                            currentShotSpeed = CurrentMaxSpeed * openPutt.playerControllerType.RightTrigger();
                        }
                        else
                        {
                            // Update shot speed based on player input
                            float moveVal = Input.GetAxis("Mouse Y") * .5f;

                            currentShotSpeed = Mathf.Clamp(currentShotSpeed -= moveVal, 0, CurrentMaxSpeed);
                        }

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
            /*   string brrr = "";
               for (int i = 350; i < 370; i++)
                   brrr += $"({i})" + Input.GetKey((KeyCode)i) + " - ";
               Utils.Log(this, brrr);*/

            if (Input.GetKeyDown(togglePlayersKey))
                desktopCameraController.ShowPlayersInCamera = !desktopCameraController.ShowPlayersInCamera;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                //  IsBallCamEnabled = false;
            }

            if (Input.GetKeyDown(desktopCameraKey))
            {
                IsBallCamEnabled = !IsBallCamEnabled;
                return;
            }

            if (openPutt.playerControllerType.LeftTrigger() > 0.5f)
            {
                IsPlayerAiming = true;
                playerIsAimingOnController = true;
            }
            else if (playerIsAimingOnController)
            {
                IsPlayerAiming = false;
                playerIsAimingOnController = false;
            }
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (localPlayerPlatform == DevicePlatform.Desktop && args.eventType == UdonInputEventType.BUTTON)
            {
                playerIsHoldingJump = value;
                IsPlayerAiming = value;
            }
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (localPlayerPlatform == DevicePlatform.Desktop && args.eventType == UdonInputEventType.BUTTON)
            {
                if (openPutt.playerControllerType.LeftTrigger() <= 0.5f)
                    IsPlayerAiming = value;
            }
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

                directionPoints[0] = golfBall.transform.position;
                directionPoints[1] = Vector3.Lerp(directionPoints[0], endPoint, .9f);
                directionPoints[1] = Vector3.Lerp(directionPoints[0], endPoint, .95f);
                directionPoints[2] = endPoint;

                directionLine.SetPositions(directionPoints);
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
            playerIsAimingWithUI = true;
            IsPlayerAiming = true;
        }

        public void OnPlayerEndShot()
        {
            playerIsAimingWithUI = false;
            IsPlayerAiming = false;
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
            SendCustomEventDelayedSeconds(nameof(DisableCam), 2f);
        }

        public override void OnLocalPlayerBallHit()
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
        }

        public void DisableCam()
        {
            IsBallCamEnabled = false;
            courseNameLabel.text = "-";
            courseParValueLabel.text = "-";
            courseHitsValueLabel.text = "-";
        }
    }

}