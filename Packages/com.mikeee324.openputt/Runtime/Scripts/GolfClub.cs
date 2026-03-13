using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace dev.mikeee324.OpenPutt
{
    public enum GolfClubType
    {
        // Putters (used for putting on the green)
        Putter,

        // Woods (typically used for longer shots)
        Driver,
        Wood3,
        Wood5,

        // Irons (used for a variety of shots from the fairway or rough)
        Iron4,
        Iron5,
        Iron6,
        Iron7,
        Iron8,
        Iron9,

        // Wedges (used for shorter, higher shots, and shots around the green)
        PitchingWedge, // PW
        GapWedge,      // GW or AW (Approach Wedge)
        SandWedge,     // SW
        LobWedge,      // LW

        // Hybrids (combine characteristics of woods and irons)
        Hybrid, // Can represent a typical hybrid, or you might specify by number/loft
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(50)]
    public class GolfClub : UdonSharpBehaviour
    {
        public PlayerManager playerManager;
        public GolfBallController ball;
        public GolfClubCollider putter;

        [FormerlySerializedAs("puttSync")]
        public OpenPuttSync openPuttSync;

        public MeshRenderer handleMesh;
        public MeshRenderer shaftMesh;
        public MeshRenderer currentHeadMesh;
        public MeshRenderer headHolderMesh;
        public Transform headContainer;
        public MeshRenderer[] lhHeadMeshes;
        public MeshRenderer[] rhHeadMeshes;
        public BoxCollider headBoxCollider;
        public GameObject shaftEndPosition;
        public VRCPickup pickup;
        public Rigidbody clubRigidbody;
        public Collider handleCollider;
        public BoxCollider shaftCollider;

        public bool throwEnabled = true;
        public float minThrowSpeed = 4f;

        public MaterialPropertyBlock headPB;
        public MaterialPropertyBlock shaftPB;
        public MaterialPropertyBlock headHolderPB;

        public LayerMask resizeLayerMask;

        public float forceMultiplier = 1f;

        public VRC_Pickup.PickupHand CurrentHand => shoulderClubHeldInHand != VRC_Pickup.PickupHand.None ? shoulderClubHeldInHand : clubHeldInHand;

        [UdonSynced, FieldChangeCallback(nameof(ClubIsArmed))]
        private bool _clubArmed;

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
                        }
                    }
                    else
                    {
                        if (Utilities.IsValid(putter))
                            putter.gameObject.SetActive(false);
                    }

                    if (!Utilities.IsValid(headPB))
                        headPB = new MaterialPropertyBlock();
                    currentHeadMesh.GetPropertyBlock(headPB);
                    if (!Utilities.IsValid(shaftPB))
                        shaftPB = new MaterialPropertyBlock();
                    shaftMesh.GetPropertyBlock(shaftPB);
                    if (!Utilities.IsValid(headHolderPB))
                        headHolderPB = new MaterialPropertyBlock();
                    if (Utilities.IsValid(headHolderMesh))
                        headHolderMesh.GetPropertyBlock(headHolderPB);

                    headPB.SetColor("_Color", value ? onColour : offColour);
                    headPB.SetColor("_EmissionColor", value ? onEmission : offEmission);

                    shaftPB.SetColor("_Color", value ? onColour : offColour);
                    shaftPB.SetColor("_EmissionColor", value ? onEmission : offEmission);

                    headHolderPB.SetColor("_Color", value ? onColour : offColour);
                    headHolderPB.SetColor("_EmissionColor", value ? onEmission : offEmission);

                    // Apply the MaterialPropertyBlock to the GameObject
                    currentHeadMesh.SetPropertyBlock(headPB);
                    shaftMesh.SetPropertyBlock(shaftPB);
                    if (Utilities.IsValid(headHolderMesh))
                        headHolderMesh.SetPropertyBlock(headHolderPB);

                    if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                        openPuttSync.RequestFastSync(forceSync: true);
                }

                _clubArmed = value;
            }
        }

        [UdonSynced, FieldChangeCallback(nameof(ClubType))]
        private GolfClubType _clubType = GolfClubType.Putter;

        public GolfClubType ClubType
        {
            get => _clubType;
            set
            {
                if (ClubIsArmed && value != _clubType)
                    return;

                var headMeshes = playerManager.IsInLeftHandedMode ? lhHeadMeshes : rhHeadMeshes;

                if ((int)value < 0)
                    value = (GolfClubType)(headMeshes.Length - 1);
                else if ((int)value >= headMeshes.Length)
                    value = 0;

                _clubType = value;

                currentHeadMesh.gameObject.SetActive(false);

                currentHeadMesh = headMeshes[(int)value];

                currentHeadMesh.gameObject.SetActive(true);

                if (Utilities.IsValid(headHolderMesh))
                {
                    headHolderMesh.gameObject.SetActive(value != GolfClubType.Putter);
                }

                if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
                {
                    playerManager.openPutt.hasChangedClubType = true;
                    if (Utilities.IsValid(playerManager.openPutt.uiController))
                        playerManager.openPutt.uiController.UpdateButtonStates();

                    if (Utilities.IsValid(playerManager.openPutt.eventHandler))
                        playerManager.openPutt.eventHandler.OnPlayerClubTypeChanged(Networking.LocalPlayer, _clubType);
                }

                if (CurrentHand != VRC_Pickup.PickupHand.None)
                    Networking.LocalPlayer.PlayHapticEventInHand(CurrentHand, vibrationDuration, vibrationStrength, vibrationFrequency);

                if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                    openPuttSync.RequestFastSync(forceSync: true);
            }
        }

        public bool AutoHoldEnabled
        {
            get
            {
                if (!Utilities.IsValid(pickup))
                    return false;

                return pickup.AutoHold == VRC_Pickup.AutoHoldMode.Yes;
            }
            set
            {
                if (Utilities.IsValid(pickup))
                    pickup.AutoHold = value ? VRC_Pickup.AutoHoldMode.Yes : VRC_Pickup.AutoHoldMode.No;

                if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
                {
                    var sp = playerManager.IsInLeftHandedMode ? playerManager.openPutt.leftShoulderPickup : playerManager.openPutt.rightShoulderPickup;
                    if (Utilities.IsValid(sp) && Utilities.IsValid(sp.pickup))
                        sp.pickup.AutoHold = value ? VRC_Pickup.AutoHoldMode.Yes : VRC_Pickup.AutoHoldMode.No;
                }
            }
        }

        [Tooltip("Allows player to extend golf club shaft to be 100m long")]
        public bool enableBigShaft;

        public Color offColour = Color.white;
        public Color onColour = Color.red;
        public Color offEmission = Color.black;
        public Color onEmission = Color.red;

        public BodyMountedObject shoulderPickup
        {
            get
            {
                if (!Utilities.IsValid(playerManager) || !Utilities.IsValid(playerManager.openPutt))
                    return null;
                return playerManager.IsInLeftHandedMode ? playerManager.openPutt.leftShoulderPickup : playerManager.openPutt.rightShoulderPickup;
            }
        }

        private VRC_Pickup.PickupHand clubHeldInHand = VRC_Pickup.PickupHand.None;
        private VRC_Pickup.PickupHand shoulderClubHeldInHand = VRC_Pickup.PickupHand.None;

        private int framesHeld = -1;

        [UdonSynced, HideInInspector]
        private float shaftScale = -1f;

        private bool LeftUseButtonDown;
        private bool RightUseButtonDown;
        private bool clubColliderIsTempDisabled;
        private bool localPlayerIsInVR;

        [Tooltip("The duration of the vibration in seconds.")]
        private float vibrationDuration = 0.1f;

        [Tooltip("The strength of the vibration (0.0 to 1.0).")]
        [Range(0f, 1f)]
        private float vibrationStrength = 0.5f;

        [Tooltip("The frequency of the vibration (roughly how many pulses per second).")]
        private float vibrationFrequency = 30f;


        private void Start()
        {
            // Make sure everything we need is on the same layer
            shaftMesh.gameObject.layer = gameObject.layer;
            headContainer.gameObject.layer = gameObject.layer;
            if (Utilities.IsValid(putter))
                putter.gameObject.layer = gameObject.layer;

            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();

            if (!Utilities.IsValid(openPuttSync))
                openPuttSync = GetComponent<OpenPuttSync>();
            shaftScale = 1;

            shaftMesh.transform.localScale = new Vector3(1, 1, 1);
            handleMesh.transform.localScale = new Vector3(1, 1, 1);
            headContainer.transform.localScale = new Vector3(1, 1, 1);

            headContainer.gameObject.transform.position = shaftEndPosition.transform.position;

            // Update the collider states
            RefreshState();

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

            enabled = false;
        }

        public override void PostLateUpdate()
        {
            var isOwner = this.LocalPlayerOwnsThisObject();

            if (isOwner)
            {
                // Player is rescaling club
                if (LeftUseButtonDown && RightUseButtonDown)
                    RescaleClub(false);
                else if (!localPlayerIsInVR && RightUseButtonDown)
                    RescaleClub(false);

                if (CurrentHand == VRC_Pickup.PickupHand.None)
                {
                    if (clubRigidbody.velocity.magnitude > 0.001f)
                    {
                        if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                            openPuttSync.RequestFastSync(forceSync: true);
                    }
                    else
                    {
                        enabled = false;
                        ResetClubThrow();
                    }
                }
                else
                {
                    framesHeld += 1;
                }
            }

            if (!Mathf.Approximately(shaftScale, shaftMesh.transform.localScale.z))
            {
                var newShaftScale = shaftScale;
                // Lerp the scale for other players
                if (!isOwner)
                    newShaftScale = Mathf.Lerp(shaftMesh.transform.localScale.z, shaftScale, 1.0f - Mathf.Pow(0.001f, Time.deltaTime));

                // Scale thickness independent of the shaft length
                var shaftGirth = Mathf.Lerp(1f, 6f, (newShaftScale - 1.5f) / 20f);

                shaftMesh.transform.localScale = new Vector3(1, 1, newShaftScale);
                handleMesh.transform.localScale = new Vector3(shaftGirth, shaftGirth, 1);
                headContainer.transform.localScale = new Vector3(1, 1, shaftGirth);

                headContainer.gameObject.transform.position = shaftEndPosition.transform.position;
            }
            else if (!isOwner)
            {
                // Local player doesn't own this club and the scale hasn't changed - do nothing
                enabled = false;
            }
        }

        public override void OnDeserialization()
        {
            enabled = true;
        }

        /// <summary>
        /// Disarms the club for the player for an amount of time
        /// </summary>
        /// <param name="duration">Amount of time to disable the club for in seconds</param>
        public void DisableClubColliderFor(float duration = 1f)
        {
            if (clubColliderIsTempDisabled) return;

            clubColliderIsTempDisabled = true;
            RefreshState();
            SendCustomEventDelayedSeconds(nameof(EnableClubCollider), duration);
        }

        public void EnableClubCollider()
        {
            clubColliderIsTempDisabled = false;
            RefreshState();
        }

        public void RefreshState()
        {
            if (shaftScale < 0)
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
                        var golfBall = playerManager.golfBall;
                        var allowHitWhileMoving = golfBall.allowBallHitWhileMoving;
                        var ballIsMoving = golfBall.BallIsMoving;

                        if (playerIsPlayingCourse)
                        {
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
            var clubCanBePickedUp = this.LocalPlayerOwnsThisObject() && shoulderClubHeldInHand == VRC_Pickup.PickupHand.None;

            if (Utilities.IsValid(pickup))
                pickup.pickupable = clubCanBePickedUp;

            if (Utilities.IsValid(handleCollider))
                handleCollider.enabled = clubCanBePickedUp;
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
            var oldShaftScale = shaftScale;
            if (resetToDefault)
            {
                // Reset all mesh scaling and work out actual default bounds
                shaftScale = 1;

                shaftMesh.transform.localScale = new Vector3(1, 1, 1);
                handleMesh.transform.localScale = new Vector3(1, 1, 1);
                headContainer.transform.localScale = new Vector3(1, 1, 1);

                headContainer.gameObject.transform.position = shaftEndPosition.transform.position;
            }
            else
            {
                var minSize = .1f;
                var maxSize = localPlayerIsInVR ? 3f : 6f;

                var raycastDir = shaftEndPosition.transform.position - shaftMesh.gameObject.transform.position;

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

            if (Math.Abs(oldShaftScale - shaftScale) > .01f && Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                openPuttSync.RequestFastSync(forceSync: true);
        }

        /// <summary>
        /// Called by external scripts when the club has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            if (!Utilities.IsValid(playerManager))
                return;

            framesHeld = 0;

            var ballShoulderPickup = shoulderPickup;
            if (Utilities.IsValid(ballShoulderPickup))
            {
                clubHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
            }
            else
            {
                clubHeldInHand = VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = VRC_Pickup.PickupHand.None;
            }

            SyncHandMode();

            ResetClubThrow();

            enabled = true;

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
            {
                if (!playerManager.openPutt.hasUsedGolfClub)
                {
                    playerManager.openPutt.hasUsedGolfClub = true;

                    if (Utilities.IsValid(playerManager.openPutt.uiController))
                        playerManager.openPutt.uiController.UpdateButtonStates();
                }

                if (Utilities.IsValid(playerManager.openPutt.openPuttPortableScoreboard))
                    playerManager.openPutt.openPuttPortableScoreboard.golfClubHeldByPlayer = true;
            }

            if (!playerManager.ClubVisible)
            {
                playerManager.ClubVisible = true;
                playerManager.RequestSync(syncNow: true);
            }

            if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                openPuttSync.RequestFastSync(forceSync: true);

            UpdateClubState();
        }

        /// <summary>
        /// Called by external scripts when the club has been dropped
        /// </summary>
        public void OnScriptDrop()
        {
            if (framesHeld > 10)
                ThrowClub();

            framesHeld = -1;

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.openPuttPortableScoreboard))
                playerManager.openPutt.openPuttPortableScoreboard.golfClubHeldByPlayer = false;

            LeftUseButtonDown = false;
            RightUseButtonDown = false;

            if (Utilities.IsValid(playerManager))
            {
                playerManager.ClubVisible = true;
                playerManager.RequestSync();
            }

            if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                openPuttSync.RequestFastSync(forceSync: true);

            var ballShoulderPickup = shoulderPickup;
            if (Utilities.IsValid(ballShoulderPickup))
            {
                clubHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
            }
            else
            {
                clubHeldInHand = VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = VRC_Pickup.PickupHand.None;
            }

            RefreshState();

            UpdateClubState();
        }

        public override void OnPickup()
        {
            var ballShoulderPickup = shoulderPickup;
            if (Utilities.IsValid(ballShoulderPickup))
            {
                clubHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
            }
            else
            {
                clubHeldInHand = VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = VRC_Pickup.PickupHand.None;
            }

            SyncHandMode();

            framesHeld = 0;

            ResetClubThrow();

            enabled = true;

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
            {
                if (!playerManager.openPutt.hasUsedGolfClub)
                {
                    playerManager.openPutt.hasUsedGolfClub = true;

                    if (Utilities.IsValid(playerManager.openPutt.uiController))
                        playerManager.openPutt.uiController.UpdateButtonStates();
                }

                if (Utilities.IsValid(playerManager.openPutt.openPuttPortableScoreboard))
                    playerManager.openPutt.openPuttPortableScoreboard.golfClubHeldByPlayer = true;
            }

            RefreshState();
        }

        public override void OnDrop()
        {
            if (framesHeld > 10)
                ThrowClub();

            framesHeld = -1;

            LeftUseButtonDown = false;
            RightUseButtonDown = false;

            if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.openPuttPortableScoreboard))
                playerManager.openPutt.openPuttPortableScoreboard.golfClubHeldByPlayer = false;

            var ballShoulderPickup = shoulderPickup;
            if (Utilities.IsValid(ballShoulderPickup))
            {
                clubHeldInHand = Utilities.IsValid(pickup) ? pickup.currentHand : VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = Utilities.IsValid(ballShoulderPickup) ? ballShoulderPickup.heldInHand : VRC_Pickup.PickupHand.None;
            }
            else
            {
                clubHeldInHand = VRC_Pickup.PickupHand.None;
                shoulderClubHeldInHand = VRC_Pickup.PickupHand.None;
            }

            RefreshState();

            UpdateClubState();
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

        private void SyncHandMode()
        {
            if (!Utilities.IsValid(playerManager) || !this.LocalPlayerOwnsThisObject())
                return;

            var hand = CurrentHand;
            if (hand == VRC_Pickup.PickupHand.None)
                return;

            var shouldBeLeftHandedMode = hand == VRC_Pickup.PickupHand.Left;
            if (playerManager.IsInLeftHandedMode != shouldBeLeftHandedMode)
            {
                playerManager.IsInLeftHandedMode = shouldBeLeftHandedMode;

                if (Utilities.IsValid(playerManager.openPutt))
                    playerManager.openPutt.SavePersistantData();

                if (Utilities.IsValid(playerManager.openPutt) && Utilities.IsValid(playerManager.openPutt.scoreboardManager))
                    playerManager.openPutt.scoreboardManager.RefreshSettingsIfVisible();
            }
        }

        private void ThrowClub()
        {
            if (!throwEnabled) return;
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            if (!Utilities.IsValid(playerManager)) return;
            if (!Utilities.IsValid(playerManager.openPutt)) return;

            // Update center of mass
            if (Utilities.IsValid(clubRigidbody) && Utilities.IsValid(shaftEndPosition))
            {
                var localShaftEnd = clubRigidbody.transform.InverseTransformPoint(shaftEndPosition.transform.position);
                clubRigidbody.centerOfMass = Vector3.Lerp(Vector3.zero, localShaftEnd, 0.7f);
            }

            var hand = CurrentHand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;
            if (!localPlayerIsInVR)
                hand = VRCPlayerApi.TrackingDataType.Head;
            var controllerVel = playerManager.openPutt.controllerTracker.GetVelocity(hand);

            // Get raw velocity without player velocity
            var rawSpeed = controllerVel.magnitude - Networking.LocalPlayer.GetVelocity().magnitude;

            // Normalize speed based on player height (Using 1.7m as reference height)
            var heightRatio = 1.7f / Mathf.Clamp(Networking.LocalPlayer.GetAvatarEyeHeightAsMeters(), 0.2f, 5f);
            var normalizedSpeed = rawSpeed * heightRatio;

            if (normalizedSpeed < minThrowSpeed) return;

            clubRigidbody.isKinematic = false;
            handleCollider.isTrigger = false;
            shaftCollider.isTrigger = false;
            clubRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var controllerTracker = playerManager.openPutt.controllerTracker;

            var offsetForCentreMass = controllerTracker.CalculateLocalOffsetFromWorldPosition(hand, clubRigidbody.worldCenterOfMass);
            var linearVelocity = controllerTracker.GetVelocityAtOffset(hand, offsetForCentreMass);
            clubRigidbody.velocity = linearVelocity;

            if (localPlayerIsInVR)
            {
                var handAngularVelocityDeg = controllerTracker.GetAngularVelocity(hand, 5);
                var handAngularVelocityRad = handAngularVelocityDeg * Mathf.Deg2Rad;

                // *** Proposed correction for VR angular velocity ***
                Vector3 transformedAngularVelocity;
                transformedAngularVelocity.x = handAngularVelocityRad.y;  // Removed negation - Fixes flipped up/down
                transformedAngularVelocity.y = -handAngularVelocityRad.x; // Kept negation - Left/right is correct
                transformedAngularVelocity.z = handAngularVelocityRad.z;  // Kept as is (assuming Z is correct)
                clubRigidbody.angularVelocity = transformedAngularVelocity;
                // *************************************************
            }
            else
            {
                var handAngularVelocityDeg = controllerTracker.GetAngularVelocity(hand, 5);
                var handAngularVelocityRad = handAngularVelocityDeg * Mathf.Deg2Rad;
                clubRigidbody.angularVelocity = handAngularVelocityRad;
            }
        }

        private void ResetClubThrow()
        {
            clubRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            clubRigidbody.isKinematic = true;
            handleCollider.isTrigger = true;
            shaftCollider.isTrigger = true;
        }
    }
}