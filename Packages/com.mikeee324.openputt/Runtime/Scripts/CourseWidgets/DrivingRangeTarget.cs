using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
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

        [Tooltip("Colour shown while the target is idle and waiting to be hit")]
        public Color defaultColour = Color.white;

        [Tooltip("Gradient played over timeAfterHit when a ball hits this target")]
        public Gradient hitGradient;

        [Tooltip("Gradient played over respawnAnimDuration when the target reappears")]
        public Gradient respawnGradient;

        public float timeAfterHit = 2f;
        public float timeToRespawn = 15f;

        [Tooltip("How long the hit colour animation lasts (should be less than timeAfterHit)")]
        public float hitAnimDuration = 0.5f;

        [Tooltip("How long the respawn colour animation lasts")]
        public float respawnAnimDuration = 0.5f;

        [Tooltip("How long the shrink animation takes when the target dies")]
        public float deathShrinkDuration = 0.6f;

        public MeshRenderer myMesh;
        private Collider myCollider;
        private MaterialPropertyBlock materialPropertyBlock;
        private Vector3 _originalScale;

        [System.NonSerialized] public float _hitColourT;
        [System.NonSerialized] public float _respawnColourT;

        private VRCTweenHandle _hitColourHandle;
        private VRCTweenHandle _hitScaleHandle;
        private VRCTweenHandle _respawnColourHandle;
        private VRCTweenHandle _deathScaleHandle;
        private VRCTweenHandle _respawnScaleHandle;

        void Start()
        {
            if (!Utilities.IsValid(myMesh))
                myMesh = GetComponent<MeshRenderer>();
            myCollider = GetComponent<Collider>();

            if (!Utilities.IsValid(materialPropertyBlock))
                materialPropertyBlock = new MaterialPropertyBlock();
            myMesh.GetPropertyBlock(materialPropertyBlock);

            _originalScale = transform.localScale;

            SetColour(defaultColour);
        }

        private void OnDisable()
        {
            gameObject.KillAllTweens();
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
            if (!golfBall.LocalPlayerOwnsThisObject())
                return;

            if (Utilities.IsValid(myCollider))
                myCollider.enabled = false;

            golfBall._RespawnBall();

            if (Utilities.IsValid(courseManager) && Utilities.IsValid(golfBall.playerManager))
                golfBall.playerManager._AddToCourseScore(courseManager, scoreToAdd);

            if (_hitColourHandle != null) _hitColourHandle.Kill();
            _hitColourHandle = VRCTween.TweenFloat(0f, 1f, hitAnimDuration, this, nameof(_hitColourT), nameof(_OnHitColourUpdate), VRCTweenEase.OutBounce);

            if (_hitScaleHandle != null) _hitScaleHandle.Kill();
            transform.localScale = _originalScale * 1.3f;
            _hitScaleHandle = transform.TweenScale(_originalScale, hitAnimDuration, VRCTweenEase.OutBounce);

            SendCustomEventDelayedSeconds(nameof(_OnTargetDeath), timeAfterHit);
        }

        public void _OnHitColourUpdate()
        {
            SetColour(hitGradient.Evaluate(_hitColourT));
        }

        public void _OnTargetDeath()
        {
            if (_deathScaleHandle != null) _deathScaleHandle.Kill();
            _deathScaleHandle = transform.TweenScale(Vector3.zero, deathShrinkDuration, VRCTweenEase.InBounce)
                .OnComplete(this, nameof(_DisableTarget));
        }

        public void _DisableTarget()
        {
            gameObject.SetActive(false);
            SendCustomEventDelayedSeconds(nameof(ResetTarget), timeToRespawn);
        }

        public void ResetTarget()
        {
            transform.localScale = Vector3.zero;

            if (Utilities.IsValid(myCollider))
                myCollider.enabled = true;

            gameObject.SetActive(true);

            if (_respawnColourHandle != null) _respawnColourHandle.Kill();
            _respawnColourHandle = VRCTween.TweenFloat(0f, 1f, respawnAnimDuration, this, nameof(_respawnColourT), nameof(_OnRespawnColourUpdate), VRCTweenEase.Linear);

            if (_respawnScaleHandle != null) _respawnScaleHandle.Kill();
            _respawnScaleHandle = transform.TweenScale(_originalScale, deathShrinkDuration, VRCTweenEase.OutElastic);
        }

        public void _OnRespawnColourUpdate()
        {
            SetColour(respawnGradient.Evaluate(_respawnColourT));
        }

        private void SetColour(Color colour)
        {
            materialPropertyBlock.SetColor("_Color", colour);
            materialPropertyBlock.SetColor("_EmissionColor", colour);
            myMesh.SetPropertyBlock(materialPropertyBlock);
        }
    }
}
