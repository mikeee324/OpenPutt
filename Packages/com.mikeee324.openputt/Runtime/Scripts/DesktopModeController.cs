using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DesktopModeController : OpenPuttEventListener
    {
        #region References
        [Header("References")]
        public OpenPutt openPutt;
        public Camera desktopCamera;
        public DesktopModeCameraController desktopCameraController;

        public LineRenderer directionLine;
        public Image powerBar;
        public Gradient powerBarGradient;

        public TextMeshProUGUI courseNameLabel;

        public TextMeshProUGUI courseParLabel;
        public TextMeshProUGUI courseParValueLabel;
        public TextMeshProUGUI courseHitsLabel;
        public TextMeshProUGUI courseHitsValueLabel;

        #endregion

        #region Public Settings
        [Header("Settings")]
        public LayerMask directionLineMask;
        public float lineMaxLength = 1.5f;
        [Tooltip("Defines which keyboard key will swap the player to the ball camera")]
        public KeyCode desktopCameraKey = KeyCode.Q;
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
            get => _desktopCamVisible;
            set
            {
                _desktopCamVisible = value;

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
        public float CurrentShotSpeed => _currentShotSpeed;
        public float CurrentShotSpeedNormalised => _currentShotSpeed / CurrentMaxSpeed;
        public bool IsPlayerAiming
        {
            get => _isAimingShot;
            set
            {
                if (!IsBallCamEnabled)
                {
                    _isAimingShot = false;
                    return;
                }

                if (_isAimingShot != value)
                {
                    // Store the last shot speed
                    if (!value && _currentShotSpeed > 0)
                        _lastShotSpeed = _currentShotSpeed;

                    // If player stopped aiming and there is a speed, hit the ball with it
                    if (!value && _currentShotSpeed > 0)
                    {
                        Vector3 ballDirection = desktopCamera.transform.position.GetDirectionTowards(golfBall.transform.position, ignoreHeight: true);
                        if (!openPutt.enableVerticalHits)
                            ballDirection.y = 0;

                        golfBall.OnBallHit(ballDirection * _currentShotSpeed);

                        UpdateUI(CurrentShotSpeedNormalised, noSmooth: true);
                        UpdateBallLineRenderer(CurrentShotSpeedNormalised, noSmooth: true);
                    }

                    // Reset speed to 0
                    _currentShotSpeed = 0;
                }
                _isAimingShot = value;

            }
        }
        #endregion

        #region Private Vars
        private bool _desktopCamVisible = false;
        private bool _isAimingShot = false;
        private float _currentShotSpeed = 0f;
        private float _lastShotSpeed = 0f;
        #endregion


        void Start()
        {
            // Disable camera by default
            IsBallCamEnabled = false;

            SendCustomEventDelayedSeconds(nameof(InitializeCamera), .25f);
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
        }


        private void LateUpdate()
        {
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
                    if (IsPlayerAiming)
                    {

                        if (ControllerUtils.LeftTrigger() > 0)
                        {
                            // LT is down
                            IsPlayerAiming = true;

                            Utils.Log(this, $@"1-{Input.GetAxis("Joy1 Axis 1")} 2-{Input.GetAxis("Joy1 Axis 2")} 7-{Input.GetAxis("Joy1 Axis 7")} 8-{Input.GetAxis("Joy1 Axis 8")} 9-{Input.GetAxis("Joy1 Axis 9")} 10-{Input.GetAxis("Joy1 Axis 10")}");
                            _currentShotSpeed = CurrentMaxSpeed * ControllerUtils.RightTrigger();
                        }
                        else
                        {
                            // Update shot speed based on player input
                            float moveVal = Input.GetAxis("Mouse ScrollWheel") * 10f;
                            if (moveVal == 0.0f)
                                moveVal = Input.GetAxis("Mouse Y") * .5f;

                            _currentShotSpeed = Mathf.Clamp(_currentShotSpeed -= moveVal, 0, CurrentMaxSpeed);
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
                    UpdateUI(_lastShotSpeed / CurrentMaxSpeed, noSmooth: true);
                    UpdateBallLineRenderer(0, noSmooth: true);
                }
            }
        }

        public void CheckInputs()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                IsBallCamEnabled = false;
            }

            if (Input.GetKeyDown(desktopCameraKey) || Input.GetKeyDown(KeyCode.Joystick1Button4))
            {
                IsBallCamEnabled = !IsBallCamEnabled;
            }

            IsPlayerAiming = ControllerUtils.LeftTrigger() > 0;
        }

        private void UpdateUI(float speedNormalised, bool noSmooth = false)
        {
            float currentUISpeed = uiSmoothSpeed == 0 || noSmooth ? speedNormalised : Mathf.Lerp(powerBar.transform.localScale.x, speedNormalised, Time.deltaTime * uiSmoothSpeed);

            powerBar.color = powerBarGradient.Evaluate(currentUISpeed);

            Vector3 targetScale = new Vector3(currentUISpeed, powerBar.transform.localScale.y, powerBar.transform.localScale.z);

            powerBar.transform.localScale = targetScale;
        }

        private void UpdateBallLineRenderer(float speedNormalised, bool noSmooth = false)
        {
            float currentUISpeed = uiSmoothSpeed == 0 || noSmooth ? speedNormalised : Mathf.Lerp(powerBar.transform.localScale.x, speedNormalised, Time.deltaTime * uiSmoothSpeed);

            // Toggle state on/off
            bool newLineState = currentUISpeed > 0 && IsPlayerAiming && CanAimBallNow;
            if (directionLine.gameObject.activeInHierarchy != newLineState)
                directionLine.gameObject.SetActive(newLineState);

            if (newLineState)
            {
                Vector3[] directionPoints = new Vector3[] { golfBall.transform.position };

                Vector3 ballDirection = golfBall.transform.position - desktopCamera.transform.position;
                if (!openPutt.enableVerticalHits)
                    ballDirection.y = 0;

                float distance = lineMaxLength * currentUISpeed;

                Ray r = new Ray(origin: golfBall.transform.position, direction: ballDirection);
                if (Physics.Raycast(r, out RaycastHit hit, distance, directionLineMask))
                    directionPoints = directionPoints.Add(hit.point);
                else
                    directionPoints = directionPoints.Add(r.GetPoint(distance));

                directionLine.SetPositions(directionPoints);
            }

            if (currentUISpeed > 0)
            {
                Color newColor = powerBarGradient.Evaluate(currentUISpeed);
                directionLine.startColor = newColor;
                directionLine.endColor = newColor;
            }
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (args.eventType == UdonInputEventType.BUTTON)
            {
                if (ControllerUtils.LeftTrigger() == 0)
                    IsPlayerAiming = value;
            }
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