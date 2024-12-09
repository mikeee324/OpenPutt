using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public class BallTester : UdonSharpBehaviour
    {
        public GolfBallController ballController;
        public Vector3 ballVelocity;
        public Vector3 originalPosition;

        public bool hitBall = false;
        public bool respawnBall = false;
        [Range(1f, 10f)]
        public float resetTime = 1f;
        private float resetTicker = -1f;

        public bool autoSpeedIncreases = false;
        public Vector3 currentVelocity = Vector3.zero;

        public OpenPutt openPutt = null;

        void Start()
        {
            originalPosition = this.transform.position;
            ballController.playerManager.openPutt = this.openPutt;
        }

        void Update()
        {
            if (autoSpeedIncreases && resetTicker < 0f)
            {
                resetTicker = 0;
                if (currentVelocity == Vector3.zero)
                    currentVelocity = ballVelocity;
            }
            else if (!autoSpeedIncreases)
            {
                currentVelocity = ballVelocity;
            }

            if (resetTicker >= 0f)
            {
                resetTicker += Time.deltaTime;
                if (resetTicker > resetTime)
                {
                    resetTicker = -1f;
                    if (autoSpeedIncreases)
                    {
                        RespawnBall();

                        //currentVelocity = currentVelocity * 1.1f;
                        hitBall = true;
                    }
                }
            }
            if (hitBall)
            {
                hitBall = false;
                HitBall();
                resetTicker = 0f;
            }
            if (respawnBall)
            {
                respawnBall = false;
                RespawnBall();
            }
        }

        public void HitBall()
        {
            if (Utilities.IsValid(ballController) && ballController.enabled)
                ballController.requestedBallVelocity = currentVelocity;
            else
                ballController.OnBallHit(currentVelocity);
        }


        public void RespawnBall()
        {
            ballController.BallIsMoving = false;
            transform.position = originalPosition;
        }
    }
}