
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

        [HideInInspector] public bool localPlayerBallEnteredEvent = false;
        [HideInInspector] public bool localPlayerHoleInOneEvent = false;

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
                if (localPlayerHoleInOneEvent)
                {
                    localPlayerHoleInOneEvent = false;
                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnLocalPlayerBallEnterHole(courseManager, this);
                    Utils.Log(this, $"Course{courseManager.holeNumber} - Local Player finished course");
                }
                else
                {
                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnRemotePlayerBallEnterHole(courseManager, this);
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
                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnLocalPlayerHoleInOne(courseManager, this);
                    Utils.Log(this, $"Course{courseManager.holeNumber} - Local Player Hole In One!");
                }
                else
                {
                    foreach (OpenPuttEventListener eventListener in courseManager.openPutt.eventListeners)
                        eventListener.OnRemotePlayerHoleInOne(courseManager, this);
                    Utils.Log(this, $"Course{courseManager.holeNumber} - Local Player Hole In One!");
                }
            }
        }
    }
}