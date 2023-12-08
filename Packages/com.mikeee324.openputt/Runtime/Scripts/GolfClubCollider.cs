using UdonSharp;
using UnityEngine;
using System;
using Varneon.VUdon.ArrayExtensions;

namespace mikeee324.OpenPutt
{
    /// <summary>
    /// Different ways of tracking the vlocity of the club collider (Used for experimenting)
    /// </summary>
    public enum ClubColliderVelocityType
    {
        SingleFrame = 0,
        SingleFrameSmoothed = 1,
        MultiFrameAverage = 2
    }

    /// <summary>
    /// This is a rigidbody that follows the club head around and actually triggers the ball to move on collision<br/>
    /// This script must run after the main GolfClub script in order to function correctly. (Hence the DefaultExecutionOrder)
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(55)]
    public class GolfClubCollider : UdonSharpBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to OpenPutt to skip a few steps")]
        public OpenPutt openPutt;
        [Tooltip("Which golf club is this collider attached to?")]
        public GolfClub golfClub;
        [Tooltip("Which golf ball can this collider interact with?")]
        public GolfBallController golfBall;
        [Tooltip("A reference point on the club that this collider should try to stay attached to")]
        public BoxCollider putterTarget;
        [Tooltip("Can be used for funny things like attaching the collider to a separate object (The size of the collider will stay the same as the club head though)")]
        public Transform targetOverride;
        [SerializeField]
        private SphereCollider ballCollider = null;
        [SerializeField]
        private Rigidbody myRigidbody = null;
        public BoxCollider golfClubHeadCollider;

        [Header("Club Head References / Settings")]
        [Tooltip("Use this to rotate this collider to the match the club head collider (if it's the same already leave as 0,0,0")]
        public Vector3 referenceClubHeadColliderRotationOffset = new Vector3(0, -90, 0);
        [Range(5f, 20f), Tooltip("How fast the club head needs to be travelling to reach the MaxSpeedScale below")]
        public float maxSpeedForScaling = 10f;
        /// <summary>
        /// Scales the club collider based on its speed, x=height,y=width,z=thickness<br/>
        /// Currently just scaling height so people don't have to aim right at the floor
        /// </summary>
        [Tooltip("How much the collider can be scaled up depending on its current velocity")]
        public Vector3 maxSpeedScale = new Vector3(3, 1, 1);

        [Header("Settings")]
        [Range(0, 8), Tooltip("How many frames to wait after a hit is registered before passing it to the ball (Helps with tiny hits to get a proper direction of travel)")]
        public int hitWaitFrames = 3;
        public AnimationCurve hitForceMultiplier;

        [Header("Velocity / Direction Tracking Settings")]
        [Tooltip("Debug stuff: Used for trying various ways of getting a velocity for the ball after a hit")]
        public ClubColliderVelocityType velocityCalculationType = ClubColliderVelocityType.SingleFrameSmoothed;
        [Range(0, 15), Tooltip("The max number of frames the collider can go back for an average")]
        public int multiFrameAverageMaxBacksteps = 3;
        [Range(0f, 1f), Tooltip("How quickly the velocity smoothing will react to changes")]
        public float singleFrameSmoothFactor = 0.8f;
        [Tooltip("Smooths out the direction that the ball will travel over multiple frames")]
        public bool smoothedHitDirection = false;
        /// <summary>
        /// Performs a bit of smoothing while following the club head - off for now because i'm not sure if it's needed
        /// </summary>
        [SerializeField, Tooltip("Should this collider smoothly follow the club head or snap to it each frame (Might help with tracking very fast hits)")]
        public bool smoothFollowClubHead = true; // Maybe helps with fast hit detection
        [Range(0, 0.99f), Tooltip("Controls how strong the Smooth Follow Clubhead option is. (Not entirely sure what the correct value is yet - 1 has bad results at low fps though)")]
        public float followStrength = .6f;
        [Range(0, 0.99f), Tooltip("Controls how strong the rotation of the Smooth Follow Clubhead option is. (Not entirely sure what the correct value is yet)")]
        public float followRotationStrength = 0.99f;
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
        /// <summary>
        /// Set to true when the player hits the ball (The force will be applied to the ball in the next FixedUpdate frame)
        /// </summary>
        private int framesSinceHit = -1;
        private AnimationCurve easeInOut = AnimationCurve.EaseInOut(0, 0, 1, 1);
        /// <summary>
        /// Tracks the last frames velocity absolutely (filtering out Vector3.zero and small moves)
        /// </summary>
        private Vector3 FrameVelocity { get; set; }
        /// <summary>
        /// Tracks the last frames velocity with some smoothing (filtering out Vector3.zero and small moves)
        /// </summary>
        private Vector3 FrameVelocitySmoothed { get; set; }
        /// <summary>
        /// Tracks the velocity with extra smoothing and is used for scaling this collider up and down (filtering out Vector3.zero and small moves)
        /// </summary>
        private Vector3 FrameVelocitySmoothedForScaling { get; set; }
        /// <summary>
        /// Logs the velocity magnitude of the last ball hit
        /// </summary>
        public float LastKnownHitVelocity { get; private set; }
        /// <summary>
        /// Logs kind of collision occured that trigger a ball hit (trigger,sweep,collision)
        /// </summary>
        public string LastKnownHitType { get; private set; }
        private bool positionBufferWasJustReset = true;
        private int framesSinceClubArmed = -1;
        private bool clubIsTouchingBall = false;
        private bool CanTrackHitsAndVel => framesSinceClubArmed > 5;
        private Vector3 CurrentPositionTarget
        {
            get
            {
                if (targetOverride != null && targetOverride.gameObject.activeSelf)
                    return targetOverride.position;
                return putterTarget.transform.position;
            }
        }
        private Quaternion CurrentRotationTarget
        {
            get
            {
                if (targetOverride != null && targetOverride.gameObject.activeSelf)
                    return targetOverride.rotation;
                return putterTarget.transform.rotation;
            }
        }

        void Start()
        {
            LastKnownHitType = string.Empty;

            MoveToClubWithoutVelocity();

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

        private void OnEnable()
        {
            MoveToClubWithoutVelocity();
        }

        private void OnDisable()
        {
            MoveToClubWithoutVelocity();
        }

        public void OnClubArmed()
        {
            framesSinceClubArmed = 0;

            MoveToClubWithoutVelocity();

            ResizeClubCollider(0);
        }

        /// <summary>
        /// Resizes the collider so that it is the same scale as the club head. Also allows for scaling the collider up while the collider is travelling fast to help pick up collisisons.<br/>
        /// </summary>
        /// <param name="overrideSpeed"></param>
        private void ResizeClubCollider(float overrideSpeed = -1f)
        {
            if (golfClubHeadCollider != null)
            {
                float speed = FrameVelocitySmoothedForScaling.magnitude;
                if (overrideSpeed != -1f)
                    speed = overrideSpeed;

                if (framesSinceHit >= 0)
                    speed = 0f;

                if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                    speed = 0f;

                if (easeInOut == null)
                    easeInOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

                if (maxSpeedForScaling <= 0f)
                    maxSpeedForScaling = 10f;

                speed = easeInOut.Evaluate(speed / maxSpeedForScaling);

                Vector3 colliderSize = targetOverride == null ? putterTarget.size : new Vector3(.05f, .3f, .1f);
                Vector3 colldierCenter = targetOverride == null ? putterTarget.center : Vector3.zero;

                Vector3 minSize = Vector3.Scale(colliderSize, putterTarget.transform.lossyScale);
                Vector3 maxSize = Vector3.Scale(minSize, maxSpeedScale);

                golfClubHeadCollider.center = colldierCenter;
                golfClubHeadCollider.size = Vector3.Lerp(minSize, maxSize, speed);
            }
        }


        private void FixedUpdate()
        {
            if (!gameObject.activeSelf)
                return;

            if (myRigidbody == null || putterTarget == null)
            {
                this.enabled = false;
                return;
            }

            myRigidbody.WakeUp();

            if (golfClub.ClubIsArmed)
            {
                golfBall.Wakeup();
                framesSinceClubArmed += 1;
            }

            if (!CanTrackHitsAndVel)
            {
                MoveToClubWithoutVelocity(resetBuffers: false);

                ResizeClubCollider(0);

                return;
            }

            if (smoothFollowClubHead && !positionBufferWasJustReset)
            {
                // Smooth follow uses Rigidbody.velocity and angularVelocity to follow the target instead of MovePosition/MoveRotation
                // This seems to allow the physics engine to register collisions better and we get less spurious velocities of 0 while the object is moving
                // https://gist.github.com/MattRix/bd0ba767f75906b7f86cb8214a60972e

                // For this to work we need a non-kinematic rigidbody with 0 drag
                if (myRigidbody.isKinematic)
                    myRigidbody.isKinematic = false;

                // Work out velocity needed to reach the target position
                Vector3 deltaPos = CurrentPositionTarget - myRigidbody.position;
                Vector3 newVel = 1f / Time.deltaTime * deltaPos * Mathf.Pow(followStrength, 90f * Time.deltaTime);
                if (newVel.magnitude > 0f)
                    myRigidbody.velocity = newVel;

                // Work out the angularVelocity needed to reach the same rotation as the target
                Quaternion deltaRot = CurrentRotationTarget * Quaternion.Inverse(transform.rotation);
                deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180.0f)
                    angle -= 360.0f;
                if (angle != 0 && !myRigidbody.isKinematic)
                    myRigidbody.angularVelocity = (1f / Time.fixedDeltaTime * angle * axis * 0.01745329251994f * Mathf.Pow(followRotationStrength, 90f * Time.fixedDeltaTime));
            }
            else
            {
                // Seems to work best when the RigidBody is kinematic
                if (!myRigidbody.isKinematic)
                    myRigidbody.isKinematic = true;

                // Just use MovePosition/MoveRotation
                myRigidbody.MovePosition(CurrentPositionTarget);
                myRigidbody.MoveRotation(CurrentRotationTarget * Quaternion.Euler(referenceClubHeadColliderRotationOffset));
            }

            Vector3 currentPos = myRigidbody.position;
            Vector3 currFrameVelocity = (currentPos - lastPositions[0]) / Time.deltaTime;

            //if (golfClub.playerManager == null|| golfClub.playerManager.openPutt == null || golfClub.playerManager.openPutt || golfClub.playerManager.openPutt.debugMode)
            //    Utils.Log("Vel", $"CurrentFrameVel: {currFrameVelocity.magnitude} RB.vel: {myRigidbody.velocity.magnitude} LastGoodFrameVel: {FrameVelocity.magnitude}");

            // Store velocity for this frame if it isn't all 0
            if (!positionBufferWasJustReset && currFrameVelocity != Vector3.zero)
            {
                FrameVelocity = currFrameVelocity;
                FrameVelocitySmoothed = Vector3.Lerp(FrameVelocitySmoothed, currFrameVelocity, singleFrameSmoothFactor);
                FrameVelocitySmoothedForScaling = Vector3.Lerp(FrameVelocitySmoothedForScaling, currFrameVelocity, .2f);
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
                {
                    // Consume the hit event
                    framesSinceHit = -1;

                    // Send the velocity to the ball
                    HandleBallHit();
                }
            }

            if (!positionBufferWasJustReset && !clubIsTouchingBall && framesSinceHit == -1)
            {
                // Perform a sweep test to see if we'll be hitting the ball in the next frame
                if (FrameVelocity.magnitude > 0.005f && myRigidbody.SweepTest(FrameVelocity, out RaycastHit hit, FrameVelocity.magnitude * Time.deltaTime))
                {
                    // We only care if this collided with the local players ball
                    if (hit.collider != null && hit.collider.gameObject == golfBall.gameObject)
                    {
                        LastKnownHitType = "(Sweep)";
                        framesSinceHit = 0;
                    }
                }
            }

            ResizeClubCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ignore extra hits to the ball until we have processed the first
            if (framesSinceHit >= 0 || positionBufferWasJustReset)
                return;

            // We only care if this collided with the local players ball
            if (other.gameObject != golfBall.gameObject)
                return;

            // Stops players from launching the ball by placing the club inside the ball and arming it
            if (!CanTrackHitsAndVel)
            {
                if (golfClub.playerManager.openPutt.debugMode)
                    Utils.Log(this, "Player armed the club and instantly hit the ball (trigger).. ignoring this collision");
                return;
            }

            clubIsTouchingBall = true;

            LastKnownHitType = "(Trigger)";
            framesSinceHit = 0;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject != golfBall.gameObject)
                return;

            clubIsTouchingBall = false;
            if (golfClub.playerManager.openPutt.debugMode)
                Utils.Log(this, "Club head is no longer in contact with the ball. Allowing collisions!");
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Ignore extra hits to the ball until we have processed the first
            if (framesSinceHit >= 0 || positionBufferWasJustReset)
                return;

            // We only care if this collided with the local players ball
            if (collision == null || collision.rigidbody == null || collision.rigidbody.gameObject != golfBall.gameObject)
                return;

            // Stops players from launching the ball by placing the club inside the ball and arming it
            if (!CanTrackHitsAndVel)
            {
                if (golfClub.playerManager.openPutt.debugMode)
                    Utils.Log(this, "Player armed the club and instantly hit the ball (collision).. ignoring this collision");
                return;
            }

            clubIsTouchingBall = true;

            LastKnownHitType = "(Collision)";
            framesSinceHit = 0;
        }


        private void OnCollisionExit(Collision collision)
        {
            if (collision == null || collision.rigidbody == null || collision.rigidbody.gameObject != golfBall.gameObject)
                return;

            clubIsTouchingBall = false;
            if (golfClub.playerManager.openPutt.debugMode)
                Utils.Log(this, "Club head is no longer in contact with the ball. Allowing collisions!");
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
                    if (smoothedHitDirection)
                        for (int i = 0; i < 3; i++)
                            if (lastPositions[i] != Vector3.zero)
                                directionOfTravel = (lastPositions[0] - lastPositions[i]).normalized;
                    break;
                case ClubColliderVelocityType.SingleFrameSmoothed:
                    directionOfTravel = FrameVelocitySmoothed.normalized;
                    velocityMagnitude = FrameVelocitySmoothed.magnitude;

                    // Use the smmothed collider direction if we are told to
                    if (smoothedHitDirection)
                        for (int i = 0; i < 3; i++)
                            if (lastPositions[i] != Vector3.zero)
                                directionOfTravel = (lastPositions[0] - lastPositions[i]).normalized;
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
                                    break;

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
                            if (golfClub.playerManager.openPutt.debugMode)
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
                if (golfClub.playerManager.openPutt.debugMode)
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

            //if (velocity.magnitude < golfBall.minBallSpeed)
            //    return;

            if (golfClub.playerManager.openPutt.debugMode)
                Utils.Log(this, $"Ball has been hit! Velocity:{velocity.magnitude}{LastKnownHitType} DirectionOfTravel({directionOfTravel})");

            LastKnownHitVelocity = velocity.magnitude;

            // Register the hit with the ball
            golfBall.OnBallHit(velocity);
        }

        private void ResetPositionBuffers()
        {
            positionBufferWasJustReset = true;

            FrameVelocity = Vector3.zero;
            FrameVelocitySmoothed = Vector3.zero;
            FrameVelocitySmoothedForScaling = Vector3.zero;

            for (int i = 0; i < lastPositions.Length; i++)
            {
                lastPositions[i] = Vector3.zero;
                lastPositionTimes[i] = 99;
            }
        }

        public void MoveToClubWithoutVelocity(bool resetBuffers = true)
        {
            if (myRigidbody != null && putterTarget != null)
            {
                myRigidbody.position = CurrentPositionTarget;
                myRigidbody.rotation = CurrentRotationTarget * Quaternion.Euler(referenceClubHeadColliderRotationOffset);
                if (!myRigidbody.isKinematic)
                {
                    myRigidbody.velocity = Vector3.zero;
                    myRigidbody.angularVelocity = Vector3.zero;
                }
            }

            if (resetBuffers)
                ResetPositionBuffers();
        }
    }
}