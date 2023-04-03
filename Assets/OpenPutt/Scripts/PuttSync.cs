using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PuttSync : UdonSharpBehaviour
    {
        #region Public Settings
        [Header("Sync Settings")]
        [Range(0, 1f), Tooltip("How long the object should keep syncing fast for after requesting a fast sync")]
        public float fastSyncTimeout = 0.5f;

        [Tooltip("This lets you define a curve to scale back the speed of fast updates based on the number on players in the instance. You can leave this empty and a default curve will be applied when the game loads")]
        public AnimationCurve fastSyncIntervalCurve;

        [Tooltip("Will periodically try to send a sync in the background every 3-10 seconds. Off by default as build 1258 should solve later joiner issues (Will only actually sync if this.transform has been touched)")]
        public bool enableSlowSync = false;

        [Tooltip("Experimental - Reduces network traffic by syncing")]
        public bool disableSyncWhileHeld = true;

        [Tooltip("If enabled PuttSync will go to sleep while it is idle to save resources")]
        public bool autoDisableWhileIdle = true;

        [Header("Pickup Settings")]
        [Tooltip("If enabled PuttSync will operate similar to VRC Object Sync")]
        public bool syncPositionAndRot = true;
        [Tooltip("Monitors a VRCPickup on the same GameObject. When it is picked up by a player fast syncs will be enabled automatically.")]
        public bool monitorPickupEvents = true;
        [Tooltip("Should this object be returned to its spawn position after players let go of it")]
        public bool returnAfterDrop = false;
        [Range(2f, 30f), Tooltip("If ReturnAfterDrop is enabled this object will be put back into its original position after this many seconds of not being held")]
        public float returnAfterDropTime = 10f;
        [Tooltip("Should the object be respawned if it goes below the height specified below?")]
        public bool autoRespawn = true;
        [Tooltip("The minimum height that this object can go to before being respawned (if enabled)")]
        public float autoRespawnHeight = -100f;
        public bool canManagePickupable = true;
        public bool canManageRigidbodyState = true;
        #endregion

        #region Private References
        private Rigidbody objectRB;
        private VRCPickup pickup;
        #endregion

        #region Synced Vars
        /// <summary>
        /// The last known position of this object from the network (synced between players)
        /// </summary>
        [UdonSynced]
        private Vector3 syncPosition;
        /// <summary>
        /// The last known rotation of this object from the network (synced between players)
        /// </summary>
        [UdonSynced]
        private Quaternion syncRotation;
        /// <summary>
        /// The respawn position for this object
        /// </summary>
        private Vector3 originalPosition;
        /// <summary>
        /// The respawn rotation for this object
        /// </summary>
        private Quaternion originalRotation;
        [UdonSynced]
        private int currentOwnerHandInt = (int)VRCPickup.PickupHand.None;
        private VRCPickup.PickupHand currentOwnerHand
        {
            get => (VRCPickup.PickupHand)currentOwnerHandInt;
            set
            {
                currentOwnerHandInt = (int)value;
            }
        }
        [HideInInspector]
        public int currentOwnerHideOverride = 0;
        #endregion

        #region Internal Working Vars
        /// <summary>
        /// A local timer that stores how much time has passed since the owner last tried to sync this object
        /// </summary>
        private float syncTimer = 0f;
        /// <summary>
        /// A local timer that tracks how much longer this object can send fast updates for
        /// </summary>
        private float currentFastSyncTimeout = 0f;
        /// <summary>
        /// Amount of time (in seconds) between fast object syncs for this object (Scaled based on player count using fastSyncIntervalCurve)
        /// </summary>
        private float fastSyncInterval = 0.05f;
        /// <summary>
        /// Amount of time (in seconds) between slow object syncs for this object (Is set to a random value between 3-10 each time)
        /// </summary>
        private float slowSyncInterval = 1f;
        /// <summary>
        /// A local timer that tracks how long since the owner of this object dropped it
        /// </summary>
        private float timeSincePlayerDroppedObject = -1f;
        private bool rigidBodyUseGravity = false;
        private bool rigidBodyisKinematic = false;
        /// <summary>
        /// True if we just received the first network sync for this object
        /// </summary>
        private bool isFirstSync = false;
        /// <summary>
        /// True if this object has had at least 1 network update
        /// </summary>
        private bool hasSynced = false;
        #endregion

        void Start()
        {
            pickup = GetComponent<VRCPickup>();
            objectRB = GetComponent<Rigidbody>();

            originalPosition = this.transform.localPosition;
            originalRotation = this.transform.localRotation;

            syncPosition = this.transform.localPosition;
            syncRotation = this.transform.localRotation;

            if (fastSyncIntervalCurve == null || fastSyncIntervalCurve.length == 0)
            {
                fastSyncIntervalCurve = new AnimationCurve();
                fastSyncIntervalCurve.AddKey(0f, 0.05f);
                fastSyncIntervalCurve.AddKey(20f, 0.05f);
                fastSyncIntervalCurve.AddKey(40f, 0.1f);
                fastSyncIntervalCurve.AddKey(82f, 0.15f);
            }

            fastSyncInterval = fastSyncIntervalCurve.Evaluate(VRCPlayerApi.GetPlayerCount());

            SetEnabled(false);
        }

        void Update()
        {
            if (Networking.LocalPlayer == null) return;

            Transform transform = this.transform;

            if (!Networking.LocalPlayer.IsOwner(gameObject))
            {
                // Disable pickup for other players if theft is disabled
                if (canManagePickupable && pickup != null)
                {
                    bool newPickupState = true;

                    // If somebody else is holding this object disable pickup (when theft is disabled)
                    if (pickup.DisallowTheft && Networking.GetOwner(gameObject) != Networking.LocalPlayer && currentOwnerHand != VRCPickup.PickupHand.None)
                        newPickupState = false;

                    if (newPickupState != pickup.pickupable)
                        pickup.pickupable = newPickupState;

                    bool shouldDropPickup = false;
                    if (Networking.GetOwner(gameObject) != Networking.LocalPlayer)
                        shouldDropPickup = true;
                    if (!newPickupState)
                        shouldDropPickup = true;

                    if (shouldDropPickup && currentOwnerHandInt != (int)VRCPickup.PickupHand.None)
                        pickup.Drop();
                }

                if (canManageRigidbodyState && objectRB != null && !objectRB.isKinematic)
                {
                    // If we are being moved by something else make the rigidbody kinematic
                    objectRB.useGravity = false;
                    objectRB.isKinematic = true;
                }

                if (syncPositionAndRot)
                {
                    // Attach this object to the players hand if they are currently holding it
                    if (disableSyncWhileHeld && currentOwnerHand != VRCPickup.PickupHand.None)
                    {
                        // In this case syncPosition/syncRotation will be offsets from the owners hand bone
                        VRCPlayerApi owner = Networking.GetOwner(gameObject);

                        // Get the world space position/rotation of the hand that is holding this object
                        HumanBodyBones currentTrackedBone = currentOwnerHand == VRCPickup.PickupHand.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

                        Vector3 handPosition = owner.GetBonePosition(currentTrackedBone);
                        Quaternion handRotation = owner.GetBoneRotation(currentTrackedBone);

                        // If the offset shave changed, try to lerp them?
                        GetPickupHandOffsets(owner, out Vector3 currentOwnerHandOffset, out Quaternion currentOwnerHandOffsetRotation);

                        Vector3 oldPos = transform.position;

                        Vector3 newPos = handPosition + transform.TransformDirection(syncPosition);
                        Quaternion newOffsetRot = handRotation * syncRotation;
                        if ((currentOwnerHandOffset - syncPosition).magnitude > 0.1f)
                            newPos = Vector3.Lerp(oldPos, handPosition + transform.TransformDirection(syncPosition), 1.0f - Mathf.Pow(0.000001f, Time.deltaTime));

                        if (Quaternion.Angle(transform.rotation, syncRotation) > 1f)
                        {
                            float lerpProgress = 1.0f - Mathf.Pow(0.000001f, Time.deltaTime);
                            newOffsetRot = Quaternion.Slerp(transform.rotation, newOffsetRot, lerpProgress);
                        }

                        transform.position = newPos;
                        transform.rotation = newOffsetRot;
                    }
                    else if ((transform.localPosition - syncPosition).magnitude > 0.001f || Quaternion.Angle(transform.localRotation, syncRotation) > 0.01f)
                    {
                        Vector3 newPosition = syncPosition;
                        Quaternion newRotation = syncRotation;

                        if (!isFirstSync && hasSynced)
                        {
                            // Try to smooth out the lerps
                            //float lerpProgress = Time.deltaTime / fastSyncInterval;
                            float lerpProgress = 1.0f - Mathf.Pow(0.001f, Time.deltaTime);

                            // Lerp the object to it's current position
                            newPosition = Vector3.Lerp(transform.localPosition, syncPosition, lerpProgress);
                            newRotation = Quaternion.Slerp(transform.localRotation, syncRotation, lerpProgress);
                        }

                        isFirstSync = false;

                        // Move this object
                        transform.localPosition = newPosition;
                        transform.localRotation = newRotation;
                    }
                    else
                    {
                        SetEnabled(false);
                    }
                }
                else
                {
                    SetEnabled(false);
                }
                return;
            }

            bool shouldStayEnabled = false;

            VRCPickup.PickupHand handStateThisFrame = pickup != null ? pickup.currentHand : VRC_Pickup.PickupHand.None;
            if (currentOwnerHideOverride > 0)
                handStateThisFrame = (VRCPickup.PickupHand)currentOwnerHideOverride;

            // If the pickup state has changed and the player is no longer holding this object - tell other clients
            if (pickup != null && handStateThisFrame != currentOwnerHand && handStateThisFrame == VRCPickup.PickupHand.None)
            {
                // Send to other clients
                RequestFastSync();
            }

            currentOwnerHand = handStateThisFrame;

            // Enable pickup for the owner
            if (canManagePickupable && pickup != null && !pickup.pickupable)
                pickup.pickupable = true;

            if (autoRespawn && transform.position.y < autoRespawnHeight)
            {
                Respawn();
            }

            if (returnAfterDrop && timeSincePlayerDroppedObject >= 0f)
            {
                if (currentOwnerHandInt != (int)VRCPickup.PickupHand.None)
                {
                    timeSincePlayerDroppedObject = -1f;
                    shouldStayEnabled = true;
                }
                else if (timeSincePlayerDroppedObject < returnAfterDropTime)
                {
                    timeSincePlayerDroppedObject += Time.deltaTime;
                    shouldStayEnabled = true;
                }
                else if (timeSincePlayerDroppedObject >= returnAfterDropTime)
                {
                    Respawn();
                }
            }

            if (monitorPickupEvents && currentOwnerHandInt != (int)VRCPickup.PickupHand.None)
            {
                if (disableSyncWhileHeld)
                {
                    UpdatePickupHandOffsets();
                }
                else
                {
                    syncPosition = transform.localPosition;
                    syncRotation = transform.localRotation;
                    RequestFastSync();
                }
                shouldStayEnabled = true;
            }
            else
            {
                syncPosition = transform.localPosition;
                syncRotation = transform.localRotation;
            }

            // Work out which timer we are currently using (Fast/Slow/None)
            float currentSyncInterval = -1;
            if (currentFastSyncTimeout > 0f)
                currentSyncInterval = fastSyncInterval;
            else if (enableSlowSync)
                currentSyncInterval = slowSyncInterval;

            if (currentSyncInterval >= 0f)
            {
                shouldStayEnabled = true;
                if (syncTimer > currentSyncInterval)
                {
                    // Send a network sync if enough time has passed and the object has moved/rotated (seems to work fine if the parent of this object moves also)
                    if (this.transform.hasChanged || currentFastSyncTimeout > 0f)
                    {
                        RequestSerialization();
                        this.transform.hasChanged = false;
                    }

                    // Reset the timer
                    syncTimer = 0f;

                    // Randomize the slow sync interval each time we do one so they don't all happen at the same time
                    if (currentFastSyncTimeout <= 0f)
                        slowSyncInterval = Random.Range(3f, 10f);
                }

                syncTimer += Time.deltaTime;

                if (currentFastSyncTimeout > 0f)
                {
                    currentFastSyncTimeout -= Time.deltaTime;
                }
            }

            SetEnabled(shouldStayEnabled);
        }

        public override void OnDeserialization()
        {
            if (!hasSynced)
                isFirstSync = true;
            hasSynced = true;

            // Enable PuttSync until we think it needs switching off
            SetEnabled(true);
        }

        public override void OnPickup()
        {
            if (pickup == null || Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid()) return;

            if (monitorPickupEvents)
            {
                bool shouldDropPickup = false;
                if (pickup.DisallowTheft && Networking.GetOwner(gameObject) != Networking.LocalPlayer && currentOwnerHandInt != (int)VRCPickup.PickupHand.None)
                    shouldDropPickup = true;
                if (!pickup.pickupable)
                    shouldDropPickup = true;

                if (shouldDropPickup && (int)pickup.currentHand != (int)VRCPickup.PickupHand.None)
                {
                    pickup.Drop();
                    return;
                }

                Utils.SetOwner(Networking.LocalPlayer, gameObject);

                // Sync which hand the player is holding this object in
                currentOwnerHand = pickup.currentHand;

                syncPosition = Vector3.zero;
                syncRotation = Quaternion.identity;

                // Notify other clients that this player picked the object up
                RequestFastSync();
            }
        }

        public override void OnDrop()
        {
            if (pickup == null || Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid()) return;

            if ((int)currentOwnerHand != (int)VRCPickup.PickupHand.None)
            {
                timeSincePlayerDroppedObject = 0f;
                currentOwnerHand = VRCPickup.PickupHand.None;

                // If we can manage the rigidbody state automatically
                if (objectRB != null && canManageRigidbodyState)
                {
                    // Set the settings back to how they were originally
                    objectRB.useGravity = rigidBodyUseGravity;
                    objectRB.isKinematic = rigidBodyisKinematic;
                }

                if (Networking.LocalPlayer.IsOwner(gameObject))
                {
                    syncPosition = transform.localPosition;
                    syncRotation = transform.localRotation;

                    currentOwnerHand = VRCPickup.PickupHand.None;

                    // Notify other clients about the drop
                    RequestFastSync();
                }
            }
        }

        /// <summary>
        /// Called by external scripts when the object has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            OnPickup();
        }

        /// <summary>
        /// Called by external scripts when the object has been dropped
        /// </summary>
        public void OnScriptDrop()
        {
            OnDrop();
        }

        void OnDisable()
        {
            if (Utils.LocalPlayerIsValid() && Networking.LocalPlayer.IsOwner(gameObject))
            {
                OnDrop();
            }
        }

        void OnEnable()
        {
            if (Utils.LocalPlayerIsValid() && Networking.LocalPlayer.IsOwner(gameObject))
            {
                OnDrop();
            }
        }

        /// <summary>
        /// Toggles whether this script is currently running. (Used to save resources while this script isn't actually doing anything)
        /// </summary>
        /// <param name="enabled"></param>
        private void SetEnabled(bool enabled)
        {
            if (autoDisableWhileIdle)
                this.enabled = enabled;
        }

        /// <summary>
        /// Triggers PuttSync to start sending fast position updates for an amount of time (fastSyncTimeout)<br/>
        /// Having this slight delay for stopping lets the sync catch up and show where the object came to stop
        /// </summary>
        public void RequestFastSync()
        {
            currentFastSyncTimeout = fastSyncTimeout;

            // Enable PuttSync until we think it needs switching off
            SetEnabled(true);
        }

        /// <summary>
        /// Resets the position of this object to it's original position/rotation and sends a sync (Only the owner can perform this!)
        /// </summary>
        public void Respawn()
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid() || !Networking.LocalPlayer.IsOwner(gameObject))
                return;

            timeSincePlayerDroppedObject = -1f;

            // Tell player to drop the object if they're holding it
            if (pickup != null)
                pickup.Drop();

            // Freeze this object at it's spawn point
            if (objectRB != null)
            {
                objectRB.velocity = Vector3.zero;
                objectRB.angularVelocity = Vector3.zero;
                if (canManageRigidbodyState)
                {
                    objectRB.useGravity = false;
                    objectRB.isKinematic = true;
                }
            }

            this.transform.localPosition = originalPosition;
            this.transform.localRotation = originalRotation;

            RequestFastSync();
        }

        /// <summary>
        /// Sets the respawn position/rotation for this object and tells all other clients about the change (Only the owner can perform this!)
        /// </summary>
        /// <param name="position">The new spawn position in world space</param>
        /// <param name="rotation">The new spawn rotation for this object</param>
        public void SetSpawnPosition(Vector3 position, Quaternion rotation)
        {
            if (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid() || !Networking.LocalPlayer.IsOwner(gameObject))
                return;


            originalPosition = this.transform.InverseTransformPoint(position);
            originalRotation = Quaternion.Inverse(transform.rotation) * rotation;

            RequestFastSync();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            fastSyncInterval = fastSyncIntervalCurve.Evaluate(VRCPlayerApi.GetPlayerCount());
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            fastSyncInterval = fastSyncIntervalCurve.Evaluate(VRCPlayerApi.GetPlayerCount());
        }

        private void UpdatePickupHandOffsets()
        {
            // Cache old values so we can check for changes that need syncing
            Vector3 oldOffset = syncPosition;
            Quaternion oldRotationOffset = syncRotation;

            GetPickupHandOffsets(Networking.LocalPlayer, out Vector3 newOwnerHandOffset, out Quaternion newOwnerHandOffsetRotation);

            float offsetPosDiff = (oldOffset - newOwnerHandOffset).magnitude;
            float offsetRotDiff = Quaternion.Angle(oldRotationOffset, newOwnerHandOffsetRotation);

            this.syncPosition = newOwnerHandOffset;
            this.syncRotation = newOwnerHandOffsetRotation;

            // If the offsets from the players hand change - send a sync
            if (offsetPosDiff > 0.1f || offsetRotDiff > 1f)
            {
                //Utils.Log(this, $"Sending update for offset change {offsetPosDiff} {offsetRotDiff}");
                RequestFastSync();
            }
        }

        private void GetPickupHandOffsets(VRCPlayerApi player, out Vector3 posOffset, out Quaternion rotOffset)
        {
            HumanBodyBones currentTrackedBone = currentOwnerHand == VRCPickup.PickupHand.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

            Vector3 handPosition = player.GetBonePosition(currentTrackedBone);
            Quaternion handRotation = player.GetBoneRotation(currentTrackedBone);

            // Work out new offsets from the players hand
            posOffset = Vector3.Scale(-transform.InverseTransformPoint(handPosition), transform.localScale);
            rotOffset = Quaternion.Inverse(handRotation) * transform.rotation;
        }
    }
}