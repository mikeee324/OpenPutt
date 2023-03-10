
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
                foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                    eventListener.OnBallEnterHole(courseManager, this);
            }
        }

        public void OnHoleInOne()
        {
            if (courseManager != null && courseManager.openPutt != null && courseManager.openPutt.SFXController != null)
            {
                foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                    eventListener.OnHoleInOne(courseManager, this);
            }
        }
    }
}