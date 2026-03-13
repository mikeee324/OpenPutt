using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SFXController : UdonSharpBehaviour
    {
        #region Settings

        [Header("References")] [Tooltip("A reference back to the game manager")]
        public OpenPutt openPutt;

        [Tooltip("The audio source to use when the ball is hitby/hits something")]
        public AudioSource localBallHitSource;

        [Tooltip("The audio source to use when the ball drops into a hole")]
        public AudioSource localBallEnteredHoleSource;

        [Tooltip("The audio source to use for all other game SFX (Infrequently used sounds - hole in one, ball reset etc)")]
        public AudioSource localExtraSoundSource;

        [Tooltip("A small pool of audio sources that play sounds for remote players")]
        public AudioSource[] remotePlayerAudioSources;

        [Header("Settings")] [Tooltip("Toggles whether hole noises are randomised or if each hole with have it's own unique sound")]
        public bool randomiseHoleSounds = true;

        [Tooltip("Toggles the use of PlayOneShot instead of Play on the AudioSource (Use carefully!)")]
        public bool usePlayOneShot;

        [Header("General Sounds")] [Tooltip("List of sounds to play when the player hits the ball (randomised on each hit)")]
        public AudioClip[] ballHitSounds;

        [Tooltip("List of sounds to play when the players ball gets reset (randomised)")]
        public AudioClip[] ballResetSounds;

        [Tooltip("List of sounds to play when the player hits the max score for the current hole (randomised)")]
        public AudioClip[] maxScoreReachSounds;

        [Tooltip("List of sounds to play when the players ball enters a hole (If randomise hole sounds is off, this list picks a sound from this list by using the course number)")]
        public AudioClip[] ballHoleSounds;

        [Header("Score Related Sounds")] [Tooltip("List of sounds to play when the playersgets a hole in one (randomised)")]
        public AudioClip[] holeInOneSounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score 4 or below par (randomised)"),]
        public AudioClip[] condorSounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score 3 below par (randomised)")]
        public AudioClip[] albatrossSounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score 2 below par (randomised)")]
        public AudioClip[] eagleSounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score 1 below par (randomised)")]
        public AudioClip[] birdieSounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score on par (randomised)")]
        public AudioClip[] parSounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score 1 above par (randomised)")]
        public AudioClip[] bogeySounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score 2 above par (randomised)")]
        public AudioClip[] doubleBogeySounds;

        [Tooltip("List of sounds to play when the player finishes a course with a score 3 above par (randomised)")]
        public AudioClip[] tripleBogeySounds;

        private int remoteAudioSourceID;

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

                for (var i = 0; i < remotePlayerAudioSources.Length; i++)
                    remotePlayerAudioSources[i].volume = remotePlayerAudioSources[i].volume > maxVolume ? maxVolume : remotePlayerAudioSources[i].volume;
            }
        }

        #endregion

        public void PlayBallHitSoundAtPosition(Vector3 position, float atVolume = 1f, bool isRemote = false)
        {
            if (ballHitSounds.Length == 0) return;

            localBallHitSource.volume = (atVolume * maxVolume);
            if (isRemote)
                PlayRemoteSoundAtPosition(ballHitSounds.GetRandom(), at: position, maxRange: 2f, canInterrupt: true);
            else
                PlayLocalSoundAtPosition(localBallHitSource, ballHitSounds.GetRandom(), at: position, canInterrupt: true);
        }

        public void PlayBallHoleSoundAtPosition(int courseNumber, Vector3 position, bool isRemote = false)
        {
            if (ballHitSounds.Length == 0) return;

            var noiseToPlay = ballHoleSounds.GetRandom();
            if (!randomiseHoleSounds && courseNumber >= 0 && courseNumber < ballHoleSounds.Length)
                noiseToPlay = ballHoleSounds[courseNumber];

            if (isRemote)
                PlayRemoteSoundAtPosition(noiseToPlay, at: position, maxRange: 5f, canInterrupt: true);
            else
                PlayLocalSoundAtPosition(localBallEnteredHoleSource, noiseToPlay, at: position, canInterrupt: true);
        }

        public void PlayBallResetSoundAtPosition(Vector3 position, bool isRemote = false)
        {
            if (ballHitSounds.Length == 0 || isRemote) return;

            PlayLocalSoundAtPosition(localBallHitSource, ballResetSounds.GetRandom(), at: position, canInterrupt: false);
        }

        public void PlayMaxScoreReachedSoundAtPosition(Vector3 position, bool isRemote = false)
        {
            if (maxScoreReachSounds.Length == 0 || isRemote) return;

            PlayLocalSoundAtPosition(localExtraSoundSource, maxScoreReachSounds.GetRandom(), at: position, canInterrupt: false);
        }

        public void PlayHoleInOneSoundAtPosition(Vector3 position, bool isRemote = false)
        {
            if (holeInOneSounds.Length == 0) return;

            if (isRemote)
                PlayRemoteSoundAtPosition(holeInOneSounds.GetRandom(), at: position, maxRange: 100f, canInterrupt: false);
            else
                PlayLocalSoundAtPosition(localBallEnteredHoleSource, holeInOneSounds.GetRandom(), at: position, canInterrupt: false);
        }

        public void PlayScoreSoundAtPosition(Vector3 position, int scoreRelativeToPar, bool isRemote = false)
        {
            if (holeInOneSounds.Length == 0) return;

            AudioClip clipToPlay = null;

            // Check special cases first
            if (scoreRelativeToPar <= -4)
            {
                clipToPlay = condorSounds.GetRandom();
            }
            else
            {
                // Check static numbers
                switch (scoreRelativeToPar)
                {
                    case -3:
                        clipToPlay = albatrossSounds.GetRandom();
                        break;
                    case -2:
                        clipToPlay = eagleSounds.GetRandom();
                        break;
                    case -1:
                        clipToPlay = birdieSounds.GetRandom();
                        break;
                    case 0:
                        clipToPlay = parSounds.GetRandom();
                        break;
                    case 1:
                        clipToPlay = bogeySounds.GetRandom();
                        break;
                    case 2:
                        clipToPlay = doubleBogeySounds.GetRandom();
                        break;
                    case 3:
                        clipToPlay = tripleBogeySounds.GetRandom();
                        break;
                }
            }

            if (Utilities.IsValid(clipToPlay))
            {
                if (isRemote)
                    PlayRemoteSoundAtPosition(clipToPlay, at: position, maxRange: 100f, canInterrupt: false);
                else
                    PlayLocalSoundAtPosition(localBallEnteredHoleSource, clipToPlay, at: position, canInterrupt: false);
            }
        }

        public void PlayLocalSoundAtPosition(AudioSource audioSource, AudioClip clip, Vector3 at, bool canInterrupt)
        {
            if (!Utilities.IsValid(audioSource)) return;

            if (!usePlayOneShot && audioSource.isPlaying)
            {
                if (canInterrupt)
                    audioSource.Stop();
                else
                    return;
            }

            if (Vector3.Distance(at, Networking.LocalPlayer.GetPosition()) > audioSource.maxDistance)
            {
                OpenPuttUtils.Log(this, $"Not playing local sound clip {clip.name} as it is too far away");
                return;
            }

            if (!audioSource.enabled)
            {
                OpenPuttUtils.Log(this, $"Not playing local sound clip {clip.name} as the audio source is disabled");
                return;
            }

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

        public void PlayRemoteSoundAtPosition(AudioClip clip, Vector3 at, float maxRange = 10f, bool canInterrupt = true)
        {
            var audioSource = remotePlayerAudioSources[remoteAudioSourceID];

            var originalSourceID = remoteAudioSourceID;


            // Make sure that the next sound plays uses the next audio source in the list
            remoteAudioSourceID += 1;
            if (remoteAudioSourceID >= remotePlayerAudioSources.Length)
                remoteAudioSourceID = 0;

            if (!Utilities.IsValid(audioSource))
                return;

            if (!usePlayOneShot && audioSource.isPlaying)
            {
                if (canInterrupt)
                {
                    audioSource.Stop();
                }
                else
                {
                    for (var i = remoteAudioSourceID; i < remotePlayerAudioSources.Length; i++)
                    {
                        /// We looped around and did not find an audiosource we can use - just give up
                        if (i == originalSourceID)
                            return;

                        // We found an audio source that isn't playing anything!
                        if (!audioSource.isPlaying)
                        {
                            remoteAudioSourceID = i;
                            if (Utilities.IsValid(remotePlayerAudioSources[remoteAudioSourceID]))
                                audioSource = remotePlayerAudioSources[remoteAudioSourceID];
                        }
                    }
                }
            }

            if (Vector3.Distance(at, Networking.LocalPlayer.GetPosition()) > maxRange)
            {
                OpenPuttUtils.Log(this, $"Not playing remote sound clip {clip.name} as it is too far away");
                return;
            }

            if (!Utilities.IsValid(audioSource))
            {
                OpenPuttUtils.Log(this, $"Not playing remote sound clip {clip.name} as the audio source is null");
                return;
            }

            if (!audioSource.enabled)
            {
                OpenPuttUtils.Log(this, $"Not playing remote sound clip {clip.name} as the audio source is disabled");
                return;
            }

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