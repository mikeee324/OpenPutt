using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
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
        public ScoreboardManager manager;

        public Canvas backgroundCanvas;
        public Transform nearbyCenterTransform;

        [Space, Header("Settings")]
        public ScoreboardVisibility scoreboardVisiblility = ScoreboardVisibility.AlwaysVisible;

        [Tooltip("How close the player needs to be to this scoreboard if one of the 'nearby' settings are used above")]
        public float nearbyMaxRadius = 10f;

        [Tooltip("Defines which course this scoreboard is attached to. Used to toggle visibility when the player finishes a course")]
        public int attachedToCourse = -1;

        public bool CanvasWasEnabledAtStart { get; private set; }

        void Start()
        {
            if (Utilities.IsValid(backgroundCanvas))
                CanvasWasEnabledAtStart = backgroundCanvas.enabled;
        }

        public bool ShouldBeVisible(Vector3 playerPosition)
        {
            var isNowActive = false;

            var scoreboardDistance = Vector3.Distance(playerPosition, Utilities.IsValid(nearbyCenterTransform) ? nearbyCenterTransform.position : transform.position);
            var playerIsNearby = scoreboardDistance < nearbyMaxRadius;

            switch (scoreboardVisiblility)
            {
                case ScoreboardVisibility.AlwaysVisible:
                    isNowActive = true;
                    break;
                case ScoreboardVisibility.NearbyOnly:
                    isNowActive = playerIsNearby;
                    break;
                case ScoreboardVisibility.NearbyAndCourseFinished:
                    isNowActive = playerIsNearby;

                    if (isNowActive && attachedToCourse >= 0)
                    {
                        var playerManager = Utilities.IsValid(manager) && Utilities.IsValid(manager.openPutt) ? manager.openPutt.LocalPlayerManager : null;

                        if (!Utilities.IsValid(playerManager) || !playerManager.IsReady || playerManager.courseStates[attachedToCourse] != CourseState.Completed)
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

        private void OnDrawGizmosSelected()
        {
            switch (scoreboardVisiblility)
            {
                case ScoreboardVisibility.AlwaysVisible:
                case ScoreboardVisibility.Hidden:
                    break;
                case ScoreboardVisibility.NearbyOnly:
                case ScoreboardVisibility.NearbyAndCourseFinished:
                    if (Utilities.IsValid(nearbyCenterTransform))
                        Gizmos.DrawWireSphere(nearbyCenterTransform.position, nearbyMaxRadius);
                    else
                        Gizmos.DrawWireSphere(transform.position, nearbyMaxRadius);
                    break;
            }
        }
    }
}