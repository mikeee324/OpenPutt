using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttHeadFollower : UdonSharpBehaviour
    {
        #region Public Settings

        [OpenPuttDescription("Makes this object continuously follow the local player's head position and rotation, e.g. for attaching a UI element or effect to the camera.")]
        public float _lerpSpeed = 10f;

        #endregion

        public void Update()
        {
            transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            transform.rotation = Quaternion.Lerp(transform.rotation, Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation, Time.deltaTime * _lerpSpeed);
        }
    }
}
