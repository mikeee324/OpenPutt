using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
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
        public BoxCollider shaftCollider;

        public MaterialPropertyBlock headPB;
        public MaterialPropertyBlock shaftPB;

        public LayerMask resizeLayerMask;

        public float forceMultiplier = 1f;

        public VRC_Pickup.PickupHand CurrentHand
        {
            get
            {
                var hand = CurrentHandFromBodyMount;
                if (hand != VRC_Pickup.PickupHand.None)
                {
                    return hand;
                }

                if (GetComponent<VRCPickup>() != null)
                    hand = GetComponent<VRCPickup>().currentHand;

                return hand;
            }
        }

        private VRC_Pickup.PickupHand CurrentHandFromBodyMount
        {
            get
            {
                if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
                {
                    var shoulderPickup = playerManager.IsInLeftHandedMode ? playerManager.openPutt.leftShoulderPickup : playerManager.openPutt.rightShoulderPickup;
                    if (Utilities.IsValid(shoulderPickup) && shoulderPickup.heldInHand != VRC_Pickup.PickupHand.None)
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
                        if (Utilities.IsValid(playerManager))
                            playerManager.PlayerIsCurrentlyFrozen = value;

                        // Toggle the collider object
                        if (Utilities.IsValid(putter))
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
                        if (Utilities.IsValid(putter))
                            putter.gameObject.SetActive(false);
                    }

                    if (!Utilities.IsValid(headPB))
                        headPB = new MaterialPropertyBlock();
                    headMesh.GetPropertyBlock(headPB);
                    if (!Utilities.IsValid(shaftPB))
                        shaftPB = new MaterialPropertyBlock();
                    shaftMesh.GetPropertyBlock(shaftPB);

                    headPB.SetColor("_Color", value ? onColour : offColour);
                    headPB.SetColor("_EmissionColor", value ? onEmission : offEmission);

                    shaftPB.SetColor("_Color", value ? onColour : offColour);
                    shaftPB.SetColor("_EmissionColor", value ? onEmission : offEmission);

                    // Apply the MaterialPropertyBlock to the GameObject
                    headMesh.SetPropertyBlock(headPB);
                    shaftMesh.SetPropertyBlock(shaftPB);
                }

                _clubArmed = value;
            }
        }

        [Tooltip("Allows player to extend golf club shaft to be 100m long")]
        public bool enableBigShaft = false;

        public Color offColour = Color.white;
        public Color onColour = Color.red;
        public Color offEmission = Color.black;
        public Color onEmission = Color.red;

        public bool canUpdatePuttSyncSpawn = false;

        [HideInInspector] public bool heldByPlayer = false;
        [UdonSynced, HideInInspector] private float shaftScale = -1f;
        private bool LeftUseButtonDown = false;
        private bool RightUseButtonDown = false;
        private bool clubColliderIsTempDisabled = false;
        private bool localPlayerIsInVR = false;

        private void Start()
        {
            // Make sure everything we need is on the same layer
            shaftMesh.gameObject.layer = this.gameObject.layer;
            headMesh.gameObject.layer = this.gameObject.layer;
            if (Utilities.IsValid(putter))
                putter.gameObject.layer = this.gameObject.layer;

            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();

            if (!Utilities.IsValid(puttSync))
                puttSync = GetComponent<PuttSync>();
            shaftScale = 1;

            shaftMesh.transform.localScale = new Vector3(1, 1, 1);
            handleMesh.transform.localScale = new Vector3(1, 1, 1);
            headMesh.transform.localScale = new Vector3(1, 1, 1);

            headMesh.gameObject.transform.position = shaftEndPostion.transform.position;

            // Update the collider states
            RefreshState();

            this.enabled = false;

            LocalPlayerCheck();
        }

        public void LocalPlayerCheck()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer))
            {
                SendCustomEventDelayedSeconds(nameof(LocalPlayerCheck), 1);
                return;
            }

            localPlayerIsInVR = Networking.LocalPlayer.IsUserInVR();
        }

        public override void PostLateUpdate()
        {
            var isOwner = this.LocalPlayerOwnsThisObject();

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
                var newShaftScale = shaftScale;
                // Lerp the scale for other players
                if (!isOwner)
                    newShaftScale = Mathf.Lerp(shaftMesh.transform.localScale.z, shaftScale, 1.0f - Mathf.Pow(0.001f, Time.deltaTime));

                // Scale thickness independent of the shaft length
                var shaftGirth = Mathf.Lerp(1f, 6f, (newShaftScale - 1.5f) / 20f);

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
            if (shaftScale == -1)
                RescaleClub(true);

            var isOwner = this.LocalPlayerOwnsThisObject();

            if (isOwner)
            {
                var newArmedState = false;
                if (CurrentHand == VRC_Pickup.PickupHand.Left)
                    newArmedState = LeftUseButtonDown;
                else if (CurrentHand == VRC_Pickup.PickupHand.Right)
                    newArmedState = RightUseButtonDown;

                if (clubColliderIsTempDisabled)
                    newArmedState = false;

                // If player has armed the club - check if we need to disable it
                if (newArmedState)
                {
                    if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.golfBall))
                    {
                        var playerIsPlayingCourse = Utilities.IsValid(playerManager.CurrentCourse);

                        if (playerIsPlayingCourse)
                        {
                            var allowHitWhileMoving = playerManager.golfBall.allowBallHitWhileMoving;
                            var ballIsMoving = playerManager.golfBall.BallIsMoving;
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
            if (Utilities.IsValid(pickup))
                pickup.pickupable = this.LocalPlayerOwnsThisObject() && CurrentHandFromBodyMount == VRC_Pickup.PickupHand.None;

            if (Utilities.IsValid(puttSync) && canUpdatePuttSyncSpawn)
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

                headMesh.gameObject.transform.position = shaftEndPostion.transform.position;
            }
            else
            {
                var minSize = .1f;
                var maxSize = localPlayerIsInVR ? 3f : 6f;

                var raycastDir = shaftEndPostion.transform.position - shaftMesh.gameObject.transform.position;

                // if (Physics.BoxCast(shaftMesh.gameObject.transform.position, boxExtents, raycastDir, out RaycastHit h, putter.transform.rotation, maxSize, resizeLayerMask, QueryTriggerInteraction.Ignore))
                //     shaftScale = Mathf.Clamp((h.distance - headMesh.localBounds.size.z) / shaftDefaultSize, minSize, maxSize);
                // else 
                if (Physics.Raycast(shaftMesh.gameObject.transform.position, raycastDir.normalized, out var hit, enableBigShaft ? 100f : maxSize, resizeLayerMask, QueryTriggerInteraction.Ignore))
                {
                    var putterScale = Mathf.Lerp(1f, 6f, (shaftScale - 1.5f) / 20f);
                    var putterHeight = putter.putterTarget.size.z * putterScale;
                    var totalDistanceToFloor = Vector3.Distance(hit.point, shaftMesh.gameObject.transform.position) - putterHeight;
                    shaftScale = Mathf.Clamp(totalDistanceToFloor / shaftCollider.size.z, minSize, enableBigShaft ? 100f : maxSize);
                }
            }

            if (Utilities.IsValid(puttSync) && puttSync.LocalPlayerOwnsThisObject())
                puttSync.RequestFastSync();
        }

        /// <summary>
        /// Called by external scripts when the club has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            if (!Utilities.IsValid(playerManager))
                return;

            this.enabled = true;
            heldByPlayer = true;
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.portableScoreboard))
                playerManager.openPutt.portableScoreboard.golfClubHeldByPlayer = true;

            playerManager.ClubVisible = true;
            playerManager.RequestSync(syncNow: true);

            if (Utilities.IsValid(puttSync) && puttSync.LocalPlayerOwnsThisObject())
                puttSync.RequestFastSync();

            if (!Utilities.IsValid(playerManager.openPutt))
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
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.portableScoreboard))
                playerManager.openPutt.portableScoreboard.golfClubHeldByPlayer = false;

            LeftUseButtonDown = false;
            RightUseButtonDown = false;

            if (Utilities.IsValid(playerManager))
            {
                playerManager.ClubVisible = true;
                playerManager.RequestSync();
            }

            if (Utilities.IsValid(puttSync) && puttSync.LocalPlayerOwnsThisObject())
                puttSync.RequestFastSync();

            UpdateClubState();

            RefreshState();
        }

        public override void OnPickup()
        {
            this.enabled = true;

            heldByPlayer = true;
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.portableScoreboard))
                playerManager.openPutt.portableScoreboard.golfClubHeldByPlayer = true;

            RefreshState();
        }

        public override void OnDrop()
        {
            this.enabled = false;

            LeftUseButtonDown = false;
            RightUseButtonDown = false;

            heldByPlayer = false;
            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.portableScoreboard))
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