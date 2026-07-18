using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public enum InfoUIDisplayType
    {
        /// <summary>
        /// Displays the cumulative distance travelled by the ball since it was last hit by the player
        /// </summary>
        LastHitTotalDistanceTravelled,

        /// <summary>
        /// Displays the largest straight line distance that the ball reached after being hit by the player
        /// </summary>
        LastHitMaxDistance,

        /// <summary>
        /// Displays last known golf club hit speed
        /// </summary>
        LastHitSpeed,

        /// <summary>
        /// Displays current ball speed
        /// </summary>
        BallSpeed,

        /// <summary>
        /// Displays the player's current score on the attached course
        /// </summary>
        CurrentScore,

        /// <summary>
        /// Displays the par score of the attached course
        /// </summary>
        Par
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InfoUI : OpenPuttEventListener
    {
        [OpenPuttDescription("Displays a piece of live information as text, such as the ball's speed or the player's current score, updating automatically while they play.")]
        /// <summary>
        /// A referenc eto the OpenPutt instance in this world
        /// </summary>
        [Tooltip("A reference to OpenPutt. This should get filled in automatically when building, but to be safe always set it!")]
        public OpenPutt openPutt;

        /// <summary>
        /// Filter out events that are happening based on which course the player is currently playing on
        /// </summary>
        [Tooltip("Can be used to filter UI updates based on which course the player is currently playing on")]
        public CourseManager attachedToCourse;

        [OpenPuttFoldoutGroup("Display Settings")]
        [Tooltip("What sort of information this UI element should display")]
        public InfoUIDisplayType displayType;

        [OpenPuttFoldoutGroup("Display Settings")]
        [Tooltip("Time in seconds between UI updates (0=Every Frame)")]
        public float updateTime;

        [OpenPuttFoldoutGroup("Display Settings")]
        [Tooltip("Toggle for swapping between metric and imperial")]
        public bool useMetric = true;

        [OpenPuttFoldoutGroup("Display Settings")]
        public string defaultText = "-";

        private TextMeshProUGUI valueTextLabel;
        private PlayerManager playerManager;
        private GolfBallController golfBall;
        private GolfClubCollider golfClubCollider;
        private bool monitoringChanges;

        void Start()
        {
            if (!Utilities.IsValid(valueTextLabel))
                valueTextLabel = GetComponent<TextMeshProUGUI>();

            if (!Utilities.IsValid(openPutt))
                return;

            // If this object isn't already registered as an event listener, register it here automatically
            openPutt._RegisterEventListener(this);

            valueTextLabel.text = defaultText;

            // Par never changes at runtime so it just needs setting once (and doesn't require a PlayerManager)
            if (displayType == InfoUIDisplayType.Par && Utilities.IsValid(attachedToCourse))
                valueTextLabel.text = $"{attachedToCourse.parScore}";
        }

        public void StartUpdate()
        {
            if (monitoringChanges)
                return;

            monitoringChanges = true;

            SendCustomEventDelayedSeconds(nameof(RunUpdate), updateTime);
        }

        public void StopUpdate()
        {
            monitoringChanges = false;
        }

        public void RunUpdate()
        {
            UpdateUI();

            if (!monitoringChanges)
                return;

            SendCustomEventDelayedSeconds(nameof(RunUpdate), updateTime);
        }

        private void UpdateUI()
        {
            if (!Utilities.IsValid(playerManager))
                return;

            // If this UI is attached to a particular course and the player isn't playing it right now - ignore updates
            if (Utilities.IsValid(attachedToCourse) && playerManager.CurrentCourse != attachedToCourse)
            {
                StopUpdate();
                return;
            }

            // Work out what to display and then display it
            switch (displayType)
            {
                case InfoUIDisplayType.LastHitTotalDistanceTravelled:
                {
                    golfBall._GetLastHitData(out var maxDistance, out var totalDistanceTravelled);
                    if (useMetric)
                        valueTextLabel.text = $"{totalDistanceTravelled:F0}m";
                    else
                        valueTextLabel.text = $"{totalDistanceTravelled * 1.09361f:F0}yd";
                    break;
                }
                case InfoUIDisplayType.LastHitMaxDistance:
                {
                    golfBall._GetLastHitData(out var maxDistance, out var totalDistanceTravelled);
                    maxDistance = Mathf.FloorToInt(maxDistance);
                    if (useMetric)
                        valueTextLabel.text = $"{maxDistance:F0}m";
                    else
                        valueTextLabel.text = $"{maxDistance * 1.09361f:F0}yd";
                    break;
                }
                case InfoUIDisplayType.LastHitSpeed:
                {
                    if (useMetric)
                        valueTextLabel.text = $"{golfClubCollider.LastKnownHitVelocity * 3.6f:F1}km/h";
                    else
                        valueTextLabel.text = $"{golfClubCollider.LastKnownHitVelocity * 2.2369362921f:F1}mph";

                    // This is a one-time update
                    StopUpdate();

                    break;
                }
                case InfoUIDisplayType.BallSpeed:
                {
                    if (useMetric)
                        valueTextLabel.text = $"{golfBall.BallCurrentSpeed * 3.6f:F1}km/h";
                    else
                        valueTextLabel.text = $"{golfBall.BallCurrentSpeed * 2.2369362921f:F1}mph";
                    break;
                }
                case InfoUIDisplayType.CurrentScore:
                {
                    var course = Utilities.IsValid(attachedToCourse) ? attachedToCourse : playerManager.CurrentCourse;
                    if (Utilities.IsValid(course) && course.holeNumber < playerManager.courseScores.Length)
                        valueTextLabel.text = $"{playerManager.courseScores[course.holeNumber]}";

                    // This is a one-time update, it gets re-triggered whenever the score changes
                    StopUpdate();

                    break;
                }
            }
        }

        public override void OnPlayerBallHit(VRCPlayerApi player, float speed)
        {
            StartUpdate();
        }

        public override void OnPlayerScoreReset(VRCPlayerApi player)
        {
            if (displayType == InfoUIDisplayType.CurrentScore)
                StartUpdate();
        }

        public override void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar, int totalHits)
        {
            // Fired for all players - only react to our own local update
            if (player.isLocal && displayType == InfoUIDisplayType.CurrentScore)
                StartUpdate();
        }

        public override void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {
            if (!player.isLocal)
                return;

            this.playerManager = playerManager;

            if (!Utilities.IsValid(playerManager)) return;

            golfBall = playerManager.golfBall;
            golfClubCollider = playerManager.golfClubHead;
        }

        public override void OnPlayerBallStopped(VRCPlayerApi player)
        {
            switch (displayType)
            {
                case InfoUIDisplayType.LastHitTotalDistanceTravelled:
                case InfoUIDisplayType.LastHitMaxDistance:
                {
                    StopUpdate();
                    break;
                }
                case InfoUIDisplayType.LastHitSpeed:
                {
                    break;
                }
                case InfoUIDisplayType.BallSpeed:
                {
                    StopUpdate();
                    break;
                }
                case InfoUIDisplayType.CurrentScore:
                {
                    StartUpdate();
                    break;
                }
            }
        }
    }
}