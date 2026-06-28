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

        [Tooltip("Identifies a wall collider for bouncing")]
        public PhysicMaterial wallMaterial;

        [Tooltip("Identifies whether the ball stopped on the course")]
        public PhysicMaterial floorMaterial;

        public PlayerManager playerManager;

        [SerializeField]
        private TrailRenderer trail;

        public Rigidbody ballRigidbody;

        [SerializeField]
        private SphereCollider ballCollider;

        [Space]
        [Header("General Settings")]
        [Tooltip("Let players pick up their ball any time it's not moving")]
        public bool allowBallPickup;

        [Tooltip("Let players pick up the ball when not playing a course and it's not moving")]
        public bool allowBallPickupWhenNotPlaying = true;

        [Tooltip("Let players hit the ball while it's moving (default off: only when not playing a course)")]
        public bool allowBallHitWhileMoving;

        [Space]
        [Header("Ball Physics")]
        [Tooltip("Gravity direction applied to the ball")]
        public Vector3 gravityDirection = Vector3.down;

        public float gravityMagnitude = 9.87f;

        [Range(0f, 1f), Tooltip("Caps spin (Magnus) force to this fraction of ball weight - stops backspin shots looping. 1 = matches gravity (max float), lower = less float/curve")]
        public float maxLiftGravityFraction = 0.85f;

        [Range(0, .5f), Tooltip("Default RigidBody drag (scripts can override for sand pits etc)")]
        public float defaultBallDrag = .055f;

        [Range(0, 1), Tooltip("Overrides the default drag above")]
        public float ballDragOverride = 0;

        [Tooltip("Air resistance - helps the ball slow down in the air and on the ground")]
        public bool enableAirResistance = true;

        [SerializeField, Range(0f, 200f), Tooltip("Fastest the ball can travel after a hit (m/s). Fastest recorded club swing is ~108 m/s")]
        private float maxBallSpeed = 100f;

        [Range(0f, .2f), Tooltip("Below this speed (m/s) the ball counts as 'not moving' and stops after the time below")]
        public float minBallSpeed = 0.03f;

        [Range(0f, .2f), Tooltip("Below this speed (m/s) a hit ball counts as 'not moving'")]
        public float minBallHitSpeed = 0.1f;

        [Range(0f, 1f), Tooltip("How long the ball can keep rolling below the minimum speed")]
        public float minBallSpeedMaxTime = 1f;

        [Tooltip("How long a ball can roll before auto-stopping (seconds)")]
        public float maxBallRollingTime = 30f;

        [Space]
        [SerializeField, Min(0f), Tooltip("Extra buffer for the grounded check (meters)")]
        float groundRaycastDistance = 0.02f;

        [Tooltip("Master toggle for script-driven ground snapping. Off = pure Unity physics. Wired to the dev-menu 'ball snapping' checkbox")]
        public bool enableBallSnap = true;

        [SerializeField, Min(0), Tooltip("Consecutive grounded frames before snapping engages, so a just-landed ball's bounce plays out first. 0 = snap immediately, higher = longer grace")]
        int snapMinGroundedSteps = 4;

        [SerializeField, Range(0, 90), Tooltip("Surfaces steeper than this (degrees from flat) are left to Unity physics instead of being snapped to")]
        float groundSnappingMaxGroundAngle = 45f;

        [SerializeField]
        LayerMask groundSnappingProbeMask = -1;

        [Space]
        [Range(0f, 0.5f), Tooltip("How far a normal may tilt from vertical and still count as a 'wall'. 0.15≈8.6°. Keep below ~0.6 so ramps/floors aren't walls")]
        public float wallDetectionTolerance = 0.15f;

        [Range(0.1f, 2f), Tooltip("Energy kept after a wall bounce (only used if the collider has no PhysicMaterial)")]
        public float wallBounceSpeedMultiplier = 0.8f;

        [Range(0, 0.5f)]
        [Tooltip("Wall reflection: 0 = perfect, 0.5 = half lost, 1 = runs along the wall")]
        public float wallBounceDeflection = .1f;

        [Space]
        [Header("Respawn Settings")]
        [Tooltip("Send the ball to its respawn position if it stops outside a course")]
        public bool respawnAutomatically = true;

        [Tooltip("World-space respawn position if the ball stops off-course")]
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
                    var handler = EventHandler;
                    if (Utilities.IsValid(handler))
                        handler.OnPlayerBallStartedMoving(playerManager.Owner);
                }

                if (ballWasMoving && !_ballMoving)
                {
                    _StopRollingSound();

                    var handler = EventHandler;
                    if (Utilities.IsValid(handler))
                        handler.OnPlayerBallStopped(playerManager.Owner);

                    if (DebugMode)
                    {
                        // Reset the per-hit speed log
                        speedDataLogging = new float[0];
                    }

                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.CurrentCourse) && playerManager.CurrentCourse.courseType == CourseType.DrivingRangeDistance)
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
                    else if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.CurrentCourse) && playerManager.CurrentCourse.courseType == CourseType.DrivingRangeWithTargets)
                    {
                        _RespawnBall();
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
                            var sfx = SfxController;
                            if (Utilities.IsValid(sfx))
                                sfx.PlayBallResetSoundAtPosition(respawnWorldPosition);
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
            }
            get => _ballMoving;
        }

        [SerializeField]
        private bool _ballMoving = false;

        public bool pickedUpByPlayer { get; private set; }

        /// <summary>
        /// True while the player is holding the shoulder pickup but the ball is moving. In this mode we don't
        /// grab/freeze the ball - we just draw a line to it so the player can see where it is.
        /// </summary>
        [HideInInspector]
        public bool trackingMovingBall;

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

        /// Speculative collides early; maybe use it at rest/high speed and dynamic while moving?
        public CollisionDetectionMode collisionType
        {
            get => ballRigidbody.collisionDetectionMode;
            set => ballRigidbody.collisionDetectionMode = value;
        }

        public bool ballGroundedDebug = false;

        [Space]
        [Header("Rolling Sound")]
        [SerializeField, Tooltip("Looping audio source on the ball used for the rolling sound")]
        private AudioSource rollingAudioSource;

        [SerializeField, Range(0.5f, 20f), Tooltip("Ball speed (m/s) at which the rolling sound hits full volume/pitch")]
        private float rollingSoundMaxSpeed = 6f;

        [SerializeField, Range(0f, 1f), Tooltip("Rolling sound volume at max speed")]
        private float rollingSoundMaxVolume = 0.6f;

        [SerializeField, Tooltip("Pitch at min speed (x) and max speed (y)")]
        private Vector2 rollingSoundPitchRange = new Vector2(0.8f, 1.4f);

        [SerializeField, Range(1f, 30f), Tooltip("How fast the rolling volume eases in/out (units/sec)")]
        private float rollingSoundFade = 6f;

        #endregion

        #region Internal Vars

        /// How long the ball has been rolling (to stop it if it rolls too long)
        private float timeMoving;

        /// How long the ball has been rolling slowly (to bring it to a stop)
        private float timeNotMoving;

        /// Last frame's velocity, for reflecting off walls
        private Vector3 lastFrameVelocity;

        /// External force-zone pushes since last FixedUpdate, applied via AddForce in UpdatePhysicsState.
        /// Kept off the rigidbody so collision impulses don't leak in.
        private Vector3 pendingExternalForce;

        private Vector3 lastFramePosition;

        private float lastKnownGroundFriction = 0;

        private int insideGravityZones = 0;

        /// Club velocity to apply next FixedUpdate
        public Vector3 requestedBallVelocity = Vector3.zero;

        public bool OnGround => stepsOnGround > 1;

        private Vector3 lastGroundContactNormal = Vector3.up;
        private float timeFlying = 0;
        int stepsOnGround;
        private bool resetBallTimers = true;
        private float defaultGravityMagnitude = 9.87f;

        /// Logs ball speed after a hit for debugging
        private float[] speedDataLogging = new float[0];

        /// Furthest the ball got from where it was hit
        private float lastHitMaxDistance = 0;

        /// Total distance the ball travelled on its last hit
        private float lastHitTravelDistance = 0;

        public BodyMountedObject shoulderPickup => playerManager.IsInLeftHandedMode ? playerManager.openPutt.rightShoulderPickup : playerManager.openPutt.leftShoulderPickup;

        private VRC_Pickup.PickupHand ballHeldInHand = VRC_Pickup.PickupHand.None;

        private VRC_Pickup.PickupHand shoulderBallHeldInHand = VRC_Pickup.PickupHand.None;

        private Vector3 _currentSpin = Vector3.zero;

        /// Gravity direction the stored spin is aligned to. If gravity rotates mid-flight we rotate
        /// _currentSpin to match so lift/curve stay relative to current gravity.
        private Vector3 _prevGravityDirection = Vector3.down;

        public bool isHeldInTeleporter { get; set; }

        /// Converts ball speed (m/s) to an audio volume scale
        private const float VelocityToAudioScale = 14.285714f;

        /// SFX controller, or null when the playerManager/openPutt/sfxController chain isn't fully wired yet
        private SFXController SfxController =>
            Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.sfxController)
                ? playerManager.openPutt.sfxController
                : null;

        /// True only when the playerManager/openPutt chain is wired up and debug mode is on
        private bool DebugMode => Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && playerManager.openPutt.debugMode;

        /// Event handler, or null when the playerManager/openPutt/eventHandler chain isn't fully wired yet
        private OpenPuttEventHandler EventHandler =>
            Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.eventHandler)
                ? playerManager.openPutt.eventHandler
                : null;

        #endregion

        void Start()
        {
            gravityMagnitude = Physics.gravity.magnitude;
            defaultGravityMagnitude = Physics.gravity.magnitude;

            // Init gravity direction from Physics.gravity (a force zone can override it later)
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
            if (!Utilities.IsValid(trail))
                trail = GetComponent<TrailRenderer>();

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
            // Re-normalize each step in case a force zone or runtime change set a non-unit vector
            gravityDirection = gravityDirection.sqrMagnitude > 0f ? gravityDirection.normalized : Vector3.down;

            if (pickedUpByPlayer)
                return;

            // Freeze the ball position without going kinematic
            if (!BallIsMoving)
                FreezeBall();

            if (!BallIsMoving && requestedBallVelocity != Vector3.zero)
            {
                BallIsMoving = true;
            }

            if (BallIsMoving)
            {
                // If in debug mode, log current speed for this frame
                if (DebugMode)
                    speedDataLogging = speedDataLogging.Add(ballRigidbody.velocity.magnitude);

                // If the rigidbody fell asleep, reapply last frame's velocity to wake it
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
                        var floorDotRelativeToGravity = Vector3.Dot(lastGroundContactNormal, -gravityDirection);
                        if (floorDotRelativeToGravity < .99f)
                        {
                            // If we have been stuck on this slope for too long force ball stop so player can hit it
                            if (timeNotMoving > minBallSpeedMaxTime * 2f)
                            {
                                BallIsMoving = false;

                                if (DebugMode)
                                    OpenPuttUtils.Log(this, "Ball is on a slope and appears to be stuck here - allow player to hit it again");
                            }
                            else
                            {
                                // Prevent timers from being reset
                                resetBallTimers = false;
                                // Don't stop the ball from moving (people hate it stopping on slopes)
                                BallIsMoving = true;

                                if (DebugMode)
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
                    var angle = directionOfTravel.magnitude * Mathf.Rad2Deg / BallWorldRadius;
                    var rotationAxis = Vector3.Cross(-gravityDirection, directionOfTravel).normalized;
                    transform.localRotation = Quaternion.Euler(rotationAxis * angle) * transform.localRotation;
                    var worldRotation = transform.parent.rotation * transform.localRotation;
                    ballRigidbody.rotation = worldRotation.normalized;
                }
            }
            else
            {
                FreezeBall();
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

            _UpdateRollingSound();
            _UpdateTrailRendererWidth();

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

            StopBallVelocity();

            _SetRespawnPosition(position.transform.position);

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

            StopBallVelocity();

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

            // Guard against spurious re-entrant OnDrop() calls that fire after a slingshot throw
            // programmatically drops the remaining pickup. By then both saved hand states have been
            // cleared to None, so there is nothing meaningful for this call to do.
            if (ballHeldInHand == VRC_Pickup.PickupHand.None && shoulderBallHeldInHand == VRC_Pickup.PickupHand.None)
                return;

            // The player is doing a slingshot throw if both the ball and the shoulder were held and they've
            // just released one of them. When that happens we want to fling the ball - ignore any nearby
            // start pad so it doesn't snap the ball to the pad instead of throwing it.
            var slingshotActive = Networking.LocalPlayer.IsUserInVR() &&
                                  ballHeldInHand != VRC_Pickup.PickupHand.None &&
                                  shoulderBallHeldInHand != VRC_Pickup.PickupHand.None &&
                                  ((currentBallHeldInHand != VRC_Pickup.PickupHand.None) != (currentShoulderBallHeldInHand != VRC_Pickup.PickupHand.None));

            if (!slingshotActive && startLine.StartDropAnimation(CurrentPosition))
            {
                if (DebugMode)
                    OpenPuttUtils.Log(this, "Player dropped ball near a start pad.. moving to the start of a course");
                ballHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                shoulderBallHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
                return;
            }

            // Player did not drop ball on a start pad and are currently playing a course
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.CurrentCourse))
            {
                if (playerManager.CurrentCourse.courseType != CourseType.Standard && !slingshotActive)
                {
                    if (DebugMode)
                        OpenPuttUtils.Log(this, "Player dropped ball away from driving range start pad - marking driving range as completed");
                    playerManager._OnCourseFinished(playerManager.CurrentCourse, null, CourseState.Completed);
                }
                else if (!slingshotActive && HasRespawnPosition)
                {
                    if (DebugMode)
                        OpenPuttUtils.Log(this, "Player dropped ball away from a start pad.. moving ball back to last valid position.");

                    BallIsMoving = false;

                    // Put the ball back where it last stopped on the course so the player can continue
                    ballRigidbody.position = respawnWorldPosition;
                    lastFramePosition = respawnWorldPosition;

                    // Play the reset noise
                    var sfx = SfxController;
                    if (Utilities.IsValid(sfx))
                        sfx.PlayBallResetSoundAtPosition(respawnWorldPosition);

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

            var pickupHand = currentShoulderBallHeldInHand != VRC_Pickup.PickupHand.None ? currentShoulderBallHeldInHand : currentBallHeldInHand;
            if (pickupHand == VRC_Pickup.PickupHand.None)
                pickupHand = shoulderBallHeldInHand != VRC_Pickup.PickupHand.None ? shoulderBallHeldInHand : ballHeldInHand;

            var hand = pickupHand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;

            var offset = playerManager.openPutt.controllerTracker.CalculateLocalOffsetFromWorldPosition(hand, ballRigidbody.worldCenterOfMass);
            var lastHeldFrameVelocity = playerManager.openPutt.controllerTracker.GetVelocityAtOffset(hand, offset);

            if (Networking.LocalPlayer.IsUserInVR() && ballHeldInHand != VRC_Pickup.PickupHand.None)
            {
                if (slingshotActive)
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

                    var sfx = SfxController;
                    if (Utilities.IsValid(sfx))
                        sfx.PlayBallHitSoundAtPosition(CurrentPosition, lastHeldFrameVelocity.magnitude * VelocityToAudioScale / 4f);
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
            var onCourse = Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.CurrentCourse);
            var onStandardCourse = onCourse && playerManager.CurrentCourse.courseType == CourseType.Standard;

            var courseIsActivelyPlaying = onStandardCourse &&
                                          playerManager.courseStates[playerManager.CurrentCourse.holeNumber] == CourseState.Playing &&
                                          playerManager.courseScores[playerManager.CurrentCourse.holeNumber] > 0;

            if (courseIsActivelyPlaying || (BallIsMoving && onCourse))
            {
                var ballShoulderPickup = shoulderPickup;
                if (Utilities.IsValid(ballShoulderPickup))
                    ballShoulderPickup.tempDisableAttachment = true;

                trackingMovingBall = true;

                if (courseIsActivelyPlaying && Utilities.IsValid(startLine))
                    startLine.SetEnabled(true);

                return;
            }

            OnPickup();
        }

        /// <summary>
        /// Called by external scripts when the ball has been dropped
        /// </summary>
        public void _OnScriptDrop()
        {
            // If we were only tracking a moving ball (drawing a line to it), just clean that up without
            // running the normal drop/throw logic - the ball was never actually held.
            if (trackingMovingBall)
            {
                trackingMovingBall = false;

                var ballShoulderPickup = shoulderPickup;
                if (Utilities.IsValid(ballShoulderPickup))
                    ballShoulderPickup.tempDisableAttachment = false;

                // The start line hides itself once the ball is no longer held/tracked, so we don't force it off
                // here (keeps it up if the ball is still held directly).
                return;
            }

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
            if (DebugMode)
                OpenPuttUtils.Log(this, $"Ball respawn position is now {respawnWorldPosition}");
        }

        public void _RespawnBallWithErrorNoise()
        {
            _RespawnBall();

            // Play the reset noise
            var sfx = SfxController;
            if (Utilities.IsValid(sfx))
                sfx.PlayBallResetSoundAtPosition(respawnWorldPosition);
        }

        public void _OnBallHit(Vector3 withVelocity, Vector3 sideSpin)
        {
            _SetEnabled(true);

            // Tell the club to disarm for a second
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.golfClub))
                playerManager.golfClub._DisableClubColliderFor();

            var playerIsPlayingACourse = Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.CurrentCourse);

            // Discard hits while the ball is moving and playing a course (free hits otherwise)
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

            var sfx = SfxController;
            if (Utilities.IsValid(sfx))
                sfx.PlayBallHitSoundAtPosition(CurrentPosition, requestedBallVelocity.magnitude * VelocityToAudioScale / 4f);
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

                // TODO: a static rigidbody might help ball collisions but confuses the steppy thing
                // if (!collision.rigidbody.isKinematic && !collision.collider.isTrigger && (collision.rigidbody.velocity.magnitude > 0f || collision.rigidbody.angularVelocity.magnitude > 0f))
                ResetBallCollisionTimers();
            }
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
        /// Calculates the wall bounce. Unity won't bounce slow balls (it just rolls them along), so we do it ourselves.
        /// </summary>
        private void ReflectCollision(Collision collision)
        {
            if (!Utilities.IsValid(ballRigidbody) || !Utilities.IsValid(collision) || collision.contacts.Length == 0 || collision.contactCount == 0)
                return;

            // Work out which direction we need to bounce off the wall
            var contact = collision.contacts[0];
            if (!Utilities.IsValid(contact))
                return;

            // Average contact.normal across the manifold for a stable outward wall normal: a single triangle can
            // report a bad edge normal, and deriving it from contact.point -> centre fails for small balls (the
            // contact point isn't the closest point on the wall).
            var wallNormal = Vector3.zero;
            for (var i = 0; i < collision.contactCount; i++)
            {
                var c = collision.contacts[i];
                if (Utilities.IsValid(c))
                    wallNormal += c.normal.Sanitized();
            }
            wallNormal = wallNormal.Sanitized();
            if (wallNormal == Vector3.zero)
                wallNormal = contact.normal.Sanitized();

            Vector3 collisionNormal;
            if (gravityMagnitude > .01f)
            {
                // Wall test: a wall's normal is horizontal (perpendicular to gravity). ~0 dot with gravity means a
                // wall; ~±1 means floor/ceiling (e.g. a ramp edge), which we skip so a ball rolling up a ramp
                // doesn't bounce back.
                var normalVsGravity = Vector3.Dot(wallNormal, gravityDirection);
                if (!normalVsGravity.IsNearZero(wallDetectionTolerance))
                    return;

                // Keep only the horizontal part of the wall normal so the bounce stays in-plane.
                var sideways = Vector3.ProjectOnPlane(wallNormal, gravityDirection);
                if (sideways.sqrMagnitude < 1e-8f)
                    return;
                collisionNormal = sideways.normalized;
            }
            else
            {
                // No gravity - there's no "floor" to exclude, just bounce off the wall normal directly.
                if (wallNormal.sqrMagnitude < 1e-6f)
                    return;
                collisionNormal = wallNormal;
            }

            // Don't bounce a ball that's already travelling away from the wall - reflecting would shove it back in.
            var ballVelForCheck = lastFrameVelocity == Vector3.zero ? ballRigidbody.velocity : lastFrameVelocity;
            var velDotNormal = Vector3.Dot(ballVelForCheck, collisionNormal);
            if (velDotNormal >= 0f)
                return;

            // Maybe fix the ball getting stuck on walls
            if (lastFrameVelocity == Vector3.zero)
                lastFrameVelocity = ballRigidbody.velocity;

            // Obstacle surface velocity at the contact. We reflect in its frame (subtract, reflect, add back)
            // so the ball picks up its speed like a bat. Zero for static walls (plain reflection).
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

            // Back to world space - re-add surface velocity so the obstacle's motion carries into the bounce, then clamp speed.
            newDirection = Vector3.ClampMagnitude((newDirection + surfaceVelocity).Sanitized(), maxBallSpeed);

            // If the bounce goes upward, suspend snapping for a frame (it needs snapMinGroundedSteps
            // grounded frames again) so PhysX can arc it.
            if (Vector3.Dot(newDirection, -gravityDirection) > .001f)
                stepsOnGround = 0;

            // Set the ball velocity so it bounces the right way
            lastFrameVelocity = ballRigidbody.velocity = newDirection.Sanitized();

            // Play a hit sound because we bounced off something
            var sfx = SfxController;
            if (Utilities.IsValid(sfx))
                sfx.PlayBallHitSoundAtPosition(CurrentPosition, ballRigidbody.velocity.magnitude / 10f);
        }

        private bool lastGrounded = false;

        void UpdatePhysicsState()
        {
            var ballVelocity = ballRigidbody.velocity;
            var velMagnitude = ballVelocity.magnitude;

            // Spherecast (not a ray) so grounding stays stable across collider seams
            var isGrounded = ProbeGround(CurrentPosition, out var groundingHit);

            // Keep last frame's ground tilt for the snap's flattening-slope (crest) test before we overwrite it
            var prevGroundUpDot = Vector3.Dot(lastGroundContactNormal, -gravityDirection);
            lastGroundContactNormal = isGrounded ? groundingHit.normal : -gravityDirection;

            // External force-zone pushes handed straight to PhysX
            if (pendingExternalForce != Vector3.zero)
            {
                ballRigidbody.AddForce(pendingExternalForce, ForceMode.Force);
                pendingExternalForce = Vector3.zero;
            }

            // Gravity scales with the ball: a shrunk ball under full gravity falls too fast for its apparent size
            // and tucks into walls instead of arcing/bouncing, so we scale the pull to match its size.
            var scaledGravity = gravityMagnitude * BallScaleRatio;

            if (gravityMagnitude > 0f)
                ballRigidbody.AddForce(gravityDirection.normalized * scaledGravity, ForceMode.Acceleration);

            if (isGrounded)
            {
                lastKnownGroundFriction = Utilities.IsValid(groundingHit) && Utilities.IsValid(groundingHit.collider) && Utilities.IsValid(groundingHit.collider.material)
                    ? Mathf.Clamp01(groundingHit.collider.material.dynamicFriction)
                    : 0;

                stepsOnGround += 1;
                timeFlying = 0;

                if (velMagnitude > .01f)
                    ballRigidbody.AddForce(-ballVelocity.normalized * GetSurfaceDrag(velMagnitude));
            }
            else
            {
                lastKnownGroundFriction = 0;
                stepsOnGround = 0;
                timeFlying += Time.deltaTime;

                if (velMagnitude > 0.01f)
                {
                    // No-gravity zone: still bleed speed with the surface drag curve
                    if (gravityMagnitude < .01f)
                        ballRigidbody.AddForce(-ballVelocity.normalized * GetSurfaceDrag(velMagnitude));

                    // Magnus lift/curve from spin: force perpendicular to velocity and spin axis (backspin lifts,
                    // sidespin hooks/slices), capped to a fraction of ball weight so it can't loop the path
                    if (_currentSpin.sqrMagnitude > .0001f)
                    {
                        // Keep spin aligned to current gravity if it rotated mid-flight (no-op for constant gravity)
                        if (_prevGravityDirection != gravityDirection)
                            _currentSpin = Quaternion.FromToRotation(_prevGravityDirection, gravityDirection) * _currentSpin;
                        _prevGravityDirection = gravityDirection;

                        const float magnusCoefficient = 0.001f;
                        var spinAxis = _currentSpin.normalized;
                        var magnusForce = Vector3.Cross(ballVelocity, spinAxis).normalized * (_currentSpin.magnitude * velMagnitude * magnusCoefficient);

                        if (gravityMagnitude > .01f)
                        {
                            var maxMagnus = ballRigidbody.mass * scaledGravity * maxLiftGravityFraction;
                            if (magnusForce.sqrMagnitude > maxMagnus * maxMagnus)
                                magnusForce = magnusForce.normalized * maxMagnus;
                        }
                        ballRigidbody.AddForce(magnusForce, ForceMode.Force);

                        // Decay spin, faster at speed to avoid sustained high spin
                        var spinDecayRate = 0.4f + Mathf.Clamp(velMagnitude * 0.05f, 0f, 2f);
                        var spinReduction = spinDecayRate * Time.fixedDeltaTime;
                        var spinMagnitude = _currentSpin.magnitude;
                        if (spinMagnitude > 0)
                            _currentSpin *= Mathf.Max(0f, 1f - spinReduction / spinMagnitude);
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

            if (enableAirResistance)
            {
                var vel = ballRigidbody.velocity.Sanitized();
                ballRigidbody.AddForce((-vel.normalized * GetAirResistance(vel.magnitude)).Sanitized());
            }

            HandleGroundSnapping(isGrounded, groundingHit, prevGroundUpDot);

            if (canPlayHitGroundSound)
            {
                var sfx = SfxController;
                if (Utilities.IsValid(sfx))
                    sfx.PlayBallHitSoundAtPosition(CurrentPosition, lastFrameVelocity.magnitude * VelocityToAudioScale * 0.6f); // Play a bounce sound but a bit quieter
            }
        }

        /// <summary>
        /// Redirects velocity to follow a downhill slope so the ball doesn't launch off mesh-edge ghosts.
        /// Skips steep faces and crests so ramps still throw it; never adds speed or pushes the ball up.
        /// </summary>
        private void HandleGroundSnapping(bool isGrounded, RaycastHit groundingHit, float prevGroundUpDot)
        {
            if (!enableBallSnap || pickedUpByPlayer || !isGrounded) return;

            // Let a fresh landing settle under PhysX first
            if (stepsOnGround < snapMinGroundedSteps) return;

            var up = -gravityDirection;
            var normal = groundingHit.normal;
            var groundUpDot = Vector3.Dot(normal, up);

            // Skip steep surfaces, and skip when the slope is flattening out (crest / ramp exit) so the ball flies off
            var minGroundUpDot = Mathf.Cos(groundSnappingMaxGroundAngle * Mathf.Deg2Rad);
            if (groundUpDot < minGroundUpDot || groundUpDot >= prevGroundUpDot) return;

            var speed = ballRigidbody.velocity.magnitude;
            if (speed < 0.0001f) return;

            // Redirect last frame's velocity along the slope at the same speed, then bleed the downhill gravity pull
            var newVelocity = Vector3.ProjectOnPlane(lastFrameVelocity, normal).normalized * speed;
            var rampAngle = Mathf.Acos(Mathf.Clamp(groundUpDot, -1f, 1f));
            newVelocity += gravityDirection * (Mathf.Sin(rampAngle) * Time.deltaTime * gravityMagnitude * BallScaleRatio);

            // Only correct when the ball would otherwise lift off (i.e. make it follow the surface downhill); never push up
            if (Vector3.Dot(newVelocity, up) > .001f)
                ballRigidbody.velocity = lastFrameVelocity = newVelocity;
        }

        #region Physics Helpers

        /// <summary>
        /// World-space radius (local radius x largest lossyScale axis). Using the raw local radius breaks the
        /// ground probe and drag/roll maths whenever the ball is scaled.
        /// </summary>
        public float BallWorldRadius
        {
            get
            {
                var s = transform.lossyScale;
                return ballCollider.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            }
        }

        /// <summary>World-space diameter of the ball, accounting for its current scale.</summary>
        public float BallWorldDiameter => BallWorldRadius * 2f;

        /// <summary>
        /// Ball size relative to full scale (1 = design size, &lt;1 shrunk, &gt;1 grown): worldRadius / localRadius.
        /// Keeps gravity feel and collision margins proportional so a scaled ball bounces instead of sticking.
        /// </summary>
        private float BallScaleRatio => ballCollider.radius > 0.0001f ? BallWorldRadius / ballCollider.radius : 1f;

        /// Spherecast straight down (along gravity) from just above the ball - sphere probes don't thread collider seams
        private bool ProbeGround(Vector3 position, out RaycastHit hit)
        {
            var radius = BallWorldRadius;
            var castRadius = radius * 0.9f;
            var castBackup = radius;
            var origin = position - gravityDirection * castBackup;
            // Buffer scales with the ball so a shrunk ball doesn't probe past its surface onto a nearby wall
            var distance = castBackup + (radius - castRadius) + groundRaycastDistance * BallScaleRatio;
            if (!Physics.SphereCast(origin, castRadius, gravityDirection, out hit, distance, groundSnappingProbeMask, QueryTriggerInteraction.Ignore))
                return false;

            // Reject wall-like hits: a downward probe can catch a wall/seam and report a near-horizontal normal
            // as ground, which sticks the ball. Treat it as "not grounded" and let ReflectCollision bounce it.
            if (gravityMagnitude > .01f && Vector3.Dot(hit.normal.Sanitized(), -gravityDirection).IsNearZero(wallDetectionTolerance))
                return false;

            return true;
        }

        /// Rolling drag force magnitude. Ground friction or a script override beats the default; eases off near a stop so the ball settles nicely
        private float GetSurfaceDrag(float speed)
        {
            var drag = defaultBallDrag;
            if (lastKnownGroundFriction > 0f) drag = lastKnownGroundFriction;
            if (ballDragOverride > 0f) drag = ballDragOverride;
            if (speed < 1f) drag *= Mathf.SmoothStep(0.1f, 1f, speed);
            return drag;
        }

        /// Air resistance force magnitude (drag equation, scales with speed squared)
        private float GetAirResistance(float speed)
        {
            const float airDensity = 1.225f;
            const float dragCoeff = .35f;
            var radius = BallWorldRadius;
            var crossSection = Mathf.PI * radius * radius;
            return 0.5f * airDensity * crossSection * dragCoeff * speed * speed;
        }

        /// Zero the ball's velocity but keep it awake, so it holds position without going kinematic
        private void FreezeBall()
        {
            StopBallVelocity();
            ballRigidbody.WakeUp();
        }

        /// Zero the ball's linear/angular velocity if it's currently dynamic (no-op while kinematic)
        private void StopBallVelocity()
        {
            if (ballRigidbody.isKinematic) return;
            ballRigidbody.velocity = Vector3.zero;
            ballRigidbody.angularVelocity = Vector3.zero;
        }

        private void _UpdateTrailRendererWidth()
        {
            if (Utilities.IsValid(trail) && Utilities.IsValid(ballCollider))
            {
                trail.startWidth = BallWorldDiameter * 0.8f;
                trail.endWidth = 0f;
            }
        }

        /// Drives the looping rolling sound from ball speed; only audible while grounded and moving
        private void _UpdateRollingSound()
        {
            if (!Utilities.IsValid(rollingAudioSource) || !Utilities.IsValid(rollingAudioSource.clip)) return;

            var rolling = BallIsMoving && OnGround && !pickedUpByPlayer && BallCurrentSpeed > minBallSpeed;
            var targetVolume = 0f;

            if (rolling)
            {
                var t = Mathf.Clamp01(BallCurrentSpeed / rollingSoundMaxSpeed);
                targetVolume = t * rollingSoundMaxVolume;
                rollingAudioSource.pitch = Mathf.Lerp(rollingSoundPitchRange.x, rollingSoundPitchRange.y, t);
            }

            var sfx = SfxController;
            if (Utilities.IsValid(sfx))
                targetVolume *= sfx.Volume;

            // Enable the source so it can fade/play; disable it again only once fully silent
            if (targetVolume > 0.001f && !rollingAudioSource.enabled)
                rollingAudioSource.enabled = true;

            rollingAudioSource.volume = Mathf.MoveTowards(rollingAudioSource.volume, targetVolume, rollingSoundFade * Time.deltaTime);

            if (rollingAudioSource.volume > 0.001f)
            {
                if (!rollingAudioSource.isPlaying)
                    rollingAudioSource.Play();
            }
            else
            {
                if (rollingAudioSource.isPlaying)
                    rollingAudioSource.Stop();
                rollingAudioSource.enabled = false;
            }
        }

        /// Immediately silences the rolling sound (the script disables itself when the ball stops so it can't fade out)
        private void _StopRollingSound()
        {
            if (!Utilities.IsValid(rollingAudioSource)) return;

            rollingAudioSource.volume = 0f;
            if (rollingAudioSource.isPlaying)
                rollingAudioSource.Stop();
            rollingAudioSource.enabled = false;
        }

        #endregion

        public void _OnRespawn()
        {
            SendCustomEventDelayedFrames(nameof(_RespawnBall), 2);
        }

        public void _Wakeup()
        {
            ballRigidbody.WakeUp();
        }

        /// <summary>
        /// Returns the last hit's furthest distance and total distance travelled
        /// </summary>
        public void _GetLastHitData(out float maxStraightLineDistance, out float totalDistanceTravelled)
        {
            maxStraightLineDistance = lastHitMaxDistance;
            totalDistanceTravelled = lastHitTravelDistance;
        }

        /// <summary>
        /// Counts the force zones the ball is inside
        /// </summary>
        public void _OnBallEnterGravityZone() => insideGravityZones += 1;

        /// <summary>
        /// Leaving a force zone - if the ball is in 0 zones, gravity resets to the default (down)
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
        /// Apply an external push (force-zone fan/conveyor). Accumulated and applied once per FixedUpdate rather
        /// than a direct AddForce, since the ground-snap overwrites rigidbody velocity each frame and would
        /// discard a raw AddForce on a grounded ball. Mirrors AddForce's Force / Acceleration handling.
        /// </summary>
        public void _AddExternalForce(Vector3 force, ForceMode mode)
        {
            pendingExternalForce += mode == ForceMode.Acceleration ? force * ballRigidbody.mass : force;
        }

        public SphereCollider GetBallCollider() => ballCollider;

        /// <summary>
        /// Updates ball toggles depending on whether the local player owns this ball
        /// </summary>
        public void _UpdateBallState(bool localPlayerIsOwner)
        {
            if (localPlayerIsOwner)
            {
                // Enable the collider and make sure it can hit surfaces
                if (Utilities.IsValid(ballCollider))
                {
                    ballCollider.enabled = true;
                    ballCollider.isTrigger = false;

                    // Grow the collision skin as the ball shrinks so a small fast ball registers a wall contact
                    // before it penetrates and sticks. (Divisor clamped to [0.0005, 0.01], unchanged at design scale.)
                    ballCollider.contactOffset = 0.0005f / Mathf.Clamp(BallScaleRatio, 0.05f, 1f);
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

                    // Speculative picks up club hits better at rest; dynamic is better while moving (speculative bounces off edges it shouldn't)
                    ballRigidbody.collisionDetectionMode = ballRigidbody.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
                }

                // Only allow the local player to pick up their ball when it has stopped
                var newPickupState = false;

                if (!BallIsMoving)
                {
                    newPickupState = allowBallPickup;

                    if (Utilities.IsValid(playerManager) && allowBallPickupWhenNotPlaying)
                        newPickupState = true;

                    // If the ball is currently held on the shoulder mount, keep it grabbable so the other
                    // hand can grab it to arm the slingshot throw - otherwise the course rules above can
                    // block the second grab and break the shot. Not on standard courses though - there the
                    // shoulder only points to the ball (you putt with the club, you can't fling it).
                    if (!newPickupState && Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
                    {
                        var onStandardCourse = Utilities.IsValid(playerManager.CurrentCourse) && playerManager.CurrentCourse.courseType == CourseType.Standard;

                        var ballShoulderPickup = shoulderPickup;
                        if (!onStandardCourse && Utilities.IsValid(ballShoulderPickup) && ballShoulderPickup.heldInHand != VRC_Pickup.PickupHand.None)
                            newPickupState = true;
                    }
                }

                if (Utilities.IsValid(pickup))
                    pickup.pickupable = newPickupState;
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

                startLine.SetEnabled(false);
            }
        }
    }
}