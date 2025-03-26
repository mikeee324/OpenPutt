using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(999)]
    public class OpenPuttSync : UdonSharpBehaviour
    {
        #region Public Settings

        [Header("Sync Settings")] [Range(0, 1f), Tooltip("How long the object should keep syncing fast for after requesting a fast sync")]
        public float fastSyncTimeout = 0.25f;

        [Tooltip("This lets you define a curve to scale back the speed of fast updates based on the number on players in the instance. You can leave this empty and a default curve will be applied when the game loads")]
        public AnimationCurve fastSyncIntervalCurve;

        [Tooltip("This defines how often this often will be updated for remote players (in seconds) based on how far away they are from this GameObject. You can leave this empty and a default curve will be applied when the game loads")]
        public AnimationCurve remoteUpdateDistanceCurve;

        [Range(0.001f, 0.05f), Tooltip("Approximately the time it will take to catch up with the position of remote objects. A smaller value will reach the target faster.")]
        public float remoteUpdateSmoothTime = 0.02f;

        [Tooltip("Experimental - Reduces network traffic by syncing")]
        public bool disableSyncWhileHeld = true;

        [Header("Pickup Settings")] [Tooltip("If enabled PuttSync will operate similar to VRC Object Sync")]
        public bool syncPositionAndRot = true;

        [Tooltip("Monitors a VRCPickup on the same GameObject. When it is picked up by a player fast syncs will be enabled automatically.")]
        public bool monitorPickupEvents = true;

        [Tooltip("Should this object be returned to its spawn position after players let go of it")]
        public bool returnAfterDrop;

        [Range(2f, 300f), Tooltip("If ReturnAfterDrop is enabled this object will be put back into its original position after this many seconds of not being held")]
        public float returnAfterDropTime = 10f;

        [Tooltip("Designate a script that is being called when object is returned. Calls the remote function written in the box below. ")]
        public UdonSharpBehaviour returnListener;

        public string remoteReturnFunction = "ReturnFunction";

        [Tooltip("Should the object be respawned if it goes below the height specified below?")]
        public bool autoRespawn = true;

        [Tooltip("The minimum height that this object can go to before being respawned (if enabled)")]
        public float autoRespawnHeight = -100f;

        public bool canManagePickupable = true;

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
        /// The respawn position for this object in world space
        /// </summary>
        public Vector3 originalPosition;

        /// <summary>
        /// The respawn rotation for this object in world space
        /// </summary>
        private Quaternion originalRotation;

        [UdonSynced]
        private int currentOwnerHandInt = (int)VRC_Pickup.PickupHand.None;

        private VRC_Pickup.PickupHand currentOwnerHand
        {
            get => (VRC_Pickup.PickupHand)currentOwnerHandInt;
            set => currentOwnerHandInt = (int)value;
        }

        public bool grabOriginalPosOnStart = true;

        #endregion

        #region Internal Working Vars

        private bool isBeingHeldByExternalScript;

        /// <summary>
        /// Amount of time (in seconds) between fast object syncs for this object (Scaled based on player count using fastSyncIntervalCurve)
        /// </summary>
        private float fastSyncInterval = 0.05f;

        private float fastSyncStopTime = -1f;
        private float returnAfterDropEndTime = -1f;

        /// <summary>
        /// True if we just received the first network sync for this object
        /// </summary>
        private bool isFirstSync;

        /// <summary>
        /// True if this object has had at least 1 network update
        /// </summary>
        private bool hasSynced;

        /// <summary>
        /// Keeps track of whether PuttSync is already monitoring remote updates and syncing positions etc
        /// </summary>
        private bool isHandlingRemoteUpdates;

        /// <summary>
        /// Used to allow a sync in case some extra data changed (e.g. player changed which hand they hold the pickup with)
        /// </summary>
        private bool extraDataChanged;

        private bool firstEnable = true;

        private VRCPlayerApi localPlayer;
        private float lastKnownDistanceUpdateValue;
        private Vector3 lastSyncVelocity = Vector3.zero;
        private Vector3 lastSyncPos = Vector3.zero;
        private Quaternion lastSyncRot = Quaternion.identity;

        #endregion

        void Start()
        {
            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();
            if (!Utilities.IsValid(objectRB))
                objectRB = GetComponent<Rigidbody>();

            if (grabOriginalPosOnStart)
            {
                originalPosition = transform.position;
                originalRotation = transform.rotation;
            }

            syncPosition = transform.localPosition;
            syncRotation = transform.localRotation;

            if (!Utilities.IsValid(fastSyncIntervalCurve) || fastSyncIntervalCurve.length == 0)
            {
                fastSyncIntervalCurve = new AnimationCurve();
                fastSyncIntervalCurve.AddKey(0f, 0.03f);
                fastSyncIntervalCurve.AddKey(10f, 0.03f);
                fastSyncIntervalCurve.AddKey(20f, 0.1f);
                fastSyncIntervalCurve.AddKey(82f, 1f);
            }

            if (!Utilities.IsValid(remoteUpdateDistanceCurve) || remoteUpdateDistanceCurve.length == 0)
            {
                remoteUpdateDistanceCurve = new AnimationCurve();
                remoteUpdateDistanceCurve.AddKey(0f, 0);
                remoteUpdateDistanceCurve.AddKey(30f, 0);
                remoteUpdateDistanceCurve.AddKey(100f, 1f);
                remoteUpdateDistanceCurve.AddKey(200f, 5f);
            }

            fastSyncInterval = fastSyncIntervalCurve.Evaluate(VRCPlayerApi.GetPlayerCount());
        }

        /// <summary>
        /// This function is where the data is gathered and then sent if needed
        /// </summary>
        public void HandleSendSync()
        {
            if (isHandlingRemoteUpdates)
            {
                fastSyncStopTime = -1;
                return;
            }

            // If the owner sees the object go underneath the respawn height
            if (autoRespawn && transform.position.y < autoRespawnHeight)
            {
                // The master will respawn the object
                if (originalPosition.y < autoRespawnHeight)
                    originalPosition = new Vector3(originalPosition.x, autoRespawnHeight, originalPosition.z);

                Respawn();
            }

            // Sync local pos/rot
            syncPosition = transform.localPosition;
            syncRotation = transform.localRotation;

            // If player is holding this object and PuttSync is tracking pickup stuff
            if (monitorPickupEvents && currentOwnerHandInt != (int)VRC_Pickup.PickupHand.None)
            {
                if (disableSyncWhileHeld)
                    UpdatePickupHandOffsets(); // Attaches the object to local space of the players hand on remote clients
                else
                    RequestFastSync(); // Just syncs the position/rotation normally
            }

            // Send a network sync if enough time has passed and the object has moved/rotated (seems to work fine if the parent of this object moves also)
            if (transform.hasChanged || extraDataChanged || !monitorPickupEvents)
            {
                RequestSerialization();
                transform.hasChanged = false;
                extraDataChanged = false;
            }

            // If we still have time left to sync, schedule in the next sync
            if (fastSyncStopTime > Time.timeSinceLevelLoad)
            {
                SendCustomEventDelayedSeconds(nameof(HandleSendSync), fastSyncInterval);
            }
            else
            {
                fastSyncStopTime = -1f;
            }
        }

        /// <summary>
        /// This handles syncing the positions up and smoothing the mnotion for remote players
        /// </summary>
        public void HandleRemoteUpdate()
        {
            var owner = Networking.GetOwner(gameObject);
            if (owner == Networking.LocalPlayer)
            {
                isHandlingRemoteUpdates = false;
                return;
            }

            // Disable pickup for other players if theft is disabled
            if (canManagePickupable && Utilities.IsValid(pickup))
            {
                var newPickupState = true;

                // If somebody else is holding this object disable pickup (when theft is disabled)
                if (pickup.DisallowTheft && currentOwnerHandInt != (int)VRC_Pickup.PickupHand.None)
                    newPickupState = false;

                if (newPickupState != pickup.pickupable)
                    pickup.pickupable = newPickupState;

                var shouldDropPickup = false;
                if (!newPickupState)
                    shouldDropPickup = true;
                if (pickup.currentHand != VRC_Pickup.PickupHand.None)
                    shouldDropPickup = true;

                if (shouldDropPickup)
                    pickup.Drop();
            }

            if (syncPositionAndRot)
            {
                // Attach this object to the players hand if they are currently holding it
                if (disableSyncWhileHeld && currentOwnerHandInt != (int)VRC_Pickup.PickupHand.None)
                {
                    // This ends up working in world space instead of local space (it looks better)
                    var currentTrackedBone = currentOwnerHand == VRC_Pickup.PickupHand.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

                    var handPosition = owner.GetBonePosition(currentTrackedBone);
                    var handRotation = owner.GetBoneRotation(currentTrackedBone);

                    var newPosition = handPosition + syncPosition;
                    var newOffsetRot = handRotation * syncRotation;

                    if (Utilities.IsValid(localPlayer))
                    {
                        var t = !Utilities.IsValid(transform.parent) ? transform : transform.parent.transform;

                        var distance = Vector3.Distance(newPosition, localPlayer.GetPosition());

                        lastKnownDistanceUpdateValue = Mathf.Clamp(remoteUpdateDistanceCurve.Evaluate(distance), 0f, 30f);
                    }

                    // If offsets have changed enough then smooth out the movement
                    if (!isFirstSync && hasSynced && lastKnownDistanceUpdateValue <= 0f)
                    {
                        if (Vector3.Distance(lastSyncPos, syncPosition) > .01f || Quaternion.Dot(lastSyncRot, syncRotation) < .999f)
                        {
                            newPosition = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * 15f);
                            newOffsetRot = Quaternion.Slerp(transform.rotation, newOffsetRot, Time.deltaTime * 15f);
                        }

                        // When we get super close to the target offset - log this as the last set of offsets so we can stop smoothing things
                        if (Vector3.Distance(transform.position, newPosition) < .001f && Quaternion.Dot(transform.rotation, newOffsetRot) > .9999f)
                        {
                            lastSyncPos = syncPosition;
                            lastSyncRot = syncRotation;
                        }
                    }

                    transform.SetPositionAndRotation(newPosition, newOffsetRot);

                    // Run this for the next frame too
                    SendCustomEventDelayedSeconds(nameof(HandleRemoteUpdate), lastKnownDistanceUpdateValue);

                    return;
                }

                if ((transform.localPosition - syncPosition).magnitude > 0.001f || Quaternion.Dot(transform.localRotation, syncRotation) < 0.999f)
                {
                    var newPosition = syncPosition;
                    var newRotation = syncRotation;

                    if (Utilities.IsValid(localPlayer))
                    {
                        var t = !Utilities.IsValid(transform.parent) ? transform : transform.parent.transform;

                        var distance = Vector3.Distance(t.TransformPoint(newPosition), localPlayer.GetPosition());

                        lastKnownDistanceUpdateValue = Mathf.Clamp(remoteUpdateDistanceCurve.Evaluate(distance), 0f, 30f);
                    }

                    // If we're allowed to smooth the movement (If object is far away then we should just snap to where we last saw it)
                    if (!isFirstSync && hasSynced && lastKnownDistanceUpdateValue == 0f)
                    {
                        // Try to smooth out the lerps
                        var lerpProgress = 1.0f - Mathf.Pow(0.001f, Time.deltaTime);

                        // Lerp the object to it's current position
                        newPosition = Vector3.SmoothDamp(transform.localPosition, syncPosition, ref lastSyncVelocity, remoteUpdateSmoothTime);

                        newRotation = Quaternion.Slerp(transform.localRotation, syncRotation, lerpProgress);
                    }

                    // Move this object
                    transform.SetLocalPositionAndRotation(newPosition, newRotation);

                    // Run this for the next frame too
                    SendCustomEventDelayedSeconds(nameof(HandleRemoteUpdate), lastKnownDistanceUpdateValue);

                    return;
                }
            }

            // If we got here we aren't doing anything so stop updating
            isHandlingRemoteUpdates = false;
        }

        public override void OnDeserialization()
        {
            if (!hasSynced)
                isFirstSync = true;
            hasSynced = true;

            if (!isHandlingRemoteUpdates)
            {
                isHandlingRemoteUpdates = true;

                if (!Utilities.IsValid(localPlayer) && OpenPuttUtils.LocalPlayerIsValid())
                    localPlayer = Networking.LocalPlayer;

                HandleRemoteUpdate();
            }

            isFirstSync = false;
        }

        /// <summary>
        /// Is called whenever a player drops this pickup.. if they drop it multiple times quickly this should only respawn once when it receives the last check
        /// </summary>
        public void ReturnAfterDropTimer()
        {
            if (!this.LocalPlayerOwnsThisObject() || returnAfterDropEndTime < 0)
                return;

            // If we haven't gotten past the respawn timer
            if (Time.timeSinceLevelLoad < returnAfterDropEndTime)
            {
                // Schedule another check in the future
                //Utils.Log(this, "Can't check for a respawn yet!");
                SendCustomEventDelayedSeconds(nameof(ReturnAfterDropTimer), 1);
                return;
            }

            // If the player isn't holding this object anymore
            if (pickup.currentHand == VRC_Pickup.PickupHand.None)
            {
                // We can respawn it
                Respawn();
                returnAfterDropEndTime = -1;
            }
        }

        public void UpdatePickupCurrentHand()
        {
            if (!this.LocalPlayerOwnsThisObject() || !Utilities.IsValid(pickup))
                return;

            var handStateThisFrame = pickup.currentHand;
            if (isBeingHeldByExternalScript)
                handStateThisFrame = VRC_Pickup.PickupHand.Right;

            // If the pickup state has changed and the player is no longer holding this object - tell other clients
            if ((int)handStateThisFrame != (int)currentOwnerHand)
            {
                currentOwnerHandInt = (int)handStateThisFrame;

                RequestFastSync(extraDataChanged: true);
            }

            // If we are still holding something - check again soon
            if ((int)currentOwnerHand != (int)VRC_Pickup.PickupHand.None)
                SendCustomEventDelayedSeconds(nameof(UpdatePickupCurrentHand), 1f);
        }

        public override void OnPickup()
        {
            if (!Utilities.IsValid(pickup) || !Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsValid()) return;

            if (monitorPickupEvents)
            {
                returnAfterDropEndTime = -1;

                var shouldDropPickup = false;
                if (pickup.DisallowTheft && !this.LocalPlayerOwnsThisObject() && currentOwnerHandInt != (int)VRC_Pickup.PickupHand.None)
                    shouldDropPickup = true;
                if (!pickup.pickupable)
                    shouldDropPickup = true;

                if (shouldDropPickup && (int)pickup.currentHand != (int)VRC_Pickup.PickupHand.None)
                {
                    if (Utilities.IsValid(pickup))
                        pickup.Drop();
                    return;
                }

                OpenPuttUtils.SetOwner(Networking.LocalPlayer, gameObject);

                currentOwnerHand = VRC_Pickup.PickupHand.None;

                syncPosition = Vector3.zero;
                syncRotation = Quaternion.identity;

                // Keep a track of which hand the player is holding the pickup in
                UpdatePickupCurrentHand();

                SendCustomEventDelayedSeconds(nameof(UpdatePickupHandOffsets), .3f);

                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ForceDrop));
            }
        }

        public void ForceDrop()
        {
            if (this.LocalPlayerOwnsThisObject()) return;
            if (Utilities.IsValid(pickup))
                pickup.Drop();
        }

        public override void OnDrop()
        {
            isBeingHeldByExternalScript = false;

            if (!Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsValid() || !this.LocalPlayerOwnsThisObject()) return;

            if (returnAfterDrop && returnAfterDropEndTime < 0)
            {
                returnAfterDropEndTime = Time.timeSinceLevelLoad + returnAfterDropTime;
                SendCustomEventDelayedSeconds(nameof(ReturnAfterDropTimer), 1);
            }

            syncPosition = transform.localPosition;
            syncRotation = transform.localRotation;

            // Keep a track of which hand the player is holding the pickup in
            UpdatePickupCurrentHand();
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (Networking.GetOwner(gameObject) != Networking.LocalPlayer) return;
            RequestFastSync(true);
        }

        /// <summary>
        /// Called by external scripts when the object has been picked up
        /// </summary>
        public void OnScriptPickup()
        {
            isBeingHeldByExternalScript = true;
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
            if (this.LocalPlayerOwnsThisObject())
            {
                OnDrop();
            }
        }

        void OnEnable()
        {
            if (!firstEnable && this.LocalPlayerOwnsThisObject())
            {
                OnDrop();
            }

            firstEnable = false;
        }

        /// <summary>
        /// Triggers PuttSync to start sending fast position updates for an amount of time (fastSyncTimeout)<br/>
        /// Having this slight delay for stopping lets the sync catch up and show where the object came to stop
        /// </summary>
        public void RequestFastSync(bool extraDataChanged = false)
        {
            if (extraDataChanged)
                this.extraDataChanged = true;

            // If there isn't a sync running already, schedule it in
            if (fastSyncStopTime < 0)
                SendCustomEventDelayedSeconds(nameof(HandleSendSync), fastSyncInterval);

            // Update the stop time for fast updates
            fastSyncStopTime = Time.timeSinceLevelLoad + fastSyncTimeout;
        }

        /// <summary>
        /// Resets the position of this object to it's original position/rotation and sends a sync (Only the owner can perform this!)
        /// </summary>
        public void Respawn()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsValid() || !Networking.LocalPlayer.IsOwner(gameObject))
                return;

            returnAfterDropEndTime = -1;

            // Tell player to drop the object if they're holding it
            if (Utilities.IsValid(pickup))
                pickup.Drop();

            transform.position = originalPosition;
            transform.rotation = originalRotation;

            if (Utilities.IsValid(objectRB))
            {
                objectRB.Sleep();
                if (!objectRB.isKinematic)
                {
                    objectRB.velocity = Vector3.zero;
                    objectRB.angularVelocity = Vector3.zero;

                    objectRB.position = originalPosition;
                    objectRB.rotation = originalRotation;
                }
            }

            if (Utilities.IsValid(returnListener) && Utilities.IsValid(remoteReturnFunction) && remoteReturnFunction.Length > 0)
                returnListener.SendCustomEvent(remoteReturnFunction);

            RequestFastSync();
        }

        public void ResetReturnTimer()
        {
            if (!returnAfterDrop)
                return;

            OpenPuttUtils.SetOwner(Networking.LocalPlayer, gameObject);

            // If there isn't a timer running, start one
            if (returnAfterDropEndTime < 0)
                SendCustomEventDelayedSeconds(nameof(ReturnAfterDropTimer), 1);

            // Update the timer end stop
            returnAfterDropEndTime = Time.timeSinceLevelLoad + returnAfterDropTime;
        }

        /// <summary>
        /// Sets the respawn position/rotation for this object and tells all other clients about the change (Only the owner can perform this!)
        /// </summary>
        /// <param name="position">The new spawn position in world space</param>
        /// <param name="rotation">The new spawn rotation for this object</param>
        public void SetSpawnPosition(Vector3 position, Quaternion rotation)
        {
            if (!Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsValid() || !Networking.LocalPlayer.IsOwner(gameObject))
                return;


            originalPosition = position;
            originalRotation = rotation;

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

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            var localPlayerIsOwner = Networking.LocalPlayer == player;
            var isPickupable = OpenPuttUtils.LocalPlayerIsValid() && localPlayerIsOwner;
            // Enable pickup for the owner
            if (canManagePickupable && Utilities.IsValid(pickup) && !pickup.pickupable)
                pickup.pickupable = isPickupable;
        }

        private void UpdatePickupHandOffsets()
        {
            // Cache old values so we can check for changes that need syncing
            var oldOffset = syncPosition;
            var oldRotationOffset = syncRotation;

            GetPickupHandOffsets(Networking.LocalPlayer, out var newOwnerHandOffset, out var newOwnerHandOffsetRotation);

            var offsetPosDiff = (oldOffset - newOwnerHandOffset).magnitude;
            var offsetRotDiff = Quaternion.Angle(oldRotationOffset, newOwnerHandOffsetRotation);

            syncPosition = newOwnerHandOffset;
            syncRotation = newOwnerHandOffsetRotation;

            // If the offsets from the players hand change - send a sync
            if (offsetPosDiff > 0.1f || offsetRotDiff > 1f)
            {
                //Utils.Log(this, $"Sending update for offset change {offsetPosDiff} {offsetRotDiff}");
                RequestFastSync();
            }
        }

        private void GetPickupHandOffsets(VRCPlayerApi player, out Vector3 posOffset, out Quaternion rotOffset)
        {
            if (!Utilities.IsValid(player) || !player.IsValid())
            {
                posOffset = Vector3.zero;
                rotOffset = Quaternion.identity;
                return;
            }

            var currentTrackedBone = currentOwnerHand == VRC_Pickup.PickupHand.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

            var handPosition = player.GetBonePosition(currentTrackedBone);
            var handRotation = player.GetBoneRotation(currentTrackedBone);

            posOffset = transform.position - handPosition;
            rotOffset = Quaternion.Inverse(handRotation) * transform.rotation;
        }
    }
}