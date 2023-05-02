using UdonSharp;
using UnityEngine;
using System;
using Varneon.VUdon.ArrayExtensions;

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
        [SerializeField] private Rigidbody myRigidbody = null;
        public BoxCollider golfClubHeadCollider;
        public AnimationCurve hitForceMultiplier;
        [Tooltip("Not so experimental now.. cos i like it more")]
        /// <summary>
        /// The collider for the putter follows the club head instead of just being attached to it.<br/>
        /// Allows us to grab the direction of travel and use that to direct where the ball goes when hit.
        /// </summary>
        public bool experimentalCollisionDetection = false;
        [Range(0, 14)]
        public int hitBackstepOffset = 1;
        [Range(1, 15)]
        public int hitMaxBacksteps = 3;
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
        private Vector3 golfClubHeadColliderSize = new Vector3(0.0846f, 0.0892f, 0.022f);
        /// <summary>
        /// Prevents collisions with the ball from ocurring as soon as the player arms the club
        /// </summary>
        private bool clubInsideBallCheck = false;
        private float clubInsideBallCheckTimer = 0f;
        /// <summary>
        /// Set to true when the player hits the ball (The force will be applied to the ball in the next FixedUpdate frame)
        /// </summary>
        private bool ballHasBeenHit = false;
        private SphereCollider ballCollider = null;

        void Start()
        {
            ResetPositionBuffers();

            if (golfClubHeadCollider == null)
                golfClubHeadCollider = GetComponent<BoxCollider>();

            if (myRigidbody == null)
                myRigidbody = GetComponent<Rigidbody>();

            if (hitForceMultiplier.length == 0)
            {
                hitForceMultiplier.AddKey(0, 1);
                hitForceMultiplier.AddKey(10, 2);
            }
        }

        public void OnClubArmed()
        {
            clubInsideBallCheck = true;
            clubInsideBallCheckTimer = 0.1f;

            if (golfBall != null)
            {
                ballCollider = golfBall.GetComponent<SphereCollider>();
                hitForceScale = golfBall.GetComponent<Rigidbody>().mass;
            }

            MoveToClubWithoutVelocity();

            ResetPositionBuffers();

            ResizeClubCollider();
        }

        private void ResizeClubCollider(float overrideSpeed = -1f, float overrideScale = -1f)
        {
            if (golfClubHeadCollider != null && putterTarget != null)
            {
                int averageSpeedStep = (int)(lastPositions.Length * 0.5f);
                float speed = (transform.position - lastPositions[averageSpeedStep]).magnitude / lastPositionTimes[averageSpeedStep];
                if (overrideSpeed != -1f)
                    speed = overrideSpeed;

                Vector3 golfClubHeadColliderSizeNow = golfClubHeadColliderSize * putterTarget.transform.parent.parent.localScale.x;
                if (overrideScale > 0f)
                    golfClubHeadColliderSizeNow = golfClubHeadColliderSize * overrideScale;
                golfClubHeadCollider.size = Vector3.Lerp(golfClubHeadColliderSizeNow, golfClubHeadColliderSizeNow * 5, speed * .2f);
            }
        }

        private void FixedUpdate()
        {
            if (clubInsideBallCheck)
            {
                if (golfClub != null && ballCollider != null && golfClubHeadCollider != null)
                {
                    if (golfClubHeadCollider.bounds.Intersects(ballCollider.bounds))
                    {
                        // While the collider is intersecting with the ball, keep timer reset
                        clubInsideBallCheckTimer = 0.05f;
                        //  if (golfClub != null)
                        //   golfClub.DisableClubColliderFor(1f);
                    }
                    else
                    {
                        // Start counting time away from the ball
                        clubInsideBallCheckTimer -= Time.fixedDeltaTime;

                        // Wait a little before enabling collisions with the ball
                        if (clubInsideBallCheckTimer <= 0f)
                        {
                            Utils.Log(this, "Club head does not appear to be intersecting with the ball - collisions can happen now!");
                            clubInsideBallCheck = false;
                        }
                    }
                }
                ResizeClubCollider(0);
            }
            else
            {
                ResizeClubCollider();
            }

            Vector3 currentPos = transform.position;

            // Make collider look in the direction of travel
            if (experimentalCollisionDetection)
            {
                Vector3 directionOfTravel = currentPos - lastPositions[3];
                if (directionOfTravel.magnitude > 0.005f)
                    transform.rotation = Quaternion.LookRotation(directionOfTravel, Vector3.up);
            }

            // Attach this collider to the end of the club
            if (putterTarget != null && myRigidbody != null)
            {
                myRigidbody.MovePosition(putterTarget.position);
                if (!experimentalCollisionDetection || clubInsideBallCheck)
                    myRigidbody.MoveRotation(putterTarget.rotation);
            }

            if (ballHasBeenHit)
            {
                // Extra precaution to stop hits while arming club while touching ball
                if (!clubInsideBallCheck)
                {
                    // Grab positons/time taken
                    Vector3 latestPos = currentPos;
                    Vector3 oldestPos = lastPositions[0];
                    float timeTaken = lastPositionTimes[0];

                    if (hitBackstepOffset > 0)
                    {
                        latestPos = lastPositions[hitBackstepOffset];
                        oldestPos = lastPositions[hitBackstepOffset + 1];
                        timeTaken = lastPositionTimes[hitBackstepOffset + 1] - lastPositionTimes[hitBackstepOffset];

                        for (int currentBackstep = hitBackstepOffset; currentBackstep < hitBackstepOffset + hitMaxBacksteps; currentBackstep++)
                        {
                            float thisDistance = Vector3.Distance(latestPos, lastPositions[currentBackstep]);
                            float currentDistance = Vector3.Distance(latestPos, oldestPos);
                            if (thisDistance > currentDistance)
                            {
                                oldestPos = lastPositions[currentBackstep];
                                timeTaken = lastPositionTimes[currentBackstep] - lastPositionTimes[hitBackstepOffset];
                                continue;
                            }

                            break;
                        }
                    }

                    // Work out velocity
                    Vector3 velocity = (latestPos - oldestPos) / timeTaken;

                    // Scale the velocity down to match the mass of the ball
                    velocity *= hitForceScale;

                    // Scale the velocity back up a bit
                    velocity *= hitForceMultiplier.Evaluate(velocity.magnitude);

                    // Apply the players final hit force multiplier
                    velocity *= golfClub.forceMultiplier;

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

                    // Disable club collision for a short while
                    clubInsideBallCheck = true;
                    clubInsideBallCheckTimer = 0f;
                }

                // Consume the hit event
                ballHasBeenHit = false;
            }

            int originalSize = lastPositions.Length;
            // Push current position onto buffers
            lastPositions = lastPositions.Insert(0, currentPos).Resize(originalSize);
            lastPositionTimes = lastPositionTimes.Insert(0, Time.fixedDeltaTime).Resize(originalSize);
            for (int i = 1; i < lastPositions.Length; i++)
                lastPositionTimes[i] += Time.fixedDeltaTime;
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

        public void MoveToClubWithoutVelocity()
        {
            if (myRigidbody != null && putterTarget != null)
            {
                myRigidbody.position = putterTarget.position;
                myRigidbody.rotation = putterTarget.rotation;
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;
            }
        }
    }
}