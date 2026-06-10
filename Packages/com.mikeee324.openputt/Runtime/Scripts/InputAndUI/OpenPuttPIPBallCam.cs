
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Passive picture-in-picture camera that chases the local ball while it's moving and off screen,
    /// rendering to a RenderTexture shown in an on-screen RawImage. Desktop/mobile only. Hides in VR
    /// and while the main ball cam (OpenPuttBallCam) is active.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(1000)]
    public class OpenPuttPIPBallCam : OpenPuttEventListener
    {
        [Header("References")]
        [Tooltip("Reference to the OpenPutt instance")]
        public OpenPutt openPutt;
        [Tooltip("The dedicated PiP camera (should render to a RenderTexture)")]
        public Camera pipCamera;
        [Tooltip("Root GameObject of the on-screen overlay (Canvas/RawImage) that displays the RenderTexture. Toggled on/off with the PiP")]
        public GameObject pipCanvasRoot;
        [Tooltip("Reference to the main desktop ball cam. The PiP hides itself while this is active")]
        public OpenPuttBallCam mainBallCam;

        [Header("Behaviour")]
        [Tooltip("Master switch - turn the whole PiP feature off for this world")]
        public bool pipEnabled = true;
        [Tooltip("Minimum ball speed (m/s) before the PiP is allowed to show")]
        public float minMoveSpeed = 0.2f;
        [Tooltip("If the ball is closer than this to the camera you're viewing through, don't bother showing the PiP")]
        public float minBallDistance = 2f;
        [Tooltip("How long to keep the PiP up after the show conditions stop (avoids flicker)")]
        public float hideDelay = 0.75f;

        [Header("Camera Framing")]
        [Tooltip("How far behind the ball the camera sits")]
        public float followDistance = 1.5f;
        [Tooltip("How high above the ball the camera sits")]
        public float followHeight = 0.6f;
        [Tooltip("How quickly the camera position catches up to the ball")]
        public float smoothingSpeed = 5f;
        [Tooltip("How quickly the camera swings around to face the ball's travel direction")]
        public float followTurnSpeed = 3f;

        [Header("Slide Animation")]
        [Tooltip("Panel that slides on/off screen. Defaults to the canvas root if left empty")]
        public RectTransform pipPanel;
        [Tooltip("Eases the slide from off-screen (0) to its resting position (1)")]
        public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("How long the slide in/out takes (seconds)")]
        public float slideDuration = 0.35f;
        [Tooltip("Offset (in canvas units) applied to the panel when hidden. Negative X slides it off the left edge")]
        public Vector2 slideHiddenOffset = new Vector2(-600f, 0f);

        private PlayerManager localPlayerManager;
        private GolfBallController ball;
        private DevicePlatform _platform = DevicePlatform.PCVR;
        private bool _ballMoving;

        private bool _pipActive;
        private bool _rendererEnabled;
        private float _slideT;
        private Vector2 _restAnchoredPos;
        private bool _snapNextFrame;
        private float _hideTimer;
        private Vector3 _followDir = Vector3.forward;
        private Vector3 _smoothedPos;
        private Vector3 _lastBallPos;

        void Start()
        {
            if (Utilities.IsValid(openPutt))
                openPutt._RegisterEventListener(this);

            if (!Utilities.IsValid(pipPanel) && Utilities.IsValid(pipCanvasRoot))
                pipPanel = pipCanvasRoot.GetComponent<RectTransform>();
            if (Utilities.IsValid(pipPanel))
                _restAnchoredPos = pipPanel.anchoredPosition;
            if (slideCurve == null || slideCurve.length == 0)
                slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            // Start fully retracted and hidden
            _slideT = 0f;
            ApplySlide();
            _rendererEnabled = true; // force the disable below to take effect
            EnableRenderer(false);
        }

        public override void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {
            if (!player.isLocal)
                return;

            localPlayerManager = playerManager;
            ball = localPlayerManager.golfBall;
            _platform = player.GetPlatform();

            if (Utilities.IsValid(ball))
                _lastBallPos = ball.transform.position;

            // Sleep until the ball is hit. OnPlayerBallHit only wakes LateUpdate back up on
            // Windows desktop, so PCVR/mobile stay asleep here forever
            enabled = false;
        }

        public override void OnPlayerBallStartedMoving(VRCPlayerApi player)
        {
            if (!player.isLocal || _platform != DevicePlatform.Desktop)
                return;

            _ballMoving = true;

            // Wake up and reset framing state so velocity/position don't carry a stale jump
            if (Utilities.IsValid(ball))
                _lastBallPos = ball.transform.position;
            enabled = true;
        }

        public override void OnPlayerBallStopped(VRCPlayerApi player)
        {
            if (!player.isLocal)
                return;

            // Let LateUpdate run on until the linger/slide-out finishes, then it sleeps itself
            _ballMoving = false;
        }

        void LateUpdate()
        {
            bool wantVisible = false;
            Vector3 ballPos = Vector3.zero;
            Vector3 velocity = Vector3.zero;

            if (pipEnabled && Utilities.IsValid(ball) && Utilities.IsValid(localPlayerManager))
            {
                ballPos = ball.transform.position;

                // Velocity from position deltas - no physics ownership needed
                velocity = Time.deltaTime > 0f ? (ballPos - _lastBallPos) / Time.deltaTime : Vector3.zero;
                _lastBallPos = ballPos;

                // Never fight the main ball cam (platform is already gated to desktop via enabled)
                if (!(Utilities.IsValid(mainBallCam) && mainBallCam.BallCamActive))
                {
                    bool shouldShow = ShouldShowPip(ballPos);

                    // Linger briefly after conditions drop to avoid flicker
                    if (shouldShow)
                        _hideTimer = hideDelay;
                    else if (_hideTimer > 0f)
                        _hideTimer -= Time.deltaTime;

                    wantVisible = shouldShow || _hideTimer > 0f;
                }
            }

            SetPipVisible(wantVisible);
            AnimateSlide();

            if (wantVisible)
                UpdateCameraPosition(ballPos, velocity);

            // Ball has stopped and the PiP has fully slid away - go back to sleep until the next hit
            if (!_ballMoving && !_rendererEnabled && _hideTimer <= 0f)
                enabled = false;
        }

        /// <summary>
        /// True when the ball is moving, far enough away, and off screen. Uses the scoreboards'
        /// FOV-aware look-at test (tracks desktop FOV and the portable camera).
        /// </summary>
        private bool ShouldShowPip(Vector3 ballPos)
        {
            if (!_ballMoving || ball.BallCurrentSpeed < minMoveSpeed)
                return false;

            // Camera being viewed through: portable cam if active, else screen cam (desktop FOV)
            var viewCamera = VRCCameraSettings.ScreenCamera;
            var photoCamera = VRCCameraSettings.PhotoCamera;
            if (Utilities.IsValid(photoCamera) && photoCamera.Active)
                viewCamera = photoCamera;

            Vector3 viewPosition;
            Vector3 viewForward;
            float viewFieldOfView;
            if (Utilities.IsValid(viewCamera))
            {
                viewPosition = viewCamera.Position;
                viewForward = viewCamera.Rotation * Vector3.forward;
                viewFieldOfView = viewCamera.FieldOfView;
            }
            else if (Utilities.IsValid(Networking.LocalPlayer))
            {
                // Fallback to head tracking
                var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                viewPosition = head.position;
                viewForward = head.rotation * Vector3.forward;
                viewFieldOfView = 60f;
            }
            else
            {
                return false;
            }

            Vector3 toBall = ballPos - viewPosition;
            if (toBall.magnitude < minBallDistance)
                return false;

            bool ballIsOnScreen = Vector3.Dot(viewForward, toBall.normalized) >= ViewDotThreshold(viewFieldOfView);
            return !ballIsOnScreen;
        }

        /// <summary>
        /// Min dot product the ball direction must clear to count as on screen. Mirrors
        /// ScoreboardPositioner.ViewDotThreshold.
        /// </summary>
        private float ViewDotThreshold(float verticalFieldOfView)
        {
            // Vertical FOV widened to horizontal half-angle, assuming 16:9
            const float aspect = 16f / 9f;
            var halfVerticalRad = verticalFieldOfView * 0.5f * Mathf.Deg2Rad;
            var halfHorizontalRad = Mathf.Atan(Mathf.Tan(halfVerticalRad) * aspect);
            return Mathf.Cos(halfHorizontalRad);
        }

        /// <summary>
        /// Sits behind the ball along its travel direction, slightly raised, looking at it.
        /// </summary>
        private void UpdateCameraPosition(Vector3 ballPos, Vector3 velocity)
        {
            if (!Utilities.IsValid(pipCamera))
                return;

            Vector3 up = ball.gravityMagnitude > 0 ? -ball.gravityDirection : Vector3.up;

            // Flatten travel direction against gravity to keep the camera level
            Vector3 flatVel = Vector3.ProjectOnPlane(velocity, up);
            if (flatVel.sqrMagnitude > 0.0001f)
            {
                Vector3 wantDir = flatVel.normalized;
                _followDir = _snapNextFrame ? wantDir : Vector3.Slerp(_followDir, wantDir, Time.deltaTime * followTurnSpeed);
            }

            Vector3 desiredPos = ballPos - _followDir * followDistance + up * followHeight;
            _smoothedPos = _snapNextFrame ? desiredPos : Vector3.Lerp(_smoothedPos, desiredPos, Time.deltaTime * smoothingSpeed);
            _snapNextFrame = false;

            pipCamera.transform.position = _smoothedPos;
            pipCamera.transform.rotation = Quaternion.LookRotation(ballPos - _smoothedPos, up);
        }

        private void SetPipVisible(bool visible)
        {
            if (_pipActive == visible)
                return;
            _pipActive = visible;

            // Snap on first frame so it doesn't lerp in from a stale position, and
            // bring the renderer online immediately so the slide-in is visible
            if (visible)
            {
                _snapNextFrame = true;
                EnableRenderer(true);
            }
        }

        /// <summary>
        /// Drives the slide animation toward the current target each frame, and disables the
        /// camera/canvas once the panel has fully slid off screen.
        /// </summary>
        private void AnimateSlide()
        {
            float target = _pipActive ? 1f : 0f;
            if (slideDuration > 0f)
                _slideT = Mathf.MoveTowards(_slideT, target, Time.deltaTime / slideDuration);
            else
                _slideT = target;

            if (_rendererEnabled)
                ApplySlide();

            // Fully retracted - stop rendering until the next show
            if (!_pipActive && _slideT <= 0f)
                EnableRenderer(false);
        }

        /// <summary>
        /// Positions the panel between its hidden (off-screen) and resting positions using the curve.
        /// </summary>
        private void ApplySlide()
        {
            if (!Utilities.IsValid(pipPanel))
                return;

            float t = slideCurve.Evaluate(_slideT);
            pipPanel.anchoredPosition = _restAnchoredPos + slideHiddenOffset * (1f - t);
        }

        private void EnableRenderer(bool enable)
        {
            if (_rendererEnabled == enable)
                return;
            _rendererEnabled = enable;

            if (Utilities.IsValid(pipCamera))
                pipCamera.enabled = enable;
            if (Utilities.IsValid(pipCanvasRoot))
                pipCanvasRoot.SetActive(enable);
        }
    }
}
