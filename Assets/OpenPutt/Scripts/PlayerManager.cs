using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Cyan.PlayerObjectPool;
using VRC.SDK3.Components;
using System;

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
                    golfBall.UpdateBallState();
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
        public int PlayerTotalScore
        {
            get
            {
                int score = 0;
                for (int i = 0; i < courseScores.Length; i++)
                {
                    if (openPutt != null && openPutt.courses[i] != null && openPutt.courses[i].drivingRangeMode)
                        continue;
                    score += courseScores[i];
                }
                return score;
            }
        }
        /// <summary>
        /// Total number of milliseconds the player took to complete the courses
        /// </summary>
        public int PlayerTotalTime
        {
            get
            {
                int totalTime = 0;
                for (int i = 0; i < courseTimes.Length; i++)
                {
                    if (openPutt != null && openPutt.courses[i] != null && openPutt.courses[i].drivingRangeMode)
                        continue;

                    if (courseStates[i] == CourseState.Playing)
                    {
                        totalTime += Networking.GetServerTimeInMilliseconds() - courseTimes[i];
                    }
                    else
                    {
                        totalTime += courseTimes[i];
                    }
                }
                return totalTime;
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
        /// A little timer that runs once a second to manage shoulder pickups etc
        /// </summary>
        private float timeSinceLastLocationCheck = 0f;
        /// <summary>
        /// Used to rate-limit network sync for each PlayerManager
        /// </summary>
        private float syncDebounceTimer = -1f;
        /// <summary>
        /// Counts how many seconds a ball has not been on top of it's current course (Used to reset the ball quicker if it flies off the course)
        /// </summary>
        private int ballNotOnCourseCounter = 0;

        public void OnBallHit()
        {
            if (currentCourse == null || courseStates.Length != openPutt.courses.Length)
                return;

            bool sendSync = openPutt != null && openPutt.playerSyncType == PlayerSyncType.All;

            // Update the state of all courses
            for (int i = 0; i < openPutt.courses.Length; i++)
            {
                if (i < currentCourse.holeNumber)
                {
                    // If this course is before the current one
                    if ((int)courseStates[i] < (int)CourseState.Completed) // Cast to int because vrc update make the enum comparison (3 < 2) == true (HOW?!)
                    {
                        CourseState oldState = courseStates[i];
                        // And it isn't completed yet, skip it
                        courseStates[i] = courseScores[i] > 0 ? CourseState.PlayedAndSkipped : CourseState.Skipped;
                        Utils.Log(this, $"Skipped course {i} with score of {courseScores[i]} OldState={oldState} NewState={courseStates[i]}");
                        courseScores[i] = openPutt.courses[i].maxScore;
                        courseTimes[i] = openPutt.courses[i].maxTimeMillis;
                    }
                }
                else if (i > currentCourse.holeNumber)
                {
                    // If this course is after the current hole
                    if ((int)courseStates[i] < (int)CourseState.Completed) // Cast to int because vrc update make the enum comparison (3 < 2) == true (HOW?!)
                    {
                        // And it isn't completed yet, reset it
                        courseScores[i] = 0;
                        courseTimes[i] = 0;
                        courseStates[i] = CourseState.NotStarted;
                    }
                }
                else
                {
                    CourseManager course = openPutt.courses[i];
                    switch (courseStates[i])
                    {
                        case CourseState.NotStarted:
                            courseScores[i] = currentCourse.drivingRangeMode ? 0 : 1;
                            courseTimes[i] = Networking.GetServerTimeInMilliseconds();
                            courseStates[i] = CourseState.Playing;

                            // Can we tell others straight away about starting a new course?
                            if (sendSync = openPutt != null && openPutt.playerSyncType < PlayerSyncType.FinishOnly)
                                sendSync = true;

                            Utils.Log(this, $"Player just started Hole {currentCourse.holeNumber}.");
                            break;
                        case CourseState.Playing:
                            if (currentCourse.drivingRangeMode)
                            {
                                courseScores[i] = 0;
                            }
                            else
                            {
                                if (courseScores[i] < course.maxScore)
                                    courseScores[i]++;
                                else
                                    courseScores[i] = course.maxScore;
                            }

                            if (courseScores[i] == course.maxScore && openPutt != null && openPutt.SFXController != null)
                                openPutt.SFXController.PlayMaxScoreReachedSoundAtPosition(golfBall.transform.position);

                            Utils.Log(this, $"Hit ball - Current score is {courseScores[course.holeNumber]}/{course.maxScore} on course {course.holeNumber}.");
                            break;
                        case CourseState.Completed:
                        case CourseState.PlayedAndSkipped:
                            if (openPutt != null && openPutt.replayableCourses)
                            {
                                // Players are allowed to restart completed courses
                                courseScores[currentCourse.holeNumber] = currentCourse.drivingRangeMode ? 0 : 1;
                                courseTimes[currentCourse.holeNumber] = Networking.GetServerTimeInMilliseconds();
                                courseStates[currentCourse.holeNumber] = CourseState.Playing;

                                // Can we tell others straight away about starting a new course?
                                if (sendSync = openPutt != null && openPutt.playerSyncType < PlayerSyncType.FinishOnly)
                                    sendSync = true;

                                Utils.Log(this, $"Restarted hole - Current score is {courseScores[currentCourse.holeNumber]}/{course.maxScore} on course {currentCourse.holeNumber}.");
                            }
                            break;
                        case CourseState.Skipped:
                            // Players can start a course they haven't touched yet.. just in case they walked past some
                            courseScores[currentCourse.holeNumber] = currentCourse.drivingRangeMode ? 0 : 1;
                            courseTimes[currentCourse.holeNumber] = Networking.GetServerTimeInMilliseconds();
                            courseStates[currentCourse.holeNumber] = CourseState.Playing;

                            // Can we tell others straight away about starting a new course?
                            if (sendSync = openPutt != null && openPutt.playerSyncType < PlayerSyncType.FinishOnly)
                                sendSync = true;

                            Utils.Log(this, $"Restarted a skipped hole - Current score is {courseScores[currentCourse.holeNumber]}/{course.maxScore} on course {currentCourse.holeNumber}.");
                            break;
                    }
                }
            }

            // Update local scoreboards
            if (openPutt != null && openPutt.scoreboardManager != null)
                openPutt.scoreboardManager.RequestRefresh();

            // If fast updates are on send current state of player to everybody - otherwise it will be done when the player finishes the course
            if (sendSync)
                RequestSync();
        }

        public void OnCourseStarted(CourseManager newCourse)
        {
            bool canReplayCourses = openPutt != null && openPutt.replayableCourses;
            if (courseStates[newCourse.holeNumber] == CourseState.Completed || courseStates[newCourse.holeNumber] == CourseState.PlayedAndSkipped)
            {
                if (!canReplayCourses)
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

            if (newCourseState == CourseState.PlayedAndSkipped || newCourseState == CourseState.Skipped)
            {
                // If the player skipped this course - assign the max score for this course
                courseScores[course.holeNumber] = course.maxScore;
                courseTimes[course.holeNumber] = course.maxTimeMillis;
            }
            else
            {
                // Calculate the amount of time player spent on this course
                courseTimes[course.holeNumber] = Networking.GetServerTimeInMilliseconds() - courseTimes[course.holeNumber];
            }

            if (courseTimes[course.holeNumber] > course.maxTimeMillis)
                courseTimes[course.holeNumber] = course.maxTimeMillis;

            Utils.Log(this, $"Course({course.holeNumber}) was {courseStates[course.holeNumber].GetString()} and is now {(newCourseState == CourseState.Completed ? "Completed" : "Skipped")}! Current score is {courseScores[course.holeNumber]}. Player took {courseTimes[course.holeNumber]}ms to do this.");

            // Update the current state for this course
            courseStates[course.holeNumber] = newCourseState;

            if (newCourseState == CourseState.Completed && courseScores[course.holeNumber] == 1)
            {
                if (hole != null)
                    hole.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "OnHoleInOne");
            }

            if (openPutt != null && openPutt.scoreboardManager != null)
                openPutt.scoreboardManager.RequestRefresh();

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

        void Update()
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid())
                return;

            if (Networking.LocalPlayer.IsOwner(gameObject))
            {
                if (syncDebounceTimer != -1f)
                {
                    syncDebounceTimer += Time.deltaTime;

                    float maxRefreshInterval = openPutt != null ? openPutt.maxRefreshInterval : 1f;

                    if (syncDebounceTimer >= maxRefreshInterval)
                    {
                        syncDebounceTimer = -1f;

                        RequestSerialization();
                    }
                }
            }
            else
            {
                syncDebounceTimer = -1f;
            }

            // Toggle this players ball label on/off depending on where the local player is
            if (golfBall != null && playerLabel != null)
            {
                float distance = Vector3.Distance(golfBall.transform.position, Networking.LocalPlayer.GetPosition());
                float visiblityVal = playerLabel.IsMyLabel ? playerLabel.localLabelVisibilityCurve.Evaluate(distance) : playerLabel.remoteLabelVisibilityCurve.Evaluate(distance);
                bool newActiveState = visiblityVal > 0.1f;

                if (playerLabel.canvas.enabled != newActiveState)
                {
                    playerLabel.canvas.enabled = newActiveState;
                    if (newActiveState)
                        playerLabel.UpdatePosition();
                }
            }

            if (Owner == Networking.LocalPlayer)
            {
                // Every second check if player is on top of the course they are currently playing
                if (timeSinceLastLocationCheck > 1f)
                {
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
                        /*if (CurrentCourse.drivingRangeMode)
                        {
                            if (golfBall != null)
                            {
                                int distance = (int)Math.Round(Vector3.Distance(this.transform.position, golfBall.respawnPosition), 0);
                                courseScores[CurrentCourse.holeNumber] = distance;
                            }

                            if (openPutt != null && openPutt.scoreboardManager != null)
                                openPutt.scoreboardManager.RequestRefresh();
                        }*/

                        // TODO: We should probably be able to do this without a raycast by monitoring collisions on the ball instead
                        // Could count if there has been more than 10 frames of constant collision with a non course floor
                        if (!ballIsOnCurrentCourse)
                        {
                            ballNotOnCourseCounter++;

                            if (!CurrentCourse.drivingRangeMode && ballNotOnCourseCounter > ballOffCourseRespawnTime)
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

                    timeSinceLastLocationCheck = 0f;
                }
                timeSinceLastLocationCheck += Time.deltaTime;
            }
        }

        public override void OnDeserialization()
        {
            if (Owner == null)
                return;

            Utils.Log(this, $"Received score update from {Owner.displayName}!\r\n{ToString()}");
            if (openPutt != null && openPutt.scoreboardManager != null)
                openPutt.scoreboardManager.RequestRefresh();
        }

        /// <summary>
        /// Requests OpenPutt to perform a debounced network sync of scores etc.
        /// </summary>
        public void RequestSync(bool syncNow = false)
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid() || !Networking.LocalPlayer.IsOwner(gameObject))
                return;

            if (syncNow)
                RequestSerialization();
            else
                syncDebounceTimer = 0f;
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
        public override void _OnOwnerSet()
        {
            if (Owner == null || Owner.displayName == null)
            {
                Utils.Log(this, $"Owner is null!");
                return;
            }

            bool localPlayerIsNowOwner = Owner == Networking.LocalPlayer;

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
                golfClub.UpdateClubState();
            }
            if (golfBall != null)
            {
                golfBall.transform.position = Vector3.zero;
                golfBall.UpdateBallState();
            }

            if (playerLabel != null)
            {
                playerLabel.gameObject.SetActive(true);
                playerLabel.UpdatePosition();
                playerLabel.RefreshPlayerName();
            }

            if (localPlayerIsNowOwner)
            {
                openPutt.leftShoulderPickup.ObjectToAttach = golfBall.gameObject;
                openPutt.rightShoulderPickup.ObjectToAttach = golfClub.gameObject;

                openPutt.leftShoulderPickup.gameObject.SetActive(true);
                openPutt.rightShoulderPickup.gameObject.SetActive(true);
            }

            // Get the local player to send their current score
            RequestSync();

            // Refresh scoreboards
            if (openPutt != null && openPutt.scoreboardManager != null)
                openPutt.scoreboardManager.RequestRefresh();
        }

        // This method will be called on all clients when the original owner has left and the object is about to be disabled.
        public override void _OnCleanup()
        {
            // Cleanup the object here
            if (openPutt != null && openPutt.scoreboardManager != null)
                openPutt.scoreboardManager.RequestRefresh();

            BallColor = Color.red;
            isPlaying = true;

            courseScores = new int[0];
            courseStates = new CourseState[0];

            ClubVisible = false;
            BallVisible = false;

            if (playerLabel != null)
                playerLabel.gameObject.SetActive(false);
            if (golfClub != null)
                golfClub.gameObject.SetActive(false);
            if (golfClubHead != null)
                golfClubHead.gameObject.SetActive(false);
            if (golfBall != null)
                golfBall.gameObject.SetActive(false);

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
        }

        public bool IsOnTopOfCurrentCourse(Vector3 position, float maxDistance = 0.1f, float radius = 0f)
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