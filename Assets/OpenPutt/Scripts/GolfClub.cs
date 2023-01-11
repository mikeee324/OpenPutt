using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
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
        public PickupHelper pickupHelper { get; private set; }
        public BodyMountedObject bodyMountedObject;

        public LayerMask resizeLayerMask;

        public float forceMultiplier = 1f;
        public VRCPickup.PickupHand CurrentHand
        {
            get
            {
                if (GetComponent<PickupHelper>() != null)
                    return GetComponent<PickupHelper>().CurrentHand;
                return VRC_Pickup.PickupHand.None;
            }
        }
        public bool ClubIsArmed
        {
            get => putter != null && putter.gameObject.activeInHierarchy;
            set
            {
                // If the state of the club has changed
                if (ClubIsArmed != value)
                {
                    // Club can never be armed for remote players
                    if (!Networking.LocalPlayer.IsOwner(gameObject))
                        value = false;

                    // Toggle the collider object
                    if (putter != null)
                        putter.gameObject.SetActive(value);

                    // Tell the collider that it was just switched back on
                    if (value)
                        putter.OnClubArmed();

                    // Toggle golf club mesh materials
                    if (headMesh != null)
                        headMesh.material = value ? onMaterial : offMaterial;
                    if (shaftMesh != null)
                        shaftMesh.material = value ? onMaterial : offMaterial;
                }
            }
        }
        [Tooltip("Forces the club to be armed - useful for testing")]
        public bool overrideIsArmed = false;
        [Tooltip("Allows player to extend golf club shaft to be 100m long")]
        public bool enableBigShaft = false;
        [Tooltip("Which material to use on the club when it is not armed")]
        public Material offMaterial;
        [Tooltip("Which material to use on the club when it is armed")]
        public Material onMaterial;

        private float shaftDefaultSize = -1f;

        [UdonSynced, HideInInspector]
        private float shaftScale = -1f;
        private float ballHitHelpTimer = -1f;
        public VRCPickup.PickupHand currentHandFromScript { get; private set; }

        void Start()
        {
            // Make sure everything we need is on the same layer
            shaftMesh.gameObject.layer = this.gameObject.layer;
            headMesh.gameObject.layer = this.gameObject.layer;
            if (putter != null)
                putter.gameObject.layer = this.gameObject.layer;

            if (pickupHelper == null)
                pickupHelper = GetComponent<PickupHelper>();

            if (puttSync == null)
                puttSync = GetComponent<PuttSync>();

            // Update the collider states
            RefreshState();
        }

        private void Update()
        {
            if (ballHitHelpTimer != -1f)
            {
                ballHitHelpTimer += Time.deltaTime;
                if (ballHitHelpTimer > 1f)
                    ballHitHelpTimer = -1f;
            }

            // Update the collider states
            RefreshState();

            // While the collider is off keep the rigidbody attached to the club (So it doesn't fly to catch up when armed)
            if (!ClubIsArmed && putter != null)
            {
                putter.transform.position = putter.putterTarget.position;
                putter.transform.rotation = putter.putterTarget.rotation;
            }
        }

        public void OnBallHitWhileMoving()
        {
            ballHitHelpTimer = 0f;
        }

        private void RefreshState()
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid())
                return;

            if (shaftScale == -1 || shaftDefaultSize == -1)
                RescaleClub(true);

            bool isOwner = Networking.LocalPlayer.IsOwner(gameObject);

            if (isOwner)
            {
                // Check if the club is armed or needs to enable scaling
                bool newArmedState = false;

                VRCPickup.PickupHand currentHand = pickupHelper != null ? pickupHelper.CurrentHand : VRC_Pickup.PickupHand.None;
                if (currentHandFromScript != VRC_Pickup.PickupHand.None)
                    currentHand = currentHandFromScript;

                if (currentHand != VRC_Pickup.PickupHand.None)
                {
                    if (currentHand == VRC_Pickup.PickupHand.Left)
                        newArmedState = pickupHelper != null ? pickupHelper.LeftUseButtonDown : true;
                    else if (currentHand == VRC_Pickup.PickupHand.Right)
                        newArmedState = pickupHelper != null ? pickupHelper.RightUseButtonDown : true;

                    // If user is holding both triggers (or on desktop), enable club scaling
                    if (pickupHelper != null && pickupHelper.LeftUseButtonDown && pickupHelper.RightUseButtonDown)
                        RescaleClub(false);
                    else if (!Networking.LocalPlayer.IsUserInVR() && newArmedState)
                        RescaleClub(false);
                }

                if (playerManager != null && playerManager.golfBall != null)
                    if (!playerManager.golfBall.allowBallHitWhileMoving && playerManager.golfBall.BallIsMoving)
                        newArmedState = false;

                ClubIsArmed = ballHitHelpTimer == -1f && (newArmedState || overrideIsArmed);
            }
            else
            {
                ClubIsArmed = false;
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
        }

        public void UpdateClubState()
        {
            VRCPickup pickup = GetComponent<VRCPickup>();
            if (pickup != null)
                pickup.pickupable = Networking.IsOwner(Networking.LocalPlayer, gameObject) && currentHandFromScript == VRCPickup.PickupHand.None;
            if (Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                PuttSync puttSync = GetComponent<PuttSync>();
                if (puttSync != null)
                    puttSync.SetSpawnPosition(Vector3.zero, Quaternion.identity);
            }
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
                shaftDefaultSize = shaftMesh.bounds.size.y;

                handleMesh.transform.localScale = new Vector3(1, 1, 1);
                headMesh.transform.localScale = new Vector3(1, 1, 1);
                headMesh.gameObject.transform.position = shaftEndPostion.transform.position;
            }
            else
            {
                float maxSize = Networking.LocalPlayer.IsValid() && Networking.LocalPlayer.IsUserInVR() ? 3f : 6f;
                Vector3 raycastDir = shaftEndPostion.transform.position - shaftMesh.gameObject.transform.position;
                if (Physics.Raycast(shaftMesh.gameObject.transform.position, raycastDir, out RaycastHit hit, 100f, resizeLayerMask))
                    //if (Physics.SphereCast(shaftMesh.transform.position, 0.03f, raycastDir, out RaycastHit hit, 100f, resizeLayerMask))
                    shaftScale = Mathf.Clamp((hit.distance - headMesh.bounds.size.y) / shaftDefaultSize, 0.1f, enableBigShaft ? 100f : maxSize);
            }

            if (puttSync != null)
                puttSync.RequestFastSync();
        }

        /// <summary>
        /// Called by external scripts when the club has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            if (playerManager == null)
                return;

            playerManager.ClubVisible = true;
            playerManager.RequestSync(syncNow: true);

            if (playerManager.openPutt == null || playerManager.openPutt.rightShoulderPickup == null)
                return;

            PickupHelper shoulderPickup = playerManager.openPutt.rightShoulderPickup.GetComponent<PickupHelper>();
            if (shoulderPickup != null)
                currentHandFromScript = shoulderPickup.CurrentHand;

            UpdateClubState();
        }

        /// <summary>
        /// Called by external scripts when the club has been dropped
        /// </summary>
        public void OnScriptDrop()
        {
            currentHandFromScript = VRCPickup.PickupHand.None;

            if (playerManager != null)
            {
                playerManager.ClubVisible = true;
                playerManager.RequestSync();
            }

            UpdateClubState();
        }

    }
}