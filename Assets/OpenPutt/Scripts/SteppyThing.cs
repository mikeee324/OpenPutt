
using UdonSharp;
using UnityEngine;

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
        public AnimationCurve stepCycleCurve;

        public Collider[] collidersToIgnore;
        private float stepCyclerTimer = 0f;
        private Vector3[] stepRestLocations;
        private bool globalUpCycle = true;

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
                if (i > 0)
                    Physics.IgnoreCollision(steps[i], stepBallHolders[i - 1], true);

                // Ignore other colliders like ground mesh etc
                foreach (Collider toIgnore in collidersToIgnore)
                    Physics.IgnoreCollision(steps[i], toIgnore, true);
            }

            if (stepCycleCurve.length == 0)
            {
                stepCycleCurve.AddKey(0, 0);
                stepCycleCurve.AddKey(1, 1);
            }
        }

        void LateUpdate()
        {
            stepCyclerTimer += Time.deltaTime;

            bool localStepCycle = globalUpCycle;

            for (int i = 0; i < steps.Length; i++)
            {
                Transform thisStep = steps[i].transform;
                Vector3 startPos = stepRestLocations[i];
                Vector3 targetPos = stepRestLocations[i];

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
                    stepBallHolders[i].isTrigger = !stepBallHolders[i].bounds.Intersects(stepBallHolderNS[i].bounds);
                }

                if (toggleBallHolderRenderers)
                    stepBallHolders[i].GetComponent<MeshRenderer>().enabled = !stepBallHolders[i].isTrigger;

                thisStep.position = Vector3.Lerp(startPos, targetPos, stepCycleCurve.Evaluate(stepCyclerTimer / stepCycleTime));

                localStepCycle = !localStepCycle;
            }

            if (stepCyclerTimer > stepCycleTime)
            {
                stepCyclerTimer = 0;
                globalUpCycle = !globalUpCycle;
            }
        }
    }
}