
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

        void Update()
        {
            if (audioSource == null)
                return;

            if (Networking.LocalPlayer == null || Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid())
                return;

            if (Networking.LocalPlayer.IsOwner(gameObject) && !audioSource.isPlaying)
            {
                currentTrackID++;
                if (currentTrackID >= audioClips.Length)
                    currentTrackID = 0;

                audioSource.clip = audioClips[currentTrackID];
                currentTrackStartedTime = (float)Networking.GetServerTimeInSeconds();

                audioSource.Play();

                RequestSerialization();
            }
        }

        public override void OnDeserialization()
        {
            if (audioSource == null)
                return;

            audioSource.clip = audioClips[currentTrackID];
            audioSource.time = (float)Networking.GetServerTimeInSeconds() - currentTrackStartedTime;

            if (!audioSource.isPlaying)
                audioSource.Play();
        }
    }
}