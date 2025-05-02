using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SteppyThing : UdonSharpBehaviour
    {
        [Tooltip("The colliders for each step in SteppyThing")]
        public Collider[] steps;
        [Tooltip("Used for debug purposes to make it easier to see if SteppyThing is working properly")]
        public bool toggleBallHolderRenderers;
        [Tooltip("A list of colliders that hold the ball from rolling off each step")]
        public Collider[] stepBallHolders;
        [FormerlySerializedAs("stepBallHolderNS")] [Tooltip("A list of colliders that reference the collider of the next step or the floor at the end of the staircase. If the ball holder for each step is in contact with the next step the ball holder will be enabled. When they no longer contact each each the holder is disabled so the ball can roll off.")]
        public Collider[] stepBallHolderNextStepCollider;

        [Range(1, 10f), Tooltip("Time in seconds for each full up/down cycle of the steps")]
        public float stepCycleTime = 1f;

        [Range(0, 5f), Tooltip("Time in seconds the steps will sit still for when they reach the up or down position")]
        public float stepStopTime = 0.5f;

        public AnimationCurve stepCycleCurve;

        public Collider[] collidersToIgnore;
        private float stepCyclerTimer;
        private float stepStopTimer;
        public Vector3[] stepRestLocations;
        private bool globalUpCycle = true;

        [UdonSynced]
        private bool _masterUpCycleState;

        private Rigidbody[] stepRBs;

        void Start()
        {
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

        public override void OnDeserialization()
        {
            globalUpCycle = _masterUpCycleState;
            stepCyclerTimer = 0f;
            stepStopTimer = 0f;
        }

        private void FixedUpdate()
        {
            stepCyclerTimer += Time.deltaTime;

            var cycleProgress = stepCycleCurve.Evaluate(stepCyclerTimer / stepCycleTime);

            var localStepCycle = globalUpCycle;

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

            if (stepCyclerTimer > stepCycleTime)
            {
                stepStopTimer += Time.deltaTime;
                if (stepStopTimer > stepStopTime)
                {
                    stepCyclerTimer = 0;
                    stepStopTimer = 0;
                    globalUpCycle = !globalUpCycle;

                    if (OpenPuttUtils.LocalPlayerIsValid() && Networking.LocalPlayer.IsOwner(gameObject))
                    {
                        _masterUpCycleState = globalUpCycle;
                        RequestSerialization();
                    }
                }
            }
        }
    }
}