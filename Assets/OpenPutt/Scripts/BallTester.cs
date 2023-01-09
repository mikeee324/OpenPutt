using UdonSharp;
using UnityEngine;

namespace mikeee324.OpenPutt
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

        void Start()
        {
            originalPosition = this.transform.position;
        }

        void Update()
        {
            if (autoSpeedIncreases && resetTicker == -1f)
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
                    RespawnBall();

                    if (autoSpeedIncreases)
                    {
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
            if (ballController != null && ballController.enabled)
                ballController.requestedBallVelocity = currentVelocity;
            else
                GetComponent<Rigidbody>().AddForce(currentVelocity, ForceMode.Impulse);
        }


        public void RespawnBall()
        {
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            ballController.BallIsMoving = false;
            transform.position = originalPosition;
        }
    }
}