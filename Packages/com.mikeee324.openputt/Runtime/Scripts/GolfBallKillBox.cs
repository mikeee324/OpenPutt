using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Forces any golf balls to jump back to their last known valid position when colliding with this object<br/>
    /// Requires a collider on the same object as this script!!
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class GolfBallKillBox : UdonSharpBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            var golfBall = collision.gameObject.GetComponent<GolfBallController>();
            if (Utilities.IsValid(golfBall))
            {
                golfBall._RespawnBallWithErrorNoise();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var golfBall = other.gameObject.GetComponent<GolfBallController>();
            if (Utilities.IsValid(golfBall))
            {
                golfBall._RespawnBallWithErrorNoise();
            }
        }
    }
}
