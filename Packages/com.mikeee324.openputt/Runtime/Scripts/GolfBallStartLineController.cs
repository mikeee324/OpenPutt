﻿using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    // This needs to run after the ball has moved so the line draws in the correct place
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(50), RequireComponent(typeof(LineRenderer))]
    public class GolfBallStartLineController : UdonSharpBehaviour
    {
        #region Public Setting/References

        public PlayerManager PlayerManager;
        public GolfBallController golfBall;

        [Tooltip("A reference to the LineRenderer (Should be on the same GameObject)")]
        public LineRenderer lineRenderer;

        [Tooltip("Limits what layers to look for CourseStartPosition colliders on so there is less to loop through every time we check")]
        public LayerMask courseStartPosLayerMask;

        [Range(16, 64), Tooltip("If you have trouble starting courses because the line doesn't appear.. try increasing this number")]
        public int maximumNoOfColliders = 32;

        #endregion

        #region Internal Vars

        /// <summary>
        /// A reference to the nearest available course start position that the ball will be snapped to if dropped by the player
        /// </summary>
        private CourseStartPosition closestBallStart;

        /// <summary>
        /// A reference to the course that will be marked as 'Started' if the player drops the ball
        /// </summary>
        private CourseManager courseThatIsBeingStarted;

        /// <summary>
        /// Makes sure we only have 1 instance on the local area check queued up with Udon
        /// </summary>
        private bool localAreaCheckActive;

        /// <summary>
        /// Contains a list of all matching colliders in the local area of the player (as of the last check)
        /// </summary>
        private Collider[] localAreaColliders = new Collider[32];

        /// <summary>
        /// A list that is populated with a list of CourseStartPositions that is assigned at startup to speed up checks
        /// </summary>
        [HideInInspector]
        public CourseStartPosition[] knownStartPositions = new CourseStartPosition[0];

        /// <summary>
        /// A list that is populated with a list of Colliders that we can use to quickly filter out non CourseStartPositions
        /// </summary>
        [HideInInspector]
        public Collider[] knownStartColliders = new Collider[0];

        #endregion

        #region Animation Stuff

        private Vector3 lerpStartPosition = Vector3.zero;
        private Vector3 lerpStopPosition = Vector3.zero;
        private float lerpToStartTime = -1f;
        private AnimationCurve simpleEaseInOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

        #endregion

        void Start()
        {
            if (maximumNoOfColliders != localAreaColliders.Length)
                localAreaColliders = new Collider[maximumNoOfColliders];
        }

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                lineRenderer.SetPosition(0, Vector3.zero);
                lineRenderer.SetPosition(1, Vector3.zero);

                closestBallStart = null;
                courseThatIsBeingStarted = null;
            }

            gameObject.SetActive(enabled);

            if (enabled)
            {
                ResetDropAnimation();

                if (!localAreaCheckActive)
                {
                    localAreaCheckActive = true;
                    SendCustomEventDelayedSeconds(nameof(CheckLocalAreaForStartPositions), 0f);
                }
            }
        }

        /// <summary>
        /// Animates the golf ball towards its start position (if there is one within range)
        /// </summary>
        /// <param name="ballWorldPosition">The current position of the ball</param>
        /// <returns></returns>
        public bool StartDropAnimation(Vector3 ballWorldPosition)
        {
            if (!gameObject.activeSelf) return false;

            var hasBallStartPad = Utilities.IsValid(closestBallStart);
            var isPlayingACourse = false;
            if (Utilities.IsValid(PlayerManager) && Utilities.IsValid(PlayerManager.CurrentCourse))
                isPlayingACourse = PlayerManager.courseStates[PlayerManager.CurrentCourse.holeNumber] == CourseState.Playing;

            if (!hasBallStartPad && !isPlayingACourse)
                return false;

            lerpStartPosition = ballWorldPosition;
            if (hasBallStartPad)
                lerpStopPosition = closestBallStart.transform.position;
            else
                lerpStopPosition = golfBall.respawnPosition;
            lerpToStartTime = 0;

            golfBall.transform.rotation = Quaternion.identity;

            return true;
        }

        public void ResetDropAnimation()
        {
            lerpToStartTime = -1f;
            closestBallStart = null;
            courseThatIsBeingStarted = null;
        }

        /// <summary>
        /// Checks every so often to find the closest ball start position.<br/>
        /// We don't need to check this every frame.
        /// </summary>
        public void CheckLocalAreaForStartPositions()
        {
            if (gameObject.activeSelf && golfBall.pickedUpByPlayer)
            {
                if (maximumNoOfColliders != localAreaColliders.Length)
                    localAreaColliders = new Collider[maximumNoOfColliders];

                var radius = 2f;
                if (OpenPuttUtils.LocalPlayerIsValid())
                    radius = Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();
                radius = Mathf.Clamp(radius, 2f, 100f);

                CourseStartPosition newSpawnPos = null;
                CourseManager newCourse = null;

                var closestDistance = -1f;

                var golfBallPos = golfBall.CurrentPosition;

                var hitColliders = Physics.OverlapSphereNonAlloc(golfBallPos, radius, localAreaColliders, courseStartPosLayerMask, QueryTriggerInteraction.Collide);

                // Find the closest start point
                if (hitColliders > 0)
                {
                    Collider colliderToFindCache = null;
                    CourseStartPosition courseStartPositionCache = null;

                    for (var i = 0; i < hitColliders; i++)
                    {
                        colliderToFindCache = localAreaColliders[i];
                        // Null colliders in this array is a thing that happens - skip over them
                        if (!Utilities.IsValid(colliderToFindCache))
                            continue;

                        // Check if this collider is in our known array
                        var indexOfCollider = knownStartColliders.IndexOf(colliderToFindCache);
                        if (indexOfCollider == -1)
                            continue;

                        // We know about this collider - so fetch the related CourseStartPosition
                        courseStartPositionCache = knownStartPositions[indexOfCollider];

                        // If this start position is null run away
                        if (!Utilities.IsValid(courseStartPositionCache))
                            continue;

                        // If this position is closer to the ball than anything we've seen so far
                        var thisDistance = Vector3.Distance(golfBallPos, courseStartPositionCache.transform.position);
                        if (!Utilities.IsValid(newSpawnPos) || thisDistance < closestDistance)
                        {
                            // Save it as the closest position
                            newSpawnPos = courseStartPositionCache;
                            newCourse = courseStartPositionCache.courseManager;
                            closestDistance = thisDistance;
                        }
                    }
                }

                closestBallStart = newSpawnPos;
                courseThatIsBeingStarted = newCourse;

                SendCustomEventDelayedSeconds(nameof(CheckLocalAreaForStartPositions), .25f);
            }
            else
            {
                localAreaCheckActive = false;
            }
        }

        /// <summary>
        /// We draw the line in PostLateUpdate so that is actually attached to the ball. (DefaultExecutionOrder helps a lot here as well!)
        /// </summary>
        public override void PostLateUpdate()
        {
            if (lerpToStartTime >= 0f)
            {
                if (golfBall.pickedUpByPlayer)
                    return;

                var lerpMaxTime = 0.5f;
                var lerpProgress = Mathf.Clamp(lerpToStartTime / lerpMaxTime, 0, 1);

                if (lerpProgress < 1f)
                {
                    golfBall.SetPosition(Vector3.Lerp(lerpStartPosition, lerpStopPosition, simpleEaseInOut.Evaluate(lerpProgress)));
                    lerpToStartTime += Time.deltaTime;
                }
                else
                {
                    golfBall.SetPosition(lerpStopPosition);

                    if (Utilities.IsValid(closestBallStart))
                        golfBall.OnBallDroppedOnPad(courseThatIsBeingStarted, closestBallStart);

                    ResetDropAnimation();
                }
            }
            else if (!golfBall.pickedUpByPlayer)
            {
                SetEnabled(false);
                return;
            }

            if (!Utilities.IsValid(PlayerManager))
            {
                if (Utilities.IsValid(closestBallStart))
                {
                    // We have a ball spawn nearby - draw a line to it
                    lineRenderer.SetPosition(0, golfBall.transform.position);
                    lineRenderer.SetPosition(1, closestBallStart.transform.position);
                }
                else
                {
                    // Can't see a ball spawn nearby - hide line renderer
                    lineRenderer.SetPosition(0, Vector3.zero);
                    lineRenderer.SetPosition(1, Vector3.zero);
                }
            }

            var ballShoulderPickup = PlayerManager.IsInLeftHandedMode ? PlayerManager.openPutt.rightShoulderPickup : PlayerManager.openPutt.leftShoulderPickup;

            if (Utilities.IsValid(closestBallStart))
            {
                // We have a ball spawn nearby - draw a line to it
                lineRenderer.SetPosition(0, golfBall.transform.position);
                lineRenderer.SetPosition(1, closestBallStart.transform.position);
            }
            else if (Utilities.IsValid(ballShoulderPickup) && ballShoulderPickup.heldInHand != VRC_Pickup.PickupHand.None && ballShoulderPickup.tempDisableAttachment)
            {
                lineRenderer.SetPosition(0, golfBall.CurrentPosition);
                lineRenderer.SetPosition(1, ballShoulderPickup.transform.position);
            }
            else if (Utilities.IsValid(PlayerManager.CurrentCourse))
            {
                var currentCourse = PlayerManager.CurrentCourse;

                var currentState = PlayerManager.courseStates[currentCourse.holeNumber];
                if (currentState != CourseState.Playing) return;

                lineRenderer.SetPosition(0, golfBall.CurrentPosition);
                lineRenderer.SetPosition(1, golfBall.respawnPosition);
            }
            else
            {
                // Can't see a ball spawn nearby - hide line renderer
                lineRenderer.SetPosition(0, Vector3.zero);
                lineRenderer.SetPosition(1, Vector3.zero);
            }
        }
    }
}