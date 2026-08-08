using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttCalloutBox : UdonSharpBehaviour
    {
        private readonly int ShineEnable = Shader.PropertyToID("_ShineEnable");

        #region Public Settings

        [OpenPuttDescription("A single floating notification bubble that tweens onto screen near the player, shows a message with an optional sound, then tweens away again after a delay. Spawned and managed internally by OpenPuttNotifications - you shouldn't need to set this up by hand.")]
        [OpenPuttFoldoutGroup("References")]
        public GameObject targetObject;

        [OpenPuttFoldoutGroup("References")]
        public TMP_Text _calloutTextField;
        
        [OpenPuttFoldoutGroup("References")]
        public UnityEngine.UI.Image calloutBackgroundImage;

        [OpenPuttFoldoutGroup("Tween Settings")]
        public float speed = 2f;
        [OpenPuttFoldoutGroup("Tween Settings")]
        public float delay = 2f;
        [OpenPuttFoldoutGroup("Tween Settings")]
        public VRCTweenEase inTweenType = VRCTweenEase.OutElastic;
        [OpenPuttFoldoutGroup("Tween Settings")]
        public VRCTweenEase outTweenType = VRCTweenEase.InElastic;

        #endregion

        #region Internal Vars

        [HideInInspector] public OpenPuttNotifications manager;
        public Vector3 startPos;
        public Vector3 targetPos;
        private VRCTweenHandle _positionTweenHandle;
        private VRCTweenHandle _scaleTweenHandle;

        #endregion

        void Start()
        {

        }

        void OnDestroy()
        {
            // Clean up tweens when the object is destroyed.
            if (_positionTweenHandle != null)
                _positionTweenHandle.Kill();

            if (_scaleTweenHandle != null)
                _scaleTweenHandle.Kill();
        }

        public void SetCalloutText(string callText, AudioClip sound, Vector3 targetPosition, Vector3 startPosition, bool shiny = false)
        {
            //Prep the things.
            targetObject = gameObject;
            startPos = startPosition;
            transform.localPosition = startPos;
            transform.localScale = Vector3.zero;

            //Set the things
            _calloutTextField.text = callText;
            targetPos = targetPosition;

            //Set material settings on the shared material
            if (calloutBackgroundImage != null && calloutBackgroundImage.material != null)
            {
                if (shiny)
                {
                    calloutBackgroundImage.material.SetFloat(ShineEnable, 1f);
                    calloutBackgroundImage.material.EnableKeyword("_SHINEENABLE_ON"); 
                }
                else
                {
                    calloutBackgroundImage.material.SetFloat(ShineEnable, 0f);
                    calloutBackgroundImage.material.DisableKeyword("_SHINEENABLE_ON");
                }
            }
            
            //Start the things
            _positionTweenHandle = targetObject.TweenLocalPosition(targetPos, speed, inTweenType);
            _scaleTweenHandle = targetObject.TweenScale(Vector3.one, speed, inTweenType).OnComplete(this, nameof(OnTweened));
        }

        public void OnTweened()
        {
            SendCustomEventDelayedSeconds(nameof(ReturnTween), delay);
        }

        public void ReturnTween()
        {
            _positionTweenHandle = targetObject.TweenLocalPosition(startPos, speed, outTweenType);
            _scaleTweenHandle = targetObject.TweenScale(Vector3.zero, speed, outTweenType).OnComplete(this, nameof(OnEvenMoreTweened));
        }

        public void OnEvenMoreTweened()
        {
            if (Utilities.IsValid(manager))
            {
                manager.OnCalloutFinished();
            }
            else Debug.LogError($"[OpenPuttCalloutBox] I have no reference to the notification script manager");
            Debug.Log("Destroying callout box.");
            Destroy(gameObject);
        }
    }
}