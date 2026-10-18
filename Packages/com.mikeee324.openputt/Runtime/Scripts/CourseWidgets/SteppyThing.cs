using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SteppyThing : UdonSharpBehaviour
    {
        [OpenPuttDescription("A set of moving stairs that continuously cycle up and down, like an escalator, carrying the ball along with them.")]
        [Tooltip("The colliders for each step in SteppyThing")]
        public Collider[] steps;
        [Tooltip("Used for debug purposes to make it easier to see if SteppyThing is working properly")]
        public bool toggleBallHolderRenderers;
        [Tooltip("A list of colliders that hold the ball from rolling off each step")]
        public Collider[] stepBallHolders;
        [FormerlySerializedAs("stepBallHolderNS")] [Tooltip("A list of colliders that reference the collider of the next step or the floor at the end of the staircase. If the ball holder for each step is in contact with the next step the ball holder will be enabled. When they no longer contact each each the holder is disabled so the ball can roll off.")]
        public Collider[] stepBallHolderNextStepCollider;

        [OpenPuttFoldoutGroup("Cycle Settings")]
        [Range(1, 10f), Tooltip("Time in seconds for each full up/down cycle of the steps")]
        public float stepCycleTime = 1f;

        [OpenPuttFoldoutGroup("Cycle Settings")]
        [Range(0, 5f), Tooltip("Time in seconds the steps will sit still for when they reach the up or down position")]
        public float stepStopTime = 0.5f;

        [OpenPuttFoldoutGroup("Cycle Settings")]
        public AnimationCurve stepCycleCurve;

        [OpenPuttFoldoutGroup("Cycle Settings")]
        [Range(0f, 1f), Tooltip("Where in its up/down cycle it is at the start. Give steps next to each other different values so they don't all line up")]
        public float cycleOffset;

        [OpenPuttFoldoutGroup("Cycle Settings")]
        [Tooltip("Keeps every player's steps in the same position. Off = each player's copy starts cycling when it loads")]
        public bool syncToServerTime = true;

        public Collider[] collidersToIgnore;
        public Vector3[] stepRestLocations;

        /// Clock errors bigger than this (in seconds) snap instead of being steered out
        private const double SnapSeconds = 0.25;

        /// Fraction of the clock error steered out per physics step
        private const double ClockCorrectionRate = 0.05;

        private Rigidbody[] stepRBs;
        private double cycleClock;
        private bool clockStarted;
        private float localStartTime;

        void Start()
        {
            localStartTime = Time.time;

            stepRBs = new Rigidbody[steps.Length];
            stepRestLocations = new Vector3[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                stepRBs[i] = steps[i].GetComponent<Rigidbody>();
                if (steps[i].gameObject.activeInHierarchy)
                    stepRestLocations[i] = stepRBs[i].position;
                else
                    stepRestLocations[i] = steps[i].transform.position;

                var collider = steps[i].GetComponent<MeshCollider>();
                collider.isTrigger = false;

                // Make Steps Ignore Collisions with neighbouring steps
                if (i < steps.Length - 2)
                    Physics.IgnoreCollision(steps[i], steps[i + 1], true);

                // Ignore ball holder collisions
                if (i > 0 && stepBallHolders.Length > 0)
                    Physics.IgnoreCollision(steps[i], stepBallHolders[i - 1], true);

                // Ignore other colliders like ground mesh etc
                foreach (var toIgnore in collidersToIgnore)
                    Physics.IgnoreCollision(steps[i], toIgnore, true);
            }

            if (stepCycleCurve.length == 0)
                stepCycleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }

        private void FixedUpdate()
        {
            UpdateCycleClock();

            // One full period is an up move + pause, then a down move + pause
            var halfPeriod = (double)stepCycleTime + stepStopTime;
            var period = halfPeriod * 2.0;
            var phase = (cycleClock + cycleOffset * period) % period;
            if (phase < 0)
                phase += period;

            var upCycle = phase < halfPeriod;
            var timeInHalf = upCycle ? phase : phase - halfPeriod;
            var cycleProgress = stepCycleCurve.Evaluate(Mathf.Clamp01((float)(timeInHalf / stepCycleTime)));

            var localStepCycle = upCycle;

            for (var i = 0; i < steps.Length; i++)
            {
                var startPos = stepRestLocations[i];
                var targetPos = stepRestLocations[i];

                if (localStepCycle)
                {
                    if (i > 0)
                        startPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i - 1].y, stepRestLocations[i].z);

                    if (i < steps.Length - 1)
                        targetPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i + 1].y, stepRestLocations[i].z);
                }
                else
                {
                    if (i > 0)
                        targetPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i - 1].y, stepRestLocations[i].z);

                    if (i < steps.Length - 1)
                        startPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i + 1].y, stepRestLocations[i].z);
                }

                if (stepBallHolderNextStepCollider.Length > i)
                    stepBallHolders[i].isTrigger = !stepBallHolders[i].bounds.Intersects(stepBallHolderNextStepCollider[i].bounds);

                if (toggleBallHolderRenderers && stepBallHolderNextStepCollider.Length > i)
                    stepBallHolders[i].GetComponent<MeshRenderer>().enabled = !stepBallHolders[i].isTrigger;

                stepRBs[i].MovePosition(Vector3.Lerp(startPos, targetPos, cycleProgress));

                localStepCycle = !localStepCycle;
            }
        }

        /// <summary>
        /// Advances the cycle clock by the physics step and steers it towards server time, so the steps move smoothly but stay in time with every player
        /// </summary>
        private void UpdateCycleClock()
        {
            var target = syncToServerTime ? Networking.GetServerTimeInMilliseconds() / 1000.0 : (double)(Time.time - localStartTime);

            if (!clockStarted)
            {
                cycleClock = target;
                clockStarted = true;
                return;
            }

            cycleClock += Time.fixedDeltaTime;

            // Server time only ticks per frame, so it is eased in rather than followed directly
            var error = target - cycleClock;
            if (error > SnapSeconds || error < -SnapSeconds)
                cycleClock = target;
            else
                cycleClock += error * ClockCorrectionRate;
        }
    }
}