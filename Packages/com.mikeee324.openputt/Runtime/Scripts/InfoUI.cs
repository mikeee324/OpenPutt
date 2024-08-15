
using mikeee324.OpenPutt;
using TMPro;
using UdonSharp;
using UnityEngine;
using Varneon.VUdon.ArrayExtensions;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{

    public enum InfoUIDisplayType : int
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
        public float updateTime = 0;
        [Tooltip("Toggle for swapping between metric and imperial")]
        public bool useMetric = true;
        public string defaultText = "-";

        private TextMeshProUGUI valueTextLabel;
        private PlayerManager localPlayerManager;
        private GolfBallController golfBall;
        private GolfClubCollider golfClubCollider;
        private bool monitoringChanges = false;
        private float topValue = 0;

        void Start()
        {
            if (valueTextLabel == null)
                valueTextLabel = GetComponent<TextMeshProUGUI>();

            if (openPutt == null)
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
            // Check if we need to populate the local player manager etc
            if (localPlayerManager == null)
            {
                localPlayerManager = openPutt.LocalPlayerManager;

                if (localPlayerManager != null)
                {
                    golfBall = localPlayerManager.golfBall;
                    golfClubCollider = localPlayerManager.golfClubHead;
                }
            }

            // If we caouldn't find one yet, just skip doing anything here
            if (localPlayerManager == null)
                return;

            // If this UI is attached to a particular course and the player isn't playing it right now - ignore updates
            if (attachedToCourse != null && localPlayerManager.CurrentCourse != attachedToCourse)
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
                        int distance = Mathf.FloorToInt(Vector3.Distance(golfBall.CurrentPosition, golfBall.respawnPosition));
                        if (distance > topValue)
                        {
                            topValue = distance;
                            if (useMetric)
                                valueTextLabel.text = string.Format("{0:F0}m", distance);
                            else
                                valueTextLabel.text = string.Format("{0:F0}yd", distance * 1.09361f);
                        }
                        break;
                    }
                case InfoUIDisplayType.HitSpeed:
                    {
                        if (useMetric)
                            valueTextLabel.text = string.Format("{0:F1}km/h", golfClubCollider.LastKnownHitVelocity * 3.6f);
                        else
                            valueTextLabel.text = string.Format("{0:F1}mph", golfClubCollider.LastKnownHitVelocity * 2.2369362921f);

                        // This is a one-time update
                        StopUpdate();

                        break;
                    }
                case InfoUIDisplayType.BallSpeed:
                    {
                        if (useMetric)
                            valueTextLabel.text = string.Format("{0:F1}km/h", golfBall.BallCurrentSpeed * 3.6f);
                        else
                            valueTextLabel.text = string.Format("{0:F1}mph", golfBall.BallCurrentSpeed * 2.2369362921f);
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