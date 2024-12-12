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
        /// Displays current hit distance from start point
        /// </summary>
        HitDistance,

        /// <summary>
        /// Displays the longest distance recorded so far
        /// </summary>
        HitLongestDistance,

        /// <summary>
        /// Displays last known golf club hit speed
        /// </summary>
        HitSpeed,

        /// <summary>
        /// Displays current ball speed
        /// </summary>
        BallSpeed
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InfoUI : OpenPuttEventListener
    {
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

        [Tooltip("What sort of information this UI element should display")]
        public InfoUIDisplayType displayType;

        [Tooltip("Time in seconds between UI updates (0=Every Frame)")]
        public float updateTime;

        [Tooltip("Toggle for swapping between metric and imperial")]
        public bool useMetric = true;

        public string defaultText = "-";

        private TextMeshProUGUI valueTextLabel;
        private PlayerManager playerManager;
        private GolfBallController golfBall;
        private GolfClubCollider golfClubCollider;
        private bool monitoringChanges;
        private float topValue;

        void Start()
        {
            if (!Utilities.IsValid(valueTextLabel))
                valueTextLabel = GetComponent<TextMeshProUGUI>();

            if (!Utilities.IsValid(openPutt))
                return;

            // If this object isn't already registered as an event listener, register it here automatically
            if (!openPutt.eventListeners.Contains(this))
                openPutt.eventListeners = openPutt.eventListeners.Add(this);

            valueTextLabel.text = defaultText;
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
                case InfoUIDisplayType.HitDistance:
                case InfoUIDisplayType.HitLongestDistance:
                {
                    var distance = Mathf.FloorToInt(Vector3.Distance(golfBall.CurrentPosition, golfBall.respawnPosition));
                    if (distance > topValue)
                    {
                        topValue = distance;
                        if (useMetric)
                            valueTextLabel.text = $"{distance:F0}m";
                        else
                            valueTextLabel.text = $"{distance * 1.09361f:F0}yd";
                    }

                    break;
                }
                case InfoUIDisplayType.HitSpeed:
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
            }
        }

        public override void OnLocalPlayerBallHit(float speed)
        {
            switch (displayType)
            {
                case InfoUIDisplayType.HitDistance:
                {
                    topValue = 0;
                    break;
                }
                case InfoUIDisplayType.HitLongestDistance:
                {
                    break;
                }
                case InfoUIDisplayType.HitSpeed:
                {
                    break;
                }
                case InfoUIDisplayType.BallSpeed:
                {
                    break;
                }
            }

            StartUpdate();
        }
        
        public override void OnLocalPlayerInitialised(PlayerManager localPlayerManager)
        {
            playerManager = localPlayerManager;

            if (!Utilities.IsValid(playerManager)) return;
            
            golfBall = playerManager.golfBall;
            golfClubCollider = playerManager.golfClubHead;
        }

        public override void OnLocalPlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
        }

        public override void OnRemotePlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
        }

        public override void OnLocalPlayerBallStopped()
        {
            switch (displayType)
            {
                case InfoUIDisplayType.HitDistance:
                case InfoUIDisplayType.HitLongestDistance:
                {
                    StopUpdate();
                    break;
                }
                case InfoUIDisplayType.HitSpeed:
                {
                    break;
                }
                case InfoUIDisplayType.BallSpeed:
                {
                    StopUpdate();
                    break;
                }
            }
        }
    }
}