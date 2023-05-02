
using UdonSharp;
using UnityEngine;

namespace mikeee324.OpenPutt
{
    // This needs to run after the ball has moved so the line draws in the correct place
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(10)]
    public class GolfBallStartLineController : UdonSharpBehaviour
    {
        public GolfBallController golfBall;
        public LineRenderer lineRenderer;
        [SerializeField]
        public LayerMask courseStartPosLayerMask;

        private CourseStartPosition closestBallStart = null;
        private CourseManager courseThatIsBeingStarted = null;
        private Collider[] localStartPadColliders = new Collider[32];
        private Vector3 lerpToSpawnStartPos = Vector3.zero;
        private float lerpToSpawnTime = -1f;
        private AnimationCurve simpleEaseInOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private bool localAreaCheckActive = false;

        void Start()
        {

        }

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                lineRenderer.SetPosition(0, Vector3.zero);
                lineRenderer.SetPosition(1, Vector3.zero);

                closestBallStart = null;
                courseThatIsBeingStarted = null;
            }

            gameObject.SetActive(enabled);

            if (enabled)
            {
                ResetDropAnimation();

                if (!localAreaCheckActive)
                {
                    localAreaCheckActive = true;
                    CheckLocalAreaForStartPositions();
                }
            }
        }

        /// <summary>
        /// Animates the golf ball towards its start position (if there is one within range)
        /// </summary>
        /// <param name="ballWorldPosition">The current position of the ball</param>
        /// <returns></returns>
        public bool StartDropAnimation(Vector3 ballWorldPosition)
        {
            if (!gameObject.activeSelf || closestBallStart == null)
                return false;

            lerpToSpawnStartPos = ballWorldPosition;
            lerpToSpawnTime = 0;

            return true;
        }

        public void ResetDropAnimation()
        {
            lerpToSpawnTime = -1f;
            lerpToSpawnStartPos = Vector3.zero;
            closestBallStart = null;
            courseThatIsBeingStarted = null;
        }

        /// <summary>
        /// Checks every so often to find the closest ball start position.<br/>
        /// We don't need to check this every frame.
        /// </summary>
        public void CheckLocalAreaForStartPositions()
        {
            if (gameObject.activeSelf && golfBall.pickedUpByPlayer)
            {
                CourseStartPosition newSpawnPos = null;
                CourseManager newCourse = null;

                float closestDistance = -1f;

                Vector3 golfBallPos = golfBall.transform.position;

                int hitColliders = Physics.OverlapSphereNonAlloc(golfBallPos, 2f, localStartPadColliders, courseStartPosLayerMask, QueryTriggerInteraction.Collide);

                // Find the closest start point
                if (hitColliders > 0)
                {
                    for (int i = 0; i < hitColliders; i++)
                    {
                        Collider hitCollider = localStartPadColliders[i];
                        if (hitCollider != null)
                        {
                            CourseStartPosition courseStart = hitCollider.GetComponent<CourseStartPosition>();

                            if (courseStart == null)
                                continue;

                            float thisDistance = Vector3.Distance(golfBallPos, courseStart.transform.position);
                            if (newSpawnPos == null || thisDistance < closestDistance)
                            {
                                newSpawnPos = courseStart;
                                newCourse = courseStart.courseManager;
                                closestDistance = thisDistance;
                            }
                        }
                    }
                }

                closestBallStart = newSpawnPos;
                courseThatIsBeingStarted = newCourse;

                SendCustomEventDelayedSeconds(nameof(CheckLocalAreaForStartPositions), .25f);
            }
            else
            {
                localAreaCheckActive = false;
            }
        }

        public override void PostLateUpdate()
        {
            if (lerpToSpawnTime >= 0f)
            {
                if (closestBallStart == null || golfBall.pickedUpByPlayer)
                {
                    return;
                }

                float lerpMaxTime = 0.5f;
                float lerpProgress = Mathf.Clamp(lerpToSpawnTime / lerpMaxTime, 0, 1);

                if (lerpProgress < 1f)
                {
                    golfBall.transform.position = Vector3.Lerp(lerpToSpawnStartPos, closestBallStart.transform.position, simpleEaseInOut.Evaluate(lerpProgress));
                    lerpToSpawnTime += Time.deltaTime;
                }
                else
                {
                    golfBall.OnBallDroppedOnPad(courseThatIsBeingStarted, closestBallStart.transform.position);

                    ResetDropAnimation();
                }
            } 
            else if (!golfBall.pickedUpByPlayer)
            {
                SetEnabled(false);
            }

            if (closestBallStart == null)
            {
                // Can't see a ball spawn nearby - hide line renderer
                lineRenderer.SetPosition(0, Vector3.zero);
                lineRenderer.SetPosition(1, Vector3.zero);
            }
            else
            {
                // We have a ball spawn nearby - draw a line to it
                lineRenderer.SetPosition(0, golfBall.transform.position);
                lineRenderer.SetPosition(1, closestBallStart.transform.position);
            }
        }
    }
}