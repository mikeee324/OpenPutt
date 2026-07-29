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
        [OpenPuttDescription("Marks a spot in the world where a scoreboard can be shown. The scoreboard manager decides which of these positions are currently visible to the player and moves a pooled scoreboard there.")]
        [OpenPuttFoldoutGroup("References")]
        public ScoreboardManager manager;

        [OpenPuttFoldoutGroup("References")]
        public Canvas backgroundCanvas;
        [OpenPuttFoldoutGroup("References")]
        public Transform nearbyCenterTransform;

        [Space, OpenPuttFoldoutGroup("Settings")]
        public ScoreboardVisibility scoreboardVisiblility = ScoreboardVisibility.AlwaysVisible;

        [OpenPuttFoldoutGroup("Settings")]
        [Tooltip("How close the player needs to be to this scoreboard if one of the 'nearby' settings are used above")]
        public float nearbyMaxRadius = 10f;

        [OpenPuttFoldoutGroup("Settings")]
        [Tooltip("How close the player needs to be to click this scoreboard's UI. The board stays visible past this distance but stops accepting clicks (stops players clicking it from across the map). Set to 0 for no limit.")]
        public float interactionMaxRadius = 0f;

        [OpenPuttFoldoutGroup("Settings")]
        [Tooltip("Defines which course this scoreboard is attached to. Used to toggle visibility when the player finishes a course")]
        public int attachedToCourse = -1;

        [OpenPuttFoldoutGroup("Settings")]
        [Tooltip("Within this distance the viewing angle check is skipped entirely (the board counts as visible no matter which way the player is facing). The required viewing angle widens smoothly from the normal FOV-based cone down to this radius.")]
        public float closeRangeFullVisibilityRadius = 2f;

        public bool CanvasWasEnabledAtStart { get; private set; }

        private Vector3[] _worldCorners = new Vector3[4];

        void Start()
        {
            if (Utilities.IsValid(backgroundCanvas))
                CanvasWasEnabledAtStart = backgroundCanvas.enabled;
        }

        /// <summary>
        /// Whether this scoreboard should be shown to the local player, based on the current view camera
        /// </summary>
        public bool ShouldBeVisible(Vector3 viewPosition, Vector3 viewForward, float viewFieldOfView, out float distanceToViewer)
        {
            var isNowActive = false;

            // Distance and look direction both target the closest point on the board's physical surface (rather than
            // a single pivot), so large boards behave consistently no matter which part of them the player is near/looking at
            var closestPoint = ClosestPointOnBoard(viewPosition);

            var scoreboardDistance = Vector3.Distance(viewPosition, closestPoint);
            distanceToViewer = scoreboardDistance;
            var playerIsNearby = scoreboardDistance < nearbyMaxRadius;

            var normalizedDirectionToScoreboard = (closestPoint - viewPosition).normalized;

            var proximity = 0f;
            if (nearbyMaxRadius > closeRangeFullVisibilityRadius)
                proximity = Mathf.Clamp01((nearbyMaxRadius - scoreboardDistance) / (nearbyMaxRadius - closeRangeFullVisibilityRadius));
            else if (scoreboardDistance <= closeRangeFullVisibilityRadius)
                proximity = 1f;

            var playerIsLookingToward = Vector3.Dot(viewForward, normalizedDirectionToScoreboard) >= ViewDotThreshold(viewFieldOfView, proximity);

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

            return Vector3.Distance(viewPosition, ClosestPointOnBoard(viewPosition)) <= interactionMaxRadius;
        }

        /// <summary>
        /// Closest point on the board's physical rectangle (from backgroundCanvas's RectTransform) to the given position.
        /// Falls back to the (optional) nearby center transform or the positioner's own transform if no canvas is set.
        /// </summary>
        private Vector3 ClosestPointOnBoard(Vector3 viewPosition)
        {
            RectTransform rectTransform = null;
            if (Utilities.IsValid(backgroundCanvas))
                rectTransform = backgroundCanvas.GetComponent<RectTransform>();

            if (!Utilities.IsValid(rectTransform))
                return Utilities.IsValid(nearbyCenterTransform) ? nearbyCenterTransform.position : transform.position;

            rectTransform.GetWorldCorners(_worldCorners);

            // Corners are ordered bottom-left, top-left, top-right, bottom-right
            var origin = _worldCorners[0];
            var uAxis = _worldCorners[3] - origin;
            var vAxis = _worldCorners[1] - origin;
            var uLen = uAxis.magnitude;
            var vLen = vAxis.magnitude;
            var uDir = uLen > 0f ? uAxis / uLen : transform.right;
            var vDir = vLen > 0f ? vAxis / vLen : transform.up;

            var toPoint = viewPosition - origin;
            var u = Mathf.Clamp(Vector3.Dot(toPoint, uDir), 0f, uLen);
            var v = Mathf.Clamp(Vector3.Dot(toPoint, vDir), 0f, vLen);

            return origin + uDir * u + vDir * v;
        }

        /// <summary>
        /// Min dot product the scoreboard direction must clear to count as on screen. Loosens with wider FOV, and widens all the way
        /// to a full 360 degrees as proximity approaches 1 (player within closeRangeFullVisibilityRadius).
        /// </summary>
        private float ViewDotThreshold(float verticalFieldOfView, float proximity)
        {
            // Vertical FOV widened to horizontal half-angle, assuming 16:9 (Udon can't read Screen size)
            const float aspect = 16f / 9f;
            var halfVerticalRad = verticalFieldOfView * 0.5f * Mathf.Deg2Rad;
            var halfHorizontalRad = Mathf.Atan(Mathf.Tan(halfVerticalRad) * aspect);
            var baseThreshold = Mathf.Cos(halfHorizontalRad);

            return Mathf.Lerp(baseThreshold, -1f, proximity);
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

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(center, closeRangeFullVisibilityRadius);
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