
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SteppyThing : UdonSharpBehaviour
    {
        public Collider[] steps;
        public bool toggleBallHolderRenderers = false;
        public Collider[] stepBallHolders;
        public Collider[] stepBallHolderNS;

        [Range(1, 10f), Tooltip("Time in seconds for each full up/down cycle of the steps")]
        public float stepCycleTime = 1f;
        [Range(0, 5f), Tooltip("Time in seconds the steps will sit still for when they reach the up or down position")]
        public float stepStopTime = 0.5f;
        public AnimationCurve stepCycleCurve;

        public Collider[] collidersToIgnore;
        private float stepCyclerTimer = 0f;
        private float stepStopTimer = 0f;
        private Vector3[] stepRestLocations;
        private bool globalUpCycle = true;

        [UdonSynced]
        private bool _masterUpCycleState = false;

        void Start()
        {
            stepRestLocations = new Vector3[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                stepRestLocations[i] = steps[i].transform.position;

                MeshCollider collider = steps[i].GetComponent<MeshCollider>();
                collider.isTrigger = false;

                // Make Steps Ignore Collisions with neighbouring steps
                if (i < steps.Length - 1)
                    Physics.IgnoreCollision(steps[i], steps[i + 1], true);

                // Ignore ball holder collisions
                if (i > 0 && stepBallHolders.Length > 0)
                    Physics.IgnoreCollision(steps[i], stepBallHolders[i - 1], true);

                // Ignore other colliders like ground mesh etc
                foreach (Collider toIgnore in collidersToIgnore)
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

        private void LateUpdate()
        {
            stepCyclerTimer += Time.deltaTime;

            bool localStepCycle = globalUpCycle;

            for (int i = 0; i < steps.Length; i++)
            {
                Transform thisStep = steps[i].transform;
                Vector3 startPos;
                Vector3 targetPos;

                if (i == 0)
                {
                    // First step
                    if (localStepCycle)
                    {
                        startPos = stepRestLocations[i];
                        targetPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i + 1].y, stepRestLocations[i].z);
                    }
                    else
                    {
                        startPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i + 1].y, stepRestLocations[i].z);
                        targetPos = stepRestLocations[i];
                    }

                    if (stepBallHolderNS.Length > i)
                        stepBallHolders[i].isTrigger = !stepBallHolders[i].bounds.Intersects(stepBallHolderNS[i].bounds);
                }
                else if (i < steps.Length - 1)
                {
                    // Not the last step
                    if (localStepCycle)
                    {
                        startPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i - 1].y, stepRestLocations[i].z);
                        targetPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i + 1].y, stepRestLocations[i].z);
                    }
                    else
                    {
                        startPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i + 1].y, stepRestLocations[i].z);
                        targetPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i - 1].y, stepRestLocations[i].z);
                    }

                    if (stepBallHolderNS.Length > i)
                        stepBallHolders[i].isTrigger = !stepBallHolders[i].bounds.Intersects(stepBallHolderNS[i].bounds);
                }
                else
                {
                    if (localStepCycle)
                    {
                        startPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i - 1].y, stepRestLocations[i].z);
                        targetPos = stepRestLocations[i];
                    }
                    else
                    {
                        startPos = stepRestLocations[i];
                        targetPos = new Vector3(stepRestLocations[i].x, stepRestLocations[i - 1].y, stepRestLocations[i].z);
                    }
                    if (stepBallHolderNS.Length > i)
                        stepBallHolders[i].isTrigger = !stepBallHolders[i].bounds.Intersects(stepBallHolderNS[i].bounds);
                }

                if (toggleBallHolderRenderers && stepBallHolderNS.Length > i)
                    stepBallHolders[i].GetComponent<MeshRenderer>().enabled = !stepBallHolders[i].isTrigger;

                thisStep.position = Vector3.Lerp(startPos, targetPos, stepCycleCurve.Evaluate(stepCyclerTimer / stepCycleTime));

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

                    if (Utils.LocalPlayerIsValid() && Networking.LocalPlayer.IsOwner(gameObject))
                    {
                        _masterUpCycleState = globalUpCycle;
                        RequestSerialization();
                    }
                }
            }
        }
    }
}