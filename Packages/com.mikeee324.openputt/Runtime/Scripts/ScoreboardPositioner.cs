
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
namespace mikeee324.OpenPutt
{
    public enum ScoreboardVisibility
    {
        AlwaysVisible,
        NearbyAndCourseFinished,
        NearbyOnly,
        Hidden,
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ScoreboardPositioner : UdonSharpBehaviour
    {
        [Header("References")]
        public ScoreboardManager manager = null;
        public Canvas backgroundCanvas = null;

        [Space, Header("Settings")]
        public ScoreboardVisibility scoreboardVisiblility = ScoreboardVisibility.AlwaysVisible;
        [Tooltip("How close the player needs to be to this scoreboard if one of the 'nearby' settings are used above")]
        public float nearbyMaxRadius = 10f;
        [Tooltip("Defines which course this scoreboard is attached to. Used to toggle visibility when the player finishes a course")]
        public int attachedToCourse = -1;
        public bool CanvasWasEnabledAtStart { get; private set; }

        void Start()
        {
            if (backgroundCanvas != null)
                CanvasWasEnabledAtStart = backgroundCanvas.enabled;
        }

        public bool ShouldBeVisible(Vector3 playerPosition)
        {
            bool isNowActive = false;

            float scoreboardDistance = Vector3.Distance(playerPosition, this.transform.position);
            bool playerIsNearby = scoreboardDistance < this.nearbyMaxRadius;

            switch (this.scoreboardVisiblility)
            {
                case ScoreboardVisibility.AlwaysVisible:
                    isNowActive = true;
                    break;
                case ScoreboardVisibility.NearbyOnly:
                    isNowActive = playerIsNearby;
                    break;
                case ScoreboardVisibility.NearbyAndCourseFinished:
                    isNowActive = playerIsNearby;

                    if (isNowActive && this.attachedToCourse >= 0)
                    {
                        PlayerManager playerManager = manager != null && manager.openPutt != null ? manager.openPutt.LocalPlayerManager : null;

                        if (playerManager == null || !playerManager.IsReady || playerManager.courseStates[this.attachedToCourse] != CourseState.Completed)
                        {
                            isNowActive = false;
                        }
                    }
                    break;
                case ScoreboardVisibility.Hidden:
                    isNowActive = false;
                    break;
            }

            return isNowActive;
        }
    }
}