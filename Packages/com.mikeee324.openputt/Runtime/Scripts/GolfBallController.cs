using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using Varneon.VUdon.ArrayExtensions;
using VRC.Dynamics;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync), RequireComponent(typeof(VRCPickup)), RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(SphereCollider)), DefaultExecutionOrder(100)]
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
        public MaterialPropertyBlock ghostMaterialPropertyBlock;

        [Tooltip("Used to identify a wall collider and perform a bounce")]
        public PhysicMaterial wallMaterial;

        [Tooltip("Used to identify whether the ball is still on the course or not when it stops rolling")]
        public PhysicMaterial floorMaterial;

        public PlayerManager playerManager;

        [SerializeField]
        private TrailRenderer trail;

        public Rigidbody ballRigidbody;

        [SerializeField]
        private SphereCollider ballCollider;

        [Space]
        [Header("General Settings")]
        [Tooltip("Allows players to pick up their ball at any time as long as the ball is not moving")]
        public bool allowBallPickup;

        [Tooltip("Allows players to pick up their ball if they are not currently playing a course AND the ball is not moving")]
        public bool allowBallPickupWhenNotPlaying = true;

        [Tooltip("Allows players to hit the ball at any time while it is moving (Default is no, which only lets them do this while they are not playing a course)")]
        public bool allowBallHitWhileMoving;

        [Tooltip("Should the ball hit noise be played if the ball falls onto a floor?")]
        public bool audioWhenBallHitsFloor = true;

        [Space]
        [Header("Ball Physics")]
        [Tooltip("The direction of the gravity that will be applied to the ball")]
        public Vector3 gravityDirection = Vector3.down;

        public float gravityMagnitude = 9.87f;

        [Range(0f, 1f), Tooltip("Caps the total Magnus force (from back/side spin) to this fraction of the ball's weight. This is what stops lofted/backspin shots from curling into a loop: the ball can float and extend its carry, but the spin force can never overpower gravity enough to bend the path back on itself. 1 = force can match gravity (max float/curve, still stable), lower = less float and gentler curve.")]
        public float maxLiftGravityFraction = 0.85f;

        // [Tooltip("Which layers can start the ball moving when they collide with the ball? (For spinny things etc)")]
        //public LayerMask allowNonClubCollisionsFrom = 0;
        [Range(0, .5f), Tooltip("The amount of drag to apply to the balls RigidBody by default (Can be overriden by other scripts for sand pits and things)")]
        public float defaultBallDrag = .055f;

        [Range(0, 1), Tooltip("The amount of drag to apply to the balls RigidBody (Overrides the default drag above)")]
        public float ballDragOverride = 0;

        [Tooltip("Toggles air resistance on the ball, helps it slow down while in the air and on ground better")]
        public bool enableAirResistance = true;

        [SerializeField, Range(0f, 200f), Tooltip("This defines the fastest this ball can travel after being hit by a club (m/s) - Bear in mind the fastest club swing recorded is 108~ m/s")]
        private float maxBallSpeed = 100f;

        [Range(0f, .2f), Tooltip("If the ball goes below this speed it will be counted as 'not moving' and will be stopped after the amount of time defined below")]
        public float minBallSpeed = 0.03f;

        [Range(0f, .2f), Tooltip("If the ball goes below this speed it will be counted as 'not moving' and will be stopped after the amount of time defined below")]
        public float minBallHitSpeed = 0.1f;

        [Range(0f, 1f), Tooltip("Defines how long the ball can keep rolling for when it goes below the minimum speed")]
        public float minBallSpeedMaxTime = 1f;

        [Tooltip("How long a ball can roll before being stopped automatically (in seconds)")]
        public float maxBallRollingTime = 30f;

        [Space]
        [SerializeField, Min(0f), Tooltip("An extra buffer to check if the ball is on the ground (in meters)")]
        float groundRaycastDistance = 0.02f;

        [Tooltip("Master toggle for the script-driven ground snapping/rolling. When off, the ball rolls purely under Unity's physics. Wired to the in-world dev-menu 'ball snapping' checkbox")]
        public bool enableBallSnap = true;

        [SerializeField, Min(0f), FormerlySerializedAs("takeOverMinSpeed"), Tooltip("Below this speed (m/s) snapping hands grounded rolling back to Unity's physics, which settles the ball against walls / in ditches cleanly (the snap integrator has no real contact solver, so it jitters when wedged). Above it, the script drives the roll to kill mesh-edge ghosts. Raise it if a ball jitters/bounces when coming to rest; lower it to keep snapping active for slower rolls")]
        float ballSnapMinSpeed = 0.4f;

        [SerializeField]
        LayerMask groundSnappingProbeMask = -1;

        [Space]
        [Range(0f, 0.5f), Tooltip("How far a surface normal may tilt from vertical and still count as a 'wall' for custom bouncing. 0.02≈1°, 0.15≈8.6°, 0.3≈17.5°. Keep below ~0.6 so ramps/floors aren't treated as walls")]
        public float wallDetectionTolerance = 0.15f;

        [Range(0.1f, 2f), Tooltip("Used to pretend to absorb energy from the ball when it collides with a wall (Only used if the collider does not have a PhysicMaterial assigned)")]
        public float wallBounceSpeedMultiplier = 0.8f;

        [Range(0, 0.5f)]
        [Tooltip("Controls how the ball reflects off walls. 0=Perfect reflection 0.5=Half the direction is lost 1=Runs along the wall it hits")]
        public float wallBounceDeflection = .1f;

        [Space]
        [Header("Respawn Settings")]
        [Tooltip("Toggles whether the ball is sent to the respawn position if it stops outside a course")]
        public bool respawnAutomatically = true;

        [Tooltip("If the ball stops outside of a course, this is where it will respawn to in world space")]
        public Vector3 respawnWorldPosition = Vector3.positiveInfinity;

        private bool HasRespawnPosition => !float.IsPositiveInfinity(respawnWorldPosition.x);

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

                _UpdateBallState(localPlayerIsOwner);

                // Tells the players golf club to update its current state
                if (Utilities.IsValid(club))
                    club._RefreshState();

                // Only the owner of the ball can run physics on it (everyone else should only receive ObjectSync updates)
                if (!localPlayerIsOwner)
                {
                    _ballMoving = false;
                    _SetEnabled(false);
                    return;
                }

                _SetEnabled(_ballMoving);

                if (!ballWasMoving && _ballMoving)
                {
                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.eventHandler))
                        playerManager.openPutt.eventHandler.OnPlayerBallStartedMoving(playerManager.Owner);
                }

                if (ballWasMoving && !_ballMoving)
                {
                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
                    {
                        if (Utilities.IsValid(playerManager.openPutt.eventHandler))
                            playerManager.openPutt.eventHandler.OnPlayerBallStopped(playerManager.Owner);
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

                        playerManager._OnCourseFinished(course, null, CourseState.Completed);

                        // If we can replay the course - automatically restart the course
                        if (playerManager.openPutt.replayableCourses || course.courseIsAlwaysReplayable)
                        {
                            playerManager._OnCourseStarted(course);
                            _RespawnBall();
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
                                _SetRespawnPosition(CurrentPosition);
                            }
                        }
                        else if (HasRespawnPosition)
                        {
                            // It is not on top of a course floor so move it to the previous position
                            ballRigidbody.position = respawnWorldPosition;

                            // Play the reset noise
                            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController))
                                playerManager.openPutt.sfxController.PlayBallResetSoundAtPosition(respawnWorldPosition);
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
                //Utils.Log(this, $"BallWasMoving({ballWasMoving}) BallMoving({_ballMoving}) RespawnAuto({respawnAutomatically}) BallValidPos({ballIsInValidPosition}) PickedUp({pickedUpByPlayer}) RespawnPos({respawnWorldPosition})");
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

        /// Script-driven velocity used while the ground-snap integrator drives grounded rolling
        private Vector3 snapVelocity;
        private bool wasGroundSnapping;

        /// External force-zone pushes (Normal/CenterPush) accumulated since the last FixedUpdate. The
        /// ground-snap folds these into its own velocity so they aren't lost while it's driving; when it
        /// isn't, UpdatePhysicsState hands them to PhysX. Kept off the rigidbody so collision impulses
        /// don't leak into the ground-snap's clean velocity.
        private Vector3 pendingExternalForce;

        /// Most recent steep (wall/obstacle) contact normal seen while the ground-snap drives grounded
        /// rolling, plus a freshness counter so it expires once the ball leaves the wall. Lets
        /// DriveGroundSnap stop integrating velocity straight into a wall it's pressed against.
        private Vector3 snapWallNormal = Vector3.up;
        private int snapWallSteps;

        private Vector3 lastFramePosition;

        private float lastKnownGroundFriction = 0;

        private int insideGravityZones = 0;

        /// Stores the velocity of the club that needs to be applied in the next FixedUpdate() frame
        public Vector3 requestedBallVelocity = Vector3.zero;

        public bool OnGround => stepsOnGround > 1;

        private Vector3 lastGroundContactNormal = Vector3.up;
        private float timeFlying = 0;
        int stepsOnGround;
        private bool resetBallTimers = true;
        private float defaultGravityMagnitude = 9.87f;

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

        private Vector3 _currentSpin = Vector3.zero;

        /// <summary>
        /// The gravity direction the stored spin axis is currently aligned to. If a world rotates its
        /// gravity mid-flight (gravity-flip zones etc) we rotate _currentSpin by the same delta so the
        /// backspin lift and sidespin curve keep behaving relative to the *current* gravity.
        /// </summary>
        private Vector3 _prevGravityDirection = Vector3.down;

        public bool isHeldInTeleporter { get; set; }

        /// Converts ball velocity (m/s) to a normalised audio volume scale used by PlayBallHitSoundAtPosition
        private const float VelocityToAudioScale = 14.285714f;

        #endregion

        void Start()
        {
            gravityMagnitude = Physics.gravity.magnitude;
            defaultGravityMagnitude = Physics.gravity.magnitude;

            // Initialise gravity direction from Physics.gravity so runtime gravity changes
            // are reflected unless a force zone overrides the ball's gravity.
            gravityDirection = Physics.gravity.sqrMagnitude > 0f ? Physics.gravity.normalized : Vector3.down;

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
                ballRigidbody.maxAngularVelocity = 300f;
            }

            DefaultBallMaxSpeed = maxBallSpeed;

            lastFramePosition = ballRigidbody.position;

            SendCustomEventDelayedSeconds(nameof(_Disable), 1f);
        }

        public void _Disable()
        {
            enabled = false;
        }

        public void _SetEnabled(bool enabled)
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
            // Ensure gravityDirection is normalized each physics step so external
            // assignments (force zones, runtime changes) don't break calculations.
            gravityDirection = gravityDirection.sqrMagnitude > 0f ? gravityDirection.normalized : Vector3.down;

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

                    // Seed the ground-snap integrator. Clearing the flag forces a clean re-adoption
                    // next frame (reads ballRigidbody.velocity which we just set), while keeping
                    // stepsOnGround intact so ghost-edge protection stays active immediately.
                    snapVelocity = requestedBallVelocity;
                    wasGroundSnapping = false;

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
                        var floorDotRelativeToGravity = Vector3.Dot(lastGroundContactNormal, -gravityDirection);
                        if (floorDotRelativeToGravity < .99f)
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
                                    OpenPuttUtils.Log(this, $"Ball would have stopped but it is on a slope - keep moving until we reach a flat surface. (FloorNormal.Dot={floorDotRelativeToGravity})");
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
                    // Fake ball roll based on speed
                    var directionOfTravel = ballRigidbody.position - lastFramePosition;
                    var angle = directionOfTravel.magnitude * 1f * (180f / Mathf.PI) / ballCollider.radius;
                    var rotationAxis = Vector3.Cross(-gravityDirection, directionOfTravel).normalized;
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
                if (!HasRespawnPosition || openPuttSync.originalPosition == respawnWorldPosition)
                {
                    if (Physics.Raycast(ballRigidbody.position, gravityDirection, out var hit, 10f, groundSnappingProbeMask, QueryTriggerInteraction.Ignore))
                    {
                        _SetRespawnPosition(hit.point);
                    }
                }

                var distanceFromRespawnPos = Vector3.Distance(ballRigidbody.position, respawnWorldPosition);
                lastHitMaxDistance = Mathf.Max(lastHitMaxDistance, distanceFromRespawnPos);

                var distanceTravelledThisFrame = Vector3.Distance(ballRigidbody.position, lastFramePosition);
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
                openPuttSync._RequestFastSync(forceSync: true);
        }

        public void _OnBallDroppedOnPad(CourseManager courseThatIsBeingStarted, CourseStartPosition position)
        {
            if (!Utilities.IsValid(courseThatIsBeingStarted))
            {
                OpenPuttUtils.LogError(position, $"Player tried to start a course but a CourseStartPosition({(Utilities.IsValid(position) && Utilities.IsValid(position.name) ? position.name : "null")}) is missing a reference to its CourseManager!");
                return;
            }

            startLine.SetEnabled(false);

            _SetPosition(position.transform.position);

            if (!ballRigidbody.isKinematic)
            {
                ballRigidbody.velocity = Vector3.zero;
                ballRigidbody.angularVelocity = Vector3.zero;
            }

            _SetRespawnPosition(position.transform.position);

            //  BallIsMoving = false;

            if (Utilities.IsValid(playerManager))
                playerManager._OnCourseStarted(courseThatIsBeingStarted);

            _UpdateBallState(this.LocalPlayerOwnsThisObject());

            // Force a sync to make sure ball syncs to the pad fully
            openPuttSync._RequestFastSync(forceSync: true);
        }

        public override void OnPickup()
        {
            if (Utilities.IsValid(playerManager) && !playerManager.BallVisible)
                playerManager.BallVisible = true;

            playerManager._RequestSync(syncNow: true);

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

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
            {
                if (!playerManager.openPutt.hasUsedGolfBall)
                {
                    playerManager.openPutt.hasUsedGolfBall = true;
                    if (Utilities.IsValid(playerManager.openPutt.uiController))
                        playerManager.openPutt.uiController.UpdateButtonStates();
                }

                if (Utilities.IsValid(playerManager.openPutt.openPuttPortableScoreboard))
                    playerManager.openPutt.openPuttPortableScoreboard.golfBallHeldByPlayer = true;
            }

            BallIsMoving = false;

            startLine.SetEnabled(true);

            _SetEnabled(true);

            _UpdateBallState(this.LocalPlayerOwnsThisObject());
            
            RequestSerialization();
        }

        public override void OnDrop()
        {
            if (Utilities.IsValid(playerManager) && !playerManager.BallVisible)
                playerManager.BallVisible = true;

            playerManager._RequestSync();

            var ballShoulderPickup = shoulderPickup;
            var currentBallHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
            var currentShoulderBallHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;

            pickedUpByPlayer = false;
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.openPuttPortableScoreboard))
                playerManager.openPutt.openPuttPortableScoreboard.golfBallHeldByPlayer = false;

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
                    playerManager._OnCourseFinished(playerManager.CurrentCourse, null, CourseState.Completed);
                }
                else if (HasRespawnPosition)
                {
                    if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                        OpenPuttUtils.Log(this, "Player dropped ball away from a start pad.. moving ball back to last valid position.");

                    BallIsMoving = false;

                    // Put the ball back where it last stopped on the course so the player can continue
                    ballRigidbody.position = respawnWorldPosition;
                    lastFramePosition = respawnWorldPosition;

                    // Play the reset noise
                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController))
                        playerManager.openPutt.sfxController.PlayBallResetSoundAtPosition(respawnWorldPosition);

                    pickedUpByPlayer = false;

                    ballHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                    shoulderBallHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
                    return;
                }
                else
                {
                    // We don't know where to put the ball - skip the current course
                    playerManager._OnCourseFinished(playerManager.CurrentCourse, null, CourseState.Skipped);
                }
            }

            if (ballHeldInHand == VRC_Pickup.PickupHand.None && shoulderBallHeldInHand == VRC_Pickup.PickupHand.None)
                return;

            var pickupHand = currentShoulderBallHeldInHand != VRC_Pickup.PickupHand.None ? currentShoulderBallHeldInHand : currentBallHeldInHand;
            if (pickupHand == VRC_Pickup.PickupHand.None)
                pickupHand = shoulderBallHeldInHand != VRC_Pickup.PickupHand.None ? shoulderBallHeldInHand : ballHeldInHand;

            var hand = pickupHand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;

            var offset = playerManager.openPutt.controllerTracker.CalculateLocalOffsetFromWorldPosition(hand, ballRigidbody.worldCenterOfMass);
            var lastHeldFrameVelocity = playerManager.openPutt.controllerTracker.GetVelocityAtOffset(hand, offset);

            if (Networking.LocalPlayer.IsUserInVR() && ballHeldInHand != VRC_Pickup.PickupHand.None)
            {
                var bothSidesWereHeld = shoulderBallHeldInHand != VRC_Pickup.PickupHand.None && ballHeldInHand != VRC_Pickup.PickupHand.None;
                var oneSideStillHeld = (currentBallHeldInHand != VRC_Pickup.PickupHand.None) != (currentShoulderBallHeldInHand != VRC_Pickup.PickupHand.None);

                if (bothSidesWereHeld && oneSideStillHeld)
                {
                    ballShoulderPickup.tempDisableAttachment = false;

                    if (Utilities.IsValid(ballShoulderPickup.pickup) && currentShoulderBallHeldInHand != VRC_Pickup.PickupHand.None)
                        ballShoulderPickup.pickup.Drop();

                    if (Utilities.IsValid(pickup) && currentBallHeldInHand != VRC_Pickup.PickupHand.None)
                        pickup.Drop();

                    var flingDir = ballShoulderPickup.transform.position - transform.position;
                    var playerHeight = Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();

                    var velocityScale = Mathf.Clamp01(flingDir.magnitude / playerHeight) * 50f;
                    lastHeldFrameVelocity = flingDir.normalized * velocityScale;

                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController))
                        playerManager.openPutt.sfxController.PlayBallHitSoundAtPosition(CurrentPosition, (lastHeldFrameVelocity.magnitude * 14.28571428571429f) / 4f);
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
        public void _OnScriptPickup()
        {
            OnPickup();
        }

        /// <summary>
        /// Called by external scripts when the ball has been dropped
        /// </summary>
        public void _OnScriptDrop()
        {
            OnDrop();
        }

        public void _RespawnBall()
        {
            var respawnPos = respawnWorldPosition;

            BallIsMoving = false;

            _SetPosition(respawnPos);

            _SetRespawnPosition(respawnPos);
        }

        public void _SetPosition(Vector3 worldPos)
        {
            ballRigidbody.position = worldPos;
            lastFramePosition = worldPos;
        }

        public void _SetRespawnPosition(Vector3 pos)
        {
            respawnWorldPosition = pos;
            if (Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Ball respawn position is now {respawnWorldPosition}");
        }

        public void _RespawnBallWithErrorNoise()
        {
            _RespawnBall();

            // Play the reset noise
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController))
                playerManager.openPutt.sfxController.PlayBallResetSoundAtPosition(respawnWorldPosition);
        }

        public void _OnBallHit(Vector3 withVelocity, Vector3 sideSpin)
        {
            _SetEnabled(true);

            // Tell the club to disarm for a second
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.golfClub))
                playerManager.golfClub._DisableClubColliderFor();

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
            _currentSpin = sideSpin;
            _prevGravityDirection = gravityDirection;

            if (Utilities.IsValid(playerManager))
                playerManager._OnBallHit(withVelocity.magnitude);

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

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController))
                playerManager.openPutt.sfxController.PlayBallHitSoundAtPosition(CurrentPosition, requestedBallVelocity.magnitude * VelocityToAudioScale / 4f);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            _UpdateBallState(this.LocalPlayerOwnsThisObject());
        }

        private void OnCollisionEnter(Collision collision)
        {
            // If we hit something with a dynamic rigidbody that is moving, make sure ball can move
            if (!pickedUpByPlayer && Utilities.IsValid(collision) && Utilities.IsValid(collision.rigidbody) && Utilities.IsValid(collision.collider))
            {
                // If the golf club hit the ball... let the golf club handle that hit properly
                if (collision.rigidbody.gameObject == playerManager.golfClubHead.gameObject) return;

                //  if (!collision.rigidbody.isKinematic && !collision.collider.isTrigger && (collision.rigidbody.velocity.magnitude > 0f || collision.rigidbody.angularVelocity.magnitude > 0f))
                _SetEnabled(true);
                ResetBallCollisionTimers();
            }

            if (!BallIsMoving)
            {
                ballRigidbody.position = lastFramePosition;
                return;
            }

            // Shave off a chunk of spin when we hit something
            if (_currentSpin.sqrMagnitude > 0.001f)
                _currentSpin *= 0.5f;

            // Remember any wall/obstacle we're touching so the ground-snap doesn't ram it (see method)
            RecordSnapWallContact(collision);

            // Hit something from the side or above the ball
            if (Utilities.IsValid(collision) && Utilities.IsValid(collision.collider))
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
                ResetBallCollisionTimers();
            }

            // Refresh the wall/obstacle memory every frame we stay in contact so the ground-snap keeps
            // steering its velocity along the wall instead of into it (see RecordSnapWallContact)
            RecordSnapWallContact(collision);
        }

        private void ResetBallCollisionTimers()
        {
            if (BallIsMoving)
            {
                timeNotMoving = 0f;
                timeMoving = 0f;
            }
            else
            {
                BallIsMoving = true;
            }
        }

        public override void OnContactEnter(ContactEnterInfo contactInfo)
        {
            ContactSenderProxy sender = contactInfo.contactSender;

            if (!Utilities.IsValid(sender) || !sender.isValid)
                return;

            if (sender.player != Networking.GetOwner(gameObject))
                return;

            if (!allowBallHitWhileMoving && BallIsMoving)
                return;

            Vector3 swingVelocity = contactInfo.enterVelocity;
            Quaternion clubRotation = sender.rotation;
            Vector3 clubFaceDirection = clubRotation * Vector3.forward;

            club.putter.HandleBallHit(swingVelocity, clubFaceDirection);

            if (Utilities.IsValid(playerManager))
                playerManager._OnWeirdThingHappened();
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

            // If we have gravity
            if (gravityMagnitude > .01f)
            {
                // Check if this is considered a wall in this gravity
                var wallNormalDotRelativeToGravity = Vector3.Dot(collisionNormal, gravityDirection);

                // A wall collision should be somewhere near zero. Allow a few degrees of tilt so
                // leaning walls / props still get the reliable custom bounce instead of falling
                // through to Unity's physics (which won't bounce slow balls and just rolls them).
                if (!wallNormalDotRelativeToGravity.IsNearZero(wallDetectionTolerance))
                    return;
            }

            // Reject mesh-edge "ghost" collisions: when the sphere catches the leading edge of a mesh
            // ramp/floor, Unity reports a near-horizontal (wall-like) normal even though the real
            // surface is a shallow ramp. Bouncing off that fakes a wall and sends the ball back.
            if (!IsGenuineContact(contact))
                return;

            // Maybe fix the ball getting stuck on walls
            if (lastFrameVelocity == Vector3.zero)
                lastFrameVelocity = ballRigidbody.velocity;

            // Velocity of the obstacle's surface at the contact point. For a moving/spinning obstacle
            // this is what flings the ball off - we reflect in the surface's frame (subtract it, reflect,
            // add it back) so the ball picks up the obstacle's speed like a bat hitting a ball, instead of
            // being dragged along the face. It's zero for static walls, so this stays a plain reflection there.
            var surfaceVelocity = Vector3.zero;
            if (Utilities.IsValid(collision.rigidbody))
                surfaceVelocity = collision.rigidbody.GetPointVelocity(contact.point).Sanitized();

            // Ball velocity relative to the (possibly moving) surface
            var relativeVelocity = lastFrameVelocity - surfaceVelocity;

            // Reflect the relative velocity off the wall
            var newDirection = Vector3.Reflect(relativeVelocity.normalized, collisionNormal).Sanitized();

            // If we still don't have a velocity don't do anything else as we might get stuck against the wall
            if (newDirection == Vector3.zero)
                return;

            var v = Vector3.Project(newDirection, collisionNormal).Sanitized();

            // How bouncy is this wall?
            var bounceMultiplier = wallBounceSpeedMultiplier;
            if (Utilities.IsValid(collision.collider) && Utilities.IsValid(collision.collider.material) && collision.collider.material.name.Length > 0)
                bounceMultiplier = collision.collider.material.bounciness;
            if (bounceMultiplier < .01f)
                bounceMultiplier = wallBounceSpeedMultiplier;
            if (bounceMultiplier < .01f)
                bounceMultiplier = .8f;

            var speedAfterBounce = relativeVelocity.magnitude * bounceMultiplier;

            newDirection = (newDirection - v * wallBounceDeflection) * speedAfterBounce;

            // Back into world space - re-add the surface velocity so the obstacle's spin/movement is
            // carried into the bounce, then clamp so a fast spinner can't launch the ball absurdly fast.
            newDirection = Vector3.ClampMagnitude((newDirection + surfaceVelocity).Sanitized(), maxBallSpeed);

            // If we reflected off a wall and the resulting bounce is going upwards, suspend the ground-snap
            // for the next frame (rolling needs stepsOnGround > 1) so PhysX can arc the ball.
            if (Vector3.Dot(newDirection, -gravityDirection) > .001f)
                stepsOnGround = 0;

            // Set the ball velocity so it bounces the right way
            lastFrameVelocity = ballRigidbody.velocity = newDirection.Sanitized();
            snapVelocity = ballRigidbody.velocity;

            // Play a hit sound because we bounced off something
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController))
                playerManager.openPutt.sfxController.PlayBallHitSoundAtPosition(CurrentPosition, ballRigidbody.velocity.magnitude / 10f);
        }

        /// <summary>
        /// True when a collision contact is a genuine surface hit rather than a mesh-edge "ghost"
        /// collision. For a sphere, a real contact normal always points from the contact point back
        /// toward the ball centre. When the sphere catches the leading edge of a mesh ramp/floor,
        /// Unity reports a contact point down at the low edge but a near-horizontal (wall-like) normal,
        /// so the two no longer line up. Comparing them rejects those ghost edges without any extra
        /// raycast or layer-mask dependency, using only the contact data we already have.
        /// </summary>
        private bool IsGenuineContact(ContactPoint contact)
        {
            var toCentre = CurrentPosition - contact.point;
            if (toCentre.sqrMagnitude < 0.000001f)
                return true;

            return Vector3.Dot(toCentre.normalized, contact.normal.Sanitized()) > 0.5f;
        }

        private bool lastGrounded = false;

        void UpdatePhysicsState()
        {
            var ballVelocity = ballRigidbody.velocity;
            var velMagnitude = ballVelocity.magnitude;

            // Probe for ground with a ball-sized spherecast rather than a single ray - a sphere can't
            // thread through the seam/gap between two colliders, so grounding stays stable at edges
            // (where the bad upward collisions happen). Start slightly above the ball to avoid the
            // initial-overlap case where SphereCast returns distance 0 with an unreliable normal.
            var castRadius = ballCollider.radius * 0.9f;
            var castBackup = ballCollider.radius;
            var castOrigin = CurrentPosition - gravityDirection * castBackup;
            var castDistance = castBackup + (ballCollider.radius - castRadius) + groundRaycastDistance;
            var isGrounded = Physics.SphereCast(castOrigin, castRadius, gravityDirection, out var groundingHit, castDistance, groundSnappingProbeMask, QueryTriggerInteraction.Ignore);

            // Keep the slope-stuck stop logic (in FixedUpdate) supplied with a fresh ground normal.
            lastGroundContactNormal = isGrounded ? groundingHit.normal : -gravityDirection;

            // The ground-snap drives grounded rolling above ballSnapMinSpeed (when enableBallSnap is on).
            // Below the cutoff / with snapping off we hand the ball back to Unity's physics, which has a
            // real contact solver and settles it cleanly against walls / in ditches (the ground-snap has
            // none and jitters when wedged). This single flag gates both the script integrator
            // (DriveGroundSnap) AND the manual gravity/drag/air forces below, so when the ground-snap
            // steps aside, normal physics gets its forces back.
            var snapActive = enableBallSnap && isGrounded && velMagnitude >= ballSnapMinSpeed;

            // Apply any external force-zone pushes accumulated since last frame. While the ground-snap is
            // driving it folds them into its own velocity instead (see DriveGroundSnap); here we hand
            // them straight to PhysX for the airborne / below-cutoff case.
            if (!snapActive && pendingExternalForce != Vector3.zero)
            {
                ballRigidbody.AddForce(pendingExternalForce, ForceMode.Force);
                pendingExternalForce = Vector3.zero;
            }

            // Apply gravity (skipped while the ground-snap is driving grounded rolling - it adds its own)
            if (gravityMagnitude > 0f && !snapActive)
                ballRigidbody.AddForce(gravityDirection.normalized * gravityMagnitude, ForceMode.Acceleration);

            if (isGrounded)
            {
                if (Utilities.IsValid(groundingHit) && Utilities.IsValid(groundingHit.collider) && Utilities.IsValid(groundingHit.collider.material))
                    lastKnownGroundFriction = Mathf.Clamp01(groundingHit.collider.material.dynamicFriction);
                else
                    lastKnownGroundFriction = 0;

                stepsOnGround += 1;
                timeFlying = 0;

                if (velMagnitude > .01f)
                {
                    // Ball surface drag force stuff
                    var currDrag = defaultBallDrag;

                    // Check if the ground has a physics material and use the dynamic friction value
                    if (lastKnownGroundFriction > 0f)
                        currDrag = lastKnownGroundFriction;

                    // A script is overriding the ball friction/drag
                    if (ballDragOverride > 0f)
                        currDrag = ballDragOverride;

                    // Apply less drag the slower the ball is rolling (Friction rolls off and ball stops look *nicer*)
                    if (velMagnitude < 1f)
                        currDrag *= Mathf.SmoothStep(0.1f, 1f, velMagnitude);

                    // The ground-snap integrator applies its own drag, so skip this one when it's driving
                    if (!snapActive)
                        ballRigidbody.AddForce(-ballVelocity.normalized * currDrag);
                }
            }
            else
            {
                lastKnownGroundFriction = 0;
                stepsOnGround = 0;
                timeFlying += Time.deltaTime;

                if (velMagnitude > 0.01f)
                {
                    // Use default drag if in a no gravity zone
                    if (gravityMagnitude < .01f)
                    {
                        // Ball surface drag force stuff
                        var currDrag = defaultBallDrag;

                        // A script is overriding the ball friction/drag
                        if (ballDragOverride > 0f)
                            currDrag = ballDragOverride;

                        // Apply less drag the slower the ball is rolling (Friction rolls off and ball stops look *nicer*)
                        if (velMagnitude < 1f)
                            currDrag *= Mathf.SmoothStep(0.1f, 1f, velMagnitude);

                        ballRigidbody.AddForce(-ballVelocity.normalized * currDrag);
                    }

                    if (_currentSpin.sqrMagnitude > .0001f)
                    {
                        // If the world rotated its gravity since we last touched the spin (gravity-flip
                        // zones etc), rotate the stored spin axis by the same delta. This keeps backspin
                        // lift pointing "up" and sidespin curving horizontally relative to the *current*
                        // gravity instead of staying locked to the gravity that was in effect at hit time.
                        // For constant-gravity worlds the directions are equal so this is a no-op.
                        if (_prevGravityDirection != gravityDirection)
                            _currentSpin = Quaternion.FromToRotation(_prevGravityDirection, gravityDirection) * _currentSpin;
                        _prevGravityDirection = gravityDirection;

                        var magnusCoefficient = 0.001f;

                        // Increase spin decay slightly with ball speed to help avoid
                        // sustained high-spin situations
                        var baseSpinDecayRate = 0.4f;
                        var speedDecayFactor = Mathf.Clamp(ballRigidbody.velocity.magnitude * 0.05f, 0f, 2f);
                        var spinDecayRate = baseSpinDecayRate + speedDecayFactor;

                        // Magnus force is perpendicular to both velocity and the spin axis. We use the
                        // full 3D spin axis so backspin (horizontal axis) produces lift while sidespin
                        // (vertical axis) produces horizontal hook/slice - both from the one spin vector.
                        var spinAxis = _currentSpin.normalized;
                        var magnusForceDirection = Vector3.Cross(ballRigidbody.velocity, spinAxis).normalized;
                        var magnusForceMagnitude = _currentSpin.magnitude * ballRigidbody.velocity.magnitude * magnusCoefficient;
                        var magnusForce = magnusForceDirection * magnusForceMagnitude;

                        // Anti-loop: a real backspinning ball lifts, but in a discrete sim a large Magnus
                        // force curls the trajectory into a loop ("loop de loop" on high-loft wedges). The
                        // force is perpendicular to velocity, so while the ball is climbing it points mostly
                        // sideways/backward - capping only the vertical (lift) component left that horizontal
                        // curl uncapped and the ball still looped. Instead we cap the WHOLE force magnitude to
                        // a fraction of the ball's weight: the ball still floats and carries, but the curve can
                        // never overpower gravity enough to bend the path back on itself.
                        if (gravityMagnitude > .01f)
                        {
                            var maxMagnus = ballRigidbody.mass * gravityMagnitude * maxLiftGravityFraction;
                            if (magnusForce.sqrMagnitude > maxMagnus * maxMagnus)
                                magnusForce = magnusForce.normalized * maxMagnus;
                        }

                        ballRigidbody.AddForce(magnusForce, ForceMode.Force);

                        // --- Spin Decay ---
                        var spinReduction = spinDecayRate * Time.fixedDeltaTime;
                        var currentSpinMagnitude = _currentSpin.magnitude;
                        if (currentSpinMagnitude > 0)
                            _currentSpin *= Mathf.Max(0f, 1f - spinReduction / currentSpinMagnitude);
                    }
                    else
                    {
                        _currentSpin = Vector3.zero;
                    }
                }
            }

            // Debug thing - Green Ball = Grounded / Red Ball = Not grounded
            if (ballGroundedDebug && isGrounded != lastGrounded)
                playerManager.BallColor = isGrounded ? Color.green : Color.red;

            lastGrounded = isGrounded;

            var canPlayHitGroundSound = timeFlying > .5f && isGrounded;

            DriveGroundSnap(snapActive, groundingHit);

            if (audioWhenBallHitsFloor && canPlayHitGroundSound)
            {
                if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController))
                    playerManager.openPutt.sfxController.PlayBallHitSoundAtPosition(CurrentPosition, lastFrameVelocity.magnitude * VelocityToAudioScale * 0.6f); // Play a bounce sound but a bit quieter
            }

            // Air resistance (skipped while the ground-snap is driving grounded rolling - it adds its own)
            if (enableAirResistance && !snapActive)
            {
                // Apply air resistance to the ball
                const float airDensity = 1.225f;
                const float dragCoeff = .35f;
                var ballCrossSection = Mathf.PI * ballCollider.radius * ballCollider.radius;
                var dragVel = ballRigidbody.velocity.Sanitized();
                var dragAccel = (0.5f * airDensity * ballCrossSection * dragCoeff * dragVel.magnitude * dragVel).Sanitized();
                ballRigidbody.AddForce(-dragAccel);
            }
        }

        /// <summary>
        /// While the ground-snap drives grounded rolling, remember any steep (wall/obstacle) surface
        /// the ball is touching so DriveGroundSnap can stop integrating velocity straight into it.
        /// The ground-snap only knows about the floor (from the down spherecast); without this it keeps
        /// re-applying gravity projected onto the downhill slope - which points into a wall at the
        /// slope's base - every frame, and the ball bounces off the wall forever instead of settling.
        /// Uses the same almost-vertical wall test (wallDetectionTolerance) and ghost-edge rejection
        /// (IsGenuineContact) ReflectCollision uses, so the bounce and pinning code agree on what a wall is.
        /// </summary>
        private void RecordSnapWallContact(Collision collision)
        {
            if (!enableBallSnap) return; // single master toggle for all snapping code

            if (!Utilities.IsValid(collision) || collision.contacts.Length == 0)
                return;

            // Don't pin the ball to a MOVING obstacle (spinner, bat, conveyor). The wall-cancellation in
            // DriveGroundSnap exists only to stop the ball infinitely bouncing off STATIC walls at the base
            // of a slope. If we record a moving face as a wall, the snap cancels the into-wall component
            // every frame and the ball just gets dragged along the surface instead of bouncing off it -
            // ReflectCollision handles the (surface-velocity-aware) bounce for these instead.
            if (Utilities.IsValid(collision.rigidbody) && collision.rigidbody.GetPointVelocity(collision.contacts[0].point).sqrMagnitude > 0.01f)
                return;

            for (var i = 0; i < collision.contacts.Length; i++)
            {
                var contact = collision.contacts[i];
                if (!Utilities.IsValid(contact)) continue;

                var normal = contact.normal.Sanitized();

                // Only ALMOST-VERTICAL contacts count as walls - anything more sloped is floor/ramp the
                // ground-snap already follows, and cancelling into it would stop the ball climbing legit
                // ramps (e.g. SteppyThing's sloped steps). Uses the exact same near-vertical test as
                // ReflectCollision so the bounce code and the pinning code agree on what a wall is.
                if (!Vector3.Dot(normal, gravityDirection).IsNearZero(wallDetectionTolerance)) continue;
                if (!IsGenuineContact(contact)) continue;

                snapWallNormal = normal;
                snapWallSteps = 2; // valid for the next couple of steps, refreshed while in contact
                return;
            }
        }

        /// <summary>
        /// While the ball is rolling on the ground above ballSnapMinSpeed, drive its velocity entirely
        /// from script - integrate gravity + drag on our own clean velocity and keep it on the surface -
        /// so Unity's collision response (and all mesh-edge ghosts) is ignored. The normal gravity/drag
        /// AddForce calls in UpdatePhysicsState are skipped while this is active and grounded; below the
        /// speed cutoff (or airborne) we hand control back to Unity's physics.
        /// </summary>
        private void DriveGroundSnap(bool snapActive, RaycastHit groundingHit)
        {
            // snapActive already folds in grounded + above ballSnapMinSpeed. Below the speed cutoff
            // (or airborne) we let Unity's physics drive, so it can rest the ball against walls / in
            // ditches without the ground-snap jittering it.
            var rolling = snapActive && stepsOnGround > 1;
            if (!rolling)
            {
                // Stay synced while physics drives so snapping resumes seamlessly above the cutoff
                snapVelocity = ballRigidbody.velocity;
                wasGroundSnapping = false;
                snapWallSteps = 0; // forget any wall once physics takes over / we leave the ground
                return;
            }

            // First ground-snap frame: adopt whatever velocity the ball currently has (hit, landing, bounce)
            if (!wasGroundSnapping)
            {
                snapVelocity = ballRigidbody.velocity;
                wasGroundSnapping = true;
            }

            var v = snapVelocity;

            // Fold in any external force-zone pushes (Normal/CenterPush) accumulated since last frame,
            // kept off the rigidbody so collision impulses don't leak into our clean velocity.
            if (pendingExternalForce != Vector3.zero)
            {
                v += pendingExternalForce / ballRigidbody.mass * Time.fixedDeltaTime;
                pendingExternalForce = Vector3.zero;
            }

            // Integrate gravity first.
            if (gravityMagnitude > 0f)
                v += gravityDirection * gravityMagnitude * Time.fixedDeltaTime;

            // If the integrated velocity points away from the surface the ball naturally wants to
            // launch (ramp lip, jump, convex curve). Release control and let PhysX handle the flight.
            if (Vector3.Dot(v, groundingHit.normal) > 0.05f)
            {
                snapVelocity = v;
                ballRigidbody.velocity = v;
                wasGroundSnapping = false;
                return;
            }

            // Still on ground — project onto surface so we follow the slope without sinking.
            v = Vector3.ProjectOnPlane(v, groundingHit.normal);

            // The ball centre should sit exactly one radius above the contact point. If it's
            // higher than that (floating), add a snap velocity to pull it back down.
            var targetPos = groundingHit.point + groundingHit.normal * ballCollider.radius;
            var dropNeeded = Vector3.Dot(targetPos - CurrentPosition, gravityDirection);
            if (dropNeeded > 0.001f)
                v += gravityDirection * Mathf.Min(dropNeeded / Time.fixedDeltaTime, 3f);

            // Surface drag + air resistance (mirrors UpdatePhysicsState; forces, so accel = force / mass)
            var speed = v.magnitude;
            if (speed > 0.0001f)
            {
                var currDrag = defaultBallDrag;
                if (lastKnownGroundFriction > 0f) currDrag = lastKnownGroundFriction;
                if (ballDragOverride > 0f) currDrag = ballDragOverride;
                if (speed < 1f) currDrag *= Mathf.SmoothStep(0.1f, 1f, speed);
                var dragDelta = currDrag / ballRigidbody.mass * Time.fixedDeltaTime;

                if (enableAirResistance)
                {
                    const float airDensity = 1.225f;
                    const float dragCoeff = .35f;
                    var ballCrossSection = Mathf.PI * ballCollider.radius * ballCollider.radius;
                    dragDelta += 0.5f * airDensity * ballCrossSection * dragCoeff * speed * speed / ballRigidbody.mass * Time.fixedDeltaTime;
                }

                v -= v.normalized * Mathf.Min(dragDelta, speed); // never reverse past a stop
            }

            // Don't integrate velocity straight into a wall/obstacle while driving at speed. On a downhill
            // slope gravity (and the snap-down above) project into a wall at the base; left alone the
            // ground-snap rams the wall every frame and the ball bounces off it forever (the "rolls downhill
            // into an obstacle and bounces back infinitely" bug). Cancelling the into-wall component lets
            // it slide along the wall instead, bleeding speed until it drops below ballSnapMinSpeed and
            // Unity's physics takes over to settle it. A fast ball still gets ReflectCollision's bounce.
            if (snapWallSteps > 0)
            {
                snapWallSteps -= 1;

                var intoWall = Vector3.Dot(v, snapWallNormal);
                if (intoWall < 0f)
                    v -= snapWallNormal * intoWall;
            }

            snapVelocity = v.Sanitized();
            ballRigidbody.velocity = snapVelocity;
        }

        public void _OnRespawn()
        {
            SendCustomEventDelayedFrames(nameof(_RespawnBall), 2);
        }

        public void _Wakeup()
        {
            ballRigidbody.WakeUp();
        }

        /// <summary>
        /// Returns the data from the last hit of the ball
        /// </summary>
        /// <param name="maxStraightLineDistance">The furthest distance recorded</param>
        /// <param name="totalDistanceTravelled">The total amount of distance that was covered by the ball</param>
        public void _GetLastHitData(out float maxStraightLineDistance, out float totalDistanceTravelled)
        {
            maxStraightLineDistance = lastHitMaxDistance;
            totalDistanceTravelled = lastHitTravelDistance;
        }

        /// <summary>
        /// Increments a counter of how many force zones the ball is currently inside
        /// </summary>
        public void _OnBallEnterGravityZone() => insideGravityZones += 1;

        /// <summary>
        /// Called when a ball leaves a force zone - if a ball ends up in 0 force zones the gravity direction will be moved to Vector3.down (the default)
        /// </summary>
        public void _OnBallExitGravityZone()
        {
            insideGravityZones -= 1;
            if (insideGravityZones > 0) return;

            insideGravityZones = 0;
            gravityDirection = Vector3.down;
            gravityMagnitude = defaultGravityMagnitude;
        }

        /// <summary>
        /// Apply an external push (e.g. a force-zone fan/conveyor) to the ball. Routed through here
        /// instead of a direct ballRigidbody.AddForce so it still works while the ground-snap is driving
        /// grounded rolling - the ground-snap overwrites the rigidbody velocity each frame, so a raw
        /// AddForce on a grounded ball would be discarded. We accumulate it and apply it once per
        /// FixedUpdate: folded into the ground-snap's velocity when it's driving, or handed to PhysX when
        /// it isn't. Mirrors Rigidbody.AddForce's ForceMode handling for Force / Acceleration.
        /// </summary>
        public void _AddExternalForce(Vector3 force, ForceMode mode)
        {
            pendingExternalForce += mode == ForceMode.Acceleration ? force * ballRigidbody.mass : force;
        }

        public SphereCollider GetBallCollider() => ballCollider;

        /// <summary>
        /// Updates the state of various toggles on the ball depending on whether the local player owns this ball or not
        /// </summary>
        /// <param name="localPlayerIsOwner">Does the local player own this ball? (Can usually pass in this.LocalPlayerOwnsThisObject())</param>
        public void _UpdateBallState(bool localPlayerIsOwner)
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
                    ballRigidbody.useGravity = false;
                    ballRigidbody.isKinematic = !BallIsMoving;
                    ballRigidbody.detectCollisions = true;
                    ballRigidbody.drag = 0.05f;
                    ballRigidbody.angularDrag = 0;

                    // Set the appropriate collision detection mode (speculative picks up club hits better - dynamic works better when the ball is moving around, speculative makes it bounce off random edges it shouldn't)
                    ballRigidbody.collisionDetectionMode = ballRigidbody.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
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