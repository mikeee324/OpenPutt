using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(50)]
    public class GolfClub : UdonSharpBehaviour
    {
        public PlayerManager playerManager;
        public GolfBallController ball;
        public GolfClubCollider putter;
        public PuttSync puttSync;
        public MeshRenderer handleMesh;
        public MeshRenderer shaftMesh;
        public MeshRenderer headMesh;
        public GameObject shaftEndPostion;
        public VRCPickup pickup;

        public LayerMask resizeLayerMask;

        public float forceMultiplier = 1f;
        public VRCPickup.PickupHand CurrentHand
        {
            get
            {
                VRCPickup.PickupHand hand = CurrentHandFromBodyMount;
                if (hand != VRC_Pickup.PickupHand.None)
                {
                    return hand;
                }

                if (GetComponent<VRCPickup>() != null)
                    hand = GetComponent<VRCPickup>().currentHand;

                return hand;
            }
        }
        private VRCPickup.PickupHand CurrentHandFromBodyMount
        {
            get
            {
                if (playerManager != null && playerManager.openPutt != null)
                {
                    BodyMountedObject shoulderPickup = playerManager.IsInLeftHandedMode ? playerManager.openPutt.leftShoulderPickup : playerManager.openPutt.rightShoulderPickup;
                    if (shoulderPickup != null && shoulderPickup.heldInHand != VRC_Pickup.PickupHand.None)
                        return shoulderPickup.heldInHand;
                }
                return VRC_Pickup.PickupHand.None;
            }
        }
        [UdonSynced, FieldChangeCallback(nameof(ClubIsArmed))]
        private bool _clubArmed = false;
        public bool ClubIsArmed
        {
            get => _clubArmed;
            set
            {
                // If the state of the club has changed
                if (ClubIsArmed != value)
                {
                    if (this.LocalPlayerOwnsThisObject())
                    {
                        // Toggles whether the player is frozen or not
                        if (playerManager != null)
                            playerManager.PlayerIsCurrentlyFrozen = value;

                        // Toggle the collider object
                        if (putter != null)
                        {
                            // Enable the collider for the club
                            putter.gameObject.SetActive(value);

                            // Tell the collider that it was just switched back on
                            if (value)
                                putter.OnClubArmed();
                        }
                    }
                    else
                    {
                        if (putter != null)
                            putter.gameObject.SetActive(false);
                    }

                    // Toggle golf club mesh materials
                    if (headMesh != null)
                        headMesh.material = value ? onMaterial : offMaterial;
                    if (shaftMesh != null)
                        shaftMesh.material = value ? onMaterial : offMaterial;
                }

                _clubArmed = value;
            }
        }
        [Tooltip("Allows player to extend golf club shaft to be 100m long")]
        public bool enableBigShaft = false;
        [Tooltip("Which material to use on the club when it is not armed")]
        public Material offMaterial;
        [Tooltip("Which material to use on the club when it is armed")]
        public Material onMaterial;

        private float shaftDefaultSize = -1f;

        [HideInInspector]
        public bool heldByPlayer = false;
        [UdonSynced, HideInInspector]
        private float shaftScale = -1f;
        private bool LeftUseButtonDown = false;
        private bool RightUseButtonDown = false;
        private bool clubColliderIsTempDisabled = false;

        private void Start()
        {
            // Make sure everything we need is on the same layer
            shaftMesh.gameObject.layer = this.gameObject.layer;
            headMesh.gameObject.layer = this.gameObject.layer;
            if (putter != null)
                putter.gameObject.layer = this.gameObject.layer;

            if (pickup == null)
                pickup = GetComponent<VRCPickup>();

            if (puttSync == null)
                puttSync = GetComponent<PuttSync>();
            shaftScale = 1;

            shaftMesh.transform.localScale = new Vector3(1, 1, 1);
            handleMesh.transform.localScale = new Vector3(1, 1, 1);
            headMesh.transform.localScale = new Vector3(1, 1, 1);

            shaftDefaultSize = shaftMesh.bounds.size.y;
            headMesh.gameObject.transform.position = shaftEndPostion.transform.position;

            // Update the collider states
            RefreshState();

            this.enabled = false;
        }


        public override void PostLateUpdate()
        {
            bool isOwner = this.LocalPlayerOwnsThisObject();

            if (isOwner)
            {
                // Player is rescaling club
                if (LeftUseButtonDown && RightUseButtonDown)
                    RescaleClub(false);
                else if (!Networking.LocalPlayer.IsUserInVR() && RightUseButtonDown)
                    RescaleClub(false);
            }
        }

        RaycastHit[] ballCheckHits = new RaycastHit[10];

        private void FixedUpdate()
        {
            int hits = Physics.SphereCastNonAlloc(shaftEndPostion.transform.position, 0.1f, shaftEndPostion.transform.forward, ballCheckHits, 100f);
            if (hits > 0)
            {
                for (int i = 0; i < hits; i++)
                {
                    if (ballCheckHits[i].collider == null) continue;
                    GolfBallController ball = ballCheckHits[i].collider.GetComponent<GolfBallController>();
                    if (ball != null)
                    {
                        Utils.LogError("Help", "Found ball");
                    }
                }
            }

            // TODO: BoxCastNonAlloc to check if we hit a ball?
            bool isOwner = this.LocalPlayerOwnsThisObject();

            if (isOwner)
            {
                // Player is rescaling club
                if (LeftUseButtonDown && RightUseButtonDown)
                    RescaleClub(false);
                else if (!Networking.LocalPlayer.IsUserInVR() && RightUseButtonDown)
                    RescaleClub(false);
            }

            if (shaftScale != shaftMesh.transform.localScale.z)
            {
                float newShaftScale = shaftScale;
                // Lerp the scale for other players
                if (!isOwner)
                    newShaftScale = Mathf.Lerp(shaftMesh.transform.localScale.z, shaftScale, 1.0f - Mathf.Pow(0.001f, Time.deltaTime));

                // Scale thickness independent of the shaft length
                float shaftGirth = Mathf.Lerp(1f, 6f, (newShaftScale - 1.5f) / 20f);

                shaftMesh.transform.localScale = new Vector3(1, 1, newShaftScale);
                handleMesh.transform.localScale = new Vector3(shaftGirth, shaftGirth, 1);
                headMesh.transform.localScale = new Vector3(1, 1, shaftGirth);

                headMesh.gameObject.transform.position = shaftEndPostion.transform.position;
            }
            else if (!isOwner)
            {
                // Local player doesn't own this club and the scale hasn't changed - do nothing
                this.enabled = false;
            }
        }

        public override void OnDeserialization()
        {
            this.enabled = true;
        }

        /// <summary>
        /// Disarms the club for the player for an amount of time
        /// </summary>
        /// <param name="duration">Amount of time to disable the club for in seconds</param>
        public void DisableClubColliderFor(float duration = 1f)
        {
            if (!clubColliderIsTempDisabled)
            {
                clubColliderIsTempDisabled = true;
                RefreshState();
                SendCustomEventDelayedSeconds(nameof(EnableClubCollider), duration);
            }
        }

        public void EnableClubCollider()
        {
            clubColliderIsTempDisabled = false;
            RefreshState();
        }

        public void RefreshState()
        {
            if (shaftScale == -1 || shaftDefaultSize == -1)
                RescaleClub(true);

            bool isOwner = this.LocalPlayerOwnsThisObject();

            if (isOwner)
            {
                bool newArmedState = false;
                if (CurrentHand == VRC_Pickup.PickupHand.Left)
                    newArmedState = LeftUseButtonDown;
                else if (CurrentHand == VRC_Pickup.PickupHand.Right)
                    newArmedState = RightUseButtonDown;

                if (clubColliderIsTempDisabled)
                    newArmedState = false;

                // If player has armed the club - check if we need to disable it
                if (newArmedState)
                {
                    if (playerManager != null && playerManager.golfBall != null)
                    {
                        bool playerIsPlayingCourse = playerManager.CurrentCourse != null;

                        if (playerIsPlayingCourse)
                        {
                            bool allowHitWhileMoving = playerManager.golfBall.allowBallHitWhileMoving;
                            bool ballIsMoving = playerManager.golfBall.BallIsMoving;
                            if (ballIsMoving && !allowHitWhileMoving)
                                newArmedState = false;
                        }
                    }
                }

                ClubIsArmed = newArmedState;
            }
            else
            {
                ClubIsArmed = false;
            }
        }

        public void UpdateClubState()
        {
            if (pickup != null)
                pickup.pickupable = this.LocalPlayerOwnsThisObject() && CurrentHandFromBodyMount == VRCPickup.PickupHand.None;

            if (puttSync != null)
                puttSync.SetSpawnPosition(new Vector3(0, -2, 0), Quaternion.identity);
        }

        public void OnRespawn()
        {
            // if (shaftScale > 10f)
            //    RescaleClub(true);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            UpdateClubState();
            RescaleClub(true);
            RequestSerialization();
        }

        /// <summary>
        /// Resizes the club for the player.
        /// </summary>
        /// <param name="resetToDefault">True=Scale is reset to 1<br/>False=Club will be resized to touch the ground</param>
        public void RescaleClub(bool resetToDefault)
        {
            if (resetToDefault)
            {
                // Reset all mesh scaling and work out actual default bounds
                shaftScale = 1;

                shaftMesh.transform.localScale = new Vector3(1, 1, 1);
                handleMesh.transform.localScale = new Vector3(1, 1, 1);
                headMesh.transform.localScale = new Vector3(1, 1, 1);

                if (shaftDefaultSize == -1)
                    shaftDefaultSize = shaftMesh.bounds.size.y * shaftMesh.transform.lossyScale.y;

                headMesh.gameObject.transform.position = shaftEndPostion.transform.position;
            }
            else
            {
                float maxSize = Networking.LocalPlayer.IsValid() && Networking.LocalPlayer.IsUserInVR() ? 3f : 6f;
                Vector3 raycastDir = shaftEndPostion.transform.position - shaftMesh.gameObject.transform.position;
                if (Physics.Raycast(shaftMesh.gameObject.transform.position, raycastDir, out RaycastHit hit, 100f, resizeLayerMask))
                    shaftScale = Mathf.Clamp((hit.distance - headMesh.bounds.size.z) / shaftDefaultSize, 0.1f, enableBigShaft ? 100f : maxSize);
            }

            if (puttSync != null && puttSync.LocalPlayerOwnsThisObject())
                puttSync.RequestFastSync();
        }

        /// <summary>
        /// Called by external scripts when the club has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            if (playerManager == null)
                return;

            this.enabled = true;
            heldByPlayer = true;
            if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.portableScoreboard != null)
                playerManager.openPutt.portableScoreboard.golfClubHeldByPlayer = true;

            playerManager.ClubVisible = true;
            playerManager.RequestSync(syncNow: true);

            if (puttSync != null && puttSync.LocalPlayerOwnsThisObject())
                puttSync.RequestFastSync();

            if (playerManager.openPutt == null)
                return;

            UpdateClubState();
        }

        /// <summary>
        /// Called by external scripts when the club has been dropped
        /// </summary>
        public void OnScriptDrop()
        {
            this.enabled = false;

            heldByPlayer = false;
            if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.portableScoreboard != null)
                playerManager.openPutt.portableScoreboard.golfClubHeldByPlayer = false;

            LeftUseButtonDown = false;
            RightUseButtonDown = false;

            if (playerManager != null)
            {
                playerManager.ClubVisible = true;
                playerManager.RequestSync();
            }

            if (puttSync != null && puttSync.LocalPlayerOwnsThisObject())
                puttSync.RequestFastSync();

            UpdateClubState();

            RefreshState();
        }

        public override void OnPickup()
        {
            this.enabled = true;

            heldByPlayer = true;
            if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.portableScoreboard != null)
                playerManager.openPutt.portableScoreboard.golfClubHeldByPlayer = true;

            RefreshState();
        }

        public override void OnDrop()
        {
            this.enabled = false;

            LeftUseButtonDown = false;
            RightUseButtonDown = false;

            heldByPlayer = false;
            if (playerManager != null && playerManager.openPutt != null && playerManager.openPutt.portableScoreboard != null)
                playerManager.openPutt.portableScoreboard.golfClubHeldByPlayer = false;

            RefreshState();
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            // Store button state
            if (args.handType == HandType.LEFT)
                LeftUseButtonDown = value;
            else if (args.handType == HandType.RIGHT)
                RightUseButtonDown = value;

            RefreshState();
        }

    }
}