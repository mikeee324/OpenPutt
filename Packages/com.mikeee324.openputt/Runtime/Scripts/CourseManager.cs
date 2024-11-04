using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
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

    /// <summary>
    /// Helper functions for working with the CourseState enum
    /// </summary>
    static class CourseStateMethods
    {
        public static string GetString(this CourseState state)
        {
            switch (state)
            {
                case CourseState.NotStarted:
                    return "Not Playing";
                case CourseState.Playing:
                    return "Playing";
                case CourseState.PlayedAndSkipped:
                    return "Played And Skipped";
                case CourseState.Completed:
                    return "Completed";
                case CourseState.Skipped:
                    return "Skipped";
                default:
                    return "Unknown";
            }
        }
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CourseManager : UdonSharpBehaviour
    {
        [HideInInspector] public int holeNumber = 0;

        [Header("Course Settings"), Tooltip("The par score for this hole")]
        public int parScore = 0;

        [Tooltip("This will stop the player after this many hits, also used as the default score if a player skips this hole")]
        public int maxScore = 12;

        [Tooltip("The par time in seconds for this hole (Default is 5 mins)")]
        public int parTime = 120;

        [Tooltip("The maximum amount of seconds a player can have on this hole (Default is 5 mins)")]
        public int maxTime = 300;

        [Tooltip("The players score on this hole will be how far they hit the ball in meters from the start pad")]
        public bool drivingRangeMode = false;

        [Tooltip("Overrides the global replayable courses setting")]
        public bool courseIsAlwaysReplayable = false;

        [Tooltip("Override the hole number column text on scoreboards (Can be used to give holes names or something I dunno..)")]
        public string scoreboardShortName = "";

        [Tooltip("This name will be displayed on course markers (If you have them attached to the courses)")]
        public string scoreboardLongName = "";

        [HideInInspector] public OpenPutt openPutt;
        [Header("Object References")] public CourseStartPosition[] ballSpawns;
        public GameObject[] holes;

        [Tooltip("A reference to all floor meshes for this course - used to detect if the ball is on the correct hole")]
        public GameObject[] floorObjects;

        [SerializeField, Header("Gizmo Settings"), Tooltip("Toggles display of the gizmos on courses between always/on selection")]
        private bool alwaysDisplayGizmos = true;

        [SerializeField, Tooltip("Draw gizmos for ball spawn positions")]
        private bool drawBallSpawns = true;

        [SerializeField, Tooltip("Draw gizmos for holes")]
        private bool drawHoles = true;

        [SerializeField, Tooltip("Draw a wireframe over meshes that are counted as a floor for this course")]
        private bool drawFloorMeshes = true;

        [SerializeField, Tooltip("Set this to be the same size as your ball sphere colliders to draw the gizmos at the right size")]
        private float ballSpawnGizmoRadius = 0.0225f;

        private void Start()
        {
            if (!Utilities.IsValid(ballSpawns))
                ballSpawns = new CourseStartPosition[0];
        }

        public void OnBallEnterHole(CourseHole hole, Collider collider)
        {
            // If this is the local players ball - tell their player manager the hole is now completed (Locks in the score etc)
            var golfBall = collider.gameObject.GetComponent<GolfBallController>();
            if (Utilities.IsValid(golfBall) && Networking.LocalPlayer.IsOwner(golfBall.gameObject))
            {
                if (golfBall.BallIsMoving && !golfBall.pickedUpByPlayer)
                {
                    golfBall.playerManager.OnCourseFinished(this, hole, CourseState.Completed);
                }
            }
        }


        private void OnDrawGizmosSelected()
        {
            if (!alwaysDisplayGizmos)
                DrawGizmos();
        }

        private void OnDrawGizmos()
        {
            if (alwaysDisplayGizmos)
                DrawGizmos();
        }

        private void DrawGizmos()
        {
            if (drawBallSpawns)
            {
                Gizmos.color = Color.green;
                foreach (var ballSpawn in ballSpawns)
                {
                    if (!Utilities.IsValid(ballSpawn)) continue;

                    Gizmos.DrawWireSphere(ballSpawn.transform.position, ballSpawnGizmoRadius);
                }
            }

            if (drawHoles)
            {
                Gizmos.color = Color.red;
                foreach (var hole in holes)
                {
                    if (!Utilities.IsValid(hole)) continue;

                    if (hole.GetComponent<BoxCollider>() != null)
                    {
                        var col = hole.GetComponent<BoxCollider>();
                        Gizmos.DrawWireCube(hole.transform.TransformPoint(col.center), col.size);
                    }
                    else if (hole.GetComponent<SphereCollider>() != null)
                    {
                        var col = hole.GetComponent<SphereCollider>();
                        Gizmos.DrawWireSphere(hole.transform.TransformPoint(col.center), col.radius);
                    }
                    else if (hole.GetComponent<CapsuleCollider>() != null)
                    {
                        var col = hole.GetComponent<CapsuleCollider>();
                        Gizmos.DrawWireSphere(hole.transform.TransformPoint(col.center), col.radius);
                    }
                    else if (hole.GetComponent<MeshCollider>() != null)
                    {
                        var col = hole.GetComponent<MeshCollider>();
                        Gizmos.DrawWireMesh(col.sharedMesh, -1, hole.transform.position, hole.transform.rotation, hole.transform.lossyScale);
                    }
                }
            }

            if (drawFloorMeshes)
            {
                foreach (var floor in floorObjects)
                {
                    if (!Utilities.IsValid(floor)) continue;

                    Gizmos.color = Color.blue;
                    if (floor.GetComponent<BoxCollider>() != null)
                    {
                        var col = floor.GetComponent<BoxCollider>();
                        Gizmos.DrawWireCube(floor.transform.TransformPoint(col.center), col.size);
                    }
                    else if (floor.GetComponent<SphereCollider>() != null)
                    {
                        var col = floor.GetComponent<SphereCollider>();
                        Gizmos.DrawWireSphere(floor.transform.TransformPoint(col.center), col.radius);
                    }
                    else if (floor.GetComponent<CapsuleCollider>() != null)
                    {
                        var col = floor.GetComponent<CapsuleCollider>();
                        Gizmos.DrawWireSphere(floor.transform.TransformPoint(col.center), col.radius);
                    }
                    else if (floor.GetComponent<MeshCollider>() != null)
                    {
                        var col = floor.GetComponent<MeshCollider>();
                        Gizmos.DrawWireMesh(col.sharedMesh, -1, floor.transform.position, floor.transform.rotation, floor.transform.lossyScale);
                    }
                }
            }
        }
    }
}