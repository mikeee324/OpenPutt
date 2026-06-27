using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Forces any golf balls to jump back to their last known valid position when colliding with this object<br/>
    /// Requires a collider on the same object as this script!!
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DrivingRangeTarget : UdonSharpBehaviour
    {
        [Tooltip("The course manager that this target is a part of")]
        public CourseManager courseManager;

        [Tooltip("The score to add to the player's score when hitting this target")]
        public int scoreToAdd = 1;

        public Color defaultColour = Color.white;
        public Color hitColour = Color.green;

        public float timeAfterHit = 2;
        public float timeToRespawn = 15;
        public bool respawnBallImmediately = true;

        private MeshRenderer myMesh;

        private MaterialPropertyBlock materialPropertyBlock;

        void Start()
        {
            myMesh = GetComponent<MeshRenderer>();

            // Apply the base armed/disarmed colours to the head/shaft/head holder
            if (!Utilities.IsValid(materialPropertyBlock))
                materialPropertyBlock = new MaterialPropertyBlock();
            myMesh.GetPropertyBlock(materialPropertyBlock);

            materialPropertyBlock.SetColor("_Color", defaultColour);
            materialPropertyBlock.SetColor("_EmissionColor", defaultColour);

            myMesh.SetPropertyBlock(materialPropertyBlock);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var golfBall = collision.gameObject.GetComponent<GolfBallController>();
            if (Utilities.IsValid(golfBall))
                _OnBallHit(golfBall);
        }

        private void OnTriggerEnter(Collider other)
        {
            var golfBall = other.gameObject.GetComponent<GolfBallController>();
            if (Utilities.IsValid(golfBall))
                _OnBallHit(golfBall);
        }

        public void _OnBallHit(GolfBallController golfBall)
        {
            if (respawnBallImmediately)
                golfBall._RespawnBall();
            if (Utilities.IsValid(courseManager))
            {
                golfBall.playerManager.courseScores[courseManager.holeNumber] += scoreToAdd;
                if (Utilities.IsValid(golfBall.playerManager.openPutt.uiController))
                    golfBall.playerManager.openPutt.uiController.UpdateDisplay();
            }

            materialPropertyBlock.SetColor("_Color", hitColour);
            materialPropertyBlock.SetColor("_EmissionColor", hitColour);

            myMesh.SetPropertyBlock(materialPropertyBlock);

            SendCustomEventDelayedSeconds(nameof(_OnTargetDeath), timeAfterHit);
        }

        public void _OnTargetDeath()
        {
            gameObject.SetActive(false);

            SendCustomEventDelayedSeconds(nameof(ResetTarget), timeToRespawn);
        }

        public void ResetTarget()
        {
            materialPropertyBlock.SetColor("_Color", defaultColour);
            materialPropertyBlock.SetColor("_EmissionColor", defaultColour);

            myMesh.SetPropertyBlock(materialPropertyBlock);

            gameObject.SetActive(true);
        }
    }
}