
using UdonSharp;
using UnityEngine;

namespace mikeee324.OpenPutt
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
            GolfBallController golfBall = collision.gameObject.GetComponent<GolfBallController>();
            if (golfBall != null)
            {
                golfBall._RespawnBallWithErrorNoise();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            GolfBallController golfBall = other.gameObject.GetComponent<GolfBallController>();
            if (golfBall != null)
            {
                golfBall._RespawnBallWithErrorNoise();
            }
        }
    }
}
