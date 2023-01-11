using UdonSharp;
using UnityEngine;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
#endif

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class GolfClubCollider : UdonSharpBehaviour
    {
        [Tooltip("Which golf club is this collider attached to?")]
        public GolfClub golfClub;
        [Tooltip("Which golf ball can this collider interact with?")]
        public GolfBallController golfBall;
        [Tooltip("A reference point on the club that this collider should try to stay attached to")]
        public Transform putterTarget;
        public bool experimentalCollisionDetection = false;

        [Range(1, 64), Tooltip("How many steps backwards in the colliders path can we go? (Used to average out hit velocity")]
        public int maxBacksteps = 3;
        /// <summary>
        /// Defines how much we need to scale down any hit forces (Matches the weight of the ball rigidbody)
        /// </summary>
        private float hitForceScale = 0.04593f;
        /// <summary>
        /// Tracks the path of the club head so we can work out an average velocity over several frames
        /// </summary>
        private Vector3[] lastPositions = new Vector3[16];
        /// <summary>
        /// Tracks how much time it has been since we recorded each position in the lastPositions array
        /// </summary>
        private float[] lastPositionTimes = new float[16];
        private BoxCollider golfClubHeadCollider;
        private Vector3 golfClubHeadColliderSize;
        /// <summary>
        /// Prevents collisions with the ball from ocurring as soon as the player arms the club
        /// </summary>
        private bool clubInsideBallCheck = false;
        private float clubInsideBallCheckTimer = 0f;
        /// <summary>
        /// Set to true when the player hits the ball (The force will be applied to the ball in the next FixedUpdate frame)
        /// </summary>
        private bool ballHasBeenHit = false;
        private Vector3 ballHasBeenHitOnVector = Vector3.zero;

        private Rigidbody myRigidbody = null;
        private BoxCollider myCollider = null;
        private SphereCollider ballCollider = null;

        void Start()
        {
            ResetPositionBuffers();
            golfClubHeadCollider = GetComponent<BoxCollider>();
            golfClubHeadColliderSize = golfClubHeadCollider.size;
        }

        public void OnClubArmed()
        {
            clubInsideBallCheck = true;
            clubInsideBallCheckTimer = 0f;

            myCollider = GetComponent<BoxCollider>();
            if (golfBall != null)
            {
                ballCollider = golfBall.GetComponent<SphereCollider>();
                hitForceScale = golfBall.GetComponent<Rigidbody>().mass;
            }

            myRigidbody = GetComponent<Rigidbody>();
            if (myRigidbody != null && putterTarget != null)
            {
                myRigidbody.MovePosition(putterTarget.position);
                myRigidbody.MoveRotation(putterTarget.rotation);
            }

            ResetPositionBuffers();
        }

        private void FixedUpdate()
        {
            if (clubInsideBallCheck)
            {
                if (ballCollider != null && myCollider != null)
                {
                    if (myCollider.bounds.Intersects(ballCollider.bounds))
                    {
                        // While the collider is intersecting with the ball, keep timer reset
                        clubInsideBallCheckTimer = 0f;
                    }
                    else
                    {
                        // Start counting time away from the ball
                        clubInsideBallCheckTimer += Time.fixedDeltaTime;

                        // Wait half a second before enabling collisions with the ball
                        if (clubInsideBallCheckTimer >= 0.5f)
                        {
                            Utils.Log(this, "Club head does not appear to be intersecting with the ball - collisions can happen now!");
                            clubInsideBallCheck = false;
                        }
                    }
                }
                Vector3 golfClubHeadColliderSizeNow = golfClubHeadColliderSize * putterTarget.transform.parent.parent.localScale.x;
                golfClubHeadCollider.size = golfClubHeadColliderSizeNow;
            }
            else
            {
                // Scale the club collider based on speed
                float speed = (transform.position - lastPositions[0]).magnitude / Time.deltaTime;
                Vector3 golfClubHeadColliderSizeNow = golfClubHeadColliderSize * putterTarget.transform.parent.parent.localScale.x;
                golfClubHeadCollider.size = Vector3.Lerp(golfClubHeadColliderSizeNow, golfClubHeadColliderSizeNow * 5, speed / 5);
            }

            Vector3 currentPos = transform.position;

            // Push current position onto buffers
            for (int i = lastPositions.Length; i-- > 0;)
            {
                if (i == 0)
                {
                    lastPositions[i] = currentPos;
                    lastPositionTimes[i] = 0;
                }
                else
                {
                    lastPositions[i] = lastPositions[i - 1];
                    lastPositionTimes[i] = lastPositionTimes[i - 1] + Time.fixedDeltaTime;
                }
            }

            // Make collider look in the direction of travel
            if (experimentalCollisionDetection)
            {
                if ((transform.position - lastPositions[1]).magnitude > 0.005f)
                {
                    transform.position = lastPositions[1];
                    transform.LookAt(putterTarget, Vector3.up);
                    transform.position = currentPos;
                }
            }

            // Attach this collider to the end of the club
            if (putterTarget != null && myRigidbody != null)
            {
                myRigidbody.MovePosition(putterTarget.position);
                if (!experimentalCollisionDetection)
                    myRigidbody.MoveRotation(putterTarget.rotation);
            }

            if (ballHasBeenHit)
            {
                // Extra precaution to stop hits while arming club while touching ball
                if (!clubInsideBallCheck)
                {
                    // Step backwards as far as we can and find the start of the swing and use that to calculate the average speed of the club head
                    int backBuffer = 0;
                    float maxDistance = 0f;
                    for (int i = 1; i < maxBacksteps; i++)
                    {
                        float thisDistance = Vector3.Distance(lastPositions[i], golfClub.ball.transform.position);
                        if (thisDistance > maxDistance)
                        {
                            maxDistance = thisDistance;
                            backBuffer = i;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Work out velocity
                    Vector3 distance = currentPos - lastPositions[backBuffer];
                    Vector3 velocity = distance / lastPositionTimes[backBuffer];

                    // Scale the velocity down so it makes sense, then multiply that by what the player wants
                    velocity = (velocity * hitForceScale) * (golfClub.forceMultiplier);

                    if (experimentalCollisionDetection)
                    {
                        // Hit ball in direction of this collider
                        velocity = velocity.magnitude * this.transform.forward;
                    }

                    OpenPutt openPutt = null;
                    if (golfClub != null && golfClub.playerManager != null && golfClub.playerManager.openPutt != null)
                        openPutt = golfClub.playerManager.openPutt;

                    // Mini golf usually works best when the ball stays on the floor initially
                    if (openPutt != null && openPutt.enableVerticalHits)
                        velocity.y = Mathf.Clamp(velocity.y, 0, 10f);
                    else
                        velocity.y = 0;

                    // Fix NaNs so we don't die
                    if (float.IsNaN(velocity.y))
                        velocity.y = 0;
                    if (float.IsNaN(velocity.x))
                        velocity.x = 0;
                    if (float.IsNaN(velocity.z))
                        velocity.z = 0;

                    // Register the hit with the ball
                    golfBall.OnBallHit(velocity);
                }

                // Consume the hit event
                ballHasBeenHit = false;
                ballHasBeenHitOnVector = Vector3.zero;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // We only care if this collided with the local players ball
            if (other.gameObject != golfBall.gameObject)
                return;

            // Wait for at least 1 frame after being enabled before registering a real collision
            if (clubInsideBallCheck)
            {
                Utils.Log(this, "Club head might be inside ball collider - ignoring this collision!");
                return;
            }

            ballHasBeenHit = true;
        }

        private void ResetPositionBuffers()
        {
            for (int i = 0; i < lastPositions.Length - 1; i++)
                lastPositions[i] = this.transform.position;
            for (int i = 0; i < lastPositionTimes.Length - 1; i++)
                lastPositionTimes[i] = 0f;
        }
    }
}