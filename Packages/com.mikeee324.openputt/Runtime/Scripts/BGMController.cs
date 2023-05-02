
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class BGMController : UdonSharpBehaviour
    {
        public AudioSource audioSource;
        public AudioClip[] audioClips;

        [UdonSynced]
        private float currentTrackStartedTime = 0f;
        [UdonSynced]
        private int currentTrackID;

        void Start()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                Utils.Log(this, "Could not find the audio source to control! Disabling myself...");
                this.enabled = false;
                return;
            }

            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        private void Update()
        {
            if (Utils.LocalPlayerIsValid())
            {
                if (this.LocalPlayerOwnsThisObject())
                {
                    currentTrackID = audioClips.Length;
                    PlayNextTrack();
                }

                this.enabled = false;
            }
        }

        public void PlayNextTrack()
        {
            if (audioSource == null || !this.LocalPlayerOwnsThisObject())
                return;

            if (audioSource.isPlaying)
            {
                SendCustomEventDelayedSeconds(nameof(PlayNextTrack), 1f);
                return;
            }

            currentTrackID++;
            if (currentTrackID >= audioClips.Length)
                currentTrackID = 0;

            audioSource.clip = audioClips[currentTrackID];
            currentTrackStartedTime = (float)Networking.GetServerTimeInSeconds();

            audioSource.Play();
            RequestSerialization();

            // Try to play next track after this one finishes
            SendCustomEventDelayedSeconds(nameof(PlayNextTrack), audioSource.clip.length + .05f);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)
                // If local player has taken ownership of this then start checking if we can play the next track
                SendCustomEventDelayedSeconds(nameof(PlayNextTrack), 1f);
        }

        public override void OnDeserialization()
        {
            if (audioSource == null)
            {
                this.enabled = false;
                return;
            }

            audioSource.clip = audioClips[currentTrackID];
            audioSource.time = (float)Networking.GetServerTimeInSeconds() - currentTrackStartedTime;

            if (!audioSource.isPlaying)
                audioSource.Play();
        }
    }
}