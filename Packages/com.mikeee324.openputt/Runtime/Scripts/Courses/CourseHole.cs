using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CourseHole : UdonSharpBehaviour
    {
        [OpenPuttDescription("Marks the hole a ball needs to enter to finish this course. Put a trigger collider on this object where the ball should fall in to score.")]
        public CourseManager courseManager;

        /// <summary>
        /// The number that will be added onto the players score when the players ball enters this hole
        /// </summary>
        [Tooltip("The number that will be added onto the players score when the players ball enters this hole")]
        public int holeScoreAddition;

        private void OnTriggerEnter(Collider other)
        {
            // Just pass the event on to the course manager - it will figure out what to do
            // If you want to add our own logic use an OpenPuttEventListener and listen for the OnPlayerFinishCourse event instead of modifying this directly
            if (Utilities.IsValid(courseManager))
                courseManager._OnLocalPlayerBallEnterHole(this, other);
        }

        [NetworkCallable(10)]
        public void OnBallEnteredHole(int totalHits, int actualScore)
        {
            if (!Utilities.IsValid(courseManager) || !Utilities.IsValid(courseManager.openPutt))
                return;

            var player = NetworkCalling.CallingPlayer;
            if (!Utilities.IsValid(player))
                return;

            int score = actualScore;
            int scoreRelative = actualScore - courseManager.parScore;

            if (Utilities.IsValid(courseManager.openPutt.eventHandler))
                courseManager.openPutt.eventHandler.OnPlayerFinishCourse(player, courseManager, this, score, scoreRelative, totalHits);

            if (courseManager.openPutt.debugMode)
                OpenPuttUtils.Log(this, $"Course{courseManager.holeNumber} - Player {player.displayName} finished with score {score} ({(scoreRelative >= 0 ? "+" : "")}{scoreRelative})");
        }
    }
}