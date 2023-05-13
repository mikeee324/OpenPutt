﻿using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Cyan.PlayerObjectPool;
using VRC.SDK3.Components;
using System;
using JetBrains.Annotations;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerManager : CyanPlayerObjectPoolObject
    {
        [Header("This prefab manages keeping/syncing scores by assigning one of these objects per player. It will also keep track of the objects the player interacts with.")]
        public GolfClub golfClub;
        public GolfBallController golfBall;
        public GolfBallPlayerLabel playerLabel;
        public GolfClubCollider golfClubHead;
        public OpenPutt openPutt;

        [Header("Game Settings")]
        [UdonSynced]
        public bool isPlaying = true;
        [UdonSynced]
        public int[] courseScores = { };
        [UdonSynced]
        public int[] courseTimes = { };
        [UdonSynced]
        public CourseState[] courseStates = { };
        [Range(5, 30), Tooltip("The number of seconds a ball can be away from the current course before being respawned back instantly")]
        public int ballOffCourseRespawnTime = 5;

        [UdonSynced, FieldChangeCallback(nameof(BallColor))]
        private Color _ballColor = Color.black;
        public Color BallColor
        {
            set
            {
                _ballColor = value;
                if (golfBall != null)
                {
                    MeshRenderer renderer = golfBall.GetComponent<MeshRenderer>();
                    renderer.material.color = _ballColor;
                    renderer.material.SetColor("_EmissionColor", _ballColor);

                    TrailRenderer tr = golfBall.GetComponent<TrailRenderer>();
                    // A simple 2 color gradient with a fixed alpha of 1.0f.
                    float alpha = 1.0f;
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(_ballColor, 0.0f), new GradientColorKey(Color.black, 1.0f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(alpha, 1f), new GradientAlphaKey(alpha, 1f) }
                    );
                    tr.colorGradient = gradient;
                    tr.material.color = _ballColor;

                    // -- Set up render queues / zwrites so ball renders through walls and stuff
                    bool localPlayerIsOwner = Networking.LocalPlayer != null && Networking.LocalPlayer.IsValid() && Networking.LocalPlayer.IsOwner(gameObject);

                    // If we aren't using the default 2000 render queue number
                    int renderQueueBase = openPutt != null ? openPutt.ballRenderQueueBase : -1;
                    if (renderQueueBase != -1)
                    {
                        // Take the base render queue number and make sure local players ball renders in front of remote player balls
                        // Adjust ball render queue
                        renderer.material.renderQueue = renderQueueBase + (localPlayerIsOwner ? 5 : 3);
                        //renderer.material.SetInt("_ZWrite", 1);
                        renderer.material.SetInt("_ZTest", 8);

                        // Adjust trail renderer render queue
                        tr.material.renderQueue = renderQueueBase + (localPlayerIsOwner ? 4 : 2);
                        //tr.material.SetInt("_ZWrite", 1);
                        tr.material.SetInt("_ZTest", 8);
                    }
                }
            }
            get => _ballColor;
        }
        [UdonSynced, FieldChangeCallback(nameof(ClubVisible))]
        private bool _clubVisible = false;
        [UdonSynced, FieldChangeCallback(nameof(BallVisible))]
        private bool _ballVisible = false;
        public int PlayerID => transform.GetSiblingIndex();
        /// <summary>
        /// Used to sync the visible state of this club to other players
        /// </summary>
        public bool ClubVisible
        {
            get => _clubVisible;
            set
            {
                if (golfClub != null && value != _clubVisible)
                {
                    golfClub.gameObject.SetActive(value);
                    golfClub.UpdateClubState();
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
                if (golfBall != null && value != _ballVisible)
                {
                    golfBall.gameObject.SetActive(value);
                    golfBall.UpdateBallState(golfBall.LocalPlayerOwnsThisObject());
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
        /// Total number of milliseconds the player took to complete the courses
        /// </summary>
        [UdonSynced]
        public int PlayerTotalTime = 999999;

        /// <summary>
        /// Check if the player has started at least 1 course in the world. Useful for checking if the player is actually playing the game or not.
        /// </summary>
        public bool PlayerHasStartedPlaying
        {
            get
            {
                if (!IsReady) return false;
                for (int i = 0; i < courseStates.Length; i++)
                    if (courseStates[i] != CourseState.NotStarted)
                        return true;
                return false;
            }
        }
        /// <summary>
        /// Contains a reference to the players current course that they are playing on (returns null if they aren't playing any)
        /// </summary>
        public CourseManager CurrentCourse => currentCourse;
        private CourseManager currentCourse;
        public bool IsInLeftHandedMode
        {
            get => openPutt != null && openPutt.leftShoulderPickup != null && openPutt.leftShoulderPickup.ObjectToAttach != null && openPutt.leftShoulderPickup.ObjectToAttach == golfClub.gameObject;
            set
            {
                openPutt.leftShoulderPickup.ObjectToAttach = value ? golfClub.gameObject : golfBall.gameObject;
                openPutt.rightShoulderPickup.ObjectToAttach = value ? golfBall.gameObject : golfClub.gameObject;
            }
        }
        /// <summary>
        /// Toggles whether the player is immobile or not. Only works if this PlayerManager belongs to the local player.
        /// </summary>
        public bool PlayerIsCurrentlyFrozen
        {
            get => _isImmobilized;
            set
            {
                _isImmobilized = value;

                if (!freezePlayerWhileClubIsArmed)
                    _isImmobilized = false;

                if (Owner != null && Owner.IsValid() && Owner.isLocal)
                    Owner.Immobilize(value);
                else
                    _isImmobilized = false;
            }
        }
        private bool _isImmobilized = false;
        /// <summary>
        /// Used to rate-limit network sync for each PlayerManager
        /// </summary>
        private bool syncRequested = false;
        /// <summary>
        /// Counts how many seconds a ball has not been on top of it's current course (Used to reset the ball quicker if it flies off the course)
        /// </summary>
        private int ballNotOnCourseCounter = 0;
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
        public bool scoreboardRowNeedsUpdating = false;
        /// <summary>
        /// Traps the player in a station while the club is armed
        /// </summary>
        public bool freezePlayerWhileClubIsArmed = true;

        public void OnBallHit()
        {
            if (currentCourse == null || courseStates.Length != openPutt.courses.Length)
                return;

            bool sendSync = openPutt != null && openPutt.playerSyncType == PlayerSyncType.All;

            // Set score on current course
            switch (courseStates[currentCourse.holeNumber])
            {
                case CourseState.Skipped:
                case CourseState.NotStarted:
                    courseStates[currentCourse.holeNumber] = CourseState.Playing;
                    if (!currentCourse.drivingRangeMode)
                        courseScores[currentCourse.holeNumber] = 1;
                    courseTimes[currentCourse.holeNumber] = Networking.GetServerTimeInMilliseconds();
                    break;
                case CourseState.Completed:
                case CourseState.PlayedAndSkipped:
                    if (openPutt != null && (openPutt.replayableCourses || currentCourse.courseIsAlwaysReplayable))
                    {
                        courseStates[currentCourse.holeNumber] = CourseState.Playing;
                        if (!currentCourse.drivingRangeMode)
                            courseScores[currentCourse.holeNumber] = 1;
                        courseTimes[currentCourse.holeNumber] = Networking.GetServerTimeInMilliseconds();
                    }
                    break;
                case CourseState.Playing:
                    if (!currentCourse.drivingRangeMode)
                    {
                        if (courseScores[currentCourse.holeNumber] < currentCourse.maxScore)
                            courseScores[currentCourse.holeNumber] += 1;
                        else
                            courseScores[currentCourse.holeNumber] = currentCourse.maxScore;

                        if (courseScores[currentCourse.holeNumber] == currentCourse.maxScore)
                        {
                            // Play max score reached sound
                            if (openPutt != null && openPutt.SFXController != null)
                                openPutt.SFXController.PlayMaxScoreReachedSoundAtPosition(golfBall.transform.position);

                            // Prevents the sound from being heard again
                            courseStates[currentCourse.holeNumber] = CourseState.Completed;
                        }
                    }
                    break;
            }

            // Update the state of all courses
            UpdateTotals();

            // Update local scoreboards
            if (openPutt != null)
                openPutt.OnPlayerUpdate(this);

            // If fast updates are on send current state of player to everybody - otherwise it will be done when the player finishes the course
            if (sendSync)
                RequestSync();

            if (openPutt != null)
            {
                foreach (OpenPuttEventListener eventListener in openPutt.eventListeners)
                    eventListener.OnLocalPlayerBallHit();
            }
        }

        public void OnCourseStarted(CourseManager newCourse)
        {
            bool canReplayCourses = openPutt != null && openPutt.replayableCourses;
            if (courseStates[newCourse.holeNumber] == CourseState.Completed || courseStates[newCourse.holeNumber] == CourseState.PlayedAndSkipped)
            {
                if (!canReplayCourses && !newCourse.courseIsAlwaysReplayable)
                {
                    Utils.Log(this, $"Player tried to restart course {newCourse.holeNumber}. They have already completed or skipped it though.");
                    return;
                }
            }

            Utils.Log(this, $"Starting course number {newCourse.holeNumber}. Current course({(currentCourse != null ? currentCourse.holeNumber : -1)}) will be closed");

            // If the player is already on a hole say they skipped it
            if (currentCourse != null && currentCourse.holeNumber != newCourse.holeNumber)
            {
                OnCourseFinished(currentCourse, null, courseScores[currentCourse.holeNumber] > 0 ? CourseState.PlayedAndSkipped : CourseState.Skipped);
            }

            currentCourse = newCourse;
        }

        public void OnCourseFinished(CourseManager course, CourseHole hole, CourseState newCourseState)
        {
            // Player either isn't playing a course already or they put the ball in the wrong hole - ignore event
            if (course == null || currentCourse != course)
            {
                Utils.Log(this, $"Course {course.holeNumber} was finished. {(course == null ? "Course is null" : "")} {(currentCourse != course ? "Wrong course!" : "")}");
                return;
            }

            currentCourse = null;

            // Add on any extra points to the players score that this particular hole has
            if (hole != null)
                courseScores[course.holeNumber] += hole.holeScoreAddition;

            if (course.drivingRangeMode)
            {
                int distance = Mathf.FloorToInt(Vector3.Distance(golfBall.transform.position, golfBall.respawnPosition));
                // Driving ranges will just track the highest score - use a separate canvas for live/previous hit distance
                if (distance > courseScores[course.holeNumber])
                    courseScores[course.holeNumber] = distance;
            }

            if (newCourseState == CourseState.PlayedAndSkipped || newCourseState == CourseState.Skipped)
            {
                // If the player skipped this course - assign the max score for this course
                courseScores[course.holeNumber] = course.maxScore;
                courseTimes[course.holeNumber] = course.maxTime;
            }
            else
            {
                // Calculate the amount of time player spent on this course
                courseTimes[course.holeNumber] = Mathf.CeilToInt((Networking.GetServerTimeInMilliseconds() - courseTimes[course.holeNumber]) * 0.001f);
            }

            Utils.Log(this, $"Course({course.holeNumber}) was {courseStates[course.holeNumber].GetString()} and is now {(newCourseState == CourseState.Completed ? "Completed" : "Skipped")}! Current score is {courseScores[course.holeNumber]}. Player took {courseTimes[course.holeNumber]}s to do this.");

            // Update the current state for this course
            courseStates[course.holeNumber] = newCourseState;

            if (newCourseState == CourseState.Completed && courseScores[course.holeNumber] == 1)
            {
                if (hole != null)
                    hole.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "OnHoleInOne");
            }

            if (openPutt != null && openPutt.scoreboardManager != null)
            {
                openPutt.scoreboardManager.requestedScoreboardView = ScoreboardView.Scoreboard;
                UpdateTotals();
                openPutt.OnPlayerUpdate(this);
            }

            if (newCourseState == CourseState.Completed)
            {
                if (hole != null)
                    hole.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "OnBallEntered");

                // If the player actually finished the hole - send a sync.. otherwise we'll wait for them to do something else that sends a sync
                RequestSync();
            }
        }

        void Start()
        {
            ResetPlayerScores();
        }

        public override void OnDeserialization()
        {
            if (Owner == null)
                return;

            Utils.Log(this, $"Received update from {Owner.displayName}!\r\n{ToString()}");

            UpdateTotals();

            if (openPutt != null && openPutt.scoreboardManager != null)
                openPutt.OnPlayerUpdate(this);
        }

        /// <summary>
        /// Requests OpenPutt to perform a debounced network sync of scores etc.
        /// </summary>
        public void RequestSync(bool syncNow = false)
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid() || !Networking.LocalPlayer.IsOwner(gameObject))
                return;

            if (syncNow)
            {
                SyncNow();
            }
            else if (!syncRequested)
            {
                // If we aren't already waiting for a sync to happen schedule one in
                syncRequested = true;
                float maxRefreshInterval = openPutt != null ? openPutt.maxRefreshInterval : 1f;
                SendCustomEventDelayedSeconds(nameof(SyncNow), maxRefreshInterval);
            }
        }

        public void SyncNow()
        {
            RequestSerialization();
            syncRequested = false;
        }

        public void CheckPlayerLocation()
        {
            if (!Utils.LocalPlayerIsValid())
            {
                SendCustomEventDelayedSeconds(nameof(CheckPlayerLocation), 1);
                return;
            }

            // Toggle golf club shoulder pickup on/off depending on if the player is holding the club or not
            if (openPutt != null && openPutt.rightShoulderPickup != null && openPutt.rightShoulderPickup.ObjectToAttach != null)
            {
                VRCPickup pickupHelper = openPutt.rightShoulderPickup.ObjectToAttach.GetComponent<VRCPickup>();
                if (pickupHelper != null)
                    openPutt.rightShoulderPickup.gameObject.SetActive(isPlaying && pickupHelper.currentHand == VRC_Pickup.PickupHand.None);
            }

            bool ballIsOnCurrentCourse = IsOnTopOfCurrentCourse(golfBall.transform.position, 100f);

            if (openPutt != null && openPutt.leftShoulderPickup != null)
            {
                bool shouldEnablePickup = true;

                // If the player is stood on their current course
                if (IsOnTopOfCurrentCourse(Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Spine), 100f))
                {
                    // Disable the pickup
                    shouldEnablePickup = false;

                    // If the ball is not on the course though - enable the pickup (so they can reset the ball to the start if they wish)
                    if (!ballIsOnCurrentCourse)
                        shouldEnablePickup = true;
                }

                if (!isPlaying)
                    shouldEnablePickup = false;
                else if (golfBall.pickedUpByPlayer)
                    shouldEnablePickup = true;

                if (shouldEnablePickup != openPutt.leftShoulderPickup.gameObject.activeInHierarchy)
                    openPutt.leftShoulderPickup.gameObject.SetActive(shouldEnablePickup);
            }

            if (CurrentCourse != null)
            {
                // Live driving range updates (kinda)
                if (CurrentCourse.drivingRangeMode)
                {
                    if (golfBall.BallIsMoving)
                    {
                        // If the ball is fairly close to any floor, start the count down to reset the ball
                        ballIsOnCurrentCourse = !golfBall.OnGround;

                        // We half the amount of time on the ground for driving ranges before resetting
                        if (golfBall.OnGround)
                            ballNotOnCourseCounter++;

                        int distance = (int)Math.Floor(Vector3.Distance(golfBall.transform.position, golfBall.respawnPosition));
                        if (distance > courseScores[CurrentCourse.holeNumber])
                        {
                            courseScores[CurrentCourse.holeNumber] = distance;

                            //if (openPutt != null && openPutt.scoreboardManager != null)
                            //    openPutt.scoreboardManager.RequestPlayerListRefresh();
                        }
                    }
                }

                // TODO: We should probably be able to do this without a raycast by monitoring collisions on the ball instead
                // Could count if there has been more than 10 frames of constant collision with a non course floor
                if (!ballIsOnCurrentCourse)
                {
                    ballNotOnCourseCounter++;

                    if (ballNotOnCourseCounter > ballOffCourseRespawnTime)
                    {
                        Utils.Log(this, "Ball has been off its course for too long");
                        ballNotOnCourseCounter = 0;
                        golfBall.BallIsMoving = false;
                    }
                }
                else
                {
                    ballNotOnCourseCounter = 0;
                }
            }

            // If the local player still owns this PlayerManager check for their location again in another second
            if (this.LocalPlayerOwnsThisObject())
            {
                SendCustomEventDelayedSeconds(nameof(CheckPlayerLocation), 1);
            }
        }

        public override string ToString()
        {
            string ownerName = Owner == null || Owner.displayName == null ? "Nobody" : Owner.displayName;
            if (ownerName.Trim().Length == 0)
                ownerName = "Eh?";
            string ready = IsReady ? "Ready" : "Not Ready";
            string playing = isPlaying ? "Playing" : "Not Playing";
            string playerState = $"{gameObject.name} - {ownerName}({ready}/{playing})";

            playerState += $" - (Total:{PlayerTotalScore}) (";
            for (int i = 0; i < courseStates.Length; i++)
                playerState += $"{courseStates[i].GetString()}-{courseScores[i]} / ";

            if (playerState.EndsWith(" / "))
                playerState = playerState.Substring(0, playerState.Length - 3) + ")";

            return playerState;
        }

        // This method will be called on all clients when the object is enabled and the Owner has been assigned.
        [PublicAPI]
        public override void _OnOwnerSet()
        {
            if (Owner == null || Owner.displayName == null)
            {
                Utils.Log(this, $"Owner is null!");
                return;
            }

            PlayerIsCurrentlyFrozen = false;

            bool localPlayerIsNowOwner = Owner == Networking.LocalPlayer;

            if (localPlayerIsNowOwner)
            {
                openPutt.LocalPlayerManager = this;
            }

            Utils.Log(this, $"{Owner.displayName}({(localPlayerIsNowOwner ? "me" : "not me")}) now owns this object!");

            // Initialise ball color
            if (_ballColor == Color.black)
            {
                BallColor = new Color(UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f));
            }

            if (localPlayerIsNowOwner || (courseScores.Length == 0 && courseStates.Length == 0 && courseTimes.Length == 0))
                ResetPlayerScores();

            if (golfClub != null)
            {
                golfClub.transform.position = Vector3.zero;
                golfClub.RescaleClub(true);
                golfClub.UpdateClubState(localPlayerIsNowOwner);
            }
            if (golfBall != null)
            {
                golfBall.transform.position = Vector3.zero;
                golfBall.UpdateBallState(golfBall.LocalPlayerOwnsThisObject());
            }

            if (playerLabel != null)
            {
                playerLabel.UpdatePosition();
                playerLabel.RefreshPlayerName();
            }

            if (localPlayerIsNowOwner)
            {
                openPutt.leftShoulderPickup.ObjectToAttach = golfBall.gameObject;
                openPutt.rightShoulderPickup.ObjectToAttach = golfClub.gameObject;

                openPutt.leftShoulderPickup.gameObject.SetActive(true);
                openPutt.rightShoulderPickup.gameObject.SetActive(true);

                // Do a regular check for the players location
                SendCustomEventDelayedSeconds(nameof(CheckPlayerLocation), 1);
            }

            UpdateTotals();

            // Get the local player to send their current score
            RequestSync();

            // Refresh scoreboards
            if (openPutt != null && openPutt.scoreboardManager != null)
                openPutt.OnPlayerUpdate(this);
        }

        // This method will be called on all clients when the original owner has left and the object is about to be disabled.
        [PublicAPI]
        public override void _OnCleanup()
        {
            // Cleanup the object here

            BallColor = Color.red;
            isPlaying = true;

            courseScores = new int[0];
            courseStates = new CourseState[0];

            ClubVisible = false;
            BallVisible = false;

            if (golfClub != null)
                golfClub.gameObject.SetActive(false);
            if (golfClubHead != null)
                golfClubHead.gameObject.SetActive(false);
            if (golfBall != null)
                golfBall.gameObject.SetActive(false);

            UpdateTotals();

            if (openPutt != null)
                openPutt.OnPlayerUpdate(this);

            RequestSerialization();

            Utils.Log(this, $"PlayerManager has been cleaned up");
        }

        public void ResetPlayerScores()
        {
            // Reset score tracking
            isPlaying = true;
            currentCourse = null;
            courseScores = new int[openPutt != null ? openPutt.courses.Length : 0];
            courseTimes = new int[openPutt != null ? openPutt.courses.Length : 0];
            courseStates = new CourseState[openPutt != null ? openPutt.courses.Length : 0];
            for (int i = 0; i < courseScores.Length; i++)
            {
                courseScores[i] = 0;
                courseTimes[i] = 0;
                courseStates[i] = CourseState.NotStarted;
            }
            UpdateTotals();
        }

        /// <summary>
        /// Updates the players total score/time properties and fixes any incorrect course states
        /// </summary>
        public void UpdateTotals()
        {
            if (openPutt == null)
                return;

            if (PlayerHasStartedPlaying)
            {
                int score = 0;
                int totalTime = 0;

                for (int i = 0; i < courseScores.Length; i++)
                {
                    // We don't count driving range scores
                    if (openPutt.courses[i] != null && openPutt.courses[i].drivingRangeMode)
                        continue;

                    UpdateCourseState(openPutt.courses[i]);

                    switch (courseStates[i])
                    {
                        case CourseState.NotStarted:
                            totalTime += courseTimes[i];
                            break;
                        case CourseState.Playing:
                            // Not counting courses that are in progress for now (but this is how you'd do it!)
                            // UpdateTotals would have to be run regularly for this to work though (can cause lag)
                            //  totalTime += Networking.GetServerTimeInMilliseconds() - courseTimes[i];
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
            if (openPutt == null)
                return;

            bool canPlayCoursesInAnyOrder = openPutt.coursesCanBePlayedInAnyOrder;

            int currentCourseNumber = currentCourse == null ? -1 : currentCourse.holeNumber;
            int courseNumber = course.holeNumber;

            // We should already be tracking the current course state properly - so don't do anything here
            if (courseNumber == currentCourseNumber)
                return;

            // Driving ranges don't do much
            if (course.drivingRangeMode)
            {
                courseStates[course.holeNumber] = CourseState.NotStarted;
                return;
            }

            CourseState oldCourseState = courseStates[courseNumber];

            // If players can play courses in any order
            if (canPlayCoursesInAnyOrder)
            {
                if (oldCourseState == CourseState.Playing)
                {
                    // Only mark any courses that the player is "Playing" as skipped
                    courseStates[courseNumber] = CourseState.PlayedAndSkipped;
                    Utils.Log(this, $"Skipped course {courseNumber} with score of {courseScores[courseNumber]} OldState={oldCourseState} NewState={courseStates[courseNumber]}");

                    courseScores[courseNumber] = openPutt.courses[courseNumber].maxScore;
                    courseTimes[courseNumber] = openPutt.courses[courseNumber].maxTime;
                }
                return;
            }

            if (courseNumber < currentCourseNumber)
            {
                // This course is before the current course in the list
                switch (oldCourseState)
                {
                    case CourseState.NotStarted:
                    case CourseState.Playing:
                        courseStates[courseNumber] = courseScores[courseNumber] > 0 ? CourseState.PlayedAndSkipped : CourseState.Skipped;

                        if (oldCourseState != courseStates[courseNumber])
                            Utils.Log(this, $"Skipped course {courseNumber} with score of {courseScores[courseNumber]} OldState={oldCourseState} NewState={courseStates[courseNumber]}");

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
            if (currentCourse == null || golfBall.floorMaterial == null || golfBall.floorMaterial.name == null)
                return false;

            // Check what is underneath the ball
            if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, maxDistance) && hit.collider != null)
            {
                // Is it the kind of floor we are looking for?
                // Collider col = hit.collider;
                // bool rightKindOfFloor = col != null && col.material != null && col.material.name != null && col.material.name.StartsWith(golfBall.floorMaterial.name);

                foreach (GameObject mesh in currentCourse.floorObjects)
                {
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
            string courseState = "NA";
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
    }
}