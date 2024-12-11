using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(50)]
    public class GolfBallPlayerLabel : UdonSharpBehaviour
    {
        #region Public Settings

        public PlayerManager playerManager;
        public GameObject attachToObject;

        [Tooltip("What GameObject this label should look towards. If empty it will look towards the local player.")]
        public GameObject lookAtTarget;

        public TextMeshProUGUI localPlayerLabel;
        public TextMeshProUGUI remotePlayerLabel;
        public Canvas canvas;
        public TextMeshProUGUI CurrentLabel { get; private set; }

        [Space, Header("Visibility Settings")] [Tooltip("The color to fade out to (usually has transparency on)")]
        public Color labelHideColor = new Color(94, 129, 172, 0);

        [Tooltip("The color to fade in to")]
        public Color labelVisibleColor = new Color(94, 129, 172);

        [Tooltip("A curve that describes how visible the label will be depending on the distance to the local player (Time=Distance In Meters)")]
        public AnimationCurve localLabelVisibilityCurve;

        [Tooltip("A curve that describes how visible the label will be depending on the distance to the local player (Time=Distance In Meters)")]
        public AnimationCurve remoteLabelVisibilityCurve;

        #endregion

        #region Internal Vars

        public bool IsMyLabel { get; private set; }
        private Vector3 lastKnownScale = Vector3.zero;
        private Color lastKnownColor = Color.black;
        private float alphaOverride = 1f;
        private GolfClub localPlayerClub;

        #endregion

        void Start()
        {
            if (localLabelVisibilityCurve.length == 0)
            {
                localLabelVisibilityCurve.AddKey(0f, 0f);
                localLabelVisibilityCurve.AddKey(1f, 0.4f);
                localLabelVisibilityCurve.AddKey(5f, 1f);
                localLabelVisibilityCurve.AddKey(10f, 2f);
                localLabelVisibilityCurve.AddKey(50f, 0f);
            }

            if (remoteLabelVisibilityCurve.length == 0)
            {
                remoteLabelVisibilityCurve.AddKey(0f, 0f);
                remoteLabelVisibilityCurve.AddKey(2f, 0.4f);
                remoteLabelVisibilityCurve.AddKey(5f, 1f);
                remoteLabelVisibilityCurve.AddKey(10f, 0f);
                remoteLabelVisibilityCurve.AddKey(50f, 0f);
            }

            // Do a regular check to see if the label should be turned on or off
            SendCustomEventDelayedSeconds(nameof(CheckVisibility), 0.15f);

            enabled = false;
        }

        public override void PostLateUpdate()
        {
            if (!canvas.enabled || !Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsValid())
                return;

            if (!Utilities.IsValid(localPlayerClub))
            {
                if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.LocalPlayerManager) && Utilities.IsValid(playerManager.openPutt.LocalPlayerManager.golfClub))
                    localPlayerClub = playerManager.openPutt.LocalPlayerManager.golfClub;
            }

            var lookAtTarget = Utilities.IsValid(this.lookAtTarget) ? this.lookAtTarget.transform.position : Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);

            transform.LookAt(lookAtTarget);

            UpdatePosition();

            var currentLabel = CurrentLabel;
            if (Utilities.IsValid(attachToObject) && Utilities.IsValid(currentLabel))
            {
                // Lerp label properties based on player distance to ball
                var distance = Vector3.Distance(transform.position, lookAtTarget);

                var visiblityVal = IsMyLabel ? localLabelVisibilityCurve.Evaluate(distance) : remoteLabelVisibilityCurve.Evaluate(distance);

                var newScale = new Vector3(visiblityVal, visiblityVal, 1);
                var newColor = Color.Lerp(labelHideColor, labelVisibleColor, visiblityVal);

                if (Utilities.IsValid(localPlayerClub))
                {
                    if (localPlayerClub.ClubIsArmed)
                        alphaOverride = Mathf.Clamp01(alphaOverride - .04f);
                    else
                        alphaOverride = Mathf.Clamp01(alphaOverride + .04f);
                }

                if (alphaOverride < newColor.a)
                {
                    newColor.a = alphaOverride;
                }

                if (lastKnownScale != newScale)
                {
                    canvas.transform.localScale = newScale;
                    lastKnownScale = newScale;
                }

                if (lastKnownColor != newColor)
                {
                    currentLabel.color = newColor;
                    currentLabel.ForceMeshUpdate();
                    lastKnownColor = newColor;
                }
            }
        }

        public void UpdatePosition()
        {
            if (Utilities.IsValid(attachToObject))
                transform.position = attachToObject.transform.position + new Vector3(0, 0.1f, 0);
        }

        public void RefreshPlayerName()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsValid() || !Utilities.IsValid(playerManager) || !Utilities.IsValid(playerManager.Owner) || !Utilities.IsValid(playerManager.Owner.displayName))
                return;

            IsMyLabel = playerManager.Owner == Networking.LocalPlayer;

            CurrentLabel = IsMyLabel ? localPlayerLabel : remotePlayerLabel;

            CurrentLabel.text = playerManager.Owner.displayName;

            localPlayerLabel.enabled = IsMyLabel;
            remotePlayerLabel.enabled = !localPlayerLabel.enabled;
        }

        public void CheckVisibility()
        {
            if (!OpenPuttUtils.LocalPlayerIsValid())
            {
                // Vary the time between each check to try and stop them all happening at once
                SendCustomEventDelayedSeconds(nameof(CheckVisibility), Random.Range(.1f, .15f));
                return;
            }

            var lookAtTarget = Utilities.IsValid(this.lookAtTarget) ? this.lookAtTarget.transform.position : Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
            var distance = Vector3.Distance(attachToObject.transform.position, lookAtTarget);
            var visiblityVal = IsMyLabel ? localLabelVisibilityCurve.Evaluate(distance) : remoteLabelVisibilityCurve.Evaluate(distance);
            var newActiveState = visiblityVal > 0.1f;

            if (enabled != newActiveState)
            {
                // Stop script from being called in Update()
                enabled = newActiveState;

                // Also toggle canvas off as this saves some time during rendering
                canvas.enabled = newActiveState;

                // If the label is now visible snap it back to th eball before the next frame
                if (newActiveState)
                    UpdatePosition();
            }

            // Vary the time between each check to try and stop them all happening at once
            SendCustomEventDelayedSeconds(nameof(CheckVisibility), Random.Range(.1f, .2f));
        }
    }
}