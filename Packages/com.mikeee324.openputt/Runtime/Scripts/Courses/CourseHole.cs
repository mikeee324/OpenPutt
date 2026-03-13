using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CourseHole : UdonSharpBehaviour
    {
        public CourseManager courseManager;

        /// <summary>
        /// The number that will be added onto the players score when the players ball enters this hole
        /// </summary>
        [Tooltip("The number that will be added onto the players score when the players ball enters this hole")]
        public int holeScoreAddition;

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
            if (Utilities.IsValid(courseManager))
            {
                courseManager.OnBallEnterHole(this, other);
            }
        }

        [NetworkCallable(10)]
        public void OnBallEnteredHole(int actualScore)
        {
            if (!Utilities.IsValid(courseManager) || !Utilities.IsValid(courseManager.openPutt))
                return;
                
            var player = NetworkCalling.CallingPlayer;
            if (Utilities.IsValid(player))
            {
                int score = actualScore;
                int scoreRelative = actualScore - courseManager.parScore;

                if (Utilities.IsValid(courseManager.openPutt.eventHandler))
                    courseManager.openPutt.eventHandler.OnPlayerFinishCourse(player, courseManager, this, score, scoreRelative);

                if (courseManager.openPutt.debugMode)
                    OpenPuttUtils.Log(this, $"Course{courseManager.holeNumber} - Player {player.displayName} finished with score {score} ({(scoreRelative >= 0 ? "+" : "")}{scoreRelative})");
            }
        }
    }
}