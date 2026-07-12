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

        [Tooltip("How close the player needs to be to click this scoreboard's UI. The board stays visible past this distance but stops accepting clicks (stops players clicking it from across the map). Set to 0 for no limit.")]
        public float interactionMaxRadius = 0f;

        [Tooltip("Defines which course this scoreboard is attached to. Used to toggle visibility when the player finishes a course")]
        public int attachedToCourse = -1;

        public bool CanvasWasEnabledAtStart { get; private set; }

        void Start()
        {
            if (Utilities.IsValid(backgroundCanvas))
                CanvasWasEnabledAtStart = backgroundCanvas.enabled;
        }

        /// <summary>
        /// Whether this scoreboard should be shown to the local player, based on the current view camera
        /// </summary>
        public bool ShouldBeVisible(Vector3 viewPosition, Vector3 viewForward, float viewFieldOfView)
        {
            var isNowActive = false;

            // Distance uses the (optional) nearby center transform, but look direction targets the positioner itself
            var nearbyPos = Utilities.IsValid(nearbyCenterTransform) ? nearbyCenterTransform.position : transform.position;

            var scoreboardDistance = Vector3.Distance(viewPosition, nearbyPos);
            var playerIsNearby = scoreboardDistance < nearbyMaxRadius;

            var normalizedDirectionToScoreboard = (transform.position - viewPosition).normalized;
            var playerIsLookingToward = Vector3.Dot(viewForward, normalizedDirectionToScoreboard) >= ViewDotThreshold(viewFieldOfView);

            // Board only readable from its front face, so ignore players standing behind it
            var boardIsFacingPlayer = Vector3.Dot(transform.forward, -normalizedDirectionToScoreboard) > 0f;
            playerIsLookingToward = playerIsLookingToward && boardIsFacingPlayer;

            switch (scoreboardVisiblility)
            {
                case ScoreboardVisibility.AlwaysVisible:
                    isNowActive = true;
                    break;
                case ScoreboardVisibility.NearbyOnly:
                    isNowActive = playerIsNearby && playerIsLookingToward;
                    break;
                case ScoreboardVisibility.NearbyAndCourseFinished:
                    isNowActive = playerIsNearby && playerIsLookingToward;

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

        /// <summary>
        /// Whether the board here should accept clicks. Always stays visible; this only blocks clicks from far away.
        /// </summary>
        public bool ShouldBeInteractable(Vector3 viewPosition)
        {
            if (interactionMaxRadius <= 0f)
                return true;

            var scoreboardPos = Utilities.IsValid(nearbyCenterTransform) ? nearbyCenterTransform.position : transform.position;

            return Vector3.Distance(viewPosition, scoreboardPos) <= interactionMaxRadius;
        }

        /// <summary>
        /// Min dot product the scoreboard direction must clear to count as on screen. Loosens with wider FOV.
        /// </summary>
        private float ViewDotThreshold(float verticalFieldOfView)
        {
            // Vertical FOV widened to horizontal half-angle, assuming 16:9 (Udon can't read Screen size)
            const float aspect = 16f / 9f;
            var halfVerticalRad = verticalFieldOfView * 0.5f * Mathf.Deg2Rad;
            var halfHorizontalRad = Mathf.Atan(Mathf.Tan(halfVerticalRad) * aspect);
            return Mathf.Cos(halfHorizontalRad);
        }

        private void OnDrawGizmosSelected()
        {
            var center = Utilities.IsValid(nearbyCenterTransform) ? nearbyCenterTransform.position : transform.position;

            switch (scoreboardVisiblility)
            {
                case ScoreboardVisibility.AlwaysVisible:
                case ScoreboardVisibility.Hidden:
                    break;
                case ScoreboardVisibility.NearbyOnly:
                case ScoreboardVisibility.NearbyAndCourseFinished:
                    Gizmos.DrawWireSphere(center, nearbyMaxRadius);
                    break;
            }

            // Distance past which the board stays visible but stops accepting clicks
            if (interactionMaxRadius > 0f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(center, interactionMaxRadius);
            }
        }
    }
}