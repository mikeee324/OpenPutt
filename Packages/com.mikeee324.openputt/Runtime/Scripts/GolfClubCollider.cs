using UdonSharp;
using UnityEngine;
using System;
using Varneon.VUdon.ArrayExtensions;

namespace mikeee324.OpenPutt
{
    public enum ClubColliderVelocityType
    {
        SingleFrame = 0,
        SingleFrameSmoothed = 1,
        MultiFrameAverage = 2
    }
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(-1)]
    public class GolfClubCollider : UdonSharpBehaviour
    {
        [Tooltip("Reference to OpenPutt to skip a few steps")]
        public OpenPutt openPutt;
        [Tooltip("Which golf club is this collider attached to?")]
        public GolfClub golfClub;
        [Tooltip("Which golf ball can this collider interact with?")]
        public GolfBallController golfBall;
        [Tooltip("A reference point on the club that this collider should try to stay attached to")]
        public Transform putterTarget;
        [SerializeField]
        private SphereCollider ballCollider = null;
        [SerializeField]
        private Rigidbody myRigidbody = null;
        public BoxCollider golfClubHeadCollider;
        public AnimationCurve hitForceMultiplier;
        [Range(0, 8), Tooltip("How many frames to wait after a hit is registered before passing it to the ball (Helps with tiny hits to get a proper direction of travel)")]
        public int hitWaitFrames = 3;
        [Tooltip("Debug stuff: Used for trying various ways of getting a velocity for the ball after a hit")]
        public ClubColliderVelocityType velocityCalculationType = ClubColliderVelocityType.MultiFrameAverage;
        [Range(0, 15), Tooltip("The max number of frames the collider can go back for an average")]
        public int multiFrameAverageMaxBacksteps = 3;
        [Range(0f, 1f), Tooltip("How quickly the velocity smoothing will react to changes")]
        public float singleFrameSmoothFactor = 0.5f;
        [Tooltip("Smooths out the direction that the ball will travel over multiple frames")]
        public bool smoothedHitDirection = false;
        /// <summary>
        /// Tracks the path of the club head so we can work out an average velocity over several frames.<br/>
        /// MUST be the same length as lastPositionTimes
        /// </summary>
        private Vector3[] lastPositions = new Vector3[16];
        /// <summary>
        /// Tracks how much time it has been since we recorded each position in the lastPositions array<br/>
        /// MUST be the same length as lastPositions
        /// </summary>
        private float[] lastPositionTimes = new float[16];
        private Vector3 golfClubHeadColliderSize = new Vector3(0.0846f, 0.0892f, 0.022f);
        /// <summary>
        /// Scales the club collider based on its speed, x=height,y=width,z=thickness<br/>
        /// Currently just scaling height so people don't have to aim right at the floor
        /// </summary>
        public Vector3 golfClubHeadColliderMaxSize = new Vector3(3, 1, 1);
        /// <summary>
        /// Prevents collisions with the ball from ocurring as soon as the player arms the club
        /// </summary>
        private bool clubInsideBallCheck = false;
        private float clubInsideBallCheckTimer = 0f;
        /// <summary>
        /// Set to true when the player hits the ball (The force will be applied to the ball in the next FixedUpdate frame)
        /// </summary>
        private int framesSinceHit = -1;
        private AnimationCurve easeInOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vector3 FrameVelocity { get; set; }
        private Vector3 FrameVelocitySmoothed { get; set; }
        private Vector3 FrameVelocitySmoothedForScaling { get; set; }
        public float LastKnownHitVelocity { get; private set; }
        public string LastKnownHitType { get; private set; }
        private bool positionBufferWasJustReset = true;

        void Start()
        {
            LastKnownHitType = string.Empty;
            ResetPositionBuffers();

            if (golfClubHeadCollider == null)
                golfClubHeadCollider = GetComponent<BoxCollider>();

            if (myRigidbody == null)
                myRigidbody = GetComponent<Rigidbody>();

            if (golfBall != null && ballCollider == null)
                ballCollider = golfBall.GetComponent<SphereCollider>();

            if (hitForceMultiplier.length == 0)
            {
                hitForceMultiplier.AddKey(0, 1);
                hitForceMultiplier.AddKey(10, 2);
            }
        }

        public void OnClubArmed()
        {
            clubInsideBallCheck = true;
            clubInsideBallCheckTimer = 0.05f;

            MoveToClubWithoutVelocity();

            ResizeClubCollider();
        }

        private void ResizeClubCollider(float overrideSpeed = -1f, float overrideScale = -1f)
        {
            if (golfClubHeadCollider != null && putterTarget != null)
            {
                  float speed = FrameVelocitySmoothedForScaling.magnitude;
                  if (overrideSpeed != -1f)
                      speed = overrideSpeed;
                  if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                      speed = 0f;
                  speed = easeInOut.Evaluate(speed / 10f);

                if (easeInOut == null)
                    easeInOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

                Vector3 minSize = golfClubHeadColliderSize * putterTarget.transform.parent.parent.localScale.x;
                if (overrideScale > 0f)
                    minSize = golfClubHeadColliderSize * overrideScale;

                Vector3 maxSize = new Vector3(minSize.x * golfClubHeadColliderMaxSize.x, minSize.y * golfClubHeadColliderMaxSize.y, minSize.z * golfClubHeadColliderMaxSize.z);

                golfClubHeadCollider.size = Vector3.Lerp(minSize, maxSize, speed);
            }
        }


        private void FixedUpdate()
        {
            if (golfClub.ClubIsArmed)
            {
                golfBall.Wakeup();
            }

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

                        ResizeClubCollider(0);

                        // Wait a little before enabling collisions with the ball
                        if (clubInsideBallCheckTimer <= 0f)
                        {
                            Utils.Log(this, "Club head does not appear to be intersecting with the ball - collisions can happen now!");
                            clubInsideBallCheck = false;
                        }
                    }
                }
            }
            else
            {
                ResizeClubCollider();
            }

            Vector3 currentPos = myRigidbody.position;

            // Attach this collider to the end of the club
            if (putterTarget != null && myRigidbody != null)
            {
                Vector3 newPos = Vector3.MoveTowards(myRigidbody.position, putterTarget.position, 200f * Time.deltaTime);
                Quaternion newRot = Quaternion.RotateTowards(myRigidbody.rotation, putterTarget.rotation, 200f * Time.deltaTime);
                myRigidbody.MovePosition(newPos);
                myRigidbody.MoveRotation(newRot);
                // myRigidbody.MovePosition(putterTarget.position);
                // myRigidbody.MoveRotation(putterTarget.rotation);
            }

            // Log last known velocity if it's not totally 0
            Vector3 currFrameVelocity = (currentPos - lastPositions[0]) / Time.deltaTime;
            if (!positionBufferWasJustReset && currFrameVelocity != Vector3.zero)
            {
                FrameVelocity = currFrameVelocity;
                FrameVelocitySmoothed = Vector3.Lerp(FrameVelocitySmoothed, currFrameVelocity, 0.8f);
                FrameVelocitySmoothedForScaling = Vector3.Lerp(FrameVelocitySmoothedForScaling, currFrameVelocity, 0.2f);
            }

            if (positionBufferWasJustReset || currFrameVelocity.magnitude > 0.001f)
            {
                // Push current position onto buffers
                lastPositions = lastPositions.Push(currentPos);
                lastPositionTimes = lastPositionTimes.Push(Time.deltaTime);
                for (int i = 1; i < lastPositions.Length; i++)
                    lastPositionTimes[i] += Time.deltaTime;
                positionBufferWasJustReset = false;
            }

            // If the ball has been hit
            if (framesSinceHit != -1)
            {
                framesSinceHit += 1;

                // If we have waited for enough frames after the hit (helps with people starting the hit from mm away from the ball)
                if (framesSinceHit >= hitWaitFrames)
                    HandleBallHit();
            }

            if (!positionBufferWasJustReset && !clubInsideBallCheck && framesSinceHit == -1)
            {
                // SweepTest hopefully helps with picking up collisions that we might have missed normally
                if (FrameVelocity.magnitude > 0.005f && myRigidbody.SweepTest(FrameVelocity, out RaycastHit hit, FrameVelocity.magnitude * Time.deltaTime))
                {
                    // We only care if this collided with the local players ball
                    if (hit.collider != null && hit.collider.gameObject == golfBall.gameObject)
                    {
                        Utils.Log(this, "SweepTest triggered a hit!");
                        LastKnownHitType = "(Sweep)";
                        framesSinceHit = 0;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ignore extra hits to the ball until we have processed the first
            if (framesSinceHit >= 0)
                return;

            // We only care if this collided with the local players ball
            if (other.gameObject != golfBall.gameObject)
                return;

            // Wait for at least 1 frame after being enabled before registering a real collision
            if (clubInsideBallCheck)
            {
                Utils.Log(this, "Club head might be inside ball collider - ignoring this collision!");
                return;
            }
            Utils.Log(this, "OnTriggerEnter triggered a hit!");
            LastKnownHitType = "(Trigger)";
            framesSinceHit = 0;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Ignore extra hits to the ball until we have processed the first
            if (framesSinceHit >= 0)
                return;

            // We only care if this collided with the local players ball
            if (collision == null || collision.rigidbody == null || collision.rigidbody.gameObject != golfBall.gameObject)
                return;

            // Wait for at least 1 frame after being enabled before registering a real collision
            if (clubInsideBallCheck)
            {
                Utils.Log(this, "Club head might be inside ball collider - ignoring this collision!");
                return;
            }
            Utils.Log(this, "OnCollisionEnter triggered a hit!");
            LastKnownHitType = "(Collision)";
            framesSinceHit = 0;
        }

        private void HandleBallHit()
        {
            Vector3 directionOfTravel = Vector3.zero;
            float velocityMagnitude = 0f;

            switch (velocityCalculationType)
            {
                case ClubColliderVelocityType.SingleFrame:
                    directionOfTravel = FrameVelocity.normalized;
                    velocityMagnitude = FrameVelocity.magnitude;

                    // Use the smmothed collider direction if we are told to
                    if (smoothedHitDirection && lastPositions[3] != Vector3.zero)
                        directionOfTravel = (lastPositions[0] - lastPositions[3]).normalized;
                    break;
                case ClubColliderVelocityType.SingleFrameSmoothed:
                    directionOfTravel = FrameVelocitySmoothed.normalized;
                    velocityMagnitude = FrameVelocitySmoothed.magnitude;

                    // Use the smmothed collider direction if we are told to
                    if (smoothedHitDirection && lastPositions[3] != Vector3.zero)
                        directionOfTravel = (lastPositions[0] - lastPositions[3]).normalized;
                    break;
                case ClubColliderVelocityType.MultiFrameAverage:
                    {
                        // Grab positons/time taken
                        Vector3 latestPos = lastPositions[0];
                        Vector3 oldestPos = lastPositions[1];
                        float timeTaken = lastPositionTimes[0];

                        // If we want to take an average velocity over the past few frames
                        if (multiFrameAverageMaxBacksteps > 0)
                        {
                            float furthestDistance = Vector3.Distance(latestPos, oldestPos);
                            int maxBacksteps = Math.Min(multiFrameAverageMaxBacksteps, lastPositions.Length);
                            for (int currentBackstep = 1; currentBackstep < maxBacksteps; currentBackstep++)
                            {
                                // Ignore any empty previous positions
                                if (lastPositions[currentBackstep] == Vector3.zero)
                                    continue;

                                float thisDistance = Vector3.Distance(latestPos, lastPositions[currentBackstep]);
                                if (thisDistance > furthestDistance)
                                {
                                    oldestPos = lastPositions[currentBackstep];
                                    timeTaken = lastPositionTimes[currentBackstep];
                                    furthestDistance = Vector3.Distance(latestPos, oldestPos);
                                    continue;
                                }

                                // We found the furthest backstep - break out and use that as the start point of the swing
                                break;
                            }
                        }

                        if (latestPos == Vector3.zero || oldestPos == Vector3.zero)
                        {
                            Utils.LogError(this, "Cannot handle ball hit as start/end pos was a Vector3.zero! Will wait 1 more frame..");
                            framesSinceHit -= 1;
                            return;
                        }

                        Vector3 newVel = (latestPos - oldestPos) / timeTaken;
                        directionOfTravel = newVel.normalized;
                        velocityMagnitude = newVel.magnitude;

                        // Don't really need this here? - Should already be averaged out - Use the smmothed collider direction if we are told to
                        //if (smoothedHitDirection && lastPositions[3] != Vector3.zero)
                        //    directionOfTravel = (lastPositions[0] - lastPositions[3]).normalized;
                        break;
                    }
            }

            // Scale the velocity back up a bit
            velocityMagnitude *= hitForceMultiplier.Evaluate(velocityMagnitude);

            // Apply the players final hit force multiplier
            velocityMagnitude *= golfClub.forceMultiplier;

            // Only clamp hit speed if they player is on a normal course
            bool shouldClampSpeed = golfClub.playerManager == null || (golfClub.playerManager.CurrentCourse != null && !golfClub.playerManager.CurrentCourse.drivingRangeMode);

            // Clamp hit speed
            if (shouldClampSpeed && velocityMagnitude > golfBall.BallMaxSpeed)
            {
                velocityMagnitude = golfBall.BallMaxSpeed;
                Utils.Log(this, $"Ball hit velocity was clamped to {velocityMagnitude}");
            }

            // Put the direction and magnitude back together
            Vector3 velocity = directionOfTravel * velocityMagnitude;

            // OpenPutt is null and we can find it
            if (openPutt == null && golfClub != null && golfClub.playerManager != null && golfClub.playerManager.openPutt != null)
                openPutt = golfClub.playerManager.openPutt;

            // Mini golf usually works best when the ball stays on the floor initially
            if (openPutt.enableVerticalHits)
                velocity.y = Mathf.Clamp(velocity.y, 0, golfBall.BallMaxSpeed);
            else
                velocity.y = 0;

            // Fix NaNs so we don't die
            if (float.IsNaN(velocity.y))
                velocity.y = 0;
            if (float.IsNaN(velocity.x))
                velocity.x = 0;
            if (float.IsNaN(velocity.z))
                velocity.z = 0;

            Utils.Log(this, $"Ball has been hit! NewBallVelocity({velocity.magnitude}) DirectionOfTravel({directionOfTravel})");

            LastKnownHitVelocity = velocity.magnitude;

            // Register the hit with the ball
            golfBall.OnBallHit(velocity);

            // Disable club collision for a short while
            clubInsideBallCheck = true;
            clubInsideBallCheckTimer = 0f;

            // Consume the hit event
            framesSinceHit = -1;
        }

        private void ResetPositionBuffers()
        {
            positionBufferWasJustReset = true;

            FrameVelocity = Vector3.zero;
            FrameVelocitySmoothed = Vector3.zero;
            FrameVelocitySmoothedForScaling = Vector3.zero;

            for (int i = 0; i < lastPositions.Length; i++)
            {
                lastPositions[i] = this.transform.position;
                lastPositionTimes[i] = 0f;
            }
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

            ResetPositionBuffers();
        }
    }
}