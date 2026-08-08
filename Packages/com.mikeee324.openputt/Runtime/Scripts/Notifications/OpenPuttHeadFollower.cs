using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    // Runs after OpenPuttBallCam (999) so we can follow the ball cam in LateUpdate without lagging a frame behind it
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(1000)]
    public class OpenPuttHeadFollower : UdonSharpBehaviour
    {
        #region Public Settings

        [OpenPuttDescription("Makes this object continuously follow the local player's head position and rotation, e.g. for attaching a UI element or effect to the camera.")]
        public float _lerpSpeed = 10f;

        [Tooltip("Optional ball camera to follow instead of the head while it is rendering. Wired up automatically by OpenPuttNotifications.")]
        public OpenPuttBallCam ballCam;

        #endregion

        private bool BallCamIsActive => Utilities.IsValid(ballCam) && Utilities.IsValid(ballCam.ballCam) && ballCam.BallCamActive;

        public void Update()
        {
            // The ball cam is what the player is actually looking through - handled in LateUpdate
            if (BallCamIsActive)
                return;

            transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            transform.rotation = Quaternion.Lerp(transform.rotation, Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation, Time.deltaTime * _lerpSpeed);
        }

        public void LateUpdate()
        {
            if (!BallCamIsActive)
                return;

            // Snap to the cam so anything attached to us stays locked to the view
            var camTransform = ballCam.ballCam.transform;
            transform.position = camTransform.position;
            transform.rotation = camTransform.rotation;
        }
    }
}
