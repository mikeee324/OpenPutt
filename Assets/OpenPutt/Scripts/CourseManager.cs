
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mikeee324.OpenPutt
{
    /// <summary>
    /// Used to track exactly what state each course is in for a player
    /// </summary>
    public enum CourseState
    {
        /// <summary>
        /// Player has not started this course yet (They can start it if they want to)
        /// </summary>
        NotStarted = 0,
        /// <summary>
        /// The course that the player is currently playing
        /// </summary>
        Playing = 1,
        /// <summary>
        /// This course has been completed by the player and can only be restarted if the global "Replayable courses" option is enabled
        /// </summary>
        Completed = 2,
        /// <summary>
        /// The player skipped this course and did not try to play it, they can restart it if they want to later.<br/>
        /// The player will be assigned maxScore for the course until they restart it
        /// </summary>
        Skipped = 3,
        /// <summary>
        /// The player skipped this course.. they started it but did not finish it before moving to the next course.<br/>
        /// The player will be assigned maxScore for the course until they restart it
        /// </summary>
        PlayedAndSkipped = 4
    }
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CourseManager : UdonSharpBehaviour
    {
        [HideInInspector]
        public int holeNumber = 0;
        [Tooltip("The part score for this hole")]
        public int parScore = 0;
        [Tooltip("This will stop the player after this many hits, also used as the default score if a player skips this hole")]
        public int maxScore = 12;

        [HideInInspector]
        public OpenPutt openPutt;
        public GameObject startPad;
        public GameObject[] ballSpawns;
        public GameObject[] holes;
        [Tooltip("A reference to all floor meshes for this course - used to detect if the ball is on the correct hole")]
        public GameObject[] floorObjects;

        public void OnBallEnterHole(CourseHole hole, Collider collider)
        {
            // If this is the local players ball - tell their player manager the hole is now completed (Locks in the score etc)
            GolfBallController golfBall = collider.gameObject.GetComponent<GolfBallController>();
            if (golfBall != null && Networking.LocalPlayer.IsOwner(golfBall.gameObject))
            {
                if (golfBall.BallIsMoving && !golfBall.pickedUpByPlayer)
                    golfBall.playerManager.OnCourseFinished(this, hole, CourseState.Completed);
            }
        }
    }
}