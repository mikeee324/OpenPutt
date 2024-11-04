using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Listens for a golf ball entering/exiting a collider on this game object.<br/>
    /// It will apply or remove the dragInArea value to the golf ball to effect how quickly the ball loses its speed while in this collider
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(0)]
    public class GolfBallDragChangeArea : UdonSharpBehaviour
    {
        [SerializeField] private float dragInArea = 0f;

        public void OnTriggerEnter(Collider other)
        {
            var golfBall = other.GetComponent<GolfBallController>();
            if (!Utilities.IsValid(golfBall))
                return;

            golfBall.ballDragOverride = dragInArea;
        }

        private void OnTriggerExit(Collider other)
        {
            var golfBall = other.GetComponent<GolfBallController>();
            if (!Utilities.IsValid(golfBall))
                return;

            golfBall.ballDragOverride = 0f;
        }
    }
}