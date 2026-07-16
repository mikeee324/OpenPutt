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

    /// <summary>
    /// Bitmask of <see cref="GolfClubType"/> for configuring allowed clubs per course. Bit positions match the enum values.
    /// </summary>
    [Flags]
    public enum GolfClubTypeMask
    {
        None = 0,
        Putter = 1 << (int)GolfClubType.Putter,
        Driver = 1 << (int)GolfClubType.Driver,
        Wood3 = 1 << (int)GolfClubType.Wood3,
        Wood5 = 1 << (int)GolfClubType.Wood5,
        Iron4 = 1 << (int)GolfClubType.Iron4,
        Iron5 = 1 << (int)GolfClubType.Iron5,
        Iron6 = 1 << (int)GolfClubType.Iron6,
        Iron7 = 1 << (int)GolfClubType.Iron7,
        Iron8 = 1 << (int)GolfClubType.Iron8,
        Iron9 = 1 << (int)GolfClubType.Iron9,
        PitchingWedge = 1 << (int)GolfClubType.PitchingWedge,
        GapWedge = 1 << (int)GolfClubType.GapWedge,
        SandWedge = 1 << (int)GolfClubType.SandWedge,
        LobWedge = 1 << (int)GolfClubType.LobWedge,
        Hybrid = 1 << (int)GolfClubType.Hybrid,
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
        public MeshRenderer[] headMeshes;
        public BoxCollider headBoxCollider;
        public GameObject shaftEndPosition;
        public VRCPickup pickup;
        public Rigidbody clubRigidbody;
        public Collider handleCollider;
        public BoxCollider shaftCollider;

        public bool throwEnabled = true;
        public float minThrowSpeed = 4f;
        [Tooltip("Scales down the spin applied to the club when thrown (1 = raw hand rotation speed, lower values reduce spin)")]
        public float throwSpinMultiplier = 0.4f;
        [Tooltip("Maximum angular velocity (degrees/sec) the club can be thrown with, clamped before the spin multiplier is applied")]
        public float maxThrowAngularVelocity = 720f;

        public MaterialPropertyBlock handlePB;
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
                    _clubArmed = value;

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

                    // Apply the base armed/disarmed colours to the head/shaft/head holder
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

                    currentHeadMesh.SetPropertyBlock(headPB);
                    shaftMesh.SetPropertyBlock(shaftPB);
                    if (Utilities.IsValid(headHolderMesh))
                        headHolderMesh.SetPropertyBlock(headHolderPB);

                    // Overlay the players ball colour on the handle/head (when enabled + disarmed)
                    _UpdateClubColour();

                    if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                        openPuttSync._RequestFastSync(forceSync: true);
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

                if ((int)value < 0)
                    value = (GolfClubType)(headMeshes.Length - 1);
                else if ((int)value >= headMeshes.Length)
                    value = 0;

                _clubType = value;

                currentHeadMesh.gameObject.SetActive(false);

                currentHeadMesh = headMeshes[(int)value];

                currentHeadMesh.gameObject.SetActive(true);

                // Left-handed clubs reuse the right-handed head meshes, mirrored on the X axis
                var headScale = currentHeadMesh.transform.localScale;
                headScale.x = Mathf.Abs(headScale.x) * (playerManager.IsInLeftHandedMode ? -1f : 1f);
                currentHeadMesh.transform.localScale = headScale;

                if (Utilities.IsValid(headHolderMesh))
                {
                    headHolderMesh.gameObject.SetActive(value != GolfClubType.Putter);
                }

                // Make sure the newly selected head/head holder shows the correct colour
                _UpdateClubColour();

                if (Utilities.IsValid(playerManager) && Utilities.IsValid(playerManager.openPutt))
                {
                    playerManager.openPutt.hasChangedClubType = true;

                    if (Utilities.IsValid(playerManager.openPutt.eventHandler))
                        playerManager.openPutt.eventHandler.OnPlayerClubTypeChanged(playerManager.Owner, _clubType);
                }

                if (CurrentHand != VRC_Pickup.PickupHand.None)
                    Networking.LocalPlayer.PlayHapticEventInHand(CurrentHand, vibrationDuration, vibrationStrength, vibrationFrequency);

                if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                    openPuttSync._RequestFastSync(forceSync: true);
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

        [Tooltip("When enabled the club scales to reach the ball's height below the hand, instead of raycasting down to the floor")]
        public bool scaleToBallHeight;

        public Color offColour = Color.white;
        public Color onColour = Color.red;
        public Color offEmission = Color.black;
        public Color onEmission = Color.red;

        [Tooltip("When enabled, the club handle and head meshes are tinted with the players ball colour while the club is disarmed")]
        public bool tintWithBallColour = true;

        [Tooltip("The players ball colour. Tints the handle and (when disarmed) the head meshes. Set via PlayerManager.BallColor")]
        [HideInInspector]
        public Color ballColour = Color.white;

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
            _RefreshState();

            _LocalPlayerCheck();
        }

        public void _LocalPlayerCheck()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer))
            {
                SendCustomEventDelayedSeconds(nameof(_LocalPlayerCheck), 1);
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
                    _RescaleClub(false);
                else if (!localPlayerIsInVR && RightUseButtonDown)
                    _RescaleClub(false);

                if (CurrentHand == VRC_Pickup.PickupHand.None)
                {
                    if (clubRigidbody.velocity.magnitude > 0.001f)
                    {
                        //if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                        //    openPuttSync._RequestFastSync(forceSync: true);
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
        public void _DisableClubColliderFor(float duration = 1f)
        {
            if (clubColliderIsTempDisabled) return;

            clubColliderIsTempDisabled = true;
            _RefreshState();
            SendCustomEventDelayedSeconds(nameof(_EnableClubCollider), duration);
        }

        public void _EnableClubCollider()
        {
            clubColliderIsTempDisabled = false;
            _RefreshState();
        }

        public void _RefreshState()
        {
            if (shaftScale < 0)
                _RescaleClub(true);

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

        public void _UpdateClubState()
        {
            var clubCanBePickedUp = this.LocalPlayerOwnsThisObject() && shoulderClubHeldInHand == VRC_Pickup.PickupHand.None;

            if (Utilities.IsValid(pickup))
                pickup.pickupable = clubCanBePickedUp;

            if (Utilities.IsValid(handleCollider))
                handleCollider.enabled = clubCanBePickedUp;
        }

        /// <summary>
        /// Tints the handle and head meshes with the player's ball colour (skipped while armed or tinting is disabled).
        /// </summary>
        public void _UpdateClubColour()
        {
            // Configured in the inspector and never toggled at runtime
            if (!tintWithBallColour)
                return;

            // Handle always shows the players ball colour
            if (Utilities.IsValid(handleMesh))
            {
                if (!Utilities.IsValid(handlePB))
                    handlePB = new MaterialPropertyBlock();
                handleMesh.GetPropertyBlock(handlePB);
                handlePB.SetColor("_Color", ballColour);
                handleMesh.SetPropertyBlock(handlePB);
            }

            // Leave the head/head holder red while armed - only tint them when disarmed
            if (_clubArmed)
                return;

            if (Utilities.IsValid(currentHeadMesh))
            {
                if (!Utilities.IsValid(headPB))
                    headPB = new MaterialPropertyBlock();
                currentHeadMesh.GetPropertyBlock(headPB);
                headPB.SetColor("_Color", ballColour);
                headPB.SetColor("_EmissionColor", offEmission);
                currentHeadMesh.SetPropertyBlock(headPB);
            }

            if (Utilities.IsValid(headHolderMesh))
            {
                if (!Utilities.IsValid(headHolderPB))
                    headHolderPB = new MaterialPropertyBlock();
                headHolderMesh.GetPropertyBlock(headHolderPB);
                headHolderPB.SetColor("_Color", ballColour);
                headHolderPB.SetColor("_EmissionColor", offEmission);
                headHolderMesh.SetPropertyBlock(headHolderPB);
            }
        }

        public void _OnRespawn()
        {
            // if (shaftScale > 10f)
            //    _RescaleClub(true);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            _UpdateClubState();
            _RescaleClub(true);
            RequestSerialization();
        }

        /// <summary>
        /// Resizes the club for the player.
        /// </summary>
        /// <param name="resetToDefault">True=Scale is reset to 1<br/>False=Club will be resized to touch the ground</param>
        public void _RescaleClub(bool resetToDefault)
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
                var maxSize = enableBigShaft ? 100f : (localPlayerIsInVR ? 3f : 6f);

                // Divide out parent scale so world-space distances are in the club's local units
                var worldScale = shaftMesh.transform.parent.lossyScale.z;
                if (worldScale < 0.0001f)
                    worldScale = 1f;

                var handPosition = shaftMesh.gameObject.transform.position;

                var shaftDir = (shaftEndPosition.transform.position - handPosition).normalized;

                var localPlayer = Networking.LocalPlayer;
                var maxBallDistance = Utilities.IsValid(localPlayer) ? localPlayer.GetAvatarEyeHeightAsMeters() : 2f;

                // Distance (in local units) from the hand/grip down to where the club head should end up
                var localDistance = -1f;
                if (scaleToBallHeight && Utilities.IsValid(ball) && shaftDir.y < -0.0001f && Vector3.Distance(handPosition, ball.transform.position) <= maxBallDistance)
                {
                    // Ball is close enough - scale along the shaft so the head reaches the height the ball sits at (accounts for club tilt)
                    var ballGroundY = ball.transform.position.y - ball.BallWorldRadius;
                    localDistance = (ballGroundY - handPosition.y) / shaftDir.y / worldScale;
                }
                else
                {
                    // Raycast down the shaft to find the floor
                    if (Physics.Raycast(handPosition, shaftDir, out var hit, maxSize, resizeLayerMask, QueryTriggerInteraction.Ignore))
                        localDistance = Vector3.Distance(hit.point, handPosition) / worldScale;
                }

                if (localDistance > 0f)
                {
                    var putterScale = Mathf.Lerp(1f, 6f, (shaftScale - 1.5f) / 20f);
                    var putterHeight = putter.putterTarget.size.z * putterScale;
                    shaftScale = Mathf.Clamp((localDistance - putterHeight) / shaftCollider.size.z, minSize, maxSize);
                }
            }

            if (Math.Abs(oldShaftScale - shaftScale) > .01f && Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                openPuttSync._RequestFastSync(forceSync: true);
        }

        /// <summary>
        /// Called by external scripts when the club has been picked up
        /// </summary>
        public void _OnScriptPickup()
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
                playerManager._RequestSync(syncNow: true);
            }

            if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                openPuttSync._RequestFastSync(forceSync: true);

            _UpdateClubState();
        }

        /// <summary>
        /// Called by external scripts when the club has been dropped
        /// </summary>
        public void _OnScriptDrop()
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
                playerManager._RequestSync();
            }

            if (Utilities.IsValid(openPuttSync) && openPuttSync.LocalPlayerOwnsThisObject())
                openPuttSync._RequestFastSync(forceSync: true);

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

            _RefreshState();

            _UpdateClubState();

            // Now the pickup is free, put the club/ball back on the shoulders that match the current
            // handedness (in case it was changed while the club was being held off a shoulder)
            if (Utilities.IsValid(playerManager))
                playerManager._UpdateShoulderPickupAttachments();
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

            _RefreshState();

            RequestSerialization();
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

            _RefreshState();

            _UpdateClubState();

            // Now the pickup is free, put the club/ball back on the shoulders that match the current
            // handedness (in case it was changed while the club was being held off a shoulder)
            if (Utilities.IsValid(playerManager))
                playerManager._UpdateShoulderPickupAttachments();
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            // Store button state
            if (args.handType == HandType.LEFT)
                LeftUseButtonDown = value;
            else if (args.handType == HandType.RIGHT)
                RightUseButtonDown = value;
            _RefreshState();
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
                    playerManager.openPutt._SavePersistantData();

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

            var handAngularVelocityDeg = controllerTracker.GetAngularVelocity(hand, 5);
            handAngularVelocityDeg = Vector3.ClampMagnitude(handAngularVelocityDeg, maxThrowAngularVelocity) * throwSpinMultiplier;
            clubRigidbody.angularVelocity = handAngularVelocityDeg * Mathf.Deg2Rad;
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