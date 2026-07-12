using System;
using com.dev.mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using Random = UnityEngine.Random;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerManager : UdonSharpBehaviour
    {
        [Header("This prefab manages keeping/syncing scores by assigning one of these objects per player. It will also keep track of the objects the player interacts with.")]
        public GolfClub golfClub;

        public GolfBallController golfBall;
        public GolfBallPlayerLabel playerLabel;
        public GolfClubCollider golfClubHead;
        public OpenPutt openPutt;
        public GameObject desktopCamera;
        public GolfClubColliderVisualiser golfClubVisualiser;
        public Renderer ballRenderer;
        public Renderer ballGhostRenderer;
        public TrailRenderer trailRenderer;

        [Header("Game Settings")]
        [UdonSynced]
        public bool isPlaying = true;

        [UdonSynced]
        public int[] courseScores = { };

        [UdonSynced]
        public long[] courseTimes = { };

        [UdonSynced]
        public CourseState[] courseStates = { };

        [Range(5, 30), Tooltip("The number of seconds a ball can be away from the current course before being respawned back instantly")]
        public int ballOffCourseRespawnTime = 5;

        private VRCPlayerApi _owner;

        public VRCPlayerApi Owner
        {
            get
            {
                if (!Utilities.IsValid(_owner))
                    _owner = Networking.GetOwner(gameObject);
                return _owner;
            }
            private set => _owner = value;
        }

        [UdonSynced, FieldChangeCallback(nameof(BallColor))]
        private Color _ballColor = Color.black;

        public Color BallColor
        {
            set
            {
                if (_ballColor == value) return;

                _ballColor = value;

                // Tint the club handle/heads to match the players ball colour
                if (Utilities.IsValid(golfClub))
                {
                    golfClub.ballColour = _ballColor;
                    golfClub._UpdateClubColour();
                }

                if (!Utilities.IsValid(golfBall)) return;

                // Create a new MaterialPropertyBlock
                if (!Utilities.IsValid(golfBall.materialPropertyBlock))
                    golfBall.materialPropertyBlock = new MaterialPropertyBlock();
                ballRenderer.GetPropertyBlock(golfBall.materialPropertyBlock);

                // Set a random color in the MaterialPropertyBlock
                golfBall.materialPropertyBlock.SetColor("_Color", _ballColor);
                golfBall.materialPropertyBlock.SetColor("_EmissionColor", _ballColor);

                // Apply the MaterialPropertyBlock to the GameObject
                ballRenderer.SetPropertyBlock(golfBall.materialPropertyBlock);

                if (Utilities.IsValid(ballGhostRenderer))
                {
                    if (!Utilities.IsValid(golfBall.ghostMaterialPropertyBlock))
                        golfBall.ghostMaterialPropertyBlock = new MaterialPropertyBlock();
                    ballGhostRenderer.GetPropertyBlock(golfBall.ghostMaterialPropertyBlock);

                    golfBall.ghostMaterialPropertyBlock.SetColor("_Color", _ballColor);

                    // Apply the MaterialPropertyBlock to the GameObject
                    ballGhostRenderer.SetPropertyBlock(golfBall.ghostMaterialPropertyBlock);
                }

                // A simple 2 color gradient with a fixed alpha of 1.0f.
                var alpha = 1.0f;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(_ballColor, 0.0f), new GradientColorKey(Color.black, 1.0f) }, new[] { new GradientAlphaKey(alpha, 1f), new GradientAlphaKey(alpha, 1f) });
                trailRenderer.colorGradient = gradient;
                trailRenderer.material.color = _ballColor;

                // -- Set up render queues / zwrites so ball renders through walls and stuff
                //bool localPlayerIsOwner = Utilities.IsValid(Networking.LocalPlayer) && Networking.LocalPlayer.IsValid() && Networking.LocalPlayer.IsOwner(gameObject);

                // If we aren't using the default 2000 render queue number
                // int renderQueueBase = Utilities.IsValid(openPutt) ? openPutt.ballRenderQueueBase : -1;
                // if (renderQueueBase != -1)
                // {
                // Take the base render queue number and make sure local players ball renders in front of remote player balls
                // Adjust ball render queue
                // renderer.material.renderQueue = renderQueueBase + (localPlayerIsOwner ? 5 : 3);
                // //renderer.material.SetInt("_ZWrite", 1);
                // renderer.material.SetInt("_ZTest", 8);

                // // Adjust trail renderer render queue
                // tr.material.renderQueue = renderQueueBase + (localPlayerIsOwner ? 4 : 2);
                // //tr.material.SetInt("_ZWrite", 1);
                // tr.material.SetInt("_ZTest", 8);
                //}
            }
            get => _ballColor;
        }

        [UdonSynced, FieldChangeCallback(nameof(ClubVisible))]
        private bool _clubVisible;

        [UdonSynced, FieldChangeCallback(nameof(BallVisible))]
        private bool _ballVisible;

        [UdonSynced, FieldChangeCallback(nameof(IsInLeftHandedMode))]
        private bool _isInLeftHandedMode;

        [UdonSynced]
        private bool _weirdThingHappened = false;

        public int PlayerID => transform.GetSiblingIndex();

        /// <summary>
        /// Used to sync the visible state of this club to other players
        /// </summary>
        public bool ClubVisible
        {
            get => _clubVisible;
            set
            {
                if (Utilities.IsValid(golfClub) && golfClub.gameObject.activeInHierarchy != value)
                {
                    golfClub.gameObject.SetActive(value);
                    golfClub._UpdateClubState();

                    if (!value)
                    {
                        // Club was disabled - reset it
                        golfClub._RescaleClub(true);

                        if (Utilities.IsValid(golfClub.openPuttSync))
                            golfClub.openPuttSync._Respawn();
                    }
                }

                _clubVisible = value;
            }
        }

        /// <summary>
        /// Used to sync the visible state of this club to other players
        /// </summary>
        public bool BallVisible
        {
            get => _ballVisible;
            set
            {
                if (Utilities.IsValid(golfBall) && golfBall.gameObject.activeInHierarchy != value)
                {
                    golfBall.gameObject.SetActive(value);
                    golfBall._UpdateBallState(golfBall.LocalPlayerOwnsThisObject());
                }

                _ballVisible = value;
            }
        }

        /// <summary>
        /// Needed to make sure this PlayerManager has been properly initialised before we try to use it
        /// </summary>
        public bool IsReady => isPlaying && courseScores.Length > 0 && courseStates.Length > 0 && courseTimes.Length > 0;

        /// <summary>
        /// Works out the players total score across all courses
        /// </summary>
        [UdonSynced]
        public int PlayerTotalScore = 999999;

        /// <summary>
        /// Total number of seconds the player took to complete the courses
        /// </summary>
        [UdonSynced]
        public double PlayerTotalTime = 999999;

        /// <summary>
        /// Check if the player has started at least 1 course in the world. Useful for checking if the player is actually playing the game or not.
        /// </summary>
        public bool PlayerHasStartedPlaying
        {
            get
            {
                if (!IsReady) return false;
                for (var i = 0; i < courseStates.Length; i++)
                    if (courseStates[i] != CourseState.NotStarted)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Contains a reference to the players current course that they are playing on (returns null if they aren't playing any)
        /// </summary>
        public CourseManager CurrentCourse { get; private set; }

        [HideInInspector]
        public int hitsOnCurrentCourse = 0;

        public bool IsInLeftHandedMode
        {
            get => _isInLeftHandedMode;
            set
            {
                if (_isInLeftHandedMode == value)
                    return;

                _isInLeftHandedMode = value;

                // Just re-set the club type to update the head meshes (automatically picks which orientation)
                golfClub.ClubType = golfClub.ClubType;

                if (Networking.LocalPlayer == Networking.GetOwner(gameObject))
                {
                    // Move the club/ball onto the correct shoulders for the new handedness
                    _UpdateShoulderPickupAttachments();

                    _RequestSync(syncNow: true);
                }

                // Tell everybody that this player changed which hand they use
                if (Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.eventHandler) && Utilities.IsValid(Owner))
                    openPutt.eventHandler.OnPlayerHandednessChanged(Owner, value ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right);
            }
        }

        /// <summary>
        /// Points the shoulder pickups at the correct object (club/ball) for the current handedness. Leaves held pickups alone.
        /// </summary>
        public void _UpdateShoulderPickupAttachments()
        {
            if (Networking.LocalPlayer != Networking.GetOwner(gameObject))
                return;

            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.leftShoulderPickup) || !Utilities.IsValid(openPutt.rightShoulderPickup))
                return;

            if (openPutt.leftShoulderPickup.heldInHand != VRC_Pickup.PickupHand.None || openPutt.rightShoulderPickup.heldInHand != VRC_Pickup.PickupHand.None)
                return;

            var isLeftHanded = IsInLeftHandedMode;

            var golfClubAutoHold = golfClub.AutoHoldEnabled;

            openPutt.leftShoulderPickup.pickup.AutoHold = golfClubAutoHold && isLeftHanded ? VRC_Pickup.AutoHoldMode.Yes : VRC_Pickup.AutoHoldMode.No;
            openPutt.leftShoulderPickup.ObjectToAttach = isLeftHanded ? golfClub.gameObject : golfBall.gameObject;

            openPutt.rightShoulderPickup.pickup.AutoHold = golfClubAutoHold && !isLeftHanded ? VRC_Pickup.AutoHoldMode.Yes : VRC_Pickup.AutoHoldMode.No;
            openPutt.rightShoulderPickup.ObjectToAttach = isLeftHanded ? golfBall.gameObject : golfClub.gameObject;
        }

        public bool canFreezeDesktopPlayers;

        /// <summary>
        /// Toggles whether the player is immobile or not. Only works if this PlayerManager belongs to the local player.
        /// </summary>
        public bool PlayerIsCurrentlyFrozen
        {
            get => _isImmobilized;
            set
            {
                if (!Networking.IsOwner(gameObject))
                {
                    _isImmobilized = false;
                    return;
                }

                var local = Networking.LocalPlayer;
                _isImmobilized = value && freezePlayerWhileClubIsArmed && (canFreezeDesktopPlayers || local.IsUserInVR());
                local.Immobilize(_isImmobilized);
            }
        }

        private bool _isImmobilized;

        /// <summary>
        /// Used to rate-limit network sync for each PlayerManager
        /// </summary>
        private bool syncRequested;

        /// <summary>
        /// Counts how many seconds a ball has not been on top of it's current course (Used to reset the ball quicker if it flies off the course)
        /// </summary>
        private int ballNotOnCourseCounter;

        /// <summary>
        /// The current position of this PlayerManager for the scoreboards - order by score ASC
        /// </summary>
        public int ScoreboardPositionByScore = -1;

        /// <summary>
        /// The current position of this PlayerManager for the scoreboards - order by time ASC
        /// </summary>
        public int ScoreboardPositionByTime = -1;

        /// <summary>
        /// Basically marks this PlayerManager as 'dirty' and the scoreboards need to refresh the row for this player.<br/>
        /// The ScoreboardManager will reset this to false when it has updated the row for this player
        /// </summary>
        public bool scoreboardRowNeedsUpdating;

        /// <summary>
        /// Traps the player in a station while the club is armed
        /// </summary>
        public bool freezePlayerWhileClubIsArmed = true;

        [HideInInspector]
        public bool ownerIsInVR;

        public void _OnBallHit(float speed)
        {
            if (!Utilities.IsValid(CurrentCourse) || courseStates.Length != openPutt.courses.Length)
            {
                if (Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.eventHandler) && Utilities.IsValid(Owner))
                    openPutt.eventHandler.OnPlayerBallHit(Owner, speed);

                return;
            }

            var sendSync = Utilities.IsValid(openPutt) && openPutt.playerSyncType == PlayerSyncType.All;

            // Set score on current course
            switch (courseStates[CurrentCourse.holeNumber])
            {
                case CourseState.Skipped:
                case CourseState.NotStarted:
                    courseStates[CurrentCourse.holeNumber] = CourseState.Playing;
                    if (CurrentCourse.courseType == CourseType.Standard)
                        courseScores[CurrentCourse.holeNumber] = 1;
                    courseTimes[CurrentCourse.holeNumber] = DateTime.UtcNow.GetUnixTimestamp();

                    hitsOnCurrentCourse += 1;
                    break;
                case CourseState.Completed:
                case CourseState.PlayedAndSkipped:
                    if (Utilities.IsValid(openPutt) && (openPutt.replayableCourses || CurrentCourse.courseIsAlwaysReplayable))
                    {
                        courseStates[CurrentCourse.holeNumber] = CourseState.Playing;
                        if (CurrentCourse.courseType == CourseType.Standard)
                            courseScores[CurrentCourse.holeNumber] = 1;
                        courseTimes[CurrentCourse.holeNumber] = DateTime.UtcNow.GetUnixTimestamp();
                    }
                    else
                    {
                        if (openPutt.debugMode)
                            OpenPuttUtils.Log(this, $"Player tried to restart course {CurrentCourse.holeNumber}. They have already completed or skipped it though. (_OnBallHit)");
                        CurrentCourse = null;
                    }

                    break;
                case CourseState.Playing:
                    if (CurrentCourse.courseType == CourseType.Standard)
                    {
                        courseScores[CurrentCourse.holeNumber] += 1;

                        if (openPutt.debugMode)
                            OpenPuttUtils.Log(this, $"Player hit ball on course {CurrentCourse.holeNumber}. Score is now {courseScores[CurrentCourse.holeNumber]}. (_OnBallHit)");

                        if (courseScores[CurrentCourse.holeNumber] >= CurrentCourse.maxScore)
                        {
                            // Clamp score to the max
                            courseScores[CurrentCourse.holeNumber] = CurrentCourse.maxScore;

                            // Tell everybody this player hit the max score on this course
                            CurrentCourse.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(CourseManager.OnPlayerHitMaxScore));

                            // Play max score reached sound
                            if (Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.sfxController))
                                openPutt.sfxController.PlayMaxScoreReachedSoundAtPosition(golfBall.CurrentPosition);

                            // Lock in the time spent on this course now that max score is reached
                            courseTimes[CurrentCourse.holeNumber] = DateTime.UtcNow.GetUnixTimestamp() - courseTimes[CurrentCourse.holeNumber];

                            // Prevents the sound from being heard again
                            courseStates[CurrentCourse.holeNumber] = CourseState.Completed;
                        }
                    }

                    hitsOnCurrentCourse += 1;

                    break;
            }

            // Update the state of all courses
            _UpdateTotals();

            // Update local scoreboards
            if (Utilities.IsValid(openPutt))
                openPutt._OnPlayerUpdate(this);

            // If fast updates are on send current state of player to everybody - otherwise it will be done when the player finishes the course
            if (sendSync)
                _RequestSync();

            if (Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.eventHandler) && Utilities.IsValid(Owner))
                openPutt.eventHandler.OnPlayerBallHit(Owner, speed);
        }

        /// <summary>
        /// Adds points to a course score from an external source (e.g. a driving range target) and syncs/refreshes scoreboards like a normal ball hit
        /// </summary>
        /// <param name="course">The course to add the score to</param>
        /// <param name="amount">How many points to add</param>
        public void _AddToCourseScore(CourseManager course, int amount)
        {
            if (!Utilities.IsValid(course) || !Utilities.IsValid(openPutt))
                return;

            if (courseStates.Length != openPutt.courses.Length)
                return;

            var holeNumber = course.holeNumber;
            if (holeNumber < 0 || holeNumber >= courseScores.Length)
                return;

            // Make sure this is their current course before scoring, in case they skipped the starting pad
            if (course.courseType != CourseType.DrivingRangeWithTargets && CurrentCourse != course)
            {
                _OnCourseStarted(course);

                // Bail out if the course couldn't be (re)started
                if (CurrentCourse != course)
                    return;
            }

            // First contact with the course - transition it to Playing and stamp the start time
            if (courseStates[holeNumber] == CourseState.NotStarted || courseStates[holeNumber] == CourseState.Skipped)
            {
                courseStates[holeNumber] = CourseState.Playing;
                courseScores[holeNumber] = 0;
                courseTimes[holeNumber] = DateTime.UtcNow.GetUnixTimestamp();
            }

            courseScores[holeNumber] += amount;

            if (course.courseType == CourseType.DrivingRangeWithTargets)
            {
                if (CurrentCourse == course)
                {
                    _OnCourseFinished(course, null, CourseState.Completed);
                    if (Utilities.IsValid(openPutt) && (openPutt.replayableCourses || course.courseIsAlwaysReplayable))
                        _OnCourseStarted(course);
                }
                else
                {
                    // Player is on a different course - update state directly without touching CurrentCourse
                    if (courseStates[holeNumber] != CourseState.Completed)
                    {
                        courseTimes[holeNumber] = DateTime.UtcNow.GetUnixTimestamp() - courseTimes[holeNumber];
                        courseStates[holeNumber] = CourseState.Completed;
                    }
                    _UpdateTotals();
                    openPutt._OnPlayerUpdate(this);
                    if (openPutt.playerSyncType == PlayerSyncType.All)
                        _RequestSync();
                }
                return;
            }

            // Update the state of all courses
            _UpdateTotals();

            // Refresh local scoreboards + save persistent data
            openPutt._OnPlayerUpdate(this);

            // If fast updates are on send current state of player to everybody - otherwise it will be done when the player finishes the course
            if (openPutt.playerSyncType == PlayerSyncType.All)
                _RequestSync();
        }

        public void _OnCourseStarted(CourseManager newCourse)
        {
            if (!Utilities.IsValid(newCourse))
            {
                if (openPutt.debugMode)
                    OpenPuttUtils.LogError(this, "Player tried to start a new course but it was null! Check that all CourseStartPositions have references to the correct CourseManager!");
                return;
            }

            var canReplayCourses = Utilities.IsValid(openPutt) && openPutt.replayableCourses;

            var newCourseOldState = courseStates[newCourse.holeNumber];
            if (newCourseOldState == CourseState.Completed || newCourseOldState == CourseState.PlayedAndSkipped)
            {
                if (!canReplayCourses && !newCourse.courseIsAlwaysReplayable)
                {
                    if (openPutt.debugMode)
                        OpenPuttUtils.Log(this, $"Player tried to restart course {newCourse.holeNumber}. They have already completed or skipped it though.");
                    CurrentCourse = null;
                    return;
                }
            }

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Starting course number {newCourse.holeNumber}. Current course({(Utilities.IsValid(CurrentCourse) ? CurrentCourse.holeNumber : -1)}) will be closed");

            // If the player is already on a hole say they skipped it
            if (Utilities.IsValid(CurrentCourse) && CurrentCourse.holeNumber != newCourse.holeNumber)
            {
                _OnCourseFinished(CurrentCourse, null, courseScores[CurrentCourse.holeNumber] > 0 ? CourseState.PlayedAndSkipped : CourseState.Skipped);
            }

            CurrentCourse = newCourse;

            hitsOnCurrentCourse = 0;

            // If the local player is carrying a club that isn't allowed on this course, swap to the first allowed one
            if (this == openPutt.LocalPlayerManager && Utilities.IsValid(golfClub) && golfClub.LocalPlayerOwnsThisObject())
            {
                if (!newCourse._IsClubAllowed(golfClub.ClubType))
                    golfClub.ClubType = newCourse._GetFirstAllowedClub();

                // Club choice availability (e.g. cycle club buttons) depends on the current course, so refresh it
                if (Utilities.IsValid(openPutt.uiController))
                    openPutt.uiController.UpdateButtonStates();
            }

            newCourse.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(CourseManager.OnPlayerStartedCourse));
        }

        public void _OnCourseFinished(CourseManager course, CourseHole hole, CourseState newCourseState)
        {
            // Player either isn't playing a course already or they put the ball in the wrong hole - ignore event
            if (!Utilities.IsValid(course) || CurrentCourse != course)
            {
                if (openPutt.debugMode)
                    OpenPuttUtils.Log(this, $"Course {course.holeNumber} was finished. {(!Utilities.IsValid(course) ? "Course is null" : "")} {(CurrentCourse != course ? "Wrong course!" : "")}");
                return;
            }

            CurrentCourse = null;

            // Club choice availability (e.g. cycle club buttons) depends on the current course, so refresh it
            if (Utilities.IsValid(openPutt.uiController))
                openPutt.uiController.UpdateButtonStates();

            // Add on any extra points to the players score that this particular hole has
            if (Utilities.IsValid(hole))
                courseScores[course.holeNumber] += hole.holeScoreAddition;

            if (course.courseType == CourseType.DrivingRangeDistance)
            {
                golfBall._GetLastHitData(out var maxDistance, out var totalDistance);
                // Driving ranges will just track the highest score - use a separate canvas for live/previous hit distance
                if (maxDistance > courseScores[course.holeNumber])
                    courseScores[course.holeNumber] = Mathf.FloorToInt(maxDistance);
            }

            switch (newCourseState)
            {
                case CourseState.PlayedAndSkipped:
                case CourseState.Skipped:
                    // If the player skipped this course - assign the max score for this course
                    courseScores[course.holeNumber] = course.maxScore;
                    courseTimes[course.holeNumber] = course.maxTime;
                    break;
                default:
                    // Calculate time spent on this course, unless it was already locked in when maxed out
                    if (courseStates[course.holeNumber] != CourseState.Completed)
                        courseTimes[course.holeNumber] = DateTime.UtcNow.GetUnixTimestamp() - courseTimes[course.holeNumber];
                    break;
            }

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this,
                    $"Course({course.holeNumber}) was {courseStates[course.holeNumber].GetString()} and is now {(newCourseState == CourseState.Completed ? "Completed" : "Skipped")}! Current score is {courseScores[course.holeNumber]}. Player took {courseTimes[course.holeNumber]}s to do this.");

            // Update the current state for this course
            courseStates[course.holeNumber] = newCourseState;

            if (Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.scoreboardManager))
            {
                openPutt.scoreboardManager.requestedScoreboardView = ScoreboardView.Scoreboard;
                _UpdateTotals();
                openPutt._OnPlayerUpdate(this);
            }

            if (newCourseState == CourseState.Completed)
            {
                // Send the finish course event to everybody
                if (Utilities.IsValid(hole))
                    hole.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(CourseHole.OnBallEnteredHole), hitsOnCurrentCourse, courseScores[course.holeNumber]);

                // If the player actually finished the hole - send a sync.. otherwise we'll wait for them to do something else that sends a sync
                _RequestSync();
            }

            if (Utilities.IsValid(openPutt))
                openPutt._SavePersistantData();
        }

        void Start()
        {
            _ResetPlayerScores();
        }

        public override void OnDeserialization()
        {
            if (!Utilities.IsValid(Owner) || !Utilities.IsValid(Owner.displayName))
                return;

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Received update from {Owner.displayName}!\r\n{ToString()}");

            if (Utilities.IsValid(openPutt))
                openPutt._OnPlayerUpdate(this);

            // Check if the synced state of the club is different from what it actually is (Maybe fixes #49)
            if (Utilities.IsValid(golfClub) && ClubVisible != golfClub.gameObject.activeInHierarchy)
            {
                golfClub.gameObject.SetActive(ClubVisible);
                golfClub._UpdateClubState();
            }

            // Check if the synced state of the club is different from what it actually is (Maybe fixes #49)
            if (Utilities.IsValid(golfBall) && BallVisible != golfBall.gameObject.activeInHierarchy)
            {
                golfBall.gameObject.SetActive(BallVisible);
                golfBall._UpdateBallState(golfBall.LocalPlayerOwnsThisObject());
            }
        }

        /// <summary>
        /// Requests OpenPutt to perform a debounced network sync of scores etc.
        /// </summary>
        public void _RequestSync(bool syncNow = false)
        {
            if (!Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsValid() || !Networking.LocalPlayer.IsOwner(gameObject))
                return;

            if (syncNow)
            {
                _SyncNow();
            }
            else if (!syncRequested)
            {
                // If we aren't already waiting for a sync to happen schedule one in
                syncRequested = true;
                var maxRefreshInterval = Utilities.IsValid(openPutt) ? openPutt.maxRefreshInterval : 1f;
                SendCustomEventDelayedSeconds(nameof(_SyncNow), maxRefreshInterval);
            }
        }

        public void _SyncNow()
        {
            RequestSerialization();
            syncRequested = false;
        }

        public void _CheckPlayerLocation()
        {
            if (!OpenPuttUtils.LocalPlayerIsValid())
            {
                SendCustomEventDelayedSeconds(nameof(_CheckPlayerLocation), 1);
                return;
            }

            var hasValidShoulderPickups = Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.leftShoulderPickup) && Utilities.IsValid(openPutt.rightShoulderPickup);
            var isLeftHanded = hasValidShoulderPickups && IsInLeftHandedMode;
            var ballShoulderPickup = isLeftHanded ? openPutt.rightShoulderPickup : openPutt.leftShoulderPickup;
            var clubShoulderPickup = isLeftHanded ? openPutt.leftShoulderPickup : openPutt.rightShoulderPickup;



            var ballIsOnCurrentCourse = false;
            var shouldEnableBallShoulderPickup = true;
            var shouldEnableClubShoulderPickup = isPlaying;

            if (Utilities.IsValid(clubShoulderPickup) && Utilities.IsValid(clubShoulderPickup.ObjectToAttach))
            {
                var pickupHelper = clubShoulderPickup.ObjectToAttach.GetComponent<VRCPickup>();
                if (Utilities.IsValid(pickupHelper))
                    shouldEnableClubShoulderPickup = isPlaying && pickupHelper.currentHand == VRC_Pickup.PickupHand.None;
            }

            if (Utilities.IsValid(CurrentCourse))
            {
                ballIsOnCurrentCourse = IsOnTopOfCurrentCourse(golfBall.CurrentPosition, 100f);

                var maxTimeOffCourse = ballOffCourseRespawnTime;

                // Live driving range updates (kinda)
                if (CurrentCourse.courseType == CourseType.DrivingRangeDistance)
                {
                    if (golfBall.BallIsMoving)
                    {
                        // If the ball is fairly close to any floor, start the count down to reset the ball
                        ballIsOnCurrentCourse = !golfBall.OnGround;

                        // We half the amount of time on the ground for driving ranges before resetting
                        if (golfBall.OnGround)
                            ballNotOnCourseCounter++;

                        // If the players new score is above the previous driving range score, then overwrite it
                        golfBall._GetLastHitData(out var maxDistance, out var totalDistance);
                        if (maxDistance > courseScores[CurrentCourse.holeNumber])
                            courseScores[CurrentCourse.holeNumber] = Mathf.FloorToInt(maxDistance);
                    }

                    // Increase max floor time for driving ranges
                    maxTimeOffCourse *= 2;
                }
                else if (CurrentCourse.courseType == CourseType.DrivingRangeWithTargets)
                {
                    if (golfBall.BallIsMoving)
                    {
                        // If the ball is fairly close to any floor, start the count down to reset the ball
                        ballIsOnCurrentCourse = !golfBall.OnGround;

                        // We half the amount of time on the ground for driving ranges before resetting
                        if (golfBall.OnGround)
                            ballNotOnCourseCounter++;
                    }

                    // Increase max floor time for driving ranges
                    maxTimeOffCourse *= 2;
                }

                // TODO: We should probably be able to do this without a raycast by monitoring collisions on the ball instead
                // Could count if there has been more than 10 frames of constant collision with a non course floor
                if (ballIsOnCurrentCourse)
                {
                    ballNotOnCourseCounter = 0;
                }
                else
                {
                    ballNotOnCourseCounter++;

                    if (ballNotOnCourseCounter > maxTimeOffCourse)
                    {
                        if (openPutt.debugMode)
                            OpenPuttUtils.Log(this, "Ball has been off its course for too long");
                        ballNotOnCourseCounter = 0;
                        if (golfBall.BallIsMoving)
                            golfBall.BallIsMoving = false;
                        else
                            golfBall._RespawnBallWithErrorNoise();
                    }
                }

            }

            if (Utilities.IsValid(ballShoulderPickup) && Utilities.IsValid(clubShoulderPickup))
            {
                if (!isPlaying)
                    shouldEnableBallShoulderPickup = false;
                else if (golfBall.pickedUpByPlayer)
                    shouldEnableBallShoulderPickup = true;

                var keepBallShoulderActive = ballShoulderPickup.heldInHand != VRC_Pickup.PickupHand.None;
                var keepClubShoulderActive = clubShoulderPickup.heldInHand != VRC_Pickup.PickupHand.None;

                var targetBallShoulderActive = shouldEnableBallShoulderPickup || keepBallShoulderActive;
                var targetClubShoulderActive = shouldEnableClubShoulderPickup || keepClubShoulderActive;

                if (targetBallShoulderActive != ballShoulderPickup.gameObject.activeInHierarchy)
                    ballShoulderPickup.gameObject.SetActive(targetBallShoulderActive);
                if (targetClubShoulderActive != clubShoulderPickup.gameObject.activeInHierarchy)
                    clubShoulderPickup.gameObject.SetActive(targetClubShoulderActive);
            }

            // If the local player still owns this PlayerManager check for their location again in another second
            if (this.LocalPlayerOwnsThisObject())
                SendCustomEventDelayedSeconds(nameof(_CheckPlayerLocation), 1);
        }

        public override string ToString()
        {
            var ownerName = !Utilities.IsValid(Owner) || !Utilities.IsValid(Owner.displayName) ? "Nobody" : Owner.displayName;
            if (ownerName.Trim().Length == 0)
                ownerName = "Eh?";
            var ready = IsReady ? "Ready" : "Not Ready";
            var playing = isPlaying ? "Playing" : "Not Playing";
            var playerState = $"{gameObject.name} - {ownerName}({ready}/{playing})";

            playerState += $" - (Total:{PlayerTotalScore}) (";
            for (var i = 0; i < courseStates.Length; i++)
                playerState += $"{courseStates[i].GetString()}-{courseScores[i]} / ";

            if (playerState.EndsWith(" / "))
                playerState = playerState.Substring(0, playerState.Length - 3) + ")";

            return playerState;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Owner = player;
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (Networking.GetOwner(gameObject) != player) return;

            Owner = player;

            PlayerIsCurrentlyFrozen = false;

            var localPlayerIsNowOwner = Networking.LocalPlayer == Networking.GetOwner(gameObject);

            if (localPlayerIsNowOwner)
            {
                openPutt.LocalPlayerManager = this;
                ownerIsInVR = Owner.IsUserInVR();
            }

            if (openPutt.debugMode)
                OpenPuttUtils.Log(this, $"{Owner.displayName}({(localPlayerIsNowOwner ? "me" : "not me")}) now owns this object!");

            if (localPlayerIsNowOwner || (courseScores.Length == 0 && courseStates.Length == 0 && courseTimes.Length == 0))
                _ResetPlayerScores();

            if (localPlayerIsNowOwner)
            {
                BallColor = Owner.ToColor();
                openPutt._LoadPersistantData();
            }

            if (Utilities.IsValid(golfClub))
            {
                if (localPlayerIsNowOwner)
                {
                    if (Utilities.IsValid(golfClub.openPuttSync))
                        golfClub.openPuttSync._Respawn();
                    else
                        golfClub.transform.position = new Vector3(0, -90, 0);
                    golfClub._RescaleClub(true);
                }

                golfClub._UpdateClubState();
            }

            if (Utilities.IsValid(golfBall))
            {
                if (localPlayerIsNowOwner)
                {
                    if (Utilities.IsValid(golfBall.openPuttSync))
                        golfBall.openPuttSync._Respawn();
                    else
                        golfBall._SetPosition(new Vector3(0, -90, 0));

                    if (Utilities.IsValid(golfBall.openPuttSync))
                    {
                        golfBall._SetRespawnPosition(golfBall.openPuttSync.originalPosition);
                    }
                }

                golfBall._UpdateBallState(golfBall.LocalPlayerOwnsThisObject());
            }

            if (Utilities.IsValid(playerLabel))
            {
                playerLabel.UpdatePosition();
                playerLabel.RefreshPlayerName();
            }

            if (localPlayerIsNowOwner)
            {
                _UpdateShoulderPickupAttachments();

                openPutt.leftShoulderPickup.gameObject.SetActive(true);
                openPutt.rightShoulderPickup.gameObject.SetActive(true);

                // Do a regular check for the players location
                SendCustomEventDelayedSeconds(nameof(_CheckPlayerLocation), 1);
            }

            _UpdateTotals();

            // Get the local player to send their current score
            _RequestSync();

            // Refresh scoreboards
            if (Utilities.IsValid(openPutt) && Utilities.IsValid(openPutt.scoreboardManager))
            {
                if (localPlayerIsNowOwner)
                    openPutt._OnLocalPlayerInitialised(this);
                openPutt._OnPlayerUpdate(this);
            }
        }

        public void _ResetPlayerScores()
        {
            // Reset score tracking
            isPlaying = true;
            CurrentCourse = null;
            courseScores = new int[Utilities.IsValid(openPutt) ? openPutt.courses.Length : 0];
            courseTimes = new long[Utilities.IsValid(openPutt) ? openPutt.courses.Length : 0];
            courseStates = new CourseState[Utilities.IsValid(openPutt) ? openPutt.courses.Length : 0];
            for (var i = 0; i < courseScores.Length; i++)
            {
                courseScores[i] = 0;
                courseTimes[i] = 0;
                courseStates[i] = CourseState.NotStarted;
            }

            _UpdateTotals();
        }

        /// <summary>
        /// Updates the players total score/time properties and fixes any incorrect course states
        /// </summary>
        public void _UpdateTotals()
        {
            if (!Utilities.IsValid(openPutt))
                return;

            if (PlayerHasStartedPlaying)
            {
                var score = 0;
                var totalTime = 0d;

                for (var i = 0; i < courseScores.Length; i++)
                {
                    // We don't count driving range scores
                    if (Utilities.IsValid(openPutt.courses[i]) && (openPutt.courses[i].courseType == CourseType.DrivingRangeDistance || openPutt.courses[i].courseType == CourseType.DrivingRangeWithTargets))
                        continue;

                    UpdateCourseState(openPutt.courses[i]);

                    switch (courseStates[i])
                    {
                        case CourseState.NotStarted:
                            totalTime += courseTimes[i];
                            break;
                        case CourseState.Playing:
                            // We don't include times for in progress courses - updating scoreboards is expensive
                            // var time = DateTime.UtcNow.GetUnixTimestamp() - courseTimes[i];
                            // if (time > 0)
                            //     totalTime += time;
                            break;
                        case CourseState.Completed:
                            totalTime += courseTimes[i];
                            break;
                        case CourseState.PlayedAndSkipped:
                        case CourseState.Skipped:
                            // Make sure scores are maxed out for skipped courses
                            courseScores[i] = openPutt.courses[i].maxScore;
                            courseTimes[i] = openPutt.courses[i].maxTime;

                            totalTime += courseTimes[i];
                            break;
                    }

                    score += courseScores[i];
                }

                PlayerTotalScore = score;
                PlayerTotalTime = totalTime;
            }
            else
            {
                PlayerTotalScore = 999999;
                PlayerTotalTime = 999999;
            }
        }

        /// <summary>
        /// Updates the current state of the course that is passed in. Does nothing if the course is the players current course as that should already be managed elsewhere.
        /// </summary>
        /// <param name="course">The course to update the state/scores on</param>
        private void UpdateCourseState(CourseManager course)
        {
            if (!Utilities.IsValid(openPutt))
                return;

            var canPlayCoursesInAnyOrder = openPutt.coursesCanBePlayedInAnyOrder;

            var CurrentCourseNumber = !Utilities.IsValid(CurrentCourse) ? -1 : CurrentCourse.holeNumber;
            var courseNumber = course.holeNumber;

            // We should already be tracking the current course state properly - so don't do anything here
            if (courseNumber == CurrentCourseNumber)
                return;

            // Driving ranges don't do much
            if (course.courseType == CourseType.DrivingRangeDistance || course.courseType == CourseType.DrivingRangeWithTargets)
            {
                courseStates[course.holeNumber] = CourseState.NotStarted;
                return;
            }

            var oldCourseState = courseStates[courseNumber];

            // If players can play courses in any order
            if (canPlayCoursesInAnyOrder)
            {
                if (oldCourseState == CourseState.Playing)
                {
                    // Only mark any courses that the player is "Playing" as skipped
                    courseStates[courseNumber] = CourseState.PlayedAndSkipped;
                    if (openPutt.debugMode)
                        OpenPuttUtils.Log(this, $"Skipped course {courseNumber} with score of {courseScores[courseNumber]} OldState={oldCourseState} NewState={courseStates[courseNumber]}");

                    courseScores[courseNumber] = openPutt.courses[courseNumber].maxScore;
                    courseTimes[courseNumber] = openPutt.courses[courseNumber].maxTime;
                }

                return;
            }

            if (courseNumber < CurrentCourseNumber)
            {
                // This course is before the current course in the list
                switch (oldCourseState)
                {
                    case CourseState.NotStarted:
                    case CourseState.Playing:
                        courseStates[courseNumber] = courseScores[courseNumber] > 0 ? CourseState.PlayedAndSkipped : CourseState.Skipped;

                        if (oldCourseState != courseStates[courseNumber] && openPutt.debugMode)
                            OpenPuttUtils.Log(this, $"Skipped course {courseNumber} with score of {courseScores[courseNumber]} OldState={oldCourseState} NewState={courseStates[courseNumber]}");

                        courseScores[courseNumber] = openPutt.courses[courseNumber].maxScore;
                        courseTimes[courseNumber] = openPutt.courses[courseNumber].maxTime;
                        break;
                    case CourseState.PlayedAndSkipped:
                    case CourseState.Skipped:
                        courseScores[courseNumber] = openPutt.courses[courseNumber].maxScore;
                        courseTimes[courseNumber] = openPutt.courses[courseNumber].maxTime;
                        break;
                    case CourseState.Completed:
                        break; // Don't touch completed courses
                }
            }
            else
            {
                // This course is after the current course in the list
                switch (oldCourseState)
                {
                    case CourseState.NotStarted:
                    case CourseState.Playing:
                        // And it isn't completed yet, reset it
                        courseScores[courseNumber] = 0;
                        courseTimes[courseNumber] = 0;
                        courseStates[courseNumber] = CourseState.NotStarted;
                        break;
                    case CourseState.PlayedAndSkipped:
                    case CourseState.Skipped:
                        // Make sure the scores are at max because they skipped it
                        courseScores[courseNumber] = openPutt.courses[courseNumber].maxScore;
                        courseTimes[courseNumber] = openPutt.courses[courseNumber].maxTime;
                        break;
                    case CourseState.Completed:
                        break; // Don't touch completed courses
                }
            }
        }

        public bool IsOnTopOfCurrentCourse(Vector3 position, float maxDistance = 0.1f)
        {
            // If we aren't playing a course the ball can be wherever
            if (!Utilities.IsValid(CurrentCourse) || !Utilities.IsValid(golfBall.floorMaterial) || !Utilities.IsValid(golfBall.floorMaterial.name))
                return false;

            // Check what is underneath the ball
            if (Physics.Raycast(position, golfBall.gravityDirection, out var hit, maxDistance) && Utilities.IsValid(hit.collider))
            {
                // Is it the kind of floor we are looking for?
                // Collider col = hit.collider;
                // bool rightKindOfFloor = Utilities.IsValid(col) && Utilities.IsValid(col.material) && Utilities.IsValid(col.material.name) && col.material.name.StartsWith(golfBall.floorMaterial.name);

                foreach (var mesh in CurrentCourse.floorObjects)
                {
                    if (!Utilities.IsValid(mesh))
                    {
                        OpenPuttUtils.LogError(CurrentCourse, "There is a null object in the list of floor objects for this course! Please fix by assigning it or removing the null entry!");
                        continue;
                    }

                    // Does this floor belong to the course the player is currently playing?
                    if (mesh.gameObject == hit.collider.gameObject)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public string CourseStateToString(CourseState state)
        {
            var courseState = "NA";
            switch (state)
            {
                case CourseState.NotStarted:
                    courseState = "Not Playing";
                    break;
                case CourseState.Playing:
                    courseState = "Playing";
                    break;
                case CourseState.PlayedAndSkipped:
                    courseState = "Played And Skipped";
                    break;
                case CourseState.Completed:
                    courseState = "Completed";
                    break;
                case CourseState.Skipped:
                    courseState = "Skipped";
                    break;
            }

            return courseState;
        }

        public void _OnWeirdThingHappened()
        {
            _weirdThingHappened = true;
        }

        public bool DidWeirdThingHappen() => _weirdThingHappened;
    }
}