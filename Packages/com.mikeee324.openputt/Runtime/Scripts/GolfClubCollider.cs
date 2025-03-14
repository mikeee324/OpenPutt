using System;
using com.dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// This is a rigidbody that follows the club head around and actually triggers the ball to move on collision<br/>
    /// This script must run after the main GolfClub script in order to function correctly. (Hence the DefaultExecutionOrder)
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(55)]
    public class GolfClubCollider : UdonSharpBehaviour
    {
        [Header("References")] [Tooltip("Reference to OpenPutt to skip a few steps")]
        public OpenPutt openPutt;

        public PlayerManager playerManager;

        [Tooltip("Which golf club is this collider attached to?")]
        public GolfClub golfClub;

        [Tooltip("Which golf ball can this collider interact with?")]
        public GolfBallController golfBall;

        [Tooltip("A reference point on the club that this collider should try to stay attached to")]
        public BoxCollider putterTarget;

        [Tooltip("Can be used for funny things like attaching the collider to a separate object (The size of the collider will stay the same as the club head though)")]
        public Transform targetOverride;

        [SerializeField]
        private SphereCollider ballCollider;

        [SerializeField]
        private Rigidbody myRigidbody;

        public BoxCollider golfClubHeadCollider;

        public GolfClubColliderVisualiser visual;

        [Header("Club Head References / Settings")] [Tooltip("Use this to rotate this collider to the match the club head collider (if it's the same already leave as 0,0,0")]
        public Vector3 referenceClubHeadColliderRotationOffset = new Vector3(0, -90, 0);

        [Range(5f, 20f), Tooltip("How fast the club head needs to be travelling to reach the MaxSpeedScale below")]
        public float maxSpeedForScaling = 10f;

        /// <summary>
        /// Scales the club collider based on its speed, x=height,y=width,z=thickness<br/>
        /// Currently just scaling height so people don't have to aim right at the floor
        /// </summary>
        [Tooltip("How much the collider can be scaled up depending on its current velocity")]
        public Vector3 maxSpeedScale = new Vector3(3, 1, 1);

        [Header("Settings")] [Range(0, 8), Tooltip("How many frames to wait after a hit is registered before passing it to the ball (Helps with tiny hits to get a proper direction of travel)")]
        public int hitWaitFrames = 3;

        public AnimationCurve hitForceMultiplier;

        public CollisionDetectionMode collisionType = CollisionDetectionMode.Continuous;
        public bool enableSweepTests = true;

        [Range(0, 15), Tooltip("The max number of frames the collider can go back for an average")]
        public int multiFrameAverageMaxBacksteps = 3;

        [Range(0f, 1f), Tooltip("How quickly the velocity smoothing will react to changes")]
        public float singleFrameSmoothFactor = 0.8f;

        [Tooltip("Smooths out the direction that the ball will travel over multiple frames")]
        public bool smoothedHitDirection;

        /// <summary>
        /// Performs a bit of smoothing while following the club head - off for now because i'm not sure if it's needed
        /// </summary>
        [SerializeField, Tooltip("Should this collider smoothly follow the club head or snap to it each frame (Might help with tracking very fast hits)")]
        public bool smoothFollowClubHead = true; // Maybe helps with fast hit detection

        [Range(0, 0.99f), Tooltip("Controls how strong the Smooth Follow Clubhead option is. (Not entirely sure what the correct value is yet - 1 has bad results at low fps though)")]
        public float followStrength = .9f;

        [Range(0, 0.99f), Tooltip("Controls how strong the rotation of the Smooth Follow Clubhead option is. (Not entirely sure what the correct value is yet)")]
        public float followRotationStrength = 0.99f;

        [Tooltip("Makes the club head take the direction of the club head into account when computing a hit (Slower hits mean the face direction matters more)")]
        public bool useClubHeadDirection;

        public AnimationCurve clubHeadDirectionInfluence;

        private int bufferIndex = 0;

        /// <summary>
        /// Tracks the path of the club head so we can work out an average velocity over several frames.<br/>
        /// MUST be the same length as lastPositionTimes
        /// </summary>
        private Vector3[] lastPositions = new Vector3[16];

        /// <summary>
        /// Tracks the path of the club head so we can work out an average velocity over several frames.<br/>
        /// MUST be the same length as lastPositionTimes
        /// </summary>
        private Quaternion[] lastPositionRotations = new Quaternion[16];

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
        public Vector3 FrameVelocitySmoothed { get; private set; }

        /// <summary>
        /// Tracks the velocity with extra smoothing and is used for scaling this collider up and down (filtering out Vector3.zero and small moves)
        /// </summary>
        private Vector3 FrameVelocitySmoothedForScaling { get; set; }

        /// <summary>
        /// Logs the velocity magnitude of the last ball hit
        /// </summary>
        public float LastKnownHitVelocity { get; private set; }

        /// <summary>
        /// Logs the how much of the balls direction was biased towards the angle of the club head instead of velocity direction
        /// </summary>
        public float LastKnownHitDirBias { get; private set; }

        /// <summary>
        /// Logs kind of collision occured that trigger a ball hit (trigger,sweep,collision)
        /// </summary>
        public string LastKnownHitType { get; private set; }

        private bool positionBufferWasJustReset = true;
        private int framesSinceClubArmed = -1;
        private bool clubIsTouchingBall;
        private bool CanTrackHitsAndVel => framesSinceClubArmed > 5;

        private Transform CurrentTarget
        {
            get
            {
                if (Utilities.IsValid(targetOverride) && targetOverride.gameObject.activeSelf)
                    return targetOverride.transform;
                return putterTarget.transform;
            }
        }

        void Start()
        {
            LastKnownHitType = string.Empty;

            MoveToClubWithoutVelocity();

            if (!Utilities.IsValid(golfClubHeadCollider))
                golfClubHeadCollider = GetComponent<BoxCollider>();

            if (!Utilities.IsValid(myRigidbody))
                myRigidbody = GetComponent<Rigidbody>();

            if (Utilities.IsValid(golfBall) && !Utilities.IsValid(ballCollider))
                ballCollider = golfBall.GetComponent<SphereCollider>();

            if (hitForceMultiplier.length == 0)
            {
                hitForceMultiplier.AddKey(0, 1);
                hitForceMultiplier.AddKey(10, 2);
            }

            if (clubHeadDirectionInfluence.length == 0)
            {
                clubHeadDirectionInfluence.AddKey(0f, .83f);
                clubHeadDirectionInfluence.AddKey(5f, 0f);
                clubHeadDirectionInfluence.AddKey(50f, 0f);
                clubHeadDirectionInfluence.SmoothTangents(0, 0.5f);
            }
        }

        private void OnEnable()
        {
            myRigidbody.collisionDetectionMode = collisionType;
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
            if (!Utilities.IsValid(golfClubHeadCollider))
                return;

            var speed = FrameVelocitySmoothedForScaling.magnitude;
            if (overrideSpeed > 0f)
                speed = overrideSpeed;

            if (framesSinceHit >= 0)
                speed = 0f;

            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                speed = 0f;

            if (!Utilities.IsValid(easeInOut))
                easeInOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

            if (maxSpeedForScaling <= 0f)
                maxSpeedForScaling = 10f;

            speed = easeInOut.Evaluate(speed / maxSpeedForScaling);

            var colliderSize = !Utilities.IsValid(targetOverride) ? putterTarget.size : new Vector3(.05f, .3f, .1f);
            var colliderCenter = !Utilities.IsValid(targetOverride) ? putterTarget.center : Vector3.zero;

            var minSize = Vector3.Scale(colliderSize, putterTarget.transform.lossyScale);
            var maxSize = Vector3.Scale(minSize, maxSpeedScale);

            golfClubHeadCollider.center = colliderCenter;
            golfClubHeadCollider.size = Vector3.Lerp(minSize, maxSize, speed);
        }


        private void FixedUpdate()
        {
            if (!gameObject.activeSelf)
                return;

            if (!Utilities.IsValid(myRigidbody) || !Utilities.IsValid(putterTarget))
            {
                enabled = false;
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
                // Most recent position is at the previous index
                int previousIndex = (bufferIndex == 0) ? lastPositions.Length - 1 : bufferIndex - 1;
                var targetPos = lastPositions[previousIndex];
                var targetRot = lastPositionRotations[previousIndex];
    
                // Work out velocity needed to reach the target position
                var deltaPos = targetPos - myRigidbody.position;

                // Time-based smoothing constant
                float smoothingTimeConstant = 0.05f;
                float t = Mathf.Clamp01(Time.fixedDeltaTime / smoothingTimeConstant);
                float adaptedFollowStrength = Mathf.Lerp(0.5f, 0.95f, t);

                var newVel = deltaPos / Time.fixedDeltaTime * adaptedFollowStrength;

                if (newVel.magnitude > 0f)
                    myRigidbody.velocity = newVel;

                // Similar time-based approach for rotation
                var deltaRot = targetRot * Quaternion.Inverse(transform.rotation);
                deltaRot.ToAngleAxis(out var angle, out var axis);
                if (angle > 180.0f)
                    angle -= 360.0f;
    
                if (angle != 0 && !myRigidbody.isKinematic)
                {
                    float rotSmoothingTimeConstant = 0.03f;
                    float rotT = Mathf.Clamp01(Time.fixedDeltaTime / rotSmoothingTimeConstant);
                    float adaptedRotationStrength = Mathf.Lerp(0.7f, 0.98f, rotT);
        
                    myRigidbody.angularVelocity = axis * (angle * Mathf.Deg2Rad / Time.fixedDeltaTime * adaptedRotationStrength);
                }
            }
            else
            {
                // Just use MovePosition/MoveRotation
                int previousIndex = (bufferIndex == 0) ? lastPositions.Length - 1 : bufferIndex - 1;
                var targetPos = lastPositions[previousIndex];
                var targetRot = lastPositionRotations[previousIndex];
    
                myRigidbody.MovePosition(targetPos);
                myRigidbody.MoveRotation(targetRot * Quaternion.Euler(referenceClubHeadColliderRotationOffset));
            }

            ResizeClubCollider();
        }
        
        private void UpdateVelocity()
        {
            var currentPos = CurrentTarget.position;
            var currentRot = CurrentTarget.rotation;

            var tRB = CurrentTarget.GetComponent<Rigidbody>();
            if (Utilities.IsValid(tRB))
            {
                currentPos = tRB.position;
                currentRot = tRB.rotation;
            }

            // Calculate velocity from the most recent position (which is at the previous index)
            Vector3 currFrameVelocity = Vector3.zero;
            if (!positionBufferWasJustReset)
            {
                // Most recent position is at the previous index
                int previousIndex = (bufferIndex == 0) ? lastPositions.Length - 1 : bufferIndex - 1;
                var prevPos = lastPositions[previousIndex];
        
                if (prevPos != Vector3.zero)
                {
                    currFrameVelocity = (currentPos - prevPos) / Time.deltaTime;
                }
            }

            // Store current position in the buffer at the current index
            lastPositions[bufferIndex] = currentPos;
            lastPositionRotations[bufferIndex] = currentRot;
            lastPositionTimes[bufferIndex] = 0f; // Reset time for newest position
    
            // Increment all other time values
            for (int i = 0; i < lastPositionTimes.Length; i++)
            {
                if (i != bufferIndex)
                    lastPositionTimes[i] += Time.deltaTime;
            }
    
            // Move to next position in circular buffer
            bufferIndex = (bufferIndex + 1) % lastPositions.Length;

            // Improved velocity smoothing
            if (!positionBufferWasJustReset && currFrameVelocity.magnitude > 0.001f)
            {
                float smoothingTimeConstant = 0.05f;
                float t = Mathf.Clamp01(Time.deltaTime / smoothingTimeConstant);
        
                FrameVelocity = Vector3.Lerp(FrameVelocity, currFrameVelocity, t);
                FrameVelocitySmoothed = Vector3.Lerp(FrameVelocitySmoothed, currFrameVelocity, t);
                FrameVelocitySmoothedForScaling = Vector3.Lerp(FrameVelocitySmoothedForScaling, currFrameVelocity, t * 0.4f);
            }
    
            positionBufferWasJustReset = false;
        }
        
        public override void PostLateUpdate()
        {
            UpdateVelocity();
            ResizeClubCollider();

            if (enableSweepTests && framesSinceHit == -1 && !positionBufferWasJustReset && !clubIsTouchingBall)
            {
                // Perform a sweep test to see if we'll be hitting the ball in the next frame
                if (FrameVelocity.magnitude > 0.005f && myRigidbody.SweepTest(FrameVelocity, out var hit, FrameVelocity.magnitude * Time.deltaTime))
                {
                    // We only care if this collided with the local players ball
                    if (Utilities.IsValid(hit.collider) && hit.collider.gameObject == golfBall.gameObject)
                    {
                        LastKnownHitType = "(L-Sweep)";
                        framesSinceHit = 0;
                    }
                }
            }

            // If the ball has not been hit, we do nothing
            if (framesSinceHit < 0) return;

            // If we have waited for enough frames after the hit (helps with people starting the hit from mm away from the ball)
            var adaptiveWaitFrames = Mathf.CeilToInt(hitWaitFrames * Mathf.Sqrt(60f * Time.deltaTime));
            if (framesSinceHit++ < adaptiveWaitFrames) return;

            // Consume the hit event
            framesSinceHit = -1;

            // Send the velocity to the ball
            HandleBallHit();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Utilities.IsValid(other))
                return;

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
                    OpenPuttUtils.Log(this, "Player armed the club and instantly hit the ball (trigger).. ignoring this collision");
                return;
            }

            clubIsTouchingBall = true;

            if (framesSinceHit < 0)
                LastKnownHitType = "(Trigger)";

            framesSinceHit = 0;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!Utilities.IsValid(other) || other.gameObject != golfBall.gameObject)
                return;

            clubIsTouchingBall = false;
            if (golfClub.playerManager.openPutt.debugMode)
                OpenPuttUtils.Log(this, "Club head is no longer in contact with the ball. Allowing collisions!");
        }

        private void OnCollisionEnter(Collision collision)
        {
            // We only care if this collided with the local players ball
            if (!Utilities.IsValid(collision) || !Utilities.IsValid(collision.rigidbody) || collision.rigidbody.gameObject != golfBall.gameObject)
                return;

            // Ignore extra hits to the ball until we have processed the first
            if (framesSinceHit >= 0 || positionBufferWasJustReset)
                return;

            // Stops players from launching the ball by placing the club inside the ball and arming it
            if (!CanTrackHitsAndVel)
            {
                if (golfClub.playerManager.openPutt.debugMode)
                    OpenPuttUtils.Log(this, "Player armed the club and instantly hit the ball (collision).. ignoring this collision");
                return;
            }

            clubIsTouchingBall = true;

            if (framesSinceHit < 0)
                LastKnownHitType = "(Collision)";

            framesSinceHit = 0;
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!Utilities.IsValid(collision) || !Utilities.IsValid(collision.rigidbody) || collision.rigidbody.gameObject != golfBall.gameObject)
                return;

            clubIsTouchingBall = false;
            if (golfClub.playerManager.openPutt.debugMode)
                OpenPuttUtils.Log(this, "Club head is no longer in contact with the ball. Allowing collisions!");
        }

        private void HandleBallHit()
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(playerManager))
            {
                OpenPuttUtils.LogError(this, "Missing references on GolfClubCollider! Cannot handle the ball hit!");
                return;
            }

            var currentCourse = playerManager.CurrentCourse;
            var currentCourseIsDrivingRange = Utilities.IsValid(currentCourse) && currentCourse.drivingRangeMode;

            var directionOfTravel = FrameVelocitySmoothed;
            var velocityMagnitude = FrameVelocitySmoothed.magnitude;

            // If using smoothed direction, use time-weighted sampling
            if (smoothedHitDirection)
            {
                Vector3 weightedDirection = Vector3.zero;
                float totalWeight = 0f;
    
                // Get the most recent position index
                int newestIdx = (bufferIndex == 0) ? lastPositions.Length - 1 : bufferIndex - 1;
                Vector3 newestPos = lastPositions[newestIdx];
    
                // Look back through history to get a time-weighted average direction
                for (int i = 1; i < multiFrameAverageMaxBacksteps && i < lastPositions.Length; i++)
                {
                    // Calculate the older index by going backward in the circular buffer
                    int olderIdx = (newestIdx - i + lastPositions.Length) % lastPositions.Length;
                    Vector3 olderPos = lastPositions[olderIdx];
                    float olderTime = lastPositionTimes[olderIdx];
        
                    if (olderPos != Vector3.zero && olderTime < 0.5f)
                    {
                        Vector3 frameDir = newestPos - olderPos;
                        float weight = 1.0f / (1.0f + olderTime);
            
                        if (frameDir.magnitude > 0.001f)
                        {
                            weightedDirection += frameDir.normalized * weight;
                            totalWeight += weight;
                        }
                    }
                }
    
                if (totalWeight > 0f)
                    directionOfTravel = (weightedDirection / totalWeight).normalized;
            }

            // If we are currently disallowing hits to go vertical
            if (!openPutt.enableVerticalHits && !currentCourseIsDrivingRange)
                directionOfTravel.y = 0; // Flatten the direction vector

            // Normalize the direction vector now it's been flattened (Apparently it has to be in this order as well!!)
            directionOfTravel = directionOfTravel.normalized;

            LastKnownHitDirBias = 0;
            if (useClubHeadDirection)
            {
                var faceDirection = putterTarget.transform.right;
                faceDirection = new Vector3(faceDirection.x, 0, faceDirection.z);

                if (Vector3.Angle(-faceDirection, directionOfTravel) < Vector3.Angle(faceDirection, directionOfTravel))
                    faceDirection = -faceDirection;

                if (Vector3.Angle(faceDirection, directionOfTravel) < 80)
                {
                    LastKnownHitDirBias = clubHeadDirectionInfluence.Evaluate(velocityMagnitude);
                    directionOfTravel = directionOfTravel.BiasedDirection(faceDirection, LastKnownHitDirBias);
                }
            }

            // Scale the velocity back up a bit
            velocityMagnitude *= hitForceMultiplier.Evaluate(velocityMagnitude);

            // Apply the players final hit force multiplier
            velocityMagnitude *= golfClub.forceMultiplier;

            // Only clamp hit speed if they player is on a normal course
            var shouldClampSpeed = !Utilities.IsValid(playerManager) || !currentCourseIsDrivingRange;

            // Clamp hit speed
            if (shouldClampSpeed && velocityMagnitude > golfBall.BallMaxSpeed)
            {
                velocityMagnitude = golfBall.BallMaxSpeed;
                if (openPutt.debugMode)
                    OpenPuttUtils.Log(this, $"Ball hit velocity was clamped to {velocityMagnitude}");
            }

            // Put the direction and magnitude back together
            var velocity = directionOfTravel * velocityMagnitude;

            // Fix NaNs so we don't die
            if (float.IsNaN(velocity.y))
                velocity.y = 0;
            if (float.IsNaN(velocity.x))
                velocity.x = 0;
            if (float.IsNaN(velocity.z))
                velocity.z = 0;

            // Clamp min velocity (we used to just return here, but maybe this is better than the ball not moving?)
            if (velocity.magnitude < golfBall.minBallSpeed)
                velocity = directionOfTravel * golfBall.minBallSpeed;

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Ball has been hit! Velocity:{velocity.magnitude}{LastKnownHitType} DirectionOfTravel({directionOfTravel})");

            LastKnownHitVelocity = velocity.magnitude;

            // Register the hit with the ball
            golfBall.OnBallHit(velocity);

            // A debug line renderer to show the resulting hit direction
            if (Utilities.IsValid(visual))
                visual.OnBallHit(golfBall.transform.position, velocity);
        }

        private void ResetPositionBuffers()
        {
            positionBufferWasJustReset = true;
            bufferIndex = 0; // Reset the buffer index

            FrameVelocity = Vector3.zero;
            FrameVelocitySmoothed = Vector3.zero;
            FrameVelocitySmoothedForScaling = Vector3.zero;

            for (var i = 0; i < lastPositions.Length; i++)
            {
                lastPositions[i] = Vector3.zero;
                lastPositionTimes[i] = 99;
            }
        }

        private void MoveToClubWithoutVelocity(bool resetBuffers = true)
        {
            if (!Utilities.IsValid(myRigidbody) || !Utilities.IsValid(putterTarget))
                return;

            var target = CurrentTarget;

            myRigidbody.position = target.position;
            myRigidbody.rotation = target.rotation * Quaternion.Euler(referenceClubHeadColliderRotationOffset);

            if (!myRigidbody.isKinematic)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;
            }

            if (resetBuffers)
                ResetPositionBuffers();
        }

        public void OverrideLastHitVelocity(float newSpeed)
        {
            LastKnownHitVelocity = newSpeed;
        }
    }
}