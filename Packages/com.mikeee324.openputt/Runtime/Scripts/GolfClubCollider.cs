using System;
using com.dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Serialization;
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
        [Header("References")]
        [Tooltip("Reference to OpenPutt to skip a few steps")]
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
        public int hitWaitFrames = 1;

        private AnimationCurve sideSpinMagnitudeCurve;
        private float sideSpinMultiplier = 1.0f;

        public AnimationCurve hitForceMultiplier;

        [NonSerialized]
        private AnimationCurve clubHeadDirectionInfluence;

        [NonSerialized]
        private AnimationCurve momentumLossByAngle;

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

        private Vector3 lastHitWorldPos = Vector3.zero;

        /// <summary>
        /// How many frames we should wait after the hit is registered before passing it to the ball (Helps with tiny hits to get a proper direction of travel)
        /// </summary>
        private int framesToWaitAfterHit = 0;

        [NonSerialized]
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

        private bool clubIsTouchingBall;

        private bool CanTrackHitsAndVel => framesSinceClubArmed > 3 && framesSinceHit < 0;
        private int framesSinceClubArmed = -1;
        private bool _initialized = false;

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

            if (!Utilities.IsValid(golfClubHeadCollider))
                golfClubHeadCollider = GetComponent<BoxCollider>();

            if (!Utilities.IsValid(myRigidbody))
                myRigidbody = GetComponent<Rigidbody>();

            if (Utilities.IsValid(golfBall) && !Utilities.IsValid(ballCollider))
                ballCollider = golfBall.GetComponent<SphereCollider>();

            _initialized = true;

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called when the collider is switched on - Moves the collider to the club head without any velocity
        /// </summary>
        private void OnEnable()
        {
            framesSinceClubArmed = 0;
            golfClubHeadCollider.isTrigger = true;
            clubIsTouchingBall = false;

            MoveToClubWithoutVelocity();
        }

        private void OnDisable()
        {
            framesSinceClubArmed = 0;
            golfClubHeadCollider.isTrigger = true;
            clubIsTouchingBall = false;
        }

        /// <summary>
        /// Resizes collider to match the club head size. Also scales the collider based on the speed of the club head. Faster speeds will make the collider larger.
        /// </summary>
        /// <param name="overrideSpeed"></param>
        private void ResizeClubCollider()
        {
            if (!Utilities.IsValid(golfClubHeadCollider))
                return;

            var speed = FrameVelocitySmoothedForScaling.magnitude;

            if (!CanTrackHitsAndVel)
                speed = 0f;

            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                speed = 0f;

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
            if (!Utilities.IsValid(myRigidbody) || !Utilities.IsValid(putterTarget))
            {
                enabled = false;
                return;
            }

            // Make sure rigidbodies are awake
            myRigidbody.WakeUp();
            golfBall._Wakeup();

            // We can assume if FixedUpdate is running, the club is armed
            if (clubIsTouchingBall)
                framesSinceClubArmed = 0;
            else
                framesSinceClubArmed += 1;

            // Non-trigger colliders can actually track fast hits
            golfClubHeadCollider.isTrigger = !CanTrackHitsAndVel || clubIsTouchingBall;

            // Most recent position is at the previous index
            var previousIndex = (bufferIndex == 0) ? lastPositions.Length - 1 : bufferIndex - 1;
            var targetPos = lastPositions[previousIndex];
            var targetRot = lastPositionRotations[previousIndex];

            // Work out velocity needed to reach the target position
            var deltaPos = targetPos - myRigidbody.position;

            // Position following
            const float FOLLOW_SPEED = 400f; // Higher = faster following
            var t = 1f - Mathf.Exp(-FOLLOW_SPEED * Time.fixedDeltaTime);
            var newVel = deltaPos / Time.fixedDeltaTime * t;

            if (newVel.magnitude > 0f)
                myRigidbody.velocity = newVel;

            // Rotation following
            var deltaRot = targetRot * Quaternion.Inverse(transform.rotation);
            deltaRot.ToAngleAxis(out var angle, out var axis);
            if (angle > 180.0f)
                angle -= 360.0f;

            if (angle != 0 && !myRigidbody.isKinematic)
            {
                const float ROT_SPEED = 400f; // Higher = faster rotation
                var rotT = 1f - Mathf.Exp(-ROT_SPEED * Time.fixedDeltaTime);
                myRigidbody.angularVelocity = axis * (angle * Mathf.Deg2Rad / Time.fixedDeltaTime * rotT);
            }

            if (CanTrackHitsAndVel && !clubIsTouchingBall)
            {
                var sweepDir = -newVel;

                // Perform a sweep test to see if we'll be hitting the ball in the next frame
                if (FrameVelocity.magnitude > 0.005f && myRigidbody.SweepTest(sweepDir.normalized, out var hit, sweepDir.magnitude * Time.deltaTime))
                {
                    // We only care if this collided with the local players ball
                    if (Utilities.IsValid(hit.collider) && hit.collider.gameObject == golfBall.gameObject)
                    {
                        LastKnownHitType = "(B-Sweep)";
                        framesSinceHit = 0;
                        framesToWaitAfterHit = hitWaitFrames; //Mathf.CeilToInt(hitWaitFrames * (60f * Time.deltaTime));
                        lastHitWorldPos = playerManager.golfClubHead.transform.position;

                        if (framesToWaitAfterHit == 0)
                        {
                            // Consume the hit event
                            framesSinceHit = -1;

                            // Send the velocity to the ball
                            HandleBallHit(Vector3.zero, Vector3.zero);
                        }
                    }
                }
            }
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
            var previousIndex = (bufferIndex == 0) ? lastPositions.Length - 1 : bufferIndex - 1;
            var prevPos = lastPositions[previousIndex];

            var currFrameVelocity = Vector3.zero;
            if (prevPos != Vector3.zero)
                currFrameVelocity = ((currentPos - prevPos) / Time.deltaTime).Sanitized();

            // Store current position in the buffer at the current index
            lastPositions[bufferIndex] = currentPos;
            lastPositionRotations[bufferIndex] = currentRot;
            lastPositionTimes[bufferIndex] = 0f; // Reset time for newest position

            // Increment all other time values
            for (var i = 0; i < lastPositionTimes.Length; i++)
            {
                if (i != bufferIndex)
                    lastPositionTimes[i] += Time.deltaTime;
            }

            // Move to next position in circular buffer
            bufferIndex = (bufferIndex + 1) % lastPositions.Length;

            // Skip velocity smoothing on first frame only
            if (!CanTrackHitsAndVel)
            {
                FrameVelocity = Vector3.zero;
                FrameVelocitySmoothed = Vector3.zero;
                FrameVelocitySmoothedForScaling = Vector3.zero;
                return;
            }

            // Improved velocity smoothing
            var t = 1f - Mathf.Exp(-60f * Time.deltaTime);
            var tScaling = 1f - Mathf.Exp(-30f * Time.deltaTime);

            FrameVelocity = currFrameVelocity.Sanitized();
            FrameVelocitySmoothed = Vector3.Lerp(FrameVelocitySmoothed, currFrameVelocity, t).Sanitized();
            FrameVelocitySmoothedForScaling = Vector3.Lerp(FrameVelocitySmoothedForScaling, currFrameVelocity, tScaling).Sanitized();
        }

        public override void PostLateUpdate()
        {
            UpdateVelocity();

            // Scale club collider based on current smoothed speed
            ResizeClubCollider();

            // If the ball has not been hit, we do nothing
            if (framesSinceHit < 0) return;

            // If we have waited for enough frames after the hit (helps with people starting the hit from mm away from the ball)
            if (framesSinceHit++ < framesToWaitAfterHit) return;

            // Consume the hit event
            framesSinceHit = -1;

            // Send the velocity to the ball
            HandleBallHit(Vector3.zero, Vector3.zero);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // We only care if this collided with the local players ball
            if (!Utilities.IsValid(collision) || !Utilities.IsValid(collision.rigidbody) || collision.rigidbody.gameObject != golfBall.gameObject)
                return;

            // Stops players from launching the ball by placing the club inside the ball and arming it
            if (!CanTrackHitsAndVel)
                return;

            // Ignore extra hits to the ball until we have processed the first
            if (framesSinceHit >= 0)
                return;

            if (framesSinceHit < 0)
                LastKnownHitType = "(Collision)";

            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                if (Utilities.IsValid(contact))
                    lastHitWorldPos = contact.point;
            }

            framesSinceHit = 0;
            framesToWaitAfterHit = hitWaitFrames; //Mathf.CeilToInt(hitWaitFrames * (60f * Time.deltaTime));

            if (framesToWaitAfterHit == 0)
            {
                // Consume the hit event
                framesSinceHit = -1;

                // Send the velocity to the ball
                HandleBallHit(Vector3.zero, Vector3.zero);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (framesSinceClubArmed > 5) return;

            clubIsTouchingBall = true;
            if (golfClub.playerManager.openPutt.debugMode)
                OpenPuttUtils.Log(this, "Player armed the club and instantly hit the ball (collision).. ignoring this collision");
        }

        private void OnTriggerExit(Collider other)
        {
            clubIsTouchingBall = false;
            if (golfClub.playerManager.openPutt.debugMode)
                OpenPuttUtils.Log(this, "Club is no longer in contact with the ball");
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!Utilities.IsValid(collision) || !Utilities.IsValid(collision.rigidbody) || collision.rigidbody.gameObject != golfBall.gameObject)
                return;

            framesSinceClubArmed = 0;
        }

        public void HandleBallHit(Vector3 overrideHitVelocity, Vector3 overrideClubDirection)
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(playerManager))
            {
                OpenPuttUtils.LogError(this, "Missing references on GolfClubCollider! Cannot handle the ball hit!");
                return;
            }

            if (!_initialized)
                Start();

            var hasGravity = golfBall.gravityMagnitude > .01f;
            var currentCourse = playerManager.CurrentCourse;
            var currentCourseIsDrivingRange = Utilities.IsValid(currentCourse) && currentCourse.drivingRangeMode;

            var puttingOnlyMode = playerManager.openPutt.puttingOnlyMode;

            if (puttingOnlyMode && !currentCourseIsDrivingRange)
                golfClub.ClubType = GolfClubType.Putter;

            var hand = golfClub.CurrentHand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;
            var headOffset = playerManager.openPutt.controllerTracker.CalculateLocalOffsetFromWorldPosition(hand, golfClubHeadCollider.transform.TransformPoint(golfClubHeadCollider.center));
            var headVelocity = playerManager.openPutt.controllerTracker.GetVelocityAtOffset(hand, headOffset);
            var directionOfTravel = headVelocity;
            var velocityMagnitude = headVelocity.magnitude;

            if (overrideHitVelocity.sqrMagnitude > 0f)
            {
                directionOfTravel = overrideHitVelocity.normalized;
                velocityMagnitude = overrideHitVelocity.magnitude;
            }

            var sideSpin = Vector3.zero;

            var gravityUp = -golfBall.gravityDirection;

            var isAttachedToClub = !Utilities.IsValid(targetOverride);

            if (golfBall.gravityMagnitude > 0f)
                directionOfTravel = directionOfTravel.FlattenDirection(gravityUp);

            // Collider is following something that isn't the club - use the velocity of this rigidbody instead for stuff
            // We also disable side spin and stuff because it's not the normal golf club
            if (!isAttachedToClub)
            {
                directionOfTravel = FrameVelocitySmoothed;
                velocityMagnitude = FrameVelocitySmoothed.magnitude;
            }

            if (hasGravity)
            {
                // Apply loft
                var rotationAxis = Vector3.Cross(directionOfTravel, gravityUp);
                if (rotationAxis.sqrMagnitude == 0)
                    rotationAxis = Vector3.Cross(gravityUp, Vector3.Cross(Vector3.forward, gravityUp).normalized).normalized;

                var loftRotation = Quaternion.AngleAxis(golfClub.ClubType.GetTypicalLoft(), rotationAxis);
                directionOfTravel = loftRotation * directionOfTravel;
            }

            // Normalize the direction vector now it's had loft applied and was initially flattened
            directionOfTravel = directionOfTravel.normalized.Sanitized();

            var rawDirectionOfTravel = directionOfTravel;
            var faceAngleDiffToDirection = 1f;
            LastKnownHitDirBias = 0;

            if (isAttachedToClub)
            {
                // Work out which way the club head is facing (And correct if player is holding it backwards)
                var faceDirection = Vector3.zero;
                switch (golfClub.CurrentHand)
                {
                    case VRC_Pickup.PickupHand.Left:
                        {
                            // Flatten the club's right vector relative to gravity
                            if (hasGravity)
                                faceDirection = putterTarget.transform.right.FlattenDirection(gravityUp).normalized;
                            else
                                faceDirection = putterTarget.transform.right.normalized;
                            break;
                        }
                    case VRC_Pickup.PickupHand.Right:
                        {
                            // Flatten the club's *negative* right vector relative to gravity
                            if (hasGravity)
                                faceDirection = (-putterTarget.transform.right).FlattenDirection(gravityUp).normalized;
                            else
                                faceDirection = (-putterTarget.transform.right).normalized;
                            break;
                        }
                }

                if (overrideClubDirection.sqrMagnitude > 0f)
                    faceDirection = overrideClubDirection.normalized;

                if (faceDirection.sqrMagnitude > 0f)
                {
                    faceAngleDiffToDirection = Vector3.Dot(faceDirection, directionOfTravel.normalized);
                    if (golfClub.ClubType == GolfClubType.Putter || !hasGravity)
                    {
                        // Putter can hit with both sides - check if the player used the "backside"
                        var oppositeAngle = Vector3.Dot(-faceDirection, directionOfTravel.normalized);

                        if (oppositeAngle > faceAngleDiffToDirection)
                        {
                            faceDirection = -faceDirection;
                            faceAngleDiffToDirection = oppositeAngle;
                        }
                    }

                    // Stuff we can only do if people aren't hitting balls at stupid angles
                    if (faceAngleDiffToDirection > .2f)
                    {
                        if (!Utilities.IsValid(clubHeadDirectionInfluence) || clubHeadDirectionInfluence.length == 0)
                        {
                            clubHeadDirectionInfluence = new AnimationCurve();
                            clubHeadDirectionInfluence.AddKey(0f, 0.95f);       // High influence for very slow speeds (near perfect putts)
                            clubHeadDirectionInfluence.AddKey(3f, 0.6f);        // Influence drops significantly by faster putting/slow chipping speeds
                            clubHeadDirectionInfluence.AddKey(10f, 0.2f);       // Influence is lower for chipping/pitching speeds
                            clubHeadDirectionInfluence.AddKey(30f, 0.05f);      // Influence is very low but not zero for iron/drive speeds
                            clubHeadDirectionInfluence.AddKey(40f, 0.0f);       // Influence is none for very fast speeds
                            clubHeadDirectionInfluence.SmoothTangents(0, 0.5f); // Smooth the transition
                            clubHeadDirectionInfluence.SmoothTangents(1, 0.5f);
                            clubHeadDirectionInfluence.SmoothTangents(2, 0.5f);
                            clubHeadDirectionInfluence.SmoothTangents(3, 0.5f);
                            clubHeadDirectionInfluence.preWrapMode = WrapMode.Clamp;
                            clubHeadDirectionInfluence.postWrapMode = WrapMode.Clamp;
                        }

                        // Face direction bias based on speed
                        LastKnownHitDirBias = clubHeadDirectionInfluence.Evaluate(velocityMagnitude);
                        directionOfTravel = directionOfTravel.BiasedDirection(faceDirection, LastKnownHitDirBias);
                    }

                    if (!Utilities.IsValid(momentumLossByAngle) || momentumLossByAngle.length == 0)
                    {
                        momentumLossByAngle = new AnimationCurve();
                        momentumLossByAngle.AddKey(-1f, 0.0f);
                        momentumLossByAngle.AddKey(0f, 0.5f);
                        momentumLossByAngle.AddKey(.5f, 0.9f);
                        momentumLossByAngle.AddKey(1f, 1.0f);
                        momentumLossByAngle.preWrapMode = WrapMode.Clamp;
                        momentumLossByAngle.postWrapMode = WrapMode.Clamp;
                    }

                    // Apply the momentum loss due to angle
                    velocityMagnitude *= momentumLossByAngle.Evaluate(faceAngleDiffToDirection);

                    // Side spin
                    if (hasGravity && golfClub.ClubType != GolfClubType.Putter)
                    {
                        // Determine the 'horizontal' swing path direction relative to gravity
                        var swingPathHorizontalDirection = golfBall.gravityMagnitude < .01f ? headVelocity : headVelocity.FlattenDirection(gravityUp).normalized.Sanitized();

                        if (!Utilities.IsValid(sideSpinMagnitudeCurve) || sideSpinMagnitudeCurve.length == 0)
                        {
                            sideSpinMagnitudeCurve = new AnimationCurve();
                            sideSpinMagnitudeCurve.AddKey(new Keyframe(0.0f, 1.0f, 0f, 0f)); // Club was 90 degrees or more off
                            sideSpinMagnitudeCurve.AddKey(new Keyframe(0.3f, 1.0f, 0f, 0f));
                            sideSpinMagnitudeCurve.AddKey(new Keyframe(0.7f, 0.4f));
                            sideSpinMagnitudeCurve.AddKey(new Keyframe(1.0f, 0.0f, 0f, 0f)); // Club is facing the ball exactly
                            sideSpinMagnitudeCurve.preWrapMode = WrapMode.Clamp;
                            sideSpinMagnitudeCurve.postWrapMode = WrapMode.Clamp;
                        }

                        // More angle difference => Faster side spin
                        var rawSpinMagnitude = sideSpinMagnitudeCurve.Evaluate(faceAngleDiffToDirection);
                        var sideSpinSpeed = rawSpinMagnitude * velocityMagnitude * sideSpinMultiplier;
                        var crossProduct = Vector3.Cross(swingPathHorizontalDirection, faceDirection);
                        var sideSpinAxis = Vector3.Dot(crossProduct, gravityUp) < 0 ? gravityUp : -gravityUp;
                        sideSpin = sideSpinAxis * sideSpinSpeed;
                    }
                }
            }

            // Scale the velocity back up a bit
            if (!Utilities.IsValid(hitForceMultiplier) || hitForceMultiplier.length == 0)
            {
                hitForceMultiplier.AddKey(0, 1);
                hitForceMultiplier.AddKey(10, 2);
            }
            velocityMagnitude *= hitForceMultiplier.Evaluate(velocityMagnitude);

            // Apply the players final hit force multiplier
            velocityMagnitude *= golfClub.forceMultiplier;

            // Work out whether we need to clamp the hit speed
            // Clamp on normal courses (not driving ranges). Use per-club max
            // speeds but never exceed the global ball max speed.
            var shouldClampSpeed = Utilities.IsValid(currentCourse) && !currentCourseIsDrivingRange;

            if (shouldClampSpeed)
            {
                var clubMax = golfClub.ClubType.GetTypicalMaxSpeed();
                var allowedMax = Mathf.Min(clubMax, golfBall.BallMaxSpeed);
                if (velocityMagnitude > allowedMax)
                {
                    velocityMagnitude = allowedMax;
                    if (openPutt.debugMode)
                        OpenPuttUtils.Log(this, $"Ball hit velocity was clamped to {velocityMagnitude} (Club:{golfClub.ClubType})");
                }
            }

            // Put the direction and magnitude back together
            // Use the modified directionOfTravel which incorporates flattening and loft relative to gravity
            var velocity = directionOfTravel * velocityMagnitude;

            // Fix NaNs so we don't die
            velocity = velocity.Sanitized();

            // Ignore nothing hits
            if (velocity.magnitude < golfBall.minBallHitSpeed)
                return;

            // Clamp min velocity (we used to just return here, but maybe this is better than the ball not moving?)
            //if (velocity.magnitude < golfBall.minBallSpeed)
            //    velocity = directionOfTravel * golfBall.minBallSpeed;

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Ball has been hit! Velocity:{velocity.magnitude}{LastKnownHitType} Loft({golfClub.ClubType.GetTypicalLoft()}) SideSpin({sideSpin}) DirectionOfTravel({directionOfTravel}) FaceAngle({faceAngleDiffToDirection})");

            LastKnownHitVelocity = velocity.magnitude;

            // A debug line renderer to show the resulting hit direction
            if (Utilities.IsValid(visual))
                visual.OnBallHit(golfBall.transform.position, lastHitWorldPos, velocity, rawDirectionOfTravel);

            // Register the hit with the ball
            golfBall._OnBallHit(velocity, sideSpin);
        }

        private void MoveToClubWithoutVelocity()
        {
            if (!Utilities.IsValid(myRigidbody))
                return;

            var target = CurrentTarget;

            myRigidbody.position = target.position;
            myRigidbody.rotation = target.rotation * Quaternion.Euler(referenceClubHeadColliderRotationOffset);

            if (!myRigidbody.isKinematic)
            {
                myRigidbody.velocity = Vector3.zero;
                myRigidbody.angularVelocity = Vector3.zero;
            }

            // Reset position buffers
            bufferIndex = 0;
            for (var i = 0; i < lastPositions.Length; i++)
            {
                lastPositions[i] = target.position;
                lastPositionRotations[i] = target.rotation;
                lastPositionTimes[i] = 0f;
            }

            // Reset stored velocities
            FrameVelocity = Vector3.zero;
            FrameVelocitySmoothed = Vector3.zero;
            FrameVelocitySmoothedForScaling = Vector3.zero;

            // Reset collider size
            ResizeClubCollider();
        }

        public void OverrideLastHitVelocity(float newSpeed)
        {
            LastKnownHitVelocity = newSpeed;
        }
    }
}