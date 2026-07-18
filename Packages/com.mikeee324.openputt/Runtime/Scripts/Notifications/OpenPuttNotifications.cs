using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDK3.Rendering;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OpenPuttNotifications : OpenPuttEventListener
    {
        #region Public Settings

        [OpenPuttDescription("Shows score callouts (Ace, Eagle, Birdie, Stroke Limit etc.) with sound whenever a player finishes a hole, queuing them one at a time so they don't overlap on screen.")]
        [OpenPuttFoldoutGroup("References")]
        public Canvas _canvas;
        [OpenPuttFoldoutGroup("References")]
        public OpenPuttHeadFollower _headFollower;
        [OpenPuttFoldoutGroup("References")]
        public GameObject _calloutBoxPrefab;
        [OpenPuttFoldoutGroup("References")]
        public OpenPutt openPutt;

        [OpenPuttFoldoutGroup("Notification Settings")]
        [Range(1, 20)] public float _vrSmoothing = 10f;
        [OpenPuttFoldoutGroup("Notification Settings")]
        public bool _notificationToggle = true;
        [OpenPuttFoldoutGroup("Notification Settings")]
        public bool _othersNotificationsToggle = true;
        [OpenPuttFoldoutGroup("Notification Settings")]
        public int maxQueueSize = 20;

        public bool _playerIsInVR = true;

        [OpenPuttFoldoutGroup("Position Settings")]
        public Vector3 _vrTargetPos, _desktopTargetPos, _vrStartPos, _desktopStartPos;

        [OpenPuttFoldoutGroup("Callout Sounds")]
        public AudioClip aceSound, condorSound, albatrossSound, eagleSound, birdieSound, parSound, bogeySound, doubleBogeySound, tripleBogeySound, strokeLimitSound;

        #endregion

        #region Internal Vars

        private AudioClip _notificationSound;
        private int[] _queuedCallouts;
        private int[] _queuedPlayerIds;
        private int _queueCount = 0;
        private bool _isDisplayingCallout = false;

        #endregion

        void Start()
        {
            // Initialize our queue arrays
            _queuedCallouts = new int[maxQueueSize];
            _queuedPlayerIds = new int[maxQueueSize];

            if (!Utilities.IsValid(openPutt))
                return;

            // If this object isn't already registered as an event listener, register it here automatically
            openPutt._RegisterEventListener(this);
        }

        public override void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar,
            int totalHits)
        {
            if (totalHits == 0)
            {
                Debug.LogError($"The player  {player.displayName} finished {course} with no hits. This isn't good...");
                return;
            }

            if (totalHits == 1)
            {
                Callout(Callouts.Ace, player.playerId);
                return;
            }

            switch (scoreRelativeToPar)
            {
                case -4:
                    Callout(Callouts.Condor, player.playerId);
                    break;
                case -3:
                    Callout(Callouts.Albatross, player.playerId);
                    break;
                case -2:
                    Callout(Callouts.Eagle, player.playerId);
                    break;
                case -1:
                    Callout(Callouts.Birdie, player.playerId);
                    break;
                case 0:
                    Callout(Callouts.Par, player.playerId);
                    break;
                case 1:
                    Callout(Callouts.Bogey, player.playerId);
                    break;
                case 2:
                    Callout(Callouts.DoubleBogey, player.playerId);
                    break;
                case 3:
                    Callout(Callouts.TripleBogey, player.playerId);
                    break;
            }
        }

        public override void OnPlayerHitCourseMaxScore(VRCPlayerApi player, CourseManager course)
        {
            Callout(Callouts.StrokeLimit, player.playerId);
        }

        //This is just to check if the player is in vr once.
        public override void OnPlayerDataUpdated(VRCPlayerApi player, PlayerData.Info[] infos)
        {
            if (player != Networking.LocalPlayer) return;
            _playerIsInVR = player.IsUserInVR();

            Vector3 baseScale = new Vector3(0.01f, 0.01f, 0.01f); // Your default canvas scale
            if (_playerIsInVR)
            {
                _headFollower._lerpSpeed = _vrSmoothing;
                _canvas.transform.localScale = baseScale;
            }
            else
            {
                _headFollower._lerpSpeed = 1000;

                // 1. Define the baseline values you used when designing the UI
                float baseFOV = 60f; // The FOV where the UI looks perfect

                // 2. Get the player's current FOV (e.g., 50, 90, 100)
                float currentFOV = VRCCameraSettings.ScreenCamera.FieldOfView;

                // 3. Calculate the multiplier
                // We divide by 2, and multiply by Mathf.Deg2Rad because Mathf.Tan expects radians!
                float currentTan = Mathf.Tan((currentFOV / 2f) * Mathf.Deg2Rad);
                float baseTan = Mathf.Tan((baseFOV / 2f) * Mathf.Deg2Rad);
                float scaleMultiplier = currentTan / baseTan;

                // 4. Apply the exact scale to your Canvas/UI
                _canvas.transform.localScale = baseScale * scaleMultiplier;
            }
        }

        public void SendCallout(Callouts callout)
        {
            //You can't pass a playerobject through networking. So we recreate it after the network.
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Callout), (int)callout, Networking.LocalPlayer.playerId);
        }

        [NetworkCallable]
        public void Callout(Callouts callout, int playerId)
        {
            // Preliminary checks
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
            if (player == null) return;
            if (!_notificationToggle) return;
            if (!_othersNotificationsToggle && player != Networking.LocalPlayer) return;

            // Add to queue if we have space
            if (_queueCount < maxQueueSize)
            {
                _queuedCallouts[_queueCount] = (int)callout;
                _queuedPlayerIds[_queueCount] = playerId;
                _queueCount++;
            }
            else
            {
                Debug.LogWarning("Callout queue is full! Dropping notification.");
            }

            // If nothing is playing, start the queue
            if (!_isDisplayingCallout)
            {
                ProcessNextQueueItem();
            }
        }

        private void ProcessNextQueueItem()
        {
            // If the queue is empty, flag that we are done and wait for the next call
            if (_queueCount == 0)
            {
                _isDisplayingCallout = false;
                return;
            }

            _isDisplayingCallout = true;

            // Pop the first item
            Callouts currentCallout = (Callouts)_queuedCallouts[0];
            int currentPlayerId = _queuedPlayerIds[0];

            // Shift the remaining items down the arrays
            for (int i = 0; i < _queueCount - 1; i++)
            {
                _queuedCallouts[i] = _queuedCallouts[i + 1];
                _queuedPlayerIds[i] = _queuedPlayerIds[i + 1];
            }
            _queueCount--;

            // It's possible the player left while sitting in the queue, check again
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(currentPlayerId);
            if (player == null)
            {
                // Player left, skip this one and instantly process the next
                ProcessNextQueueItem();
                return;
            }

            string calloutText = "This is an error!";

            switch (currentCallout)
            {
                case Callouts.Ace:
                    _notificationSound = aceSound;
                    calloutText = player == Networking.LocalPlayer ? "ACE!" : $"{player.displayName} scored an ACE!";
                    break;
                case Callouts.Condor:
                    _notificationSound = condorSound;
                    calloutText = player == Networking.LocalPlayer ? "Condor!" : $"{player.displayName} scored a Condor!";
                    break;
                case Callouts.Albatross:
                    _notificationSound = albatrossSound;
                    calloutText = player == Networking.LocalPlayer ? "Albatross!" : $"{player.displayName} scored an Albatross!";
                    break;
                case Callouts.Eagle:
                    _notificationSound = eagleSound;
                    calloutText = player == Networking.LocalPlayer ? "Eagle!" : $"{player.displayName} scored an Eagle!";
                    break;
                case Callouts.Birdie:
                    _notificationSound = birdieSound;
                    calloutText = player == Networking.LocalPlayer ? "Birdie!" : $"{player.displayName} scored a Birdie!";
                    break;
                case Callouts.Par:
                    _notificationSound = parSound;
                    calloutText = player == Networking.LocalPlayer ? "Par!" : $"{player.displayName} scored a Par!";
                    break;
                case Callouts.Bogey:
                    _notificationSound = bogeySound;
                    calloutText = player == Networking.LocalPlayer ? "Bogey!" : $"{player.displayName} scored a Bogey.";
                    break;
                case Callouts.DoubleBogey:
                    _notificationSound = doubleBogeySound;
                    calloutText = player == Networking.LocalPlayer ? "Double Bogey!" : $"{player.displayName} scored a Double Bogey.";
                    break;
                case Callouts.TripleBogey:
                    _notificationSound = tripleBogeySound;
                    calloutText = player == Networking.LocalPlayer ? "Triple Bogey!" : $"{player.displayName} scored a Triple Bogey.";
                    break;
                case Callouts.StrokeLimit:
                    _notificationSound = strokeLimitSound;
                    calloutText = player == Networking.LocalPlayer ? "Stroke Limit!" : $"{player.displayName} hit the Stroke Limit.";
                    break;
            }

            InstantiateCalloutBox(calloutText, _notificationSound);
        }

        public void InstantiateCalloutBox(string callText, AudioClip callAudio = null)
        {
            GameObject lastCallout = Instantiate(_calloutBoxPrefab, _canvas.transform);
            var calloutScript = lastCallout.GetComponent<OpenPuttCalloutBox>();

            // Pass the reference of this script so the box can tell us when it dies
            calloutScript.manager = this;

            calloutScript.targetPos = _playerIsInVR ? _vrTargetPos : _desktopTargetPos;
            calloutScript.SetCalloutText(callText, callAudio
                , targetPosition: _playerIsInVR ? _vrTargetPos : _desktopTargetPos
                , startPosition: _playerIsInVR ? _vrStartPos : _desktopStartPos);
        }

        // OpenPuttCalloutBox will trigger this when its destruction tween finishes
        public void OnCalloutFinished()
        {
            ProcessNextQueueItem();
        }

        public void SendTestNotification()
        {
            //You can't pass a playerobject through networking. So we recreate it after the network.
            int randomIndex = Random.Range(0, (int)Callouts.StrokeLimit + 1);
            Callouts randomResult = (Callouts)randomIndex;
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Callout), (int)randomResult, Networking.LocalPlayer.playerId);
        }
    }

    public enum Callouts
    {
        Ace,
        Condor,
        Albatross,
        Eagle,
        Birdie,
        Par,
        Bogey,
        DoubleBogey,
        TripleBogey,
        StrokeLimit
    }
}
