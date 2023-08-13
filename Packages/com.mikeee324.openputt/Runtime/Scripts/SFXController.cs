using UdonSharp;
using UnityEngine;
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
        public AudioSource localBallHitSource;
        [Tooltip("The audio source to use when the ball drops into a hole")]
        public AudioSource localBallEnteredHoleSource;
        [Tooltip("The audio source to use for all other game SFX (Infrequently used sounds - hole in one, ball reset etc)")]
        public AudioSource localExtraSoundSource;

        [Tooltip("A small pool of audio sources that play sounds for remote players")]
        public AudioSource[] remotePlayerAudioSources;

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

        private int remoteAudioSourceID = 0;

        private float maxVolume = 1f;
        public float Volume
        {
            get => maxVolume;
            set
            {
                maxVolume = value;
                localBallHitSource.volume = localBallHitSource.volume > maxVolume ? maxVolume : localBallHitSource.volume;
                localExtraSoundSource.volume = localExtraSoundSource.volume > maxVolume ? maxVolume : localExtraSoundSource.volume;
                localBallEnteredHoleSource.volume = localBallEnteredHoleSource.volume > maxVolume ? maxVolume : localBallEnteredHoleSource.volume;

                for (int i = 0; i < remotePlayerAudioSources.Length; i++)
                    remotePlayerAudioSources[i].volume = remotePlayerAudioSources[i].volume > maxVolume ? maxVolume : remotePlayerAudioSources[i].volume;
            }
        }
        #endregion

        public void PlayBallHitSoundAtPosition(Vector3 position, float atVolume = 1f, bool isRemote = false)
        {
            if (ballHitSounds.Length > 0)
            {
                localBallHitSource.volume = (atVolume * maxVolume);
                if (isRemote)
                    PlayRemoteSoundAtPosition(ballHitSounds[Random.Range(0, ballHitSounds.Length)], at: position, maxRange: 2f, canInterrupt: true);
                else
                    PlayLocalSoundAtPosition(localBallHitSource, ballHitSounds[Random.Range(0, ballHitSounds.Length)], at: position, canInterrupt: true);
            }
        }

        public void PlayBallHoleSoundAtPosition(int courseNumber, Vector3 position, bool isRemote = false)
        {
            if (ballHitSounds.Length > 0)
            {
                int noiseToPlay = Random.Range(0, ballHoleSounds.Length);
                if (!randomiseHoleSounds && courseNumber >= 0 && courseNumber < ballHoleSounds.Length)
                    noiseToPlay = courseNumber;
                if (isRemote)
                    PlayRemoteSoundAtPosition(ballHoleSounds[noiseToPlay], at: position, maxRange: 5f, canInterrupt: true);
                else
                    PlayLocalSoundAtPosition(localBallEnteredHoleSource, ballHoleSounds[noiseToPlay], at: position, canInterrupt: true);
            }
        }

        public void PlayBallResetSoundAtPosition(Vector3 position, bool isRemote = false)
        {
            if (ballHitSounds.Length > 0)
            {
                if (isRemote)
                    return;

                PlayLocalSoundAtPosition(localBallHitSource, ballResetSounds[Random.Range(0, ballResetSounds.Length)], at: position, canInterrupt: false);
            }
        }

        public void PlayMaxScoreReachedSoundAtPosition(Vector3 position, bool isRemote = false)
        {
            if (maxScoreReachSounds.Length > 0)
            {
                if (isRemote)
                    return;
                PlayLocalSoundAtPosition(localExtraSoundSource, maxScoreReachSounds[Random.Range(0, maxScoreReachSounds.Length)], at: position, canInterrupt: false);
            }
        }

        public void PlayHoleInOneSoundAtPosition(Vector3 position, bool isRemote = false)
        {
            if (holeInOneSounds.Length > 0)
            {
                if (isRemote)
                    PlayRemoteSoundAtPosition(holeInOneSounds[Random.Range(0, holeInOneSounds.Length)], at: position, maxRange: 100f, canInterrupt: false);
                else
                    PlayLocalSoundAtPosition(localBallEnteredHoleSource, holeInOneSounds[Random.Range(0, holeInOneSounds.Length)], at: position, canInterrupt: false);
            }
        }

        public void PlayLocalSoundAtPosition(AudioSource audioSource, AudioClip clip, Vector3 at, bool canInterrupt)
        {
            if (!usePlayOneShot && audioSource.isPlaying)
            {
                if (canInterrupt)
                    audioSource.Stop();
                else
                    return;
            }

            if (audioSource != null && audioSource.enabled && !audioSource.isPlaying && Vector3.Distance(at, Networking.LocalPlayer.GetPosition()) < audioSource.maxDistance)
            {
                transform.position = at;

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

        public void PlayRemoteSoundAtPosition(AudioClip clip, Vector3 at, float maxRange = 10f, bool canInterrupt = true)
        {
            AudioSource audioSource = remotePlayerAudioSources[remoteAudioSourceID];

            int originalSourceID = remoteAudioSourceID;


            // Make sure that the next sound plays uses the next audio source in the list
            remoteAudioSourceID += 1;
            if (remoteAudioSourceID >= remotePlayerAudioSources.Length)
                remoteAudioSourceID = 0;

            if (audioSource == null)
                return;

            if (!usePlayOneShot && audioSource.isPlaying)
            {
                if (canInterrupt)
                {
                    audioSource.Stop();
                }
                else
                {
                    for (int i = remoteAudioSourceID; i < remotePlayerAudioSources.Length; i++)
                    {
                        /// We looped around and did not find an audiosource we can use - just give up
                        if (i == originalSourceID)
                            return;

                        // We found an audio source that isn't playing anything!
                        if (!audioSource.isPlaying)
                        {
                            remoteAudioSourceID = i;
                            audioSource = remotePlayerAudioSources[remoteAudioSourceID];
                        }
                    }
                }
            }

            if (audioSource.enabled && !audioSource.isPlaying && Vector3.Distance(at, Networking.LocalPlayer.GetPosition()) < maxRange)
            {
                audioSource.maxDistance = maxRange;
                transform.position = at;

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
