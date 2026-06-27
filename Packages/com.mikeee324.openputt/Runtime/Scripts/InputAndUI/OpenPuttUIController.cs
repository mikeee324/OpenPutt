using UdonSharp;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttUIController : OpenPuttEventListener
    {
        /// <summary>
        /// A reference to the OpenPutt instance in this world
        /// </summary>
        [Tooltip("A reference to OpenPutt. This should get filled in automatically when building, but to be safe always set it!")]
        public OpenPutt openPutt;

        [Tooltip("Reference to the input handler for this UI")]
        public OpenPuttInputHandler inputHandler;

        public OpenPuttBallCam ballCam;

        public OpenPuttUIView activeUI; // The currently active UI (desktop, mobile, or VR)

        public OpenPuttUIView desktopUI;
        public OpenPuttUIView mobileUI;
        public OpenPuttUIView vrUI;
        private PlayerManager localPlayerManager;

        private bool isMonitoringBallDistance = false;
        private bool isBallMoving = false;
        private bool isInVR = false;

        void Start()
        {
            if (!Utilities.IsValid(openPutt))
                return;

            isInVR = Networking.LocalPlayer.IsUserInVR();

            // If this object isn't already registered as an event listener, register it here automatically
            openPutt._RegisterEventListener(this);

            if (isInVR)
            {
                if (Utilities.IsValid(vrUI))
                {
                    vrUI.transform.parent.gameObject.SetActive(true);
                    activeUI = vrUI;
                    SetUIVisible(false);
                }
                if (Utilities.IsValid(desktopUI)) desktopUI.transform.parent.gameObject.SetActive(false);
                if (Utilities.IsValid(mobileUI)) mobileUI.transform.parent.gameObject.SetActive(false);
            }
            else
            {
#if UNITY_ANDROID || UNITY_IOS
                if (Utilities.IsValid(mobileUI)) {  
                    mobileUI.transform.parent.gameObject.SetActive(true);
                    activeUI = mobileUI; 
                }
                if (Utilities.IsValid(desktopUI)) desktopUI.transform.parent.gameObject.SetActive(false);
                if (Utilities.IsValid(vrUI)) vrUI.transform.parent.gameObject.SetActive(false);
#else
                if (Utilities.IsValid(desktopUI))
                {
                    desktopUI.transform.parent.gameObject.SetActive(true);
                    activeUI = desktopUI;
                }
                if (Utilities.IsValid(mobileUI)) mobileUI.transform.parent.gameObject.SetActive(false);
                if (Utilities.IsValid(vrUI)) vrUI.transform.parent.gameObject.SetActive(false);
#endif
            }

        }

        void Update()
        {
            if (!Utilities.IsValid(activeUI) || !Utilities.IsValid(inputHandler))
                return;

            activeUI.SetPowerIndicator(inputHandler.CurrentShotPowerNormalized);
        }

        /// <summary>
        /// Called when OpenPutt has assigned and finished setting up a PlayerManager for a player
        /// </summary>
        /// <param name="player">The player that was initialized</param>
        /// <param name="playerManager">The PlayerManager that was assigned</param>
        public override void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {
            if (!player.isLocal) return;

            localPlayerManager = playerManager;

            if (player.isLocal && Utilities.IsValid(activeUI))
            {
                UpdateDisplay();
            }

            UpdateButtonStates();
        }

        public void UpdateButtonStates()
        {
            if (!Utilities.IsValid(activeUI)) return;

            bool userInVR = Networking.LocalPlayer.IsUserInVR();

            bool cameraIsOn = false;
            if (Utilities.IsValid(ballCam))
                cameraIsOn = ballCam.BallCamActive;

            if (userInVR)
            {
                if (Utilities.IsValid(activeUI.toggleCameraButton))
                    activeUI.toggleCameraButton.SetActive(false);
                if (Utilities.IsValid(activeUI.fetchBallButton))
                    activeUI.fetchBallButton.SetActive(false);
                if (Utilities.IsValid(activeUI.shootButton))
                    activeUI.shootButton.SetActive(false);
                if (Utilities.IsValid(activeUI.cycleClubPreviousButton))
                    activeUI.cycleClubPreviousButton.SetActive(!openPutt.hasChangedClubType);
                if (Utilities.IsValid(activeUI.cycleClubNextButton))
                    activeUI.cycleClubNextButton.SetActive(!openPutt.hasChangedClubType);
                if (Utilities.IsValid(localPlayerManager))
                {
                    if (Utilities.IsValid(activeUI.leftShoulderButton))
                        activeUI.leftShoulderButton.SetActive(!(localPlayerManager.IsInLeftHandedMode ? openPutt.hasUsedGolfClub : openPutt.hasUsedGolfBall));
                    if (Utilities.IsValid(activeUI.rightShoulderButton))
                        activeUI.rightShoulderButton.SetActive(!(localPlayerManager.IsInLeftHandedMode ? openPutt.hasUsedGolfBall : openPutt.hasUsedGolfClub));

                    if (Utilities.IsValid(activeUI.leftShoulderBallIcon))
                        activeUI.leftShoulderBallIcon.gameObject.SetActive(!localPlayerManager.IsInLeftHandedMode);
                    if (Utilities.IsValid(activeUI.leftShoulderClubIcon))
                        activeUI.leftShoulderClubIcon.gameObject.SetActive(localPlayerManager.IsInLeftHandedMode);

                    if (Utilities.IsValid(activeUI.rightShoulderBallIcon))
                        activeUI.rightShoulderBallIcon.gameObject.SetActive(localPlayerManager.IsInLeftHandedMode);
                    if (Utilities.IsValid(activeUI.rightShoulderClubIcon))
                        activeUI.rightShoulderClubIcon.gameObject.SetActive(!localPlayerManager.IsInLeftHandedMode);
                }
                else
                {
                    if (Utilities.IsValid(activeUI.leftShoulderButton))
                        activeUI.leftShoulderButton.SetActive(false);
                    if (Utilities.IsValid(activeUI.rightShoulderButton))
                        activeUI.rightShoulderButton.SetActive(false);
                }

                if (Utilities.IsValid(activeUI.cycleClubPreviousText))
                    activeUI.cycleClubPreviousText.text = "Prev Club\n<size=50>( Right Stick Down )</size>";
                if (Utilities.IsValid(activeUI.cycleClubNextText))
                    activeUI.cycleClubNextText.text = "Next Club\n<size=50>( Right Stick Up )</size>";

                if (Utilities.IsValid(activeUI.portableMenuButton))
                    activeUI.portableMenuButton.SetActive(!openPutt.hasUsedPortableScoreboard);
                return;
            }

            if (Utilities.IsValid(activeUI.toggleCameraButton))
                activeUI.toggleCameraButton.SetActive(openPutt.hasUsedGolfBall);
            if (Utilities.IsValid(activeUI.fetchBallButton))
                activeUI.fetchBallButton.SetActive(!cameraIsOn);
            if (Utilities.IsValid(activeUI.shootButton))
                activeUI.shootButton.SetActive(cameraIsOn);
            var currentCourse = Utilities.IsValid(localPlayerManager) ? localPlayerManager.CurrentCourse : null;
            var canChooseClub = Utilities.IsValid(currentCourse) ? currentCourse._HasClubChoice() : openPutt.allowAnyClubOffCourse;
            if (Utilities.IsValid(activeUI.cycleClubPreviousButton))
                activeUI.cycleClubPreviousButton.SetActive(canChooseClub && ballCam.BallCamActive);
            if (Utilities.IsValid(activeUI.cycleClubNextButton))
                activeUI.cycleClubNextButton.SetActive(canChooseClub && ballCam.BallCamActive);
            if (Utilities.IsValid(activeUI.leftShoulderButton))
                activeUI.leftShoulderButton.SetActive(false);
            if (Utilities.IsValid(activeUI.rightShoulderButton))
                activeUI.rightShoulderButton.SetActive(false);
            if (Utilities.IsValid(activeUI.portableMenuButton))
                activeUI.portableMenuButton.SetActive(false);
        }

        public void UpdateDisplay()
        {
            if (!Utilities.IsValid(activeUI)) return;
            if (!Utilities.IsValid(localPlayerManager)) return;

            var currentCourse = localPlayerManager.CurrentCourse;

            if (Utilities.IsValid(currentCourse))
            {
                int holeNum = currentCourse.holeNumber;
                if (holeNum < openPutt.courses.Length)
                {
                    var course = openPutt.courses[holeNum];
                    if (Utilities.IsValid(course.scoreboardLongName) && course.scoreboardLongName.Length > 0)
                        activeUI.courseName.text = $"{course.scoreboardLongName} ({holeNum + 1})";
                    else if (Utilities.IsValid(course.scoreboardShortName) && course.scoreboardShortName.Length > 0)
                        activeUI.courseName.text = $"{course.scoreboardShortName} ({holeNum + 1})";
                    else if (course.courseType == CourseType.DrivingRangeDistance || course.courseType == CourseType.DrivingRangeWithTargets)
                        activeUI.courseName.text = $"Driving Range {holeNum + 1}";
                    else
                        activeUI.courseName.text = $"Hole {holeNum + 1}";
                }
                else
                {
                    activeUI.courseName.text = "All courses completed";
                }

                switch (currentCourse.courseType)
                {
                    case CourseType.Standard:
                        {
                            int currentHoleScore = localPlayerManager.courseScores[currentCourse.holeNumber];
                            int currentPar = currentCourse.parScore;
                            int currentRelative = currentHoleScore - currentPar;
                            activeUI.parScore.text = $"Par {currentPar}";
                            activeUI.relativeScore.text = (currentRelative >= 0 ? "+" : "") + currentRelative.ToString();
                            activeUI.totalScore.text = $"{currentHoleScore} / {currentPar}\nShots";
                            break;
                        }
                    case CourseType.DrivingRangeDistance:
                        {
                            float maxDist = 0f;
                            if (Utilities.IsValid(localPlayerManager.golfBall))
                            {
                                localPlayerManager.golfBall._GetLastHitData(out maxDist, out var totalDist);
                                maxDist = Mathf.FloorToInt(maxDist);
                            }
                            int currentPar = currentCourse.parScore;
                            activeUI.parScore.text = $"Par {currentPar}m";
                            activeUI.relativeScore.text = $"{maxDist:F0}m";
                            activeUI.totalScore.text = $"{maxDist:F0}m / {currentPar}m\nDistance";
                            break;
                        }
                    case CourseType.DrivingRangeWithTargets:
                        {
                            int currentHoleScore = localPlayerManager.courseScores[currentCourse.holeNumber];
                            int currentPar = currentCourse.parScore;
                            activeUI.parScore.text = $"";
                            activeUI.relativeScore.text = currentHoleScore.ToString();
                            activeUI.totalScore.text = $"Hit The Targets";
                            break;
                        }
                }
            }
            else
            {
                activeUI.courseName.text = "OpenPutt";
                activeUI.parScore.text = "Place a ball to start";
                activeUI.relativeScore.text = "-";

                int holesCompleted = 0;
                foreach (var course in openPutt.courses)
                {
                    if (Utilities.IsValid(course) && course.holeNumber >= 0 && course.holeNumber < localPlayerManager.courseStates.Length)
                    {
                        var state = localPlayerManager.courseStates[course.holeNumber];
                        if (state == CourseState.Completed || state == CourseState.PlayedAndSkipped)
                            holesCompleted++;
                    }
                }
                activeUI.relativeScore.text = $"<size=200>{holesCompleted}/{openPutt.courses.Length}</size>";
                activeUI.totalScore.text = $"Holes\nCompleted";

                // var rankText = "-";
                // if (Utilities.IsValid(localPlayerManager) && localPlayerManager.ScoreboardPositionByScore >= 0)
                //     rankText = GetPlaceName(localPlayerManager.ScoreboardPositionByScore + 1);
                // activeUI.relativeScore.text = rankText;
            }
        }

        private string GetPlaceName(int position)
        {
            if (position <= 0) return "-";
            var rem100 = position % 100;
            if (rem100 >= 11 && rem100 <= 13)
                return position + "th";

            switch (position % 10)
            {
                case 1:
                    return position + "st";
                case 2:
                    return position + "nd";
                case 3:
                    return position + "rd";
                default:
                    return position + "th";
            }
        }

        public override void OnPlayerHandednessChanged(VRCPlayerApi player, VRC_Pickup.PickupHand newHand)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;

            if (Utilities.IsValid(activeUI))
            {
                activeUI.leftShoulderText.text = newHand == VRC_Pickup.PickupHand.Left ? "Grab Club" : "Grab Ball";
                activeUI.rightShoulderText.text = newHand == VRC_Pickup.PickupHand.Right ? "Grab Club" : "Grab Ball";
            }
        }

        /// <summary>
        /// Called when a player hits their ball
        /// </summary>
        /// <param name="player">The player who hit the ball</param>
        /// <param name="speed">The velocity magnitude that was just applied to the ball</param>
        public override void OnPlayerBallHit(VRCPlayerApi player, float speed)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;

            if (Utilities.IsValid(localPlayerManager) && Utilities.IsValid(localPlayerManager.golfClub))
            {
                isBallMoving = true;

                if (Utilities.IsValid(activeUI))
                    activeUI.currentClub.text = localPlayerManager.golfClub.ClubType.GetName();

                UpdateDisplay();

                // Start monitoring ball distance for driving range
                if (!isMonitoringBallDistance && Utilities.IsValid(localPlayerManager) && Utilities.IsValid(localPlayerManager.CurrentCourse) && localPlayerManager.CurrentCourse.courseType == CourseType.DrivingRangeDistance)
                {
                    isMonitoringBallDistance = true;
                    MonitorBallDistance();
                }
            }
        }

        /// <summary>
        /// Called when a player's ball has stopped moving
        /// </summary>
        /// <param name="player">The player whose ball stopped</param>
        public override void OnPlayerBallStopped(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;

            // Stop monitoring ball distance when ball stops
            isBallMoving = false;
            isMonitoringBallDistance = false;
        }

        /// <summary>
        /// Monitors the ball distance and updates the UI for driving range mode
        /// </summary>
        public void MonitorBallDistance()
        {
            if (!isMonitoringBallDistance || !Utilities.IsValid(localPlayerManager) || !Utilities.IsValid(localPlayerManager.golfBall))
            {
                return;
            }

            // Check if ball is still moving
            if (isBallMoving)
            {
                // Update the score display with current distance
                UpdateDisplay();

                // Continue monitoring
                SendCustomEventDelayedSeconds(nameof(MonitorBallDistance), 0.1f);
            }
            else
            {
                // Ball has stopped, stop monitoring
                isMonitoringBallDistance = false;
            }
        }

        /// <summary>
        /// Called when a player ball drops into a course hole
        /// </summary>
        /// <param name="player">The player who finished the course</param>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        /// <param name="score">The absolute score that the player got on the course</param>
        /// <param name="scoreRelativeToPar">The score relative to the par on this course</param>
        public override void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar, int totalHits)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;

            UpdateDisplay();
        }

        /// <summary>
        /// Called when a player resets their score
        /// </summary>
        /// <param name="player">The player who reset their score</param>
        public override void OnPlayerScoreReset(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;

            UpdateDisplay();
        }

        /// <summary>
        /// Called when a player's club type changes
        /// </summary>
        /// <param name="player">The player whose club type changed</param>
        /// <param name="newClubType">The new club type</param>
        public override void OnPlayerClubTypeChanged(VRCPlayerApi player, GolfClubType newClubType)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;

            // Update the UI to reflect the new club type
            if (Utilities.IsValid(activeUI))
                activeUI.UpdateClubDisplay(newClubType);
        }

        // Input Handler Events

        /// <summary>
        /// Called when the fetch ball button is pressed
        /// </summary>
        public void OnFetchBallPressed()
        {
            if (Utilities.IsValid(inputHandler))
                inputHandler.OnUIFetchBall();
        }

        /// <summary>
        /// Called when the toggle camera button is pressed
        /// </summary>
        public void OnToggleCameraPressed()
        {
            // Only allow toggling if the ball has been picked up at least once
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager) || !Utilities.IsValid(openPutt.LocalPlayerManager.golfBall))
            {
                return;
            }

            var b = openPutt.LocalPlayerManager.IsInLeftHandedMode ? openPutt.rightShoulderPickup : openPutt.leftShoulderPickup;
            if (!Utilities.IsValid(b) || !b.pickedUpAtLeastOnce)
            {
                return;
            }

            // Toggle ball camera
            if (Utilities.IsValid(ballCam))
                ballCam.ToggleCamera();
        }

        /// <summary>
        /// Called when the ball camera is toggled
        /// </summary>
        public void OnBallCameraToggled()
        {
            UpdateButtonStates();
        }

        /// <summary>
        /// Called when the shoot button is pressed down (start charging)
        /// </summary>
        public void OnShootStart()
        {
            if (Utilities.IsValid(inputHandler))
                inputHandler.OnUIShootStart();

            // Start power charging logic
            if (Utilities.IsValid(activeUI) && Utilities.IsValid(activeUI.strokePowerRoot) && Utilities.IsValid(activeUI.strokePowerIndicator))
            {
                activeUI.strokePowerRoot.SetActive(true);
                activeUI.isShooting = true;
            }
        }

        /// <summary>
        /// Called when the shoot button is released (execute shot) - for UI state updates
        /// </summary>
        public void OnShootEnd()
        {
            if (Utilities.IsValid(inputHandler))
                inputHandler.OnUIShootEnd();

            // Handle UI state updates here if needed
            if (Utilities.IsValid(activeUI))
            {
                activeUI.isShooting = false;
                activeUI.SetPowerIndicator(0f); // Reset power indicator
            }
        }

        /// <summary>
        /// Called when cycling to the next club
        /// </summary>
        public void OnCycleClubNext()
        {
            if (Utilities.IsValid(inputHandler))
                inputHandler.CycleClubNext();
        }

        /// <summary>
        /// Called when cycling to the previous club
        /// </summary>
        public void OnCycleClubPrevious()
        {
            if (Utilities.IsValid(inputHandler))
                inputHandler.CycleClubPrevious();
        }

        /// <summary>
        /// Sets the visibility of the active UI
        /// </summary>
        /// <param name="visible">Whether the UI should be visible</param>
        public void SetUIVisible(bool visible)
        {
            if (!Utilities.IsValid(activeUI))
                return;

            if (isInVR && activeUI == vrUI)
            {
                if (activeUI.transform.parent.gameObject.activeSelf != visible)
                    activeUI.transform.parent.gameObject.SetActive(visible);
                return;
            }

            if (activeUI.transform.parent.gameObject.activeSelf != visible)
                activeUI.transform.parent.gameObject.SetActive(visible);
        }
    }
}
