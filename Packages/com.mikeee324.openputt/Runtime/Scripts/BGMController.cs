using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class BGMController : UdonSharpBehaviour
    {
        [Header("References")]
        public AudioSource audioSource;

        [Header("BGM Tracks")]
        public AudioClip[] audioClips;

        [Header("Settings")]
        public bool isSynced = true;

        [UdonSynced]
        private int currentTrackStartedTime;

        [UdonSynced]
        private int currentTrackID;

        void Start()
        {
            if (!Utilities.IsValid(audioSource))
                audioSource = GetComponent<AudioSource>();

            if (!Utilities.IsValid(audioSource))
            {
                OpenPuttUtils.Log(this, "Could not find the audio source to control! Disabling myself...");
                enabled = false;
                return;
            }

            // Make sure the audio source is stopped before doing anything
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.Stop();

            // If this player is the owner they will start the audio and sync when it started
            TryStartNextTrack();
        }

        /// <summary>
        /// Checks if this play can start the next track in the list. If they can they will tell other clients to play the same thing.
        /// </summary>
        public void TryStartNextTrack()
        {
            if (!Utilities.IsValid(audioSource))
                return;

            // Check every 250ms
            SendCustomEventDelayedSeconds(nameof(TryStartNextTrack), .25f);

            // If local player doesn't own this object then they should wait for OnDeserialization from the owner to start playing
            if (isSynced && !this.LocalPlayerOwnsThisObject())
                return;

            // We can only start the next track if this one finished
            if (!audioSource.enabled || audioSource.isPlaying || audioClips.Length == 0)
                return;

            // Move to the next track in the list
            currentTrackID++;
            if (currentTrackID >= audioClips.Length)
                currentTrackID = 0;


            StartTrack(currentTrackID);
        }

        public void StartTrack(int trackID)
        {
            currentTrackID = trackID;
            if (currentTrackID < 0 || currentTrackID >= audioClips.Length)
                currentTrackID = 0;

            audioSource.clip = audioClips[currentTrackID];

            // Log when the owner started playing the current track
            currentTrackStartedTime = Networking.GetServerTimeInMilliseconds();

            // Start playing the track
            audioSource.Play();

            // Tell other clients to start playing this track
            if (isSynced && this.LocalPlayerOwnsThisObject())
                RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (!Utilities.IsValid(audioSource) || !isSynced)
                return;

            OpenPuttUtils.Log(this, $"Received a clip to play! It started playing {Networking.GetServerTimeInMilliseconds() - currentTrackStartedTime}ms ago for the owner.");

            audioSource.clip = audioClips[currentTrackID];
            audioSource.time = (Networking.GetServerTimeInMilliseconds() - currentTrackStartedTime) * 0.001f;

            if (!audioSource.isPlaying)
                audioSource.Play();
        }
    }
}