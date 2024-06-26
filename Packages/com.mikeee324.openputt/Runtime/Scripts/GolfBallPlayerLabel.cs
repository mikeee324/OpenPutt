﻿
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
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

        [Space, Header("Visibility Settings")]
        [Tooltip("The color to fade out to (usually has transparency on)")]
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

            this.enabled = false;
        }

        public override void PostLateUpdate()
        {
            if (!canvas.enabled || Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid())
                return;

            if (localPlayerClub == null)
            {
                if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.LocalPlayerManager != null && playerManager.openPutt.LocalPlayerManager.golfClub != null)
                    localPlayerClub = playerManager.openPutt.LocalPlayerManager.golfClub;
            }

            Vector3 lookAtTarget = this.lookAtTarget != null ? this.lookAtTarget.transform.position : Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);

            transform.LookAt(lookAtTarget);

            UpdatePosition();

            TextMeshProUGUI currentLabel = CurrentLabel;
            if (attachToObject != null && currentLabel != null)
            {
                // Lerp label properties based on player distance to ball
                float distance = Vector3.Distance(transform.position, lookAtTarget);

                float visiblityVal = IsMyLabel ? localLabelVisibilityCurve.Evaluate(distance) : remoteLabelVisibilityCurve.Evaluate(distance);

                Vector3 newScale = new Vector3(visiblityVal, visiblityVal, 1);
                Color newColor = Color.Lerp(labelHideColor, labelVisibleColor, visiblityVal);

                if (localPlayerClub != null)
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
            if (attachToObject != null)
                transform.position = attachToObject.transform.position + new Vector3(0, 0.1f, 0);
        }

        public void RefreshPlayerName()
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid() || playerManager == null || playerManager.Owner == null || playerManager.Owner.displayName == null)
                return;

            IsMyLabel = playerManager.Owner == Networking.LocalPlayer;

            CurrentLabel = IsMyLabel ? localPlayerLabel : remotePlayerLabel;

            CurrentLabel.text = playerManager.Owner.displayName;

            localPlayerLabel.enabled = IsMyLabel;
            remotePlayerLabel.enabled = !localPlayerLabel.enabled;
        }

        public void CheckVisibility()
        {
            if (!Utils.LocalPlayerIsValid())
            {
                // Vary the time between each check to try and stop them all happening at once
                SendCustomEventDelayedSeconds(nameof(CheckVisibility), Random.Range(.1f, .15f));
                return;
            }

            Vector3 lookAtTarget = this.lookAtTarget != null ? this.lookAtTarget.transform.position : Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
            float distance = Vector3.Distance(attachToObject.transform.position, lookAtTarget);
            float visiblityVal = IsMyLabel ? localLabelVisibilityCurve.Evaluate(distance) : remoteLabelVisibilityCurve.Evaluate(distance);
            bool newActiveState = visiblityVal > 0.1f;

            if (this.enabled != newActiveState)
            {
                // Stop script from being called in Update()
                this.enabled = newActiveState;

                // Also toggle canvas off as this saves some time during rendering
                this.canvas.enabled = newActiveState;

                // If the label is now visible snap it back to th eball before the next frame
                if (newActiveState)
                    this.UpdatePosition();
            }

            // Vary the time between each check to try and stop them all happening at once
            SendCustomEventDelayedSeconds(nameof(CheckVisibility), Random.Range(.1f, .2f));
        }
    }
}