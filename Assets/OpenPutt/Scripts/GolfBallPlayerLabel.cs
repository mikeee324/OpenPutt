
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class GolfBallPlayerLabel : UdonSharpBehaviour
    {
        #region Public Settings
        public PlayerManager playerManager;
        public GameObject attachToObject;
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
        }

        void Update()
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid())
                return;

            if (!canvas.enabled)
                return;

            transform.LookAt(Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head));

            UpdatePosition();

            TextMeshProUGUI currentLabel = CurrentLabel;
            if (attachToObject != null && currentLabel != null)
            {
                Color oldColor = currentLabel.color;
                Vector3 oldScale = currentLabel.transform.localScale;

                // Lerp label properties based on player distance to ball
                float distance = Vector3.Distance(transform.position, Networking.LocalPlayer.GetPosition());
                float visiblityVal = IsMyLabel ? localLabelVisibilityCurve.Evaluate(distance) : remoteLabelVisibilityCurve.Evaluate(distance);

                currentLabel.color = Color.Lerp(labelHideColor, labelVisibleColor, visiblityVal);
                currentLabel.transform.localScale = new Vector3(-visiblityVal, visiblityVal, 1);

                if (oldColor != currentLabel.color || currentLabel.transform.localScale != oldScale)
                    currentLabel.ForceMeshUpdate();
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
    }
}