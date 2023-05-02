﻿using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [RequireComponent(typeof(VRCPickup)), RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(SphereCollider))]
    public class GolfBallController : UdonSharpBehaviour
    {
        #region Public Settings
        [Header("References")]
        public GolfClub club;
        public PuttSync puttSync;
        public VRCPickup pickup;
        public GolfBallStartLineController startLine;

        [Tooltip("Used to identify a wall collider and perform a bounce")]
        public PhysicMaterial wallMaterial;
        [Tooltip("Used to identify whether the ball is still on the course or not when it stops rolling")]
        public PhysicMaterial floorMaterial;
        public PlayerManager playerManager;
        [SerializeField]
        private Rigidbody ballRigidbody = null;
        [SerializeField]
        private SphereCollider ballCollider = null;

        [Space]
        [Header("General Settings")]
        [Tooltip("Allows players to pick up their ball at any time as long as the ball is not moving")]
        public bool allowBallPickup = false;
        [Tooltip("Allows players to pick up their ball if they are not currently playing a course AND the ball is not moving")]
        public bool allowBallPickupWhenNotPlaying = true;
        [Tooltip("Allows players to hit the ball at any time while it is moving (Default is no, which only lets them do this while they are not playing a course)")]
        public bool allowBallHitWhileMoving = false;
        [Tooltip("Should the ball hit noise be played if the ball falls onto a floor?")]
        public bool audioWhenBallHitsFloor = true;
        [Space]
        [Header("Ball Physics")]
        // [Tooltip("Which layers can start the ball moving when they collide with the ball? (For spinny things etc)")]
        //public LayerMask allowNonClubCollisionsFrom = 0;
        [Range(0f, 50f), Tooltip("This defines the fastest this ball can travel after being hit by a club (m/s)")]
        public float maxBallSpeed = 15f;
        [Range(0f, .2f), Tooltip("If the ball goes below this speed it will be counted as 'not moving' and will be stopped after the amount of time defined below")]
        public float minBallSpeed = 0.015f;
        [Range(0f, 1f), Tooltip("Defines how long the ball can keep rolling for when it goes below the minimum speed")]
        public float minBallSpeedMaxTime = 1f;
        [Tooltip("How long a ball can roll before being stopped automatically (in seconds)")]
        public float maxBallRollingTime = 30f;

        [Space]
        [SerializeField, Range(0, 90)]
        float groundSnappingMaxGroundAngle = 45f;

        [SerializeField, Range(0f, 100f)]
        float groundSnappingMinSpeed = 15f;

        [SerializeField, Range(0f, 100f)]
        float groundSnappingMaxSpeed = 100f;

        [SerializeField, Min(0f)]
        float groundSnappingProbeDistance = 1f;

        [SerializeField]
        LayerMask groundSnappingProbeMask = -1;

        [Space]
        [Range(0, 2), Tooltip("Used to pretend to absorb energy from the ball when it collides with a wall (Only used if the collider does not have a PhysicMaterial assigned)")]
        public float wallBounceSpeedMultiplier = 0.7f;
        [Range(0, 0.5f)]
        [Tooltip("Controls how the ball reflects off walls. 0=Perfect reflection 0.5=Half the direction is lost 1=Runs along the wall it hits")]
        public float wallBounceDeflection = .1f;
        [Range(0, 1), Tooltip("Determines if the ball will ignore wall bounces from directions below (0.5 is from the middle downward)")]
        public float wallBounceHeightIgnoreAmount = 0.5f;

        [Space]
        [Header("Respawn Settings")]
        [Tooltip("Toggles whether the ball is sent to the respawn position if it stops outside a course")]
        public bool respawnAutomatically = true;
        [Tooltip("If the ball stops outside of a course, this is where it will respawn to in world space")]
        public Vector3 respawnPosition = Vector3.zero;

        public bool BallIsMoving
        {
            set
            {
                bool localPlayerIsOwner = this.LocalPlayerOwnsThisObject();
                bool ballWasMoving = _ballMoving;

                // Store the new value
                _ballMoving = value;

                if (!_ballMoving && _ballMoving != ballWasMoving)
                {
                    droppedByPlayer = false;
                }

                // Reset timers
                timeNotMoving = 0f;
                timeMoving = 0f;

                ClearPhysicsState();
                UpdateBallState(localPlayerIsOwner);

                // Tells the players golf club to update its current state
                if (club != null)
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
                    if (playerManager != null && playerManager.CurrentCourse != null && playerManager.CurrentCourse.drivingRangeMode)
                    {
                        CourseManager course = playerManager.CurrentCourse;

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
                        bool ballIsInValidPosition = playerManager != null && (playerManager.CurrentCourse == null || playerManager.IsOnTopOfCurrentCourse(this.transform.position));
                        if (ballIsInValidPosition)
                        {
                            if (!pickedUpByPlayer)
                            {
                                // Ball stopped on top of a course - save this position so we can respawn here if needed
                                respawnPosition = this.transform.position;
                                Utils.Log(this, $"Ball respawn position is now {respawnPosition}");
                            }
                        }
                        else if (respawnPosition != Vector3.zero)
                        {
                            // It is not on top of a course floor so move it to the previous position
                            ballRigidbody.position = respawnPosition;

                            // Play the reset noise
                            if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.SFXController != null)
                                playerManager.openPutt.SFXController.PlayBallResetSoundAtPosition(respawnPosition);
                        }
                    }

                    // Stop the ball moving
                    ballRigidbody.velocity = Vector3.zero;
                    ballRigidbody.angularVelocity = Vector3.zero;
                    ballRigidbody.WakeUp();
                    lastFramePosition = ballRigidbody.position;
                    lastFrameVelocity = Vector3.zero;
                }

                //Utils.Log(this, $"BallWasMoving({ballWasMoving}) BallMoving({_ballMoving}) RespawnAuto({respawnAutomatically}) BallValidPos({ballIsInValidPosition}) PickedUp({pickedUpByPlayer}) RespawnPos({respawnPosition})");
            }
            get => _ballMoving;
        }
        [SerializeField]
        private bool _ballMoving = false;
        public bool pickedUpByPlayer { get; private set; }
        /// <summary>
        /// Used to ignore the first collision after we drop the ball - hopefully stops it flying away on flat surfaces
        /// </summary>
        private bool droppedByPlayer = false;
        [HideInInspector]
        public int currentOwnerHideOverride = 0;
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
            get => ballRigidbody.drag;
            set => ballRigidbody.drag = value;
        }
        public float BallAngularDrag
        {
            get => ballRigidbody.angularDrag;
            set => ballRigidbody.angularDrag = value;
        }
        #endregion

        #region Internal Vars
        /// Tracks how long a ball has been rolling for so we can stop it if it rolls for way too long
        private float timeMoving = 0f;
        /// Tracks how long the ball has been slowly rolling for so we can just bring it to a proper stop
        private float timeNotMoving = 0f;
        /// Tracks the velocity of the ball in the last frame so we can reflect properly on walls
        private Vector3 lastFrameVelocity;
        private Vector3 lastFramePosition;
        /// Stores the velocity of the club that needs to be applied in the next FixedUpdate() frame
        [SerializeField]
        public Vector3 requestedBallVelocity = Vector3.zero;

        Vector3 velocity;

        Vector3 contactNormal, steepNormal;

        int groundContactCount, steepContactCount;

        public bool OnGround => groundContactCount > 0;

        bool OnSteep => steepContactCount > 0;

        float minGroundDotProduct;

        private Vector3 lastGroundContactNormal = Vector3.up;
        int stepsSinceLastGrounded;
        #endregion

        void Start()
        {
            pickedUpByPlayer = false;
            BallIsMoving = false;

            if (wallMaterial == null)
                Utils.LogError(this, "Cannot detect walls! Please assign a wall PhysicMaterial to this ball!");

            if (floorMaterial == null)
                Utils.LogError(this, "Cannot detect floors! Please assign a floor PhysicMaterial to this ball!");

            if (puttSync == null)
                puttSync = GetComponent<PuttSync>();

            if (ballRigidbody == null)
                ballRigidbody = GetComponent<Rigidbody>();
            if (ballCollider == null)
                ballCollider = GetComponent<SphereCollider>();
            if (pickup == null)
                pickup = GetComponent<VRCPickup>();

            minGroundDotProduct = Mathf.Cos(groundSnappingMaxGroundAngle * Mathf.Deg2Rad);

            SetEnabled(false);
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (!this.LocalPlayerOwnsThisObject() || ballRigidbody == null)
                {
                    this.enabled = false;
                    return;
                }
            }

            this.enabled = enabled;
        }

        public int numberOfStillPickedUpFrames = 0;

        private void FixedUpdate()
        {
            if (pickedUpByPlayer)
            {
                Vector3 newFrameVelocity = (ballRigidbody.position - lastFramePosition) / Time.deltaTime;
                if (newFrameVelocity.magnitude > 0.05f)
                {
                    numberOfStillPickedUpFrames = 0;
                    lastFrameVelocity = newFrameVelocity * ballRigidbody.mass;
                }
                else
                {
                    numberOfStillPickedUpFrames++;

                    if (numberOfStillPickedUpFrames >= 5)
                    {
                        lastFrameVelocity = Vector3.zero;
                    }
                }

                lastFramePosition = ballRigidbody.position;

                if (puttSync != null)
                    puttSync.RequestFastSync();

                return;
            }

            // Freeze the ball position without going kinematic
            if (!BallIsMoving)
            {
                ballRigidbody.velocity = Vector3.zero;
                ballRigidbody.angularVelocity = Vector3.zero;
                ballRigidbody.WakeUp();
            }

            if (!BallIsMoving && requestedBallVelocity != Vector3.zero)
                BallIsMoving = true;

            if (BallIsMoving)
            {
                // If the ball has been hit
                if (requestedBallVelocity != Vector3.zero)
                {
                    // Reset velocity of ball
                    ballRigidbody.velocity = Vector3.zero;

                    // Apply new force to ball
                    ballRigidbody.AddForce(requestedBallVelocity, ForceMode.Impulse);

                    // Ball shouldn't be able to bounce off things faster than this hit for now
                    //rb.maxDepenetrationVelocity = requestedBallVelocity.magnitude;

                    // Consume the hit event
                    requestedBallVelocity = Vector3.zero;
                }
                else
                {
                    UpdatePhysicsState();

                    ClearPhysicsState();
                }

                timeMoving += Time.fixedDeltaTime;

                // Has the ball been rolling for too long?
                if (timeMoving > maxBallRollingTime)
                {
                    bool keepBallMoving = false;
                    if (Physics.Raycast(ballRigidbody.position, Vector3.down, out RaycastHit hit, groundSnappingProbeDistance, groundSnappingProbeMask))
                    {
                        // If the ball is currently rolling down a slope
                        if (hit.normal.y < 1f)
                        {
                            // Don't stop the ball from moving (people hate it stopping on slopes)
                            keepBallMoving = true;
                        }
                    }
                    BallIsMoving = keepBallMoving;
                }
                else if (ballRigidbody.velocity.magnitude < minBallSpeed)
                {
                    // If the ball is moving super slowly - count it as "not moving"
                    timeNotMoving += Time.deltaTime;

                    // If the ball "hasn't moved" for this amount of time
                    if (timeNotMoving >= minBallSpeedMaxTime)
                    {
                        //Utils.Log(this, "Ball not moving");
                        BallIsMoving = false; // Stop it
                    }
                }
                else
                {
                    // Ball has sped up reset not moving timer
                    timeNotMoving = 0f;
                }
            }
            else
            {
                ballRigidbody.velocity = Vector3.zero;
                ballRigidbody.angularVelocity = Vector3.zero;
                ballRigidbody.WakeUp();
            }

            if (ballRigidbody.isKinematic)
                lastFrameVelocity = (ballRigidbody.position - lastFramePosition) / Time.deltaTime;
            else
                lastFrameVelocity = ballRigidbody.velocity;

            lastFramePosition = ballRigidbody.position;

            // Tell PuttSync to sync position if it's attached
            bool sendFastPositionSync = currentOwnerHideOverride > 0 || BallIsMoving || startLine.gameObject.activeSelf;
            if (puttSync != null && sendFastPositionSync)
                puttSync.RequestFastSync();
        }

        public void OnBallDroppedOnPad(CourseManager courseThatIsBeingStarted, Vector3 position)
        {
            startLine.SetEnabled(false);

            this.transform.position = position;
            respawnPosition = position;

            BallIsMoving = false;

            if (playerManager != null)
                playerManager.OnCourseStarted(courseThatIsBeingStarted);
        }

        public override void OnPickup()
        {
            numberOfStillPickedUpFrames = 0;
            pickedUpByPlayer = true;

            BallIsMoving = false;

            startLine.SetEnabled(true);

            SetEnabled(true);

            UpdateBallState(this.LocalPlayerOwnsThisObject());
        }

        public override void OnDrop()
        {
            pickedUpByPlayer = false;

            if (startLine.StartDropAnimation(this.transform.position))
            {
                Utils.Log(this, "Player dropped ball near a start pad.. moving to to the start of a course");

                // Make sure physics are off for ball
                BallIsMoving = false; // TODO: This was true before - check if this is ok
            }
            else
            {
                // Player did not drop ball on a start pad and are currently playing a course
                if (playerManager != null && playerManager.openPutt != null && playerManager.CurrentCourse != null)
                {
                    if (respawnPosition != null)
                    {
                        Utils.Log(this, "Player dropped ball away from a start pad.. moving ball back to last valid position.");

                        BallIsMoving = false;

                        // Put the ball back where it last stopped on the course so the player can continue
                        ballRigidbody.position = respawnPosition;

                        // Play the reset noise
                        if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.SFXController != null)
                            playerManager.openPutt.SFXController.PlayBallResetSoundAtPosition(respawnPosition);

                        pickedUpByPlayer = false;
                        return;
                    }

                    // We don't know where to put the ball - skip the current course
                    playerManager.OnCourseFinished(playerManager.CurrentCourse, null, CourseState.Skipped);
                }

                // Allows the ball to drop to the floor when you drop it
                droppedByPlayer = true;
                BallIsMoving = true;

                // Apply velocity of the ball that we saw last frame so players can throw the ball
                requestedBallVelocity = lastFrameVelocity;

                // Allows ball to bounce
                stepsSinceLastGrounded = 2;
            }
        }

        /// <summary>
        /// Called by external scripts when the ball has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            SetEnabled(true);

            OnPickup();

            if (playerManager != null)
                playerManager.BallVisible = true;

            playerManager.RequestSync(syncNow: true);
        }

        /// <summary>
        /// Called by external scripts when the ball has been dropped
        /// </summary>
        public void OnScriptDrop()
        {
            OnDrop();

            if (playerManager != null)
                playerManager.BallVisible = true;

            playerManager.RequestSync();
        }

        public void RespawnBall(bool playErrorNoise = false)
        {
            Vector3 respawnPos = respawnPosition;

            BallIsMoving = false;

            ballRigidbody.MovePosition(respawnPos);
            respawnPosition = respawnPos;

            // Play the reset noise
            if (playErrorNoise && playerManager != null && playerManager.openPutt != null && playerManager.openPutt.SFXController != null)
                playerManager.openPutt.SFXController.PlayBallResetSoundAtPosition(respawnPosition);
        }

        public void OnBallHit(Vector3 withVelocity)
        {
            SetEnabled(true);

            bool playerIsPlayingACourse = playerManager != null && playerManager.CurrentCourse != null;

            // Discard any hits while the ball is already moving and the player is playing a course (allows them to hit the ball as much as they want otherwise)
            if (playerIsPlayingACourse && !allowBallHitWhileMoving && BallIsMoving)
            {
                // Tell the club to disarm for a second
                if (playerManager != null && playerManager.golfClub != null)
                    playerManager.golfClub.DisableClubColliderFor();
                return;
            }

            float speed = withVelocity.magnitude;

            // If ball wasn't hit hard enough ignore this hit
            // if (withVelocity == Vector3.zero || speed < minBallSpeed)
            if (withVelocity == Vector3.zero)
                return;

            // Clamp hit velocity
            if (speed > maxBallSpeed)
                withVelocity = withVelocity.normalized * maxBallSpeed;

            ClearPhysicsState();

            // Set the lastFrameVelocity to this, so if we collide with a wall straight away we bounce off it properly
            lastFrameVelocity = withVelocity;

            // Tell the ball to apply the velocity in the next FixedUpdate() frame
            requestedBallVelocity = withVelocity;

            Utils.Log(this, $"Ball has been hit (vel={requestedBallVelocity}) (mag={speed})");

            if (playerManager != null)
                playerManager.OnBallHit();

            // Vibrate the controller to give feedback to the player
            VRCPickup.PickupHand currentHand = club.CurrentHand;
            if (currentHand != VRC_Pickup.PickupHand.None)
                Networking.LocalPlayer.PlayHapticEventInHand(currentHand, 0.7f, 1f, 1f);

            if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.SFXController != null)
                playerManager.openPutt.SFXController.PlayBallHitSoundAtPosition(this.transform.position, (requestedBallVelocity.magnitude * 14.28571428571429f) / 4f);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            UpdateBallState(this.LocalPlayerOwnsThisObject());
        }

        private void OnCollisionEnter(Collision collision)
        {
            // If we hit something with a dynamic rigidbody that is moving, make sure ball can move
            if (!pickedUpByPlayer && collision != null && collision.rigidbody != null && collision.collider != null)
            {
                if (!collision.rigidbody.isKinematic && !collision.collider.isTrigger && (collision.rigidbody.velocity.magnitude > 0f || collision.rigidbody.angularVelocity.magnitude > 0f))
                {
                    SetEnabled(true);

                    if (BallIsMoving)
                    {
                        // Just reset the timers
                        timeNotMoving = 0f;
                        timeMoving = 0f;

                        ClearPhysicsState();
                    }
                    else
                    {
                        BallIsMoving = true;
                    }
                }
            }

            if (!BallIsMoving)
            {
                ballRigidbody.MovePosition(lastFramePosition);
                return;
            }

            if (collision.collider != null && collision.collider.material != null && collision.collider.material.name.StartsWith(wallMaterial.name))
                ReflectCollision(collision);

            EvaluateCollision(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            // If we hit something with a dynamic rigidbody that is moving, make sure ball can move
            if (!pickedUpByPlayer && collision != null && collision.rigidbody != null && collision.collider != null)
            {
                if (!collision.rigidbody.isKinematic && !collision.collider.isTrigger && (collision.rigidbody.velocity.magnitude > 0f || collision.rigidbody.angularVelocity.magnitude > 0f))
                {
                    if (BallIsMoving)
                    {
                        // Just reset the timers
                        timeNotMoving = 0f;
                        timeMoving = 0f;

                        ClearPhysicsState();
                    }
                    else
                    {
                        BallIsMoving = true;
                    }
                }
            }

            EvaluateCollision(collision);
        }

        /// <summary>
        /// Actually calculates where the ball should bounce when it contacts a wall<br/>
        /// Default Unity physics has an issue where the ball won't bounce under a certain speed and just rolls along it instead.
        /// </summary>
        /// <param name="collision">The collision that happened</param>
        private void ReflectCollision(Collision collision)
        {
            if (ballRigidbody == null)
                return;

            // if (Vector3.Dot(collisionNormal, Vector3.up) > 0.866f) // Use to ignore bounces on top of things
            // How bouncy is this wall?
            float bounceMultiplier = collision.collider != null && collision.collider.material != null && collision.collider.material.name.Length == 0 ? wallBounceSpeedMultiplier : collision.collider.material.bounciness;

            // Work out which direction we need to bounce off the wall
            var contact = collision.contacts[0];

            var collisionNormal = contact.normal;

            // Checks if the collision was from "below" and ignores it
            if (collisionNormal.y > wallBounceHeightIgnoreAmount)
            {
                Utils.Log(this, "Ignored wall bounce because it was below me!");
                return;
            }

            // Maybe fix the ball getting stuck on walls
            if (lastFrameVelocity == Vector3.zero)
                lastFrameVelocity = ballRigidbody.velocity;

            // Reflect the current vector off the wall
            Vector3 newDirection = Vector3.Reflect(lastFrameVelocity.normalized, collisionNormal);

            // If we still don't have a velocity don't do anything else as we might get stuck against the wall
            if (newDirection == Vector3.zero)
                return;

            var v = Vector3.Project(newDirection, collisionNormal);

            float speedAfterBounce = lastFrameVelocity.magnitude * bounceMultiplier;

            newDirection = (newDirection - v * wallBounceDeflection) * speedAfterBounce;

            // Set the ball velocity so it bounces the right way
            lastFrameVelocity = velocity = ballRigidbody.velocity = newDirection;

            // Play a hit sound because we bounced off something
            if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.SFXController != null)
                playerManager.openPutt.SFXController.PlayBallHitSoundAtPosition(this.transform.position, ballRigidbody.velocity.magnitude / 10f);
        }

        void ClearPhysicsState()
        {
            groundContactCount = steepContactCount = 0;
            contactNormal = steepNormal = Vector3.zero;
            //  lastGroundContactNormal = 1f;
        }

        void UpdatePhysicsState()
        {
            stepsSinceLastGrounded += 1;
            velocity = ballRigidbody.velocity;
            bool wasNotGrounded = stepsSinceLastGrounded > 30;

            if (SnapToGround() || OnGround || CheckSteepContacts())
            {
                stepsSinceLastGrounded = 0;
                if (groundContactCount > 1)
                {
                    contactNormal.Normalize();
                }

                if (wasNotGrounded && OnGround && audioWhenBallHitsFloor)
                    if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.SFXController != null)
                        playerManager.openPutt.SFXController.PlayBallHitSoundAtPosition(this.transform.position, (velocity.magnitude * 14.28571428571429f) * 0.6f); // Play a bounce sound but a bit quieter
            }
            else
            {
                contactNormal = Vector3.up;
            }
        }

        /// <summary>
        /// Tries to keep the ball from bouncing off edges on flat meshes (also works when ball is travelling up hills)
        /// </summary>
        /// <returns>True if the ball velocity was corrected, false if nothing was done</returns>
        bool SnapToGround()
        {
            if (stepsSinceLastGrounded > 1)
                return false;

            if (!Physics.Raycast(ballRigidbody.position, Vector3.down, out RaycastHit hit, groundSnappingProbeDistance, groundSnappingProbeMask))
                return false;

            float lastNormalY = lastGroundContactNormal.y;
            lastGroundContactNormal = hit.normal;

            float speed = velocity.magnitude;
            if (speed < groundSnappingMinSpeed || speed > groundSnappingMaxSpeed)
                return false;

            if (hit.normal.y < minGroundDotProduct || hit.normal.y > lastNormalY)
                return false;

            groundContactCount = 1;
            contactNormal = hit.normal;

            // If we have a velocity from the previous frame use that as it will be our velocity before the bounce
            if (lastFrameVelocity.magnitude > 0)
                velocity = lastFrameVelocity;

            float dot = Vector3.Dot(velocity, hit.normal);

            if (dot < 0f)
            {
                ballRigidbody.velocity = (velocity - hit.normal * dot).normalized * speed;
                lastFrameVelocity = ballRigidbody.velocity;
            }
            return true;
        }

        bool CheckSteepContacts()
        {
            if (steepContactCount > 1)
            {
                steepNormal.Normalize();
                if (steepNormal.y >= minGroundDotProduct)
                {
                    steepContactCount = 0;
                    groundContactCount = 1;
                    contactNormal = steepNormal;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Keeps track of the angle of the floor that the ball is currently on (Used for snapping the ball to the floor when travelling on flat or upward slopes<br/>
        /// This is needed as sometimes the ball can jump on flat surfaces when travelling over edges in meshes
        /// </summary>
        /// <param name="collision">The collision that happened</param>
        void EvaluateCollision(Collision collision)
        {
            if (droppedByPlayer || ballRigidbody == null || !BallIsMoving) return;

            float minDot = minGroundDotProduct;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                if (normal.y >= minDot)
                {
                    groundContactCount += 1;
                    contactNormal += normal;
                }
                else if (normal.y > -0.01f)
                {
                    steepContactCount += 1;
                    steepNormal += normal;
                }
            }
        }

        /// <summary>
        /// Not used for anything.. just here in case I need it ever
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        private Vector3 ProjectOnContactPlane(Vector3 vector)
        {
            return vector - contactNormal * Vector3.Dot(vector, contactNormal);
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
                if (ballCollider != null)
                {
                    ballCollider.enabled = true;
                    ballCollider.isTrigger = false;
                }

                if (ballRigidbody != null)
                {
                    // This avoids a warning when setting the collision mode below
                    ballRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                    // Toggle gravity/kinematic on/off depending on whether the ball is moving
                    ballRigidbody.useGravity = BallIsMoving;
                    ballRigidbody.isKinematic = false;
                    ballRigidbody.detectCollisions = true;

                    // Set the appropriate collision detection mode
                    ballRigidbody.collisionDetectionMode = ballRigidbody.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
                }

                // Only allow the local player to pick up their ball when it has stopped
                if (pickup != null)
                {
                    bool newPickupState = false;

                    if (!BallIsMoving)
                    {
                        newPickupState = allowBallPickup;

                        if (allowBallPickupWhenNotPlaying)
                            newPickupState = playerManager != null && playerManager.CurrentCourse == null;
                    }

                    pickup.pickupable = newPickupState;
                }

                if (puttSync != null)
                    puttSync.SetSpawnPosition(Vector3.zero, Quaternion.identity);
            }
            else
            {
                // Disable collider so other players can't collide
                if (ballCollider != null)
                {
                    ballCollider.enabled = false;
                    ballCollider.isTrigger = true;
                }

                // Disable rigidbody movement because it's not needed for non-local players
                if (ballRigidbody != null)
                {
                    ballRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    ballRigidbody.useGravity = false;
                    ballRigidbody.isKinematic = true;
                    ballRigidbody.detectCollisions = false;
                    ballRigidbody.Sleep();
                }

                // Stop other players from picking this ball up
                if (pickup != null)
                    pickup.pickupable = false;

                startLine.SetEnabled(false);
            }
        }
    }
}