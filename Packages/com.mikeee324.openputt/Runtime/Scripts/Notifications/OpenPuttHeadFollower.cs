using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public class OpenPuttHeadFollower : UdonSharpBehaviour
    {
        #region Public Settings

        public float _lerpSpeed = 10f;

        #endregion

        public void Update()
        {
            transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            transform.rotation = Quaternion.Lerp(transform.rotation, Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation, Time.deltaTime * _lerpSpeed);
        }
    }
}
