using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class WristUI : OpenPuttEventListener
    {
        #region Public Settings
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffsetEuler= new Vector3(90f, 150f, 7f); 

        public Canvas canvas;

        public OpenPutt openPutt;

        public TextMeshProUGUI currentScoreLabel;
        public TextMeshProUGUI parScoreLabel;
        public TextMeshProUGUI courseNameLabel;
        public TextMeshProUGUI currentClubLabel;

        #endregion

        #region Private Vars

        private PlayerManager playerManager;
        private VRCPlayerApi localPlayer;
        private int totalPar = 0;

        #endregion

        void Start()
        {
            if (Utilities.IsValid(openPutt))
            {
                var foundThisScript = false;
                foreach (var listener in openPutt.eventListeners)
                {
                    if (listener.gameObject != gameObject) continue;
                    foundThisScript = true;
                    break;
                }

                if (!foundThisScript)
                    openPutt.eventListeners = openPutt.eventListeners.Add(this);
            }
        }

        public override void PostLateUpdate()
        {
            if (!Utilities.IsValid(playerManager) || !Utilities.IsValid(localPlayer)) return;
            if (!localPlayer.IsUserInVR())
            {
                gameObject.SetActive(false);
                return;
            }

            var hand = localPlayer.GetTrackingData(playerManager.IsInLeftHandedMode ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
            var newPosition = hand.position;
            var newRotation = hand.rotation * Quaternion.Euler(rotationOffsetEuler);
            if (playerManager.IsInLeftHandedMode)
            {
                newPosition += hand.rotation * new Vector3(positionOffset.x, -positionOffset.y, positionOffset.z);
                canvas.transform.localEulerAngles = new Vector3(0, 180, 0);
            }
            else
            {
                newPosition += hand.rotation * positionOffset;
                canvas.transform.localEulerAngles = new Vector3(0, 0, 0);
            }
            
            transform.SetPositionAndRotation(newPosition, newRotation);

            // Hide canvas when not looking at it
            var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            canvas.enabled = Vector3.Dot(playerManager.IsInLeftHandedMode ? transform.forward : -transform.forward, (head.position - transform.position).normalized) > .4f;
        }

        public void _UpdateUI()
        {
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.CurrentCourse))
            {
                var currentCourse = playerManager.CurrentCourse;
                if (Utilities.IsValid(currentCourse.scoreboardLongName) && currentCourse.scoreboardLongName.Length > 0)
                    courseNameLabel.text = $"{currentCourse.scoreboardLongName} ({currentCourse.holeNumber + 1})";
                else if (Utilities.IsValid(currentCourse.scoreboardShortName) && currentCourse.scoreboardShortName.Length > 0)
                    courseNameLabel.text = $"{currentCourse.scoreboardShortName} ({currentCourse.holeNumber + 1})";
                else if (currentCourse.drivingRangeMode)
                    courseNameLabel.text = $"Driving Range {currentCourse.holeNumber + 1}";
                else
                    courseNameLabel.text = $"Hole {currentCourse.holeNumber + 1}";

                if (currentCourse.drivingRangeMode)
                {
                    float maxDist = 0;
                    if (Utilities.IsValid(playerManager.golfBall))
                    {
                        playerManager.golfBall.GetLastHitData(out maxDist, out var totalDist);
                        maxDist = Mathf.FloorToInt(maxDist);
                    }

                    currentScoreLabel.text = $"{maxDist:F0}m";
                    parScoreLabel.text = $"Par {currentCourse.parScore}m";
                }
                else
                {
                    parScoreLabel.text = $"Par {currentCourse.parScore}";
                    currentScoreLabel.text = $"{playerManager.courseScores[currentCourse.holeNumber]}";
                }

                currentClubLabel.text = playerManager.golfClub.ClubType.GetName();
            }
            else if (Utilities.IsValid(playerManager))
            {
                totalPar = 0;
                foreach (var course in openPutt.courses)
                {
                    if (playerManager.courseStates[course.holeNumber] == CourseState.NotStarted)
                        break;
                    if (!course.drivingRangeMode)
                        totalPar += course.parScore;
                }

                var scoreTrack = (playerManager.PlayerTotalScore == 999999 ? 0 : playerManager.PlayerTotalScore) - totalPar;

                currentScoreLabel.text = $"{(scoreTrack >= 0 ? "+" : "-")}{Math.Abs(scoreTrack)}";
                parScoreLabel.text = "To Par";

                courseNameLabel.text = "OpenPutt";
            }
            else
            {
                currentScoreLabel.text = $"-";
                parScoreLabel.text = "-";

                courseNameLabel.text = "OpenPutt";
            }

            if (Utilities.IsValid(currentClubLabel) && Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.golfClub))
                currentClubLabel.text = playerManager.golfClub.ClubType.GetName();
        }

        public override void OnLocalPlayerInitialised(PlayerManager localPlayerManager)
        {
            playerManager = localPlayerManager;
            localPlayer = Networking.LocalPlayer;

            _UpdateUI();
        }

        public override void OnLocalPlayerBallHit(float speed)
        {
            _UpdateUI();
        }

        public override void OnLocalPlayerBallStopped()
        {
        }

        public override void OnLocalPlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
            _UpdateUI();
        }

        public override void OnRemotePlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
        }
    }
}