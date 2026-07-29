using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PuttPalmVRUI : UdonSharpBehaviour
    {
        private const float VR_MIN_PLAYER_HEIGHT = 0.2f;
        private const float VR_MAX_PLAYER_HEIGHT = 5f;

        [OpenPuttDescription("Shows a small UI panel above the players palm in VR when they turn their non-dominant hand upwards - used for quick access to menu options while playing.")]
        [Tooltip("Reference to OpenPutt for reading local handedness")]
        public OpenPutt openPutt;

        [Tooltip("Reference to the VR UI view")]
        public OpenPuttUIView vrUI;

        [Tooltip("Reference to the UI controller, used to force the palm UI open for the fetch ball panel")]
        public OpenPuttUIController uiController;

        [OpenPuttFoldoutGroup("Height Scaling")]
        [Tooltip("Reference player height in meters that corresponds to 1x UI scale")]
        public float vrReferenceHeight = 1.1f;

        [OpenPuttFoldoutGroup("Visibility Animation")]
        [Tooltip("How many seconds VR UI takes to animate in/out when shown or hidden")]
        public float vrUIVisibilityAnimSeconds = 0.12f;

        [OpenPuttFoldoutGroup("Visibility Animation")]
        [Tooltip("Curve used for VR UI visibility animation (0-1 input, 0-1 output)")]
        public AnimationCurve vrUIVisibilityAnimCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.2f, 0.7058f, 0f, 0f),
            new Keyframe(0.4f, 1.0290f, 0f, 0f),
            new Keyframe(0.58f, 1.1000f, 0f, 0f),
            new Keyframe(0.8f, 1.0465f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

        [Tooltip("Palm-up dot threshold to SHOW the VR UI. Higher = must face more upward to show.")]
        [Range(0f, 1f)]
        public float palmUpShowThreshold = 0.4f;

        [Tooltip("Palm-up dot threshold to HIDE the VR UI when it is already visible. Lower = stays visible more easily.")]
        [Range(0f, 1f)]
        public float palmUpHideThreshold = 0.25f;

        [Tooltip("Vertical offset from hand center for VR UI anchor")]
        public float handHeightOffset = 0.1f;

        [OpenPuttFoldoutGroup("Visibility Animation")]
        [Tooltip("How quickly the palm UI rotates to face the head. Lower values are snappier, higher values are smoother but can look laggy when turning quickly. Set to 0 to disable smoothing.")]
        public float vrUIRotationSmoothTime = 0.08f;

        // [Header("Debug")]
        // [Tooltip("Line renderer to show palm direction")]
        // public LineRenderer debugPalmLine;

        // [Tooltip("Line renderer to show world up direction")]
        // public LineRenderer debugWorldUpLine;

        private Transform vrUIRoot;
        private Vector3 vrUIBaseScale = Vector3.one;
        private Vector3 vrUITargetScale = Vector3.one;
        private float vrUIDistanceScaleMultiplier = 1f;
        private float vrUIVisibilityLerp = 1f;
        private bool vrUIVisibilityTarget = false;
        private float vrUIVisibilityDirection = 0f;
        private bool isInitialised = false;
        private bool isInVR = false;
        private Quaternion vrUICurrentRotation = Quaternion.identity;

        private void UpdateScaleFromEyeHeight(float eyeHeightAsMeters)
        {
            float safeReferenceHeight = Mathf.Max(vrReferenceHeight, 0.01f);
            float clampedHeight = Mathf.Clamp(eyeHeightAsMeters, VR_MIN_PLAYER_HEIGHT, VR_MAX_PLAYER_HEIGHT);
            float scaleMultiplier = clampedHeight / safeReferenceHeight;

            vrUITargetScale = vrUIBaseScale * scaleMultiplier;
            vrUIDistanceScaleMultiplier = Mathf.Max(scaleMultiplier, 0.01f);
        }

        // private void DrawDebugLines(Vector3 origin, Vector3 palmDir, float palmUpDot, float activeThreshold)
        // {
        //     if (!Utilities.IsValid(debugPalmLine) || !Utilities.IsValid(debugWorldUpLine))
        //         return;

        //     debugPalmLine.positionCount = 2;
        //     debugPalmLine.SetPosition(0, origin);
        //     debugPalmLine.SetPosition(1, origin + palmDir.normalized * .1f * vrUIDistanceScaleMultiplier);
        //     Color palmColor = palmUpDot > activeThreshold ? Color.green : Color.red;
        //     debugPalmLine.sharedMaterial.color = palmColor;

        //     debugWorldUpLine.positionCount = 2;
        //     debugWorldUpLine.SetPosition(0, origin);
        //     debugWorldUpLine.SetPosition(1, origin + Vector3.up * .1f * vrUIDistanceScaleMultiplier);
        //     Color worldColor = Color.blue;
        //     debugWorldUpLine.sharedMaterial.color = worldColor;
        // }

        void Start()
        {
            if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                OpenPuttUtils.Log(this, "PalmUI[Start] script started");

            TryInitialise();
        }

        public override void PostLateUpdate()
        {
            if (!isInitialised)
            {
                if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                    OpenPuttUtils.Log(this, "PalmUI[frame] not initialised yet -> TryInitialise");
                TryInitialise();
                return;
            }

            if (!isInVR)
            {
                if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                    OpenPuttUtils.Log(this, "PalmUI[frame] not in VR -> skipping updates");
                return;
            }

            var localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer) || !Utilities.IsValid(vrUIRoot))
            {
                if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                    OpenPuttUtils.Log(this, $"PalmUI[frame] missing refs localPlayerValid={Utilities.IsValid(localPlayer)} vrUIRootValid={Utilities.IsValid(vrUIRoot)}");
                return;
            }

            bool isLeftHandedMode = Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.LocalPlayerManager) && openPutt.LocalPlayerManager.IsInLeftHandedMode;
            bool nonDominantIsLeftHand = !isLeftHandedMode;

            // While the fetch ball panel is forced visible, anchor the UI to whichever hand is actually
            // holding the ball on the shoulder mount rather than assuming it's the non-dominant hand.
            bool showingFetchBallPanel = Utilities.IsValid(uiController) && uiController.FetchBallPanelVisible;
            bool useLeftHand = nonDominantIsLeftHand;
            if (showingFetchBallPanel && Utilities.IsValid(openPutt))
            {
                var ballShoulderPickup = isLeftHandedMode ? openPutt.rightShoulderPickup : openPutt.leftShoulderPickup;
                if (Utilities.IsValid(ballShoulderPickup) && ballShoulderPickup.heldInHand != VRC_Pickup.PickupHand.None)
                    useLeftHand = ballShoulderPickup.heldInHand == VRC_Pickup.PickupHand.Left;
            }

            var handData = localPlayer.GetTrackingData(useLeftHand ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
            var headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

            Vector3 handPosition = handData.position;
            Vector3 targetPosition = handPosition + Vector3.up * (handHeightOffset * vrUIDistanceScaleMultiplier);

            Vector3 palmUpDirection = handData.rotation * (useLeftHand ? Vector3.up : Vector3.down);
            float showThreshold = Mathf.Clamp01(palmUpShowThreshold);
            float hideThreshold = Mathf.Clamp01(palmUpHideThreshold);
            if (hideThreshold > showThreshold)
                hideThreshold = showThreshold;

            float palmUpDot = Vector3.Dot(palmUpDirection, Vector3.up);
            float activeThreshold = vrUIVisibilityTarget ? hideThreshold : showThreshold;
            bool palmFacingUp = palmUpDot > activeThreshold;

            // Override: if the local player manager exists and the player isn't playing,
            // keep the palm UI hidden regardless of palm orientation.
            bool overrideHideBecauseNotPlaying = Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.LocalPlayerManager) && !openPutt.LocalPlayerManager.IsPlaying;
            if (overrideHideBecauseNotPlaying)
                palmFacingUp = false;

            // Override: force the palm UI open while the fetch ball panel needs to be seen (ball held on
            // shoulder mount during a standard course), regardless of palm orientation.
            if (showingFetchBallPanel)
                palmFacingUp = true;

            if (palmFacingUp != vrUIVisibilityTarget)
                vrUIVisibilityDirection = palmFacingUp ? 1f : -1f;

            vrUIVisibilityTarget = palmFacingUp;
            bool vrUIJustActivated = false;
            if (vrUIVisibilityTarget && !vrUIRoot.gameObject.activeSelf)
            {
                vrUIRoot.gameObject.SetActive(true);
                vrUIJustActivated = true;
            }

            // draw debug lines for palm direction and world up when debugMode is enabled
            // if (Utilities.IsValid(openPutt) && openPutt.debugMode)
            // {
            //     // disable debug lines when not in debug mode to avoid unnecessary overhead
            //     if (Utilities.IsValid(debugPalmLine) && !debugPalmLine.gameObject.activeSelf)
            //         debugPalmLine.gameObject.SetActive(true);
            //     if (Utilities.IsValid(debugWorldUpLine) && !debugWorldUpLine.gameObject.activeSelf)
            //         debugWorldUpLine.gameObject.SetActive(true);

            //     DrawDebugLines(handPosition, palmUpDirection, palmUpDot, activeThreshold);
            // }
            // else
            // {
            //     // disable debug lines when not in debug mode to avoid unnecessary overhead
            //     if (Utilities.IsValid(debugPalmLine) && debugPalmLine.gameObject.activeSelf)
            //         debugPalmLine.gameObject.SetActive(false);
            //     if (Utilities.IsValid(debugWorldUpLine) && debugWorldUpLine.gameObject.activeSelf)
            //         debugWorldUpLine.gameObject.SetActive(false);
            // }

            if (Mathf.Approximately(vrUIVisibilityDirection, 0f))
                vrUIVisibilityDirection = vrUIVisibilityTarget ? 1f : -1f;

            float previousVisibilityLerp = vrUIVisibilityLerp;
            float step = Time.deltaTime / Mathf.Max(vrUIVisibilityAnimSeconds, 0.01f);
            vrUIVisibilityLerp = Mathf.Clamp01(vrUIVisibilityLerp + (vrUIVisibilityDirection * step));

            // toggle textmeshes active to force them to update their geometry when the UI becomes visible
            if (vrUIVisibilityTarget && vrUIVisibilityDirection > 0f && previousVisibilityLerp < 0.5f && vrUIVisibilityLerp >= 0.5f)
            {
                var vrUIRootObject = vrUIRoot.gameObject;
                if (Utilities.IsValid(vrUIRootObject) && vrUIRootObject.activeSelf)
                {
                    vrUIRootObject.SetActive(false);
                    vrUIRootObject.SetActive(true);
                }
            }

            float curvedLerp = vrUIVisibilityAnimCurve != null ? vrUIVisibilityAnimCurve.Evaluate(Mathf.Clamp01(vrUIVisibilityLerp)) : vrUIVisibilityLerp;
            Vector3 animatedPosition = Vector3.Lerp(handPosition, targetPosition, curvedLerp);
            Vector3 toHeadDirection = (headData.position - animatedPosition).normalized;

            if (toHeadDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-toHeadDirection, Vector3.up);
                if (vrUIJustActivated || vrUIRotationSmoothTime <= 0f)
                {
                    vrUICurrentRotation = targetRotation;
                }
                else
                {
                    float rotationLerp = 1f - Mathf.Exp(-Time.deltaTime / vrUIRotationSmoothTime);
                    vrUICurrentRotation = Quaternion.Slerp(vrUICurrentRotation, targetRotation, rotationLerp);
                }
                vrUIRoot.SetPositionAndRotation(animatedPosition, vrUICurrentRotation);
            }
            else
            {
                vrUIRoot.position = animatedPosition;
            }

            vrUIRoot.localScale = vrUITargetScale * curvedLerp;

            float target = vrUIVisibilityTarget ? 1f : 0f;
            bool hasReachedTarget = Mathf.Approximately(vrUIVisibilityLerp, target);
            if (hasReachedTarget)
                vrUIVisibilityDirection = 0f;

            if (!vrUIVisibilityTarget && Mathf.Approximately(vrUIVisibilityLerp, 0f) && vrUIRoot.gameObject.activeSelf)
                vrUIRoot.gameObject.SetActive(false);
        }

        public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
        {
            if (!Utilities.IsValid(player) || !player.isLocal || !isInVR || !Utilities.IsValid(vrUIRoot))
            {
                //if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                //    OpenPuttUtils.Log(this, $"PalmUI[height-change] ignored playerValid={Utilities.IsValid(player)} isLocal={(Utilities.IsValid(player) ? player.isLocal : false)} isInVR={isInVR} rootValid={Utilities.IsValid(vrUIRoot)}");
                return;
            }

            UpdateScaleFromEyeHeight(player.GetAvatarEyeHeightAsMeters());

            if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                OpenPuttUtils.Log(this, $"PalmUI[height-change] prevEye={prevEyeHeightAsMeters:F3} newEye={player.GetAvatarEyeHeightAsMeters():F3} targetScale={vrUITargetScale} distScale={vrUIDistanceScaleMultiplier:F3}");
        }

        public void TryInitialise()
        {
            var localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer))
            {
                if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                    OpenPuttUtils.Log(this, "PalmUI[init] local player invalid, retrying in 1s");
                SendCustomEventDelayedSeconds(nameof(TryInitialise), 1f);
                return;
            }

            isInVR = localPlayer.IsUserInVR();
            if (!isInVR)
            {
                if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                    OpenPuttUtils.Log(this, "PalmUI[init] local player is not in VR, disabling script");
                isInitialised = true;
                enabled = false;
                return;
            }

            if (!Utilities.IsValid(uiController))
                uiController = GetComponent<OpenPuttUIController>();

            if (!Utilities.IsValid(vrUI) && Utilities.IsValid(uiController))
                vrUI = uiController.vrUI;

            if (!Utilities.IsValid(vrUI) || !Utilities.IsValid(vrUI.transform.parent))
            {
                if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                    OpenPuttUtils.Log(this, $"PalmUI[init] waiting for references vrUIValid={Utilities.IsValid(vrUI)} hasParent={(Utilities.IsValid(vrUI) && Utilities.IsValid(vrUI.transform.parent))}");
                SendCustomEventDelayedSeconds(nameof(TryInitialise), 1f);
                return;
            }

            vrUIRoot = vrUI.transform.parent;
            vrUIBaseScale = vrUIRoot.localScale;
            vrUITargetScale = vrUIBaseScale;
            vrUIVisibilityLerp = 0f;
            vrUIVisibilityTarget = false;
            vrUIVisibilityDirection = 0f;

            vrUIRoot.gameObject.SetActive(false);

            UpdateScaleFromEyeHeight(localPlayer.GetAvatarEyeHeightAsMeters());

            float curvedLerp = vrUIVisibilityAnimCurve != null ? vrUIVisibilityAnimCurve.Evaluate(Mathf.Clamp01(vrUIVisibilityLerp)) : vrUIVisibilityLerp;
            vrUIRoot.localScale = vrUITargetScale * curvedLerp;

            isInitialised = true;
            if (Utilities.IsValid(openPutt) && openPutt.debugMode)
                OpenPuttUtils.Log(this, $"PalmUI[init] complete baseScale={vrUIBaseScale} targetScale={vrUITargetScale}");
        }
    }
}
