﻿using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [RequireComponent(typeof(VRCPickup)), RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(SphereCollider)), DefaultExecutionOrder(100)]
    public class GolfBallController : UdonSharpBehaviour
    {
        #region Public Settings

        [Header("References")]
        public GolfClub club;

        [FormerlySerializedAs("puttSync")]
        public OpenPuttSync openPuttSync;

        public VRCPickup pickup;
        public GolfBallStartLineController startLine;
        public MaterialPropertyBlock materialPropertyBlock;

        [Tooltip("Used to identify a wall collider and perform a bounce")]
        public PhysicMaterial wallMaterial;

        [Tooltip("Used to identify whether the ball is still on the course or not when it stops rolling")]
        public PhysicMaterial floorMaterial;

        public PlayerManager playerManager;

        [SerializeField]
        private TrailRenderer trail;

        [SerializeField]
        private Rigidbody ballRigidbody;

        [SerializeField]
        private SphereCollider ballCollider;

        [Space] [Header("General Settings")] [Tooltip("Allows players to pick up their ball at any time as long as the ball is not moving")]
        public bool allowBallPickup;

        [Tooltip("Allows players to pick up their ball if they are not currently playing a course AND the ball is not moving")]
        public bool allowBallPickupWhenNotPlaying = true;

        [Tooltip("Allows players to hit the ball at any time while it is moving (Default is no, which only lets them do this while they are not playing a course)")]
        public bool allowBallHitWhileMoving;

        [Tooltip("Should the ball hit noise be played if the ball falls onto a floor?")]
        public bool audioWhenBallHitsFloor = true;

        [Space]
        [Header("Ball Physics")]
        // [Tooltip("Which layers can start the ball moving when they collide with the ball? (For spinny things etc)")]
        //public LayerMask allowNonClubCollisionsFrom = 0;
        [Range(0, .5f), Tooltip("The amount of drag to apply to the balls RigidBody by default (Can be overriden by other scripts for sand pits and things)")]
        public float defaultBallDrag = .055f;

        [Range(0, 1), Tooltip("The amount of drag to apply to the balls RigidBody (Overrides the default drag above)")]
        public float ballDragOverride;

        [Tooltip("Toggles air resistance on the ball, helps it slow down while in the air and on ground better")]
        public bool enableAirResistance = true;

        [SerializeField, Range(0f, 150f), Tooltip("This defines the fastest this ball can travel after being hit by a club (m/s) - Bear in mind the fastest club swing recorded is 108~ m/s")]
        private float maxBallSpeed = 100f;

        [Range(0f, .2f), Tooltip("If the ball goes below this speed it will be counted as 'not moving' and will be stopped after the amount of time defined below")]
        public float minBallSpeed = 0.03f;

        [Range(0f, .2f), Tooltip("If the ball goes below this speed it will be counted as 'not moving' and will be stopped after the amount of time defined below")]
        public float minBallHitSpeed = 0.1f;

        [Range(0f, 1f), Tooltip("Defines how long the ball can keep rolling for when it goes below the minimum speed")]
        public float minBallSpeedMaxTime = 1f;

        [Tooltip("How long a ball can roll before being stopped automatically (in seconds)")]
        public float maxBallRollingTime = 30f;

        [Space] [SerializeField, Range(0, 90)]
        float groundSnappingMaxGroundAngle = 45f;

        [SerializeField, Min(0f), Tooltip("An extra buffer to check if the ball is on the ground (in meters)")]
        float groundRaycastDistance = 0.02f;

        [SerializeField, Min(0f), Tooltip("The distance to check for the ground snapping probe. This helps keep the ball from bouncing weird on certain floor edges. (in meters)")]
        float groundSnappingProbeDistance = 0.05f;

        [SerializeField]
        LayerMask groundSnappingProbeMask = -1;

        [Space] [Range(0.1f, 2f), Tooltip("Used to pretend to absorb energy from the ball when it collides with a wall (Only used if the collider does not have a PhysicMaterial assigned)")]
        public float wallBounceSpeedMultiplier = 0.8f;

        [Range(0, 0.5f)] [Tooltip("Controls how the ball reflects off walls. 0=Perfect reflection 0.5=Half the direction is lost 1=Runs along the wall it hits")]
        public float wallBounceDeflection = .1f;

        [Range(0, 1), Tooltip("Determines if the ball will ignore wall bounces from directions below (0.5 is from the middle downward)")]
        public float wallBounceHeightIgnoreAmount = 0.5f;

        [Space] [Header("Respawn Settings")] [Tooltip("Toggles whether the ball is sent to the respawn position if it stops outside a course")]
        public bool respawnAutomatically = true;

        [Tooltip("If the ball stops outside of a course, this is where it will respawn to in world space")]
        public Vector3 respawnPosition = Vector3.zero;

        public Vector3 CurrentPosition
        {
            get => Time.inFixedTimeStep && Utilities.IsValid(ballRigidbody) && !ballRigidbody.isKinematic ? ballRigidbody.position : transform.position;
            set => ballRigidbody.position = value;
        }

        public bool BallIsMoving
        {
            set
            {
                var localPlayerIsOwner = this.LocalPlayerOwnsThisObject();
                var ballWasMoving = _ballMoving;

                // Store the new value
                _ballMoving = value;

                if (resetBallTimers)
                {
                    timeNotMoving = 0f;
                    timeMoving = 0f;
                }
                else
                {
                    resetBallTimers = true;
                }

                UpdateBallState(localPlayerIsOwner);

                // Tells the players golf club to update its current state
                if (Utilities.IsValid(club))
                    club.RefreshState();

                // Only the owner of the ball can run physics on it (everyone else should only receive ObjectSync updates)
                if (!localPlayerIsOwner)
                {
                    _ballMoving = false;
                    SetEnabled(false);
                    return;
                }

                SetEnabled(_ballMoving);

                if (ballWasMoving && !_ballMoving)
                {
                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
                    {
                        foreach (var listener in playerManager.openPutt.eventListeners)
                            listener.OnLocalPlayerBallStopped();
                    }

                    if (playerManager.openPutt.debugMode)
                    {
                        // Ball stopped moving, output ball speed log for this hit to the log
                        /*string sss = "";
                        for (int i = 0; i < speedDataLogging.Length; i++)
                            sss += speedDataLogging[i] + ",";
                        Utils.LogError(this, "SpeedData:\r\n" + sss + "0");*/

                        // Reset log
                        speedDataLogging = new float[0];
                    }

                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.CurrentCourse) && playerManager.CurrentCourse.drivingRangeMode)
                    {
                        var course = playerManager.CurrentCourse;

                        playerManager.OnCourseFinished(course, null, CourseState.Completed);

                        // If we can replay the course - automatically restart the course
                        if (playerManager.openPutt.replayableCourses || course.courseIsAlwaysReplayable)
                        {
                            playerManager.OnCourseStarted(course);
                            RespawnBall();
                        }
                    }
                    else if (respawnAutomatically)
                    {
                        var ballIsInValidPosition = Utilities.IsValid(playerManager) && (!Utilities.IsValid(playerManager.CurrentCourse) || playerManager.IsOnTopOfCurrentCourse(CurrentPosition));
                        if (ballIsInValidPosition)
                        {
                            if (!pickedUpByPlayer)
                            {
                                // Ball stopped on top of a course - save this position so we can respawn here if needed
                                SetRespawnPosition(CurrentPosition);
                            }
                        }
                        else if (respawnPosition != Vector3.zero)
                        {
                            // It is not on top of a course floor so move it to the previous position
                            ballRigidbody.position = respawnPosition;

                            // Play the reset noise
                            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.SFXController))
                                playerManager.openPutt.SFXController.PlayBallResetSoundAtPosition(respawnPosition);
                        }
                    }

                    if (!ballRigidbody.isKinematic)
                    {
                        // Stop the ball moving
                        ballRigidbody.velocity = Vector3.zero;
                        ballRigidbody.angularVelocity = Vector3.zero;
                        ballRigidbody.WakeUp();
                    }

                    lastFramePosition = ballRigidbody.position;
                    lastFrameVelocity = Vector3.zero;
                }

                //if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                //Utils.Log(this, $"BallWasMoving({ballWasMoving}) BallMoving({_ballMoving}) RespawnAuto({respawnAutomatically}) BallValidPos({ballIsInValidPosition}) PickedUp({pickedUpByPlayer}) RespawnPos({respawnPosition})");
            }
            get => _ballMoving;
        }

        [SerializeField]
        private bool _ballMoving = false;

        public bool pickedUpByPlayer { get; private set; }

        [HideInInspector]
        public int currentOwnerHideOverride;

        public float BallWeight
        {
            get => ballRigidbody.mass;
            set => ballRigidbody.mass = value;
        }

        public float BallFriction
        {
            get => ballCollider.material.dynamicFriction;
            set => ballCollider.material.dynamicFriction = value;
        }

        public float BallDrag
        {
            get => ballDragOverride;
            set => ballDragOverride = value;
        }

        public float BallMaxSpeed
        {
            get => maxBallSpeed;
            set => maxBallSpeed = value;
        }

        public float BallAngularDrag { get; set; }
        public float DefaultBallWeight { get; private set; }
        public float DefaultBallFriction { get; private set; }
        public float DefaultBallDrag { get; private set; }
        public float DefaultBallAngularDrag { get; private set; }
        public float DefaultBallMaxSpeed { get; private set; }
        public float BallCurrentSpeed => Utilities.IsValid(ballRigidbody) && !ballRigidbody.isKinematic ? ballRigidbody.velocity.magnitude : 0;

        /// <summary>
        /// Speculative seems to make the ball collide way before hitting a wall.. so maybe we have speculative when at rest and dynamic while moving?
        /// Maybe even switch to speculative at high speeds just soi we don't go through floors and walls?
        /// </summary>
        public CollisionDetectionMode collisionType
        {
            get => ballRigidbody.collisionDetectionMode;
            set => ballRigidbody.collisionDetectionMode = value;
        }

        public bool ballGroundedDebug = false;

        #endregion

        #region Internal Vars

        /// Tracks how long a ball has been rolling for so we can stop it if it rolls for way too long
        private float timeMoving;

        /// Tracks how long the ball has been slowly rolling for so we can just bring it to a proper stop
        private float timeNotMoving;

        /// Tracks the velocity of the ball in the last frame so we can reflect properly on walls
        private Vector3 lastFrameVelocity;

        private Vector3 lastFramePosition;

        /// Stores the velocity of the club that needs to be applied in the next FixedUpdate() frame
        [SerializeField]
        public Vector3 requestedBallVelocity = Vector3.zero;

        public bool OnGround => stepsOnGround > 1;

        float minGroundDotProduct;

        private Vector3 lastGroundContactNormal = Vector3.up;
        private float timeFlying = 0;
        public bool enableBallSnap = true;
        int stepsOnGround, stepsInAir;
        private bool resetBallTimers = true;

        /// Stores the last known dynamic friction value of the surface the ball was last on top of
        //private float currentGroundDynamicFriction = 0f;
        public CollisionDetectionMode requestedCollisionMode = CollisionDetectionMode.ContinuousSpeculative;

        /// <summary>
        /// Used to log ball speed after it gets hit, can be used to track down issues... maybe
        /// </summary>
        private float[] speedDataLogging = new float[0];

        /// <summary>
        /// The furthest distance that the ball was from the position where it was hit
        /// </summary>
        private float lastHitMaxDistance = 0;

        /// <summary>
        /// Keeps track of how far the ball actually travelled in total for its last hit
        /// </summary>
        private float lastHitTravelDistance = 0;

        public BodyMountedObject shoulderPickup => playerManager.IsInLeftHandedMode ? playerManager.openPutt.rightShoulderPickup : playerManager.openPutt.leftShoulderPickup;

        private VRC_Pickup.PickupHand ballHeldInHand = VRC_Pickup.PickupHand.None;

        private VRC_Pickup.PickupHand shoulderBallHeldInHand = VRC_Pickup.PickupHand.None;

        #endregion

        void Start()
        {
            pickedUpByPlayer = false;
            BallIsMoving = false;

            if (!Utilities.IsValid(wallMaterial))
                OpenPuttUtils.LogError(this, "Cannot detect walls! Please assign a wall PhysicMaterial to this ball!");

            if (!Utilities.IsValid(floorMaterial))
                OpenPuttUtils.LogError(this, "Cannot detect floors! Please assign a floor PhysicMaterial to this ball!");

            if (!Utilities.IsValid(openPuttSync))
                openPuttSync = GetComponent<OpenPuttSync>();

            if (!Utilities.IsValid(ballRigidbody))
                ballRigidbody = GetComponent<Rigidbody>();
            if (!Utilities.IsValid(ballCollider))
                ballCollider = GetComponent<SphereCollider>();
            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();

            if (Utilities.IsValid(ballRigidbody))
            {
                DefaultBallWeight = BallWeight;
                DefaultBallFriction = BallFriction;
                DefaultBallDrag = BallDrag;
                DefaultBallAngularDrag = BallAngularDrag;
            }

            if (Utilities.IsValid(ballRigidbody))
                ballRigidbody.maxAngularVelocity = 300f;

            DefaultBallMaxSpeed = maxBallSpeed;

            minGroundDotProduct = Mathf.Cos(groundSnappingMaxGroundAngle * Mathf.Deg2Rad);

            SendCustomEventDelayedSeconds(nameof(Disable), 1f);
        }

        public void Disable()
        {
            enabled = false;
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (!this.LocalPlayerOwnsThisObject() || !Utilities.IsValid(ballRigidbody))
                {
                    this.enabled = false;
                    return;
                }
            }

            this.enabled = enabled;
        }

        private void FixedUpdate()
        {
            // If ball is picked up by player - we do stuff in PostLateUpdate instead
            if (pickedUpByPlayer)
                return;

            // Freeze the ball position without going kinematic
            if (!BallIsMoving)
            {
                if (!ballRigidbody.isKinematic)
                {
                    ballRigidbody.velocity = Vector3.zero;
                    ballRigidbody.angularVelocity = Vector3.zero;
                }

                ballRigidbody.WakeUp();
            }

            if (!BallIsMoving && requestedBallVelocity != Vector3.zero)
            {
                BallIsMoving = true;
            }

            if (BallIsMoving)
            {
                // If in debug mode, log current speed for this frame
                if (playerManager.openPutt.debugMode)
                    speedDataLogging = speedDataLogging.Add(ballRigidbody.velocity.magnitude);

                // If the rigidbody fell asleep - Try applying the velocity we logged from the last frame to keep it moving (this should also wake it back up)
                if (ballRigidbody.IsSleeping() && lastFrameVelocity != Vector3.zero)
                    ballRigidbody.velocity = lastFrameVelocity;

                // If the ball has been hit
                if (requestedBallVelocity.magnitude > .001f)
                {
                    // Reset velocity of ball
                    ballRigidbody.velocity = requestedBallVelocity;

                    // Consume the hit event
                    requestedBallVelocity = Vector3.zero;

                    lastHitMaxDistance = 0;
                    lastHitTravelDistance = 0;

                    // Reset time counters
                    timeNotMoving = 0f;
                    timeMoving = 0f;
                }

                UpdatePhysicsState();

                if (Utilities.IsValid(playerManager.CurrentCourse))
                    timeMoving += Time.deltaTime;

                // If the ball is moving super slowly - count it as "not moving" and increment the timer
                if (ballRigidbody.velocity.magnitude < minBallSpeed)
                    timeNotMoving += Time.deltaTime;
                else
                    timeNotMoving = 0;

                var ballRollingForTooLong = timeMoving > maxBallRollingTime;
                var ballRollingTooSlowForTooLong = timeNotMoving >= minBallSpeedMaxTime;

                if (ballRollingForTooLong || ballRollingTooSlowForTooLong)
                {
                    if (OnGround)
                    {
                        // If the ball is currently rolling down a slope
                        if (lastGroundContactNormal.y < .99f)
                        {
                            // If we have been stuck on this slope for too long force ball stop so player can hit it
                            if (timeNotMoving > minBallSpeedMaxTime * 2f)
                            {
                                BallIsMoving = false;

                                if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                                    OpenPuttUtils.Log(this, "Ball is on a slope and appears to be stuck here - allow player to hit it again");
                            }
                            else
                            {
                                // Prevent timers from being reset
                                resetBallTimers = false;
                                // Don't stop the ball from moving (people hate it stopping on slopes)
                                BallIsMoving = true;

                                if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                                    OpenPuttUtils.Log(this, $"Ball would have stopped but it is on a slope - keep moving until we reach a flat surface. (FloorNormal.Y={lastGroundContactNormal.y})");
                            }
                        }
                        else
                        {
                            BallIsMoving = false;
                        }
                    }
                    else
                    {
                        BallIsMoving = false;
                    }
                }
                else if (!pickedUpByPlayer)
                {
                    var velMagnitude = ballRigidbody.velocity.magnitude;
                    // Apply drag force based on curve as long as the ball is rolling faster than the threshold and is grounded
                    if (velMagnitude > 0.01f && OnGround)
                    {
                        var currDrag = defaultBallDrag;

                        if (ballDragOverride > 0f)
                            currDrag = ballDragOverride;

                        if (velMagnitude < 1f)
                            currDrag *= Mathf.SmoothStep(0.1f, 1f, velMagnitude);

                        ballRigidbody.AddForce(-ballRigidbody.velocity.normalized * currDrag);
                    }

                    // Fake ball roll based on speed
                    var directionOfTravel = ballRigidbody.position - lastFramePosition;
                    var angle = directionOfTravel.magnitude * 1f * (180f / Mathf.PI) / ballCollider.radius;
                    var rotationAxis = Vector3.Cross(Vector3.up, directionOfTravel).normalized;
                    transform.localRotation = Quaternion.Euler(rotationAxis * angle) * transform.localRotation;
                    var worldRotation = transform.parent.rotation * transform.localRotation;
                    ballRigidbody.rotation = worldRotation.normalized;
                }
            }
            else
            {
                if (!ballRigidbody.isKinematic)
                {
                    ballRigidbody.velocity = Vector3.zero;
                    ballRigidbody.angularVelocity = Vector3.zero;
                }

                ballRigidbody.WakeUp();
            }

            if (BallIsMoving)
            {
                if (openPuttSync.originalPosition == respawnPosition)
                {
                    if (Physics.Raycast(ballRigidbody.position, Vector3.down, out var hit, 10f, groundSnappingProbeMask, QueryTriggerInteraction.Ignore))
                    {
                        SetRespawnPosition(hit.point);
                    }
                }

                var distanceFromRespawnPos = Vector3.Distance(ballRigidbody.position, respawnPosition);
                if (lastHitMaxDistance < distanceFromRespawnPos)
                    lastHitMaxDistance = distanceFromRespawnPos;

                var distanceTravelledThisFrame = Vector3.Distance(ballRigidbody.position, lastFramePosition);
                if (lastFramePosition.magnitude > .01f)
                    lastHitTravelDistance += distanceTravelledThisFrame;
            }

            if (ballRigidbody.isKinematic)
                lastFrameVelocity = (ballRigidbody.position - lastFramePosition) / Time.deltaTime;
            else
                lastFrameVelocity = ballRigidbody.velocity;

            lastFramePosition = ballRigidbody.position;

            // Tell PuttSync to sync position if it's attached
            var sendFastPositionSync = currentOwnerHideOverride > 0 || BallIsMoving || (Utilities.IsValid(startLine) && startLine.gameObject.activeSelf);
            if (Utilities.IsValid(openPuttSync) && sendFastPositionSync)
                openPuttSync.RequestFastSync(forceSync: true);
        }

        public void OnBallDroppedOnPad(CourseManager courseThatIsBeingStarted, CourseStartPosition position)
        {
            if (!Utilities.IsValid(courseThatIsBeingStarted))
            {
                OpenPuttUtils.LogError(position, $"Player tried to start a course but a CourseStartPosition({(Utilities.IsValid(position) && Utilities.IsValid(position.name) ? position.name : "null")}) is missing a reference to its CourseManager!");
                return;
            }

            startLine.SetEnabled(false);

            SetPosition(position.transform.position);

            if (!ballRigidbody.isKinematic)
            {
                ballRigidbody.velocity = Vector3.zero;
                ballRigidbody.angularVelocity = Vector3.zero;
            }

            SetRespawnPosition(position.transform.position);

            //  BallIsMoving = false;

            if (Utilities.IsValid(playerManager))
                playerManager.OnCourseStarted(courseThatIsBeingStarted);

            UpdateBallState(this.LocalPlayerOwnsThisObject());

            // Force a sync to make sure ball syncs to the pad fully
            openPuttSync.RequestFastSync(forceSync: true);
        }

        public override void OnPickup()
        {
            var ballShoulderPickup = shoulderPickup;
            ballHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
            shoulderBallHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;

            if (!ballRigidbody.isKinematic)
            {
                ballRigidbody.velocity = Vector3.zero;
                ballRigidbody.angularVelocity = Vector3.zero;
            }

            lastFramePosition = CurrentPosition;
            lastFrameVelocity = Vector3.zero;

            pickedUpByPlayer = true;

            if (ballHeldInHand != VRC_Pickup.PickupHand.None && ballHeldInHand != shoulderBallHeldInHand)
            {
                if (Utilities.IsValid(ballShoulderPickup) && Utilities.IsValid(ballShoulderPickup.pickup))
                    ballShoulderPickup.tempDisableAttachment = true;
            }

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.portableScoreboard))
                playerManager.openPutt.portableScoreboard.golfBallHeldByPlayer = true;

            BallIsMoving = false;

            startLine.SetEnabled(true);

            SetEnabled(true);

            UpdateBallState(this.LocalPlayerOwnsThisObject());
        }

        public override void OnDrop()
        {
            var ballShoulderPickup = shoulderPickup;

            pickedUpByPlayer = false;
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.portableScoreboard))
                playerManager.openPutt.portableScoreboard.golfBallHeldByPlayer = false;

            if (startLine.StartDropAnimation(CurrentPosition))
            {
                if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                    OpenPuttUtils.Log(this, "Player dropped ball near a start pad.. moving to the start of a course");
                ballHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                shoulderBallHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
                return;
            }

            // Player did not drop ball on a start pad and are currently playing a course
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.CurrentCourse))
            {
                if (playerManager.CurrentCourse.drivingRangeMode)
                {
                    if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                        OpenPuttUtils.Log(this, "Player dropped ball away from driving range start pad - marking driving range as completed");
                    playerManager.OnCourseFinished(playerManager.CurrentCourse, null, CourseState.Completed);
                }
                else if (Utilities.IsValid(respawnPosition))
                {
                    if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                        OpenPuttUtils.Log(this, "Player dropped ball away from a start pad.. moving ball back to last valid position.");

                    BallIsMoving = false;

                    // Put the ball back where it last stopped on the course so the player can continue
                    ballRigidbody.position = respawnPosition;

                    // Play the reset noise
                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.SFXController))
                        playerManager.openPutt.SFXController.PlayBallResetSoundAtPosition(respawnPosition);

                    pickedUpByPlayer = false;

                    ballHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                    shoulderBallHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
                    return;
                }
                else
                {
                    // We don't know where to put the ball - skip the current course
                    playerManager.OnCourseFinished(playerManager.CurrentCourse, null, CourseState.Skipped);
                }
            }

            if (ballHeldInHand == VRC_Pickup.PickupHand.None && shoulderBallHeldInHand == VRC_Pickup.PickupHand.None)
                return;

            var pickupHand = shoulderBallHeldInHand != VRC_Pickup.PickupHand.None ? shoulderBallHeldInHand : ballHeldInHand;
            var hand = pickupHand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;

            var offset = playerManager.openPutt.controllerTracker.CalculateLocalOffsetFromWorldPosition(hand, ballRigidbody.worldCenterOfMass);
            var lastHeldFrameVelocity = playerManager.openPutt.controllerTracker.GetVelocityAtOffset(hand, offset);

            if (Networking.LocalPlayer.IsUserInVR() && ballHeldInHand != VRC_Pickup.PickupHand.None)
            {
                if (shoulderBallHeldInHand != VRC_Pickup.PickupHand.None && ballHeldInHand != VRC_Pickup.PickupHand.None)
                {
                    ballShoulderPickup.tempDisableAttachment = false;
                    if (Utilities.IsValid(ballShoulderPickup.pickup))
                        ballShoulderPickup.pickup.Drop();

                    if (Utilities.IsValid(pickup))
                        pickup.Drop();

                    var flingDir = ballShoulderPickup.transform.position - transform.position;
                    var playerHeight = Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();

                    var velocityScale = Mathf.Clamp01(flingDir.magnitude / playerHeight) * 50f;
                    lastHeldFrameVelocity = flingDir.normalized * velocityScale;

                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.SFXController))
                        playerManager.openPutt.SFXController.PlayBallHitSoundAtPosition(CurrentPosition, (lastHeldFrameVelocity.magnitude * 14.28571428571429f) / 4f);
                }
            }
            else
            {
                // Normal drop behaviour
                if (Networking.LocalPlayer.IsUserInVR())
                    lastHeldFrameVelocity *= pickup.ThrowVelocityBoostScale;
            }

            // Switch ball physics on
            BallIsMoving = true;

            // Allows ball to bounce
            stepsInAir = -1;

            // Apply velocity of the ball that we saw last frame so players can throw the ball
            if (lastHeldFrameVelocity.magnitude > .001f || ballHeldInHand != VRC_Pickup.PickupHand.None)
            {
                requestedBallVelocity = lastHeldFrameVelocity.Sanitized();
                ballHeldInHand = VRC_Pickup.PickupHand.None;
                shoulderBallHeldInHand = VRC_Pickup.PickupHand.None;
            }
        }

        /// <summary>
        /// Called by external scripts when the ball has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            OnPickup();

            if (Utilities.IsValid(playerManager))
                playerManager.BallVisible = true;

            playerManager.RequestSync(syncNow: true);
        }

        /// <summary>
        /// Called by external scripts when the ball has been dropped
        /// </summary>
        public void OnScriptDrop()
        {
            OnDrop();

            if (Utilities.IsValid(playerManager))
                playerManager.BallVisible = true;

            playerManager.RequestSync();
        }

        public void RespawnBall()
        {
            var respawnPos = respawnPosition;

            BallIsMoving = false;

            SetPosition(respawnPos);

            SetRespawnPosition(respawnPos);
        }

        public void SetPosition(Vector3 worldPos)
        {
            ballRigidbody.position = worldPos;
        }

        public void SetRespawnPosition(Vector3 pos)
        {
            respawnPosition = pos;
            if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Ball respawn position is now {respawnPosition}");
        }

        public void _RespawnBallWithErrorNoise()
        {
            RespawnBall();

            // Play the reset noise
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.SFXController))
                playerManager.openPutt.SFXController.PlayBallResetSoundAtPosition(respawnPosition);
        }

        public void OnBallHit(Vector3 withVelocity)
        {
            SetEnabled(true);

            // Tell the club to disarm for a second
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.golfClub))
                playerManager.golfClub.DisableClubColliderFor();

            var playerIsPlayingACourse = Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.CurrentCourse);

            // Discard any hits while the ball is already moving and the player is playing a course (allows them to hit the ball as much as they want otherwise)
            if (playerIsPlayingACourse && !allowBallHitWhileMoving && BallIsMoving)
                return;

            if (withVelocity.magnitude == 0)
                return;

            // Set the lastFrameVelocity to this, so if we collide with a wall straight away we bounce off it properly
            lastFrameVelocity = withVelocity;

            // Tell the ball to apply the velocity in the next FixedUpdate() frame
            requestedBallVelocity = withVelocity;

            if (Utilities.IsValid(playerManager))
                playerManager.OnBallHit(withVelocity.magnitude);

            // Vibrate the controller to give feedback to the player
            var currentHand = club.CurrentHand;
            if (currentHand != VRC_Pickup.PickupHand.None)
            {
                var velocity = withVelocity.magnitude;

                // Half the amplitude for native quest (quest vibration is stronger)
#if UNITY_ANDROID
                velocity *= 0.5f;
#endif

                var hapticAmplitude = 1f * Mathf.Clamp(velocity / maxBallSpeed, .5f, 1f);
                Networking.LocalPlayer.PlayHapticEventInHand(currentHand, 0.25f, hapticAmplitude, 230f);
            }

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.SFXController))
                playerManager.openPutt.SFXController.PlayBallHitSoundAtPosition(CurrentPosition, (requestedBallVelocity.magnitude * 14.28571428571429f) / 4f);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            UpdateBallState(this.LocalPlayerOwnsThisObject());
        }

        private void OnCollisionEnter(Collision collision)
        {
            // If we hit something with a dynamic rigidbody that is moving, make sure ball can move
            if (!pickedUpByPlayer && Utilities.IsValid(collision) && Utilities.IsValid(collision.rigidbody) && Utilities.IsValid(collision.collider))
            {
                // If the golf club hit the ball... let the golf club handle that hit properly
                if (collision.rigidbody.gameObject == playerManager.golfClubHead.gameObject) return;

                //  if (!collision.rigidbody.isKinematic && !collision.collider.isTrigger && (collision.rigidbody.velocity.magnitude > 0f || collision.rigidbody.angularVelocity.magnitude > 0f))
                {
                    SetEnabled(true);

                    if (BallIsMoving)
                    {
                        // Just reset the timers
                        timeNotMoving = 0f;
                        timeMoving = 0f;
                    }
                    else
                    {
                        BallIsMoving = true;
                    }
                }
            }

            if (!BallIsMoving)
            {
                ballRigidbody.position = lastFramePosition;
                return;
            }

            // Hit something from the side or above the ball
            if (Utilities.IsValid(collision.collider) && Utilities.IsValid(collision.collider.material) && collision.collider.material.name.StartsWith(wallMaterial.name))
                ReflectCollision(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            // If we hit something with a dynamic rigidbody that is moving, make sure ball can move
            if (!pickedUpByPlayer && Utilities.IsValid(collision) && Utilities.IsValid(collision.rigidbody) && Utilities.IsValid(collision.collider))
            {
                // If the golf club hit the ball... let the golf club handle that hit properly
                if (collision.rigidbody.gameObject == playerManager.golfClubHead.gameObject) return;

                // TODO: A static rigidbody may or may not help with ball collisions
                // This if statement will confuse things though.. need to think of a better way (Steppy thing doesn't work properly with it)
                // if (!collision.rigidbody.isKinematic && !collision.collider.isTrigger && (collision.rigidbody.velocity.magnitude > 0f || collision.rigidbody.angularVelocity.magnitude > 0f))
                {
                    if (BallIsMoving)
                    {
                        // Just reset the timers
                        timeNotMoving = 0f;
                        timeMoving = 0f;
                    }
                    else
                    {
                        BallIsMoving = true;
                    }
                }
            }
        }

        /// <summary>
        /// Actually calculates where the ball should bounce when it contacts a wall<br/>
        /// Default Unity physics has an issue where the ball won't bounce under a certain speed and just rolls along it instead.
        /// </summary>
        /// <param name="collision">The collision that happened</param>
        private void ReflectCollision(Collision collision)
        {
            if (!Utilities.IsValid(ballRigidbody) || !Utilities.IsValid(collision) || collision.contacts.Length == 0 || collision.contactCount == 0)
                return;

            // Work out which direction we need to bounce off the wall
            var contact = collision.contacts[0];
            if (!Utilities.IsValid(contact)) return;

            var collisionNormal = contact.normal.Sanitized();

            // Checks if the collision was from "below" and ignores it
            if (collisionNormal.y > wallBounceHeightIgnoreAmount)
            {
                if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                    OpenPuttUtils.Log(this, "Ignored wall bounce because it was below me!");
                return;
            }

            // Maybe fix the ball getting stuck on walls
            if (lastFrameVelocity == Vector3.zero)
                lastFrameVelocity = ballRigidbody.velocity;

            // Reflect the current vector off the wall
            var newDirection = Vector3.Reflect(lastFrameVelocity.normalized, collisionNormal).Sanitized();

            // If we still don't have a velocity don't do anything else as we might get stuck against the wall
            if (newDirection == Vector3.zero)
                return;

            var v = Vector3.Project(newDirection, collisionNormal).Sanitized();

            // How bouncy is this wall?
            var bounceMultiplier = wallBounceSpeedMultiplier;
            if (Utilities.IsValid(collision.collider) && Utilities.IsValid(collision.collider.material) && collision.collider.material.name.Length == 0)
                bounceMultiplier = collision.collider.material.bounciness;
            if (bounceMultiplier < .01f)
                bounceMultiplier = wallBounceSpeedMultiplier;
            if (bounceMultiplier < .01f)
                bounceMultiplier = .8f;

            var speedAfterBounce = lastFrameVelocity.magnitude * bounceMultiplier;

            newDirection = (newDirection - v * wallBounceDeflection) * speedAfterBounce;

            // If we reflected off a wall and the resulting bounce is going upwards
            if (newDirection.y > .001f)
            {
                // Pretend we're in the air for the next frame so the snapping is switched off
                stepsOnGround = 0;
                stepsInAir = -1;
            }

            // Set the ball velocity so it bounces the right way
            lastFrameVelocity = ballRigidbody.velocity = newDirection.Sanitized();

            // Play a hit sound because we bounced off something
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.SFXController))
                playerManager.openPutt.SFXController.PlayBallHitSoundAtPosition(CurrentPosition, ballRigidbody.velocity.magnitude / 10f);
        }

        private bool lastGrounded = false;

        void UpdatePhysicsState()
        {
            var isGrounded = Physics.Raycast(CurrentPosition, Vector3.down, out var groundingHit, ballCollider.radius + groundRaycastDistance, groundSnappingProbeMask);

            if (isGrounded)
            {
                stepsOnGround += 1;
                stepsInAir = 0;
                timeFlying = 0;
            }
            else
            {
                stepsOnGround = 0;
                stepsInAir += 1;
                timeFlying += Time.deltaTime;
            }

            // Debug thing - Green Ball = Grounded / Red Ball = Not grounded
            if (ballGroundedDebug && isGrounded != lastGrounded)
                playerManager.BallColor = isGrounded ? Color.green : Color.red;

            lastGrounded = isGrounded;

            var canPlayHitGroundSound = timeFlying > .5f && isGrounded;

            HandleGroundSnapping(isGrounded);

            if (audioWhenBallHitsFloor && canPlayHitGroundSound)
            {
                if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.SFXController))
                    playerManager.openPutt.SFXController.PlayBallHitSoundAtPosition(CurrentPosition, (lastFrameVelocity.magnitude * 14.28571428571429f) * 0.6f); // Play a bounce sound but a bit quieter
            }

            if (!enableAirResistance) return;

            // Apply air resistance to the ball
            const float airDensity = 1.225f;
            const float dragCoeff = .35f;
            var ballCrossSection = Mathf.PI * ballCollider.radius * ballCollider.radius;
            var dragVel = ballRigidbody.velocity.Sanitized();
            var dragAccel = (0.5f * airDensity * ballCrossSection * dragCoeff * dragVel.magnitude * dragVel).Sanitized();
            ballRigidbody.AddForce(-dragAccel);
        }

        private void HandleGroundSnapping(bool isGrounded)
        {
            if (!enableBallSnap) return;

            var shouldSnapToGround = enableBallSnap && !pickedUpByPlayer && isGrounded && stepsOnGround > 2 && stepsInAir == 0;
            if (!shouldSnapToGround) return;

            var velocity = ballRigidbody.velocity;
            var speed = velocity.magnitude;

            // Ground surface detection
            var foundFloor = Physics.Raycast(CurrentPosition, Vector3.down, out var snapHit, ballCollider.radius + groundSnappingProbeDistance, groundSnappingProbeMask);

            if (!foundFloor) return;

            // Update ground contact normal
            var currentGroundNormal = snapHit.normal;
            var lastNormalY = lastGroundContactNormal.y;
            lastGroundContactNormal = currentGroundNormal;

            // Ramp angle and steepness checks
            var rampAngle = Mathf.Acos(Vector3.Dot(currentGroundNormal, Vector3.up)) * Mathf.Rad2Deg;

            // Disable snapping for steep surfaces or flattening slopes
            if (currentGroundNormal.y < minGroundDotProduct || currentGroundNormal.y >= lastNormalY)
                return;

            // Project velocity onto ground plane
            var newVelocity = Vector3.ProjectOnPlane(lastFrameVelocity, currentGroundNormal).normalized * speed;

            // Apply gravity influence
            var gravityInfluence = Mathf.Sin(rampAngle * Mathf.Deg2Rad);
            newVelocity.y -= gravityInfluence * Time.deltaTime * Physics.gravity.magnitude;

            // Apply velocity correction if vertical velocity is above threshold
            if (newVelocity.y > .001f)
            {
                ballRigidbody.velocity = lastFrameVelocity = newVelocity;
            }
        }

        public void OnRespawn()
        {
            SendCustomEventDelayedFrames(nameof(RespawnBall), 2);
        }

        public void Wakeup()
        {
            ballRigidbody.WakeUp();
        }

        /// <summary>
        /// Returns the data from the last hit of the ball
        /// </summary>
        /// <param name="maxStraightLineDistance">The furthest distance recorded</param>
        /// <param name="totalDistanceTravelled">The total amount of distance that was covered by the ball</param>
        public void GetLastHitData(out float maxStraightLineDistance, out float totalDistanceTravelled)
        {
            maxStraightLineDistance = lastHitMaxDistance;
            totalDistanceTravelled = lastHitTravelDistance;
        }

        /// <summary>
        /// Updates the state of various toggles on the ball depending on whether the local player owns this ball or not
        /// </summary>
        /// <param name="localPlayerIsOwner">Does the local player own this ball? (Can usually pass in this.LocalPlayerOwnsThisObject())</param>
        public void UpdateBallState(bool localPlayerIsOwner)
        {
            if (localPlayerIsOwner)
            {
                // Enable the collider and make sure it can hit surfaces
                if (Utilities.IsValid(ballCollider))
                {
                    ballCollider.enabled = true;
                    ballCollider.isTrigger = false;

                    ballCollider.contactOffset = 0.0005f;
                }

                if (Utilities.IsValid(ballRigidbody))
                {
                    // This avoids a warning when setting the collision mode below
                    ballRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                    // Toggle gravity/kinematic on/off depending on whether the ball is moving
                    ballRigidbody.useGravity = BallIsMoving;
                    ballRigidbody.isKinematic = !BallIsMoving;
                    ballRigidbody.detectCollisions = true;
                    ballRigidbody.drag = 0.05f;
                    ballRigidbody.angularDrag = 0;

                    // Set the appropriate collision detection mode (speculative picks up club hits better - dynamic works better when the ball is moving around, speculative makes it bounce off random edges it shouldn't)
                    ballRigidbody.collisionDetectionMode = ballRigidbody.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
                    //ballRigidbody.collisionDetectionMode = requestedCollisionMode;
                }

                // Only allow the local player to pick up their ball when it has stopped
                var newPickupState = false;

                if (!BallIsMoving)
                {
                    newPickupState = allowBallPickup;

                    if (Utilities.IsValid(playerManager) && allowBallPickupWhenNotPlaying)
                    {
                        newPickupState = !Utilities.IsValid(playerManager.CurrentCourse);

                        if (!newPickupState && Utilities.IsValid(playerManager.CurrentCourse) && playerManager.courseScores[playerManager.CurrentCourse.holeNumber] == 0)
                            newPickupState = true; // Should let players pick the ball up from the start pad
                    }
                }

                if (Utilities.IsValid(pickup))
                    pickup.pickupable = newPickupState;

                if (Utilities.IsValid(ballCollider))
                    ballCollider.enabled = true;
            }
            else
            {
                // Disable collider so other players can't collide
                if (Utilities.IsValid(ballCollider))
                {
                    ballCollider.enabled = false;
                    ballCollider.isTrigger = true;

                    ballCollider.contactOffset = 0.01f;
                }

                // Disable rigidbody movement because it's not needed for non-local players
                if (Utilities.IsValid(ballRigidbody))
                {
                    ballRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    ballRigidbody.useGravity = false;
                    ballRigidbody.isKinematic = true;
                    ballRigidbody.detectCollisions = false;
                    ballRigidbody.Sleep();
                }

                // Stop other players from picking this ball up
                if (Utilities.IsValid(pickup))
                    pickup.pickupable = false;

                if (Utilities.IsValid(ballCollider))
                    ballCollider.enabled = false;

                startLine.SetEnabled(false);
            }
        }
    }
}