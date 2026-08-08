using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// A target that reacts when a golf ball hits it, awarding score, playing a hit animation/particles/sound, then hiding itself and respawning after a delay.<br/>
    /// Requires a collider on the same object as this script!!
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DrivingRangeTarget : UdonSharpBehaviour
    {
        [OpenPuttDescription("A target that players can hit with their ball to score points. It plays a hit animation and briefly disappears before respawning.")]
        [Tooltip("The course manager that this target is a part of")]
        public CourseManager courseManager;

        [Tooltip("The score to add to the player's score when hitting this target")]
        public int scoreToAdd = 1;

        [OpenPuttFoldoutGroup("Visual Settings")]
        [Tooltip("Colour shown while the target is idle and waiting to be hit")]
        public Color defaultColour = Color.white;

        [OpenPuttFoldoutGroup("Visual Settings")]
        [Tooltip("Gradient played over timeAfterHit when a ball hits this target")]
        public Gradient hitGradient;

        [OpenPuttFoldoutGroup("Visual Settings")]
        [Tooltip("Gradient played over respawnAnimDuration when the target reappears")]
        public Gradient respawnGradient;

        [OpenPuttFoldoutGroup("Timing Settings")]
        public float timeAfterHit = 2f;

        [OpenPuttFoldoutGroup("Timing Settings")]
        public float timeToRespawn = 15f;

        [OpenPuttFoldoutGroup("Timing Settings")]
        [Tooltip("How long the hit colour animation lasts (should be less than timeAfterHit)")]
        public float hitAnimDuration = 0.5f;

        [OpenPuttFoldoutGroup("Timing Settings")]
        [Tooltip("How long the respawn colour animation lasts")]
        public float respawnAnimDuration = 0.5f;

        [OpenPuttFoldoutGroup("Timing Settings")]
        [Tooltip("How long the shrink animation takes when the target dies")]
        public float deathShrinkDuration = 0.6f;

        [OpenPuttFoldoutGroup("Hit Effects")]
        [Tooltip("Particle system to play when a ball hits this target")]
        public ParticleSystem hitParticleSystem;

        [OpenPuttFoldoutGroup("Hit Effects")]
        [Tooltip("If true, hitParticleSystem will be moved to this target's position/rotation before it is played (useful if it's shared between multiple targets). Requires its simulation space to be set to World so already emitted particles don't move with it afterwards")]
        public bool moveParticleSystemToTarget;

        [OpenPuttFoldoutGroup("Hit Effects")]
        [Tooltip("Audio source to play when a ball hits this target")]
        public AudioSource hitAudioSource;

        [OpenPuttFoldoutGroup("Hit Effects")]
        [Tooltip("If true, hitAudioSource's clip is played via AudioSource.PlayClipAtPoint at this target's position instead of moving/playing hitAudioSource directly. This spawns a temporary one-shot source so the sound stays anchored even if hitAudioSource is shared and reused by another target before the clip finishes")]
        public bool moveAudioSourceToTarget;

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
        private bool _initialized;

        void Start()
        {
            if (!Utilities.IsValid(myMesh))
                myMesh = GetComponent<MeshRenderer>();
            myCollider = GetComponent<Collider>();

            if (!Utilities.IsValid(materialPropertyBlock))
                materialPropertyBlock = new MaterialPropertyBlock();
            myMesh.GetPropertyBlock(materialPropertyBlock);

            _originalScale = transform.localScale;
            _initialized = true;

            SetColour(defaultColour);
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            SendCustomEventDelayedFrames(nameof(_StartRespawnTweens), 0);
        }

        public void _StartRespawnTweens()
        {
            if (_respawnColourHandle != null) _respawnColourHandle.Kill();
            _respawnColourHandle = VRCTween.TweenFloat(0f, 1f, respawnAnimDuration, this, nameof(_respawnColourT), nameof(_OnRespawnColourUpdate), VRCTweenEase.Linear);

            transform.localScale = Vector3.zero;
            if (_respawnScaleHandle != null) _respawnScaleHandle.Kill();
            _respawnScaleHandle = transform.TweenScale(_originalScale, deathShrinkDuration, VRCTweenEase.OutQuint);
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
            _hitColourHandle = VRCTween.TweenFloat(0f, 1f, hitAnimDuration, this, nameof(_hitColourT), nameof(_OnHitColourUpdate), VRCTweenEase.OutQuint);

            if (_hitScaleHandle != null) _hitScaleHandle.Kill();
            transform.localScale = _originalScale * 1.3f;
            _hitScaleHandle = transform.TweenScale(_originalScale, hitAnimDuration, VRCTweenEase.OutQuint);

            if (Utilities.IsValid(hitParticleSystem))
            {
                if (moveParticleSystemToTarget)
                {
                    hitParticleSystem.transform.SetPositionAndRotation(transform.position, transform.rotation);
                }
                hitParticleSystem.Play();
            }

            if (Utilities.IsValid(hitAudioSource))
            {
                if (moveAudioSourceToTarget)
                    AudioSource.PlayClipAtPoint(hitAudioSource.clip, transform.position, hitAudioSource.volume);
                else
                    hitAudioSource.Play();
            }

            SendCustomEventDelayedSeconds(nameof(_OnTargetDeath), timeAfterHit);
        }

        public void _OnHitColourUpdate()
        {
            SetColour(hitGradient.Evaluate(_hitColourT));
        }

        public void _OnTargetDeath()
        {
            if (_deathScaleHandle != null) _deathScaleHandle.Kill();
            _deathScaleHandle = transform.TweenScale(Vector3.zero, deathShrinkDuration, VRCTweenEase.InQuint)
                .OnComplete(this, nameof(_DisableTarget));
        }

        public void _DisableTarget()
        {
            // Only hide the mesh (rather than disabling the whole GameObject) so that hitParticleSystem/hitAudioSource
            // can keep playing on their own even though this target is "dead" and waiting to respawn
            myMesh.enabled = false;
            SendCustomEventDelayedSeconds(nameof(ResetTarget), timeToRespawn);
        }

        public void ResetTarget()
        {
            transform.localScale = Vector3.zero;

            if (Utilities.IsValid(myCollider))
                myCollider.enabled = true;

            myMesh.enabled = true;

            SendCustomEventDelayedFrames(nameof(_StartRespawnTweens), 0);
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
