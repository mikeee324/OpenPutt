
using UdonSharp;
using UnityEngine;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CourseHole : UdonSharpBehaviour
    {
        public CourseManager courseManager;
        /// <summary>
        /// The number that will be added onto the players score when the players ball enters this hole
        /// </summary>
        [Tooltip("The number that will be added onto the players score when the players ball enters this hole")]
        public int holeScoreAddition = 0;

        /// <summary>
        /// A quick workaround for being unable to send network event to 'Others'.<br/>
        /// If the local player and a remote player putt a ball at the same time we set this to true for the local player so we play 2 sounds, one on a local audio source and another on a remot eaudio source (with less range)
        /// </summary>
        [HideInInspector]
        public bool localPlayerBallEnteredEvent = false;
        /// <summary>
        /// A quick workaround for being unable to send network event to 'Others'.<br/>
        /// If the local player and a remote player putt a ball at the same time we set this to true for the local player so we play 2 sounds, one on a local audio source and another on a remot eaudio source (with less range)
        /// </summary>
        [HideInInspector]
        public bool localPlayerHoleInOneEvent = false;

        private void OnTriggerEnter(Collider other)
        {
            if (courseManager != null)
            {
                courseManager.OnBallEnterHole(this, other);
            }
        }

        public void OnBallEntered()
        {
            if (courseManager != null && courseManager.openPutt != null)
            {
                if (localPlayerBallEnteredEvent)
                {
                    int localPlayerScore = courseManager.openPutt.LocalPlayerManager.courseScores[courseManager.holeNumber];
                    int localPlayerScoreRelative = localPlayerScore - courseManager.parScore;

                    localPlayerBallEnteredEvent = false;
                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnLocalPlayerFinishCourse(courseManager, this, localPlayerScore, localPlayerScoreRelative);
                    if (courseManager.openPutt.debugMode)
                        Utils.Log(this, $"Course{courseManager.holeNumber} - Local Player finished course");
                }
                else
                {
                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnRemotePlayerFinishCourse(courseManager, this, -1, -1);
                    if (courseManager.openPutt.debugMode)
                        Utils.Log(this, $"Course{courseManager.holeNumber} - Remote Player finished course");
                }
            }
        }

        public void OnHoleInOne()
        {
            if (courseManager != null && courseManager.openPutt != null && courseManager.openPutt.SFXController != null)
            {
                if (localPlayerHoleInOneEvent)
                {
                    localPlayerHoleInOneEvent = false;

                    int localPlayerScore = courseManager.openPutt.LocalPlayerManager.courseScores[courseManager.holeNumber];
                    int localPlayerScoreRelative = localPlayerScore - courseManager.parScore;

                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnLocalPlayerFinishCourse(courseManager, this, localPlayerScore, localPlayerScoreRelative);
                    if (courseManager.openPutt.debugMode)
                        Utils.Log(this, $"Course{courseManager.holeNumber} - Local Player Hole In One!");
                }
                else
                {
                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnRemotePlayerFinishCourse(courseManager, this, 1, 1 - courseManager.parScore);
                    if (courseManager.openPutt.debugMode)
                        Utils.Log(this, $"Course{courseManager.holeNumber} - Local Player Hole In One!");
                }
            }
        }
    }
}