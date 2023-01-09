
using mikeee324.OpenPutt;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SFXController : UdonSharpBehaviour
    {
        #region Settings
        [Header("References")]
        [Tooltip("A reference back to the game manager")]
        public OpenPutt openPutt;
        [Tooltip("The audio source to use when the ball is hitby/hits something")]
        public AudioSource ballHitSource;
        [Tooltip("The audio source to use when the ball drops into a hole")]
        public AudioSource ballHoleSource;
        [Tooltip("The audio source to use for all other game SFX (Infrequently used sounds - hole in one, ball reset etc)")]
        public AudioSource extraSource;

        [Header("Settings")]
        [Tooltip("Toggles whether hole noises are randomised or if each hole with have it's own unique sound")]
        public bool randomiseHoleSounds = true;
        [Tooltip("Toggles the use of PlayOneShot instead of Play on the AudioSource (Use carefully!)")]
        public bool usePlayOneShot = false;

        [Header("Sounds")]
        public AudioClip[] ballHitSounds;
        public AudioClip[] ballResetSounds;
        public AudioClip[] maxScoreReachSounds;
        public AudioClip[] ballHoleSounds;
        public AudioClip[] holeInOneSounds;

        private float maxVolume = 1f;

        public float Volume
        {
            get => maxVolume;
            set
            {
                maxVolume = value;
                ballHitSource.volume = ballHitSource.volume > maxVolume ? maxVolume : ballHitSource.volume;
                extraSource.volume = extraSource.volume > maxVolume ? maxVolume : extraSource.volume;
                ballHoleSource.volume = ballHoleSource.volume > maxVolume ? maxVolume : ballHoleSource.volume;
            }
        }
        #endregion

        public void PlayBallHitSoundAtPosition(Vector3 position, float atVolume = 1f)
        {
            if (ballHitSounds.Length > 0)
            {
                ballHitSource.volume = (atVolume * maxVolume);
                PlaySoundAtPosition(ballHitSource, ballHitSounds[Random.Range(0, ballHitSounds.Length)], position, true);
            }
        }

        public void PlayBallHoleSoundAtPosition(int courseNumber, Vector3 position)
        {
            if (ballHitSounds.Length > 0)
            {
                int noiseToPlay = Random.Range(0, ballHoleSounds.Length);
                if (!randomiseHoleSounds && courseNumber >= 0 && courseNumber < ballHoleSounds.Length)
                    noiseToPlay = courseNumber;
                PlaySoundAtPosition(ballHoleSource, ballHoleSounds[noiseToPlay], position, true);
            }
        }

        public void PlayBallResetSoundAtPosition(Vector3 position)
        {
            if (ballHitSounds.Length > 0)
            {
                PlaySoundAtPosition(extraSource, ballResetSounds[Random.Range(0, ballResetSounds.Length)], position, false);
            }
        }

        public void PlayMaxScoreReachedSoundAtPosition(Vector3 position)
        {
            if (maxScoreReachSounds.Length > 0)
            {
                PlaySoundAtPosition(extraSource, maxScoreReachSounds[Random.Range(0, maxScoreReachSounds.Length)], position, false);
            }
        }

        public void PlayHoleInOneSoundAtPosition(Vector3 position)
        {
            if (holeInOneSounds.Length > 0)
            {
                PlaySoundAtPosition(extraSource, holeInOneSounds[Random.Range(0, holeInOneSounds.Length)], position, false);
            }
        }

        public void PlaySoundAtPosition(AudioSource audioSource, AudioClip clip, Vector3 position, bool canInterrupt)
        {
            if (!usePlayOneShot && audioSource.isPlaying)
            {
                if (canInterrupt)
                    audioSource.Stop();
                else
                    return;
            }

            if (audioSource != null && audioSource.enabled && !audioSource.isPlaying && Vector3.Distance(position, Networking.LocalPlayer.GetPosition()) < audioSource.maxDistance)
            {
                transform.position = position;

                // Limit volume to player settings
                if (audioSource.volume > maxVolume)
                    audioSource.volume = maxVolume;

                audioSource.volume = Mathf.Clamp(audioSource.volume, 0, 1);
                audioSource.mute = maxVolume < 0.01f;

                if (usePlayOneShot)
                {
                    audioSource.PlayOneShot(clip);
                }
                else
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
        }
    }
}
