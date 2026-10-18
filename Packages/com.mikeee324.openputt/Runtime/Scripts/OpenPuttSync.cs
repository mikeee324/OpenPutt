using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(999)]
    public class OpenPuttSync : UdonSharpBehaviour
    {
        #region Public Settings

        [OpenPuttDescription("Handles keeping this object's position and rotation synced across the network, and optionally makes it a pickup that can be returned to its start point or respawned if dropped or knocked out of bounds.")]
        [OpenPuttFoldoutGroup("Sync Settings")]
        [Range(0, 1f), Tooltip("How long the object should keep syncing fast for after requesting a fast sync")]
        public float fastSyncTimeout = 0.25f;

        [OpenPuttFoldoutGroup("Sync Settings")]
        [Tooltip("This lets you define a curve to scale back the speed of fast updates based on the number on players in the instance. You can leave this empty and a default curve will be applied when the game loads")]
        public AnimationCurve fastSyncIntervalCurve;

        [OpenPuttFoldoutGroup("Sync Settings")]
        [Tooltip("This defines how often this often will be updated for remote players (in seconds) based on how far away they are from this GameObject. You can leave this empty and a default curve will be applied when the game loads")]
        public AnimationCurve remoteUpdateDistanceCurve;

        [OpenPuttFoldoutGroup("Sync Settings")]
        [Range(0.001f, 0.1f), Tooltip("How far (in metres) remote copies can drift from the real position before the owner sends an update. Lower is more accurate but uses more bandwidth")]
        public float positionTolerance = 0.01f;

        [HideInInspector, Tooltip("No longer used - kept so existing scenes keep their serialized data")]
        public float remoteUpdateSmoothTime = 0.02f;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("If enabled PuttSync will operate similar to VRC Object Sync")]
        public bool syncPositionAndRot = true;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("Should this object be returned to its spawn position after players let go of it")]
        public bool returnAfterDrop;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Range(2f, 300f), Tooltip("If ReturnAfterDrop is enabled this object will be put back into its original position after this many seconds of not being held")]
        public float returnAfterDropTime = 10f;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("Designate a script that is being called when object is returned. Calls the remote function written in the box below. ")]
        public UdonSharpBehaviour returnListener;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("Method name to call on the Return Listener when this object respawns")]
        public string remoteReturnFunction = "";

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("Should the object be respawned if it goes below the height specified below?")]
        public bool autoRespawn = true;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("The minimum height that this object can go to before being respawned (if enabled)")]
        public float autoRespawnHeight = -100f;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("Lets PuttSync toggle the VRCPickup's pickupable state automatically")]
        public bool canManagePickupable = true;

        [OpenPuttFoldoutGroup("Pickup Settings")]
        [Tooltip("Syncs a dynamic rigidbody: the owner simulates it, remote copies are kinematic and follow the owner's velocity, and the next owner carries on from that velocity")]
        public bool syncDynamicRigidbody = false;

        [Tooltip("World-space position this object respawns to")]
        public Vector3 originalPosition;

        [Tooltip("Capture this object's transform as the respawn point on Start")]
        public bool grabOriginalPosOnStart = true;

        [HideInInspector, Tooltip("Hand holding this object for script pickups - set by BodyMountedObject")]
        public int currentOwnerHideOverride;

        #endregion

        #region Constants

        private const int NoHand = 0;
        private const int LeftHand = 1;
        private const int RightHand = 2;
        private const int Head = 3;

        private const float RotationRange = 0.70710678f;
        private const int RotationSteps = 1022;
        private const float MaxVelocity = 64f;
        private const int VelocitySteps = 2097150;
        private const float MaxAngularVelocity = 300f;
        private const int AngularSteps = 1022;

        private const float RotationTolerance = 2f;
        private const float SpinToleranceTime = 0.05f;
        private const float HeldPositionTolerance = 0.1f;
        private const float HeldRotationTolerance = 5f;
        private const float HeldCheckInterval = 0.2f;
        private const float MinHeartbeat = 0.5f;
        private const float SnapDistance = 5f;
        private const float DelayMargin = 0.02f;
        private const float HeldOffsetSharpness = 15f;
        private const int BufferSize = 8;
        private const int TeleportMask = 31;
        private const int AirborneBit = 128;
        private const float GravityTolerance = 0.25f;
        private const float GravitySendInterval = 0.25f;
        private const float MaxGravity = 1000f;

        #endregion

        #region Private References

        private Rigidbody objectRB;
        private VRCPickup pickup;
        private VRCPlayerApi localPlayer;
        private VRCPlayerApi ownerPlayer;

        #endregion

        #region Synced Vars

        /// <summary>
        /// Local position, or the offset from the owner's hand while held
        /// </summary>
        [UdonSynced]
        private Vector3 syncPosition;

        [UdonSynced]
        private uint syncRotation;

        [UdonSynced]
        private ulong syncVelocity;

        [UdonSynced]
        private uint syncAngularVelocity;

        /// <summary>
        /// Bits 0-1 hold hand, bits 2-6 teleport counter, bit 7 airborne
        /// </summary>
        [UdonSynced]
        private byte syncFlags;

        #endregion

        #region Internal Working Vars

        private Quaternion originalRotation = Quaternion.identity;
        private bool startedKinematic;
        private bool firstEnable = true;

        /// <summary>
        /// Amount of time (in seconds) between fast object syncs for this object (Scaled based on player count using fastSyncIntervalCurve)
        /// </summary>
        private float fastSyncInterval = 0.05f;

        // Shared by owner and remotes
        private int holdHand = NoHand;
        private int teleportCounter;

        // Custom gravity as remotes know it, sent by event only when it changes
        private bool customGravityActive;
        private Vector3 customGravity;

        // Owner state
        private bool ownerLoopRunning;
        private bool ownerHolding;
        private bool heldByScript;
        private bool forceNextSync;
        private bool pendingTeleport;
        private float fastSyncStopTime = -1f;
        private float returnTime = -1f;
        private bool returnTimerRunning;
        private Vector3 measuredOffsetPosition;
        private Quaternion measuredOffsetRotation = Quaternion.identity;
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation = Quaternion.identity;
        private Vector3 lastSentVelocity;
        private Vector3 lastSentAngularVelocity;
        private float lastSentTime;
        private bool lastSentHeld;
        private bool lastSentMoving;
        private bool spinTracking;
        private Quaternion lastSpinRotation = Quaternion.identity;
        private Vector3 measuredSpin;
        private bool lastSentAirborne;
        private float lastContactTime = -1f;
        private bool scriptGrounding;
        private bool scriptGrounded;
        private bool wantCustomGravity;
        private Vector3 wantedGravity;
        private bool gravitySendQueued;
        private float lastGravitySendTime = -1f;

        // Remote state
        private bool hasReceived;
        private bool remoteActive;
        private float nextRemoteUpdateTime;
        private float interpolationDelay = 0.1f;
        private Vector3 renderVelocity;
        private Vector3 targetPosition;
        private Quaternion targetRotation = Quaternion.identity;

        // Snapshot buffer, oldest first, times on the local realtimeSinceStartup timeline
        private int bufferCount;
        private float[] bufferTime = new float[BufferSize];
        private Vector3[] bufferPosition = new Vector3[BufferSize];
        private Quaternion[] bufferRotation = new Quaternion[BufferSize];
        private Vector3[] bufferVelocity = new Vector3[BufferSize];
        private Vector3[] bufferAngularVelocity = new Vector3[BufferSize];
        private bool[] bufferAirborne = new bool[BufferSize];

        private Vector3 heldOffsetPosition;
        private Quaternion heldOffsetRotation = Quaternion.identity;

        // Output of GetHandPose
        private Vector3 handPosition;
        private Quaternion handRotation;

        #endregion

        #region Unity Events

        void Start()
        {
            localPlayer = Networking.LocalPlayer;

            if (!Utilities.IsValid(pickup))
                pickup = GetComponent<VRCPickup>();
            if (!Utilities.IsValid(objectRB))
                objectRB = GetComponent<Rigidbody>();

            if (Utilities.IsValid(objectRB))
                startedKinematic = objectRB.isKinematic;

            if (grabOriginalPosOnStart)
                originalPosition = transform.position;
            originalRotation = transform.rotation;

            lastSentPosition = transform.localPosition;
            lastSentRotation = transform.localRotation;
            syncPosition = lastSentPosition;
            syncRotation = PackRotation(lastSentRotation);
            syncVelocity = PackVelocity(Vector3.zero);
            syncAngularVelocity = PackAngularVelocity(Vector3.zero);

            if (fastSyncIntervalCurve == null || fastSyncIntervalCurve.length == 0)
            {
                fastSyncIntervalCurve = new AnimationCurve();
                fastSyncIntervalCurve.AddKey(0f, 0.03f);
                fastSyncIntervalCurve.AddKey(10f, 0.03f);
                fastSyncIntervalCurve.AddKey(20f, 0.1f);
                fastSyncIntervalCurve.AddKey(82f, 1f);
            }

            if (remoteUpdateDistanceCurve == null || remoteUpdateDistanceCurve.length == 0)
            {
                remoteUpdateDistanceCurve = new AnimationCurve();
                remoteUpdateDistanceCurve.AddKey(0f, 0);
                remoteUpdateDistanceCurve.AddKey(100f, 0);
                remoteUpdateDistanceCurve.AddKey(200f, 1f);
                remoteUpdateDistanceCurve.AddKey(500f, 5f);
            }

            UpdateFastSyncInterval();

            ownerPlayer = Networking.GetOwner(gameObject);
            if (!this.LocalPlayerOwnsThisObject())
                SetRemoteKinematic();
        }

        void OnEnable()
        {
            if (!firstEnable && this.LocalPlayerOwnsThisObject())
                OnDrop();

            firstEnable = false;
        }

        void OnDisable()
        {
            if (this.LocalPlayerOwnsThisObject())
                OnDrop();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Wakes the owner loop when something knocks a resting object
            lastContactTime = Time.fixedTime;

            if (RigidbodyIsDynamic() && this.LocalPlayerOwnsThisObject())
                _RequestFastSync();
        }

        private void OnCollisionStay(Collision collision)
        {
            lastContactTime = Time.fixedTime;
        }

        public override void PostLateUpdate()
        {
            if (ownerHolding)
            {
                MeasureHandOffset();
                return;
            }

            if (ownerLoopRunning)
            {
                MeasureSpin();
                return;
            }

            if (!remoteActive)
                return;

            var now = Time.realtimeSinceStartup;
            if (now < nextRemoteUpdateTime)
                return;

            var updateDelay = GetRemoteUpdateDelay();
            nextRemoteUpdateTime = now + updateDelay;
            var smooth = updateDelay <= 0f;

            if (holdHand != NoHand)
                UpdateRemoteHeld(smooth);
            else
                UpdateRemoteFree(now, smooth);
        }

        #endregion

        #region VRChat Events

        public override void OnPickup()
        {
            if (!OpenPuttUtils.LocalPlayerIsValid()) return;

            if (Utilities.IsValid(pickup) && pickup.currentHand != VRC_Pickup.PickupHand.None)
            {
                var stealing = pickup.DisallowTheft && holdHand != NoHand && !this.LocalPlayerOwnsThisObject();
                if (!pickup.pickupable || stealing)
                {
                    pickup.Drop();
                    return;
                }
            }

            returnTime = -1f;

            OpenPuttUtils.SetOwner(localPlayer, gameObject);

            _RequestFastSync(forceSync: true);
        }

        public override void OnDrop()
        {
            heldByScript = false;

            if (!this.LocalPlayerOwnsThisObject()) return;

            if (returnAfterDrop && returnTime < 0)
                StartReturnTimer();

            _RequestFastSync(forceSync: true);
        }

        public override void OnDeserialization(DeserializationResult result)
        {
            if (this.LocalPlayerOwnsThisObject()) return;

            var now = Time.realtimeSinceStartup;
            var sendTime = Mathf.Min(result.sendTime, now);

            // Render far enough behind to cover latency plus one owner tick
            var wantedDelay = Mathf.Clamp(now - sendTime, 0f, 1f) + fastSyncInterval + DelayMargin;
            interpolationDelay = wantedDelay > interpolationDelay ? wantedDelay : Mathf.Lerp(interpolationDelay, wantedDelay, 0.05f);

            var flags = (int)syncFlags;
            var newHand = flags & 3;
            var newTeleport = (flags >> 2) & TeleportMask;
            var airborne = (flags & AirborneBit) != 0;
            var snap = !hasReceived || newTeleport != teleportCounter;
            var handChanged = newHand != holdHand;
            var released = handChanged && newHand == NoHand && hasReceived;

            hasReceived = true;
            holdHand = newHand;
            teleportCounter = newTeleport;

            targetPosition = syncPosition;
            targetRotation = UnpackRotation(syncRotation);
            var velocity = UnpackVelocity(syncVelocity);
            var angularVelocity = UnpackAngularVelocity(syncAngularVelocity);

            ownerPlayer = Networking.GetOwner(gameObject);

            SetRemoteKinematic();

            if (handChanged)
                ApplyPickupState();

            if (!syncPositionAndRot)
                return;

            nextRemoteUpdateTime = 0f;
            remoteActive = true;

            if (holdHand != NoHand)
            {
                bufferCount = 0;
                if (handChanged || snap)
                {
                    heldOffsetPosition = targetPosition;
                    heldOffsetRotation = targetRotation;
                }

                return;
            }

            if (handChanged || snap)
                bufferCount = 0;

            if (bufferCount > 0)
            {
                var last = bufferCount - 1;

                // Stale or out of order
                if (sendTime <= bufferTime[last])
                    return;

                if (Vector3.Distance(bufferPosition[last], targetPosition) > SnapDistance)
                {
                    bufferCount = 0;
                }
                else if (bufferVelocity[last] == Vector3.zero && bufferAngularVelocity[last] == Vector3.zero && sendTime - bufferTime[last] > fastSyncInterval)
                {
                    // It was at rest, so start moving one tick before this packet rather than across the whole idle gap
                    MoveSnapshot(last, 0);
                    bufferCount = 1;
                    bufferTime[0] = sendTime - fastSyncInterval;
                }
            }

            // Leave the hand already at throw speed instead of popping to the drop point
            if (released)
                AddSnapshot(now - interpolationDelay, transform.localPosition, transform.localRotation, velocity, angularVelocity, false);

            AddSnapshot(sendTime, targetPosition, targetRotation, velocity, angularVelocity, airborne);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            ownerPlayer = player;

            if (this.LocalPlayerOwnsThisObject())
            {
                remoteActive = false;

                if (syncDynamicRigidbody && Utilities.IsValid(objectRB) && !startedKinematic)
                {
                    objectRB.isKinematic = false;

                    // Carry on from the last owner's motion
                    if (hasReceived && holdHand == NoHand && bufferCount > 0)
                    {
                        objectRB.velocity = ToWorldVector(renderVelocity);
                        objectRB.angularVelocity = ToWorldDirection(bufferAngularVelocity[bufferCount - 1]);
                    }
                }

                // Reset together or UpdateHoldHand never notices the hand is empty
                holdHand = NoHand;
                ownerHolding = false;
                bufferCount = 0;
                wantCustomGravity = customGravityActive;
                wantedGravity = customGravity;
                lastSentAirborne = false;
                lastSentPosition = transform.localPosition;
                lastSentRotation = transform.localRotation;
                lastSentVelocity = Vector3.zero;
                lastSentAngularVelocity = Vector3.zero;
                lastSentHeld = false;
                lastSentMoving = false;

                _RequestFastSync(forceSync: true);
            }
            else
            {
                ownerHolding = false;
                SetRemoteKinematic();
            }

            ApplyPickupState();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            UpdateFastSyncInterval();

            // Late joiners missed the gravity event
            if (customGravityActive && this.LocalPlayerOwnsThisObject())
                SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(SetRemoteGravity), true, customGravity);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            UpdateFastSyncInterval();
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (this.LocalPlayerOwnsThisObject())
                _RequestFastSync(forceSync: true);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts sending updates for fastSyncTimeout seconds. forceSync guarantees at least one is sent
        /// </summary>
        public void _RequestFastSync(bool forceSync = false)
        {
            if (forceSync)
                forceNextSync = true;

            fastSyncStopTime = Time.time + fastSyncTimeout;

            if (ownerLoopRunning)
                return;

            ownerLoopRunning = true;
            spinTracking = false;
            measuredSpin = Vector3.zero;
            SendCustomEventDelayedFrames(nameof(_OwnerTick), 1);
        }

        /// <summary>
        /// Puts this object back at its spawn point and tells remotes to snap there (owner only)
        /// </summary>
        public void _Respawn()
        {
            if (!this.LocalPlayerOwnsThisObject())
                return;

            if (Utilities.IsValid(pickup))
                pickup.Drop();

            returnTime = -1f;

            if (autoRespawn && originalPosition.y < autoRespawnHeight)
                originalPosition = new Vector3(originalPosition.x, autoRespawnHeight, originalPosition.z);

            transform.SetPositionAndRotation(originalPosition, originalRotation);

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

            if (Utilities.IsValid(returnListener) && !string.IsNullOrEmpty(remoteReturnFunction))
                returnListener.SendCustomEvent(remoteReturnFunction);

            _FlagDiscontinuity();
        }

        /// <summary>
        /// Tells remotes to snap to the next update instead of smoothing to it (owner only)
        /// </summary>
        public void _FlagDiscontinuity()
        {
            if (!this.LocalPlayerOwnsThisObject())
                return;

            teleportCounter = (teleportCounter + 1) & TeleportMask;
            pendingTeleport = true;
            _RequestFastSync(forceSync: true);
        }

        /// <summary>
        /// Sets the local respawn position/rotation for this object (owner only, not synced)
        /// </summary>
        public void _SetSpawnPosition(Vector3 position, Quaternion rotation)
        {
            if (!this.LocalPlayerOwnsThisObject())
                return;

            originalPosition = position;
            originalRotation = rotation;

            _RequestFastSync();
        }

        public void _ResetReturnTimer()
        {
            if (!returnAfterDrop)
                return;

            OpenPuttUtils.SetOwner(localPlayer, gameObject);

            StartReturnTimer();
        }

        /// <summary>
        /// Called by external scripts when the object has been picked up
        /// </summary>
        public void _OnScriptPickup()
        {
            heldByScript = true;
            OnPickup();
        }

        /// <summary>
        /// Called by external scripts when the object has been dropped
        /// </summary>
        public void _OnScriptDrop()
        {
            OnDrop();
        }

        [NetworkCallable(maxEventsPerSecond: 5)]
        public void ForceDrop()
        {
            if (this.LocalPlayerOwnsThisObject()) return;
            if (Utilities.IsValid(pickup))
                pickup.Drop();
        }

        /// <summary>
        /// World-space gravity for scripts that apply their own instead of Rigidbody.useGravity (owner only)
        /// </summary>
        public void _SetGravity(Vector3 gravity)
        {
            wantCustomGravity = true;
            wantedGravity = gravity;
            QueueGravitySend();
        }

        /// <summary>
        /// Goes back to Physics.gravity when Rigidbody.useGravity is on, otherwise none (owner only)
        /// </summary>
        public void _ClearGravity()
        {
            wantCustomGravity = false;
            QueueGravitySend();
        }

        /// <summary>
        /// Lets a script that knows when it's on the ground replace the collision contact check
        /// </summary>
        public void _SetGrounded(bool grounded)
        {
            scriptGrounding = true;
            scriptGrounded = grounded;
        }

        [NetworkCallable(maxEventsPerSecond: 10)]
        public void SetRemoteGravity(bool active, Vector3 gravity)
        {
            if (this.LocalPlayerOwnsThisObject()) return;

            // NaN fails this too
            if (!(gravity.sqrMagnitude <= MaxGravity * MaxGravity)) return;

            customGravityActive = active;
            customGravity = active ? gravity : Vector3.zero;
        }

        #endregion

        #region Owner

        public void _OwnerTick()
        {
            if (!this.LocalPlayerOwnsThisObject())
            {
                ownerLoopRunning = false;
                ownerHolding = false;
                return;
            }

            if (autoRespawn && transform.position.y < autoRespawnHeight)
                _Respawn();

            UpdateHoldHand();

            var now = Time.realtimeSinceStartup;
            var keepRunning = ownerHolding || Time.time < fastSyncStopTime || RigidbodyIsAwake();

            // Always send a final at-rest packet so remotes stop extrapolating
            var send = forceNextSync || (!keepRunning && lastSentMoving);
            if (!send)
            {
                if (ownerHolding)
                    send = now - lastSentTime >= HeldCheckInterval && HeldOffsetChanged();
                else
                    send = PredictionDrifted(now) || (lastSentMoving && now - lastSentTime >= GetHeartbeat());
            }

            forceNextSync = false;

            if (send)
                RequestSend();

            if (keepRunning)
                SendCustomEventDelayedSeconds(nameof(_OwnerTick), fastSyncInterval);
            else
                ownerLoopRunning = false;
        }

        private void RequestSend()
        {
#if !OPENPUTT_DEMO_MODE
            RequestSerialization();
#endif
        }

        /// <summary>
        /// Captures state at send time so the packet's sendTime matches its data
        /// </summary>
        public override void OnPreSerialization()
        {
            if (!this.LocalPlayerOwnsThisObject())
                return;

            var now = Time.realtimeSinceStartup;
            var moving = RigidbodyIsAwake() || Time.time < fastSyncStopTime;
            var velocity = Vector3.zero;
            var angularVelocity = Vector3.zero;
            Quaternion rotation;

            if (ownerHolding)
            {
                syncPosition = measuredOffsetPosition;
                rotation = measuredOffsetRotation;
            }
            else
            {
                syncPosition = transform.localPosition;
                rotation = transform.localRotation;

                if (moving && !pendingTeleport)
                {
                    if (RigidbodyIsDynamic())
                    {
                        velocity = ToLocalVector(objectRB.velocity);
                        angularVelocity = ToLocalDirection(objectRB.angularVelocity);
                    }
                    else if (!lastSentHeld && now > lastSentTime && now - lastSentTime < GetHeartbeat() * 2f)
                    {
                        velocity = (syncPosition - lastSentPosition) / (now - lastSentTime);
                    }

                    // Rotation driven by a script (or frozen rigidbody rotation) doesn't show up on the rigidbody
                    if (angularVelocity.sqrMagnitude < 0.0001f)
                        angularVelocity = measuredSpin;
                }
            }

            syncRotation = PackRotation(rotation);
            syncVelocity = PackVelocity(velocity);
            syncAngularVelocity = PackAngularVelocity(angularVelocity);
            var airborne = !ownerHolding && moving && !pendingTeleport && RigidbodyIsDynamic() && IsAirborne();
            syncFlags = (byte)(holdHand | (teleportCounter << 2) | (airborne ? AirborneBit : 0));

            // Track what remotes will actually see after quantising
            lastSentPosition = syncPosition;
            lastSentRotation = UnpackRotation(syncRotation);
            lastSentVelocity = UnpackVelocity(syncVelocity);
            lastSentAngularVelocity = UnpackAngularVelocity(syncAngularVelocity);
            lastSentTime = now;
            lastSentHeld = ownerHolding;
            lastSentAirborne = airborne;
            lastSentMoving = lastSentVelocity != Vector3.zero || lastSentAngularVelocity != Vector3.zero;
            pendingTeleport = false;
        }

        private bool PredictionDrifted(float now)
        {
            var elapsed = Mathf.Min(now - lastSentTime, GetMaxExtrapolation());

            var predictedPosition = lastSentPosition + lastSentVelocity * elapsed;
            if (lastSentAirborne)
                predictedPosition += GetLocalGravity() * (0.5f * elapsed * elapsed);

            if ((transform.localPosition - predictedPosition).sqrMagnitude > positionTolerance * positionTolerance)
                return true;

            // Fast spin hides rotation error, so allow ~50ms worth of it
            var rotationTolerance = RotationTolerance + lastSentAngularVelocity.magnitude * Mathf.Rad2Deg * SpinToleranceTime;
            var predictedRotation = ExtrapolateRotation(lastSentRotation, lastSentAngularVelocity, elapsed);
            return Quaternion.Angle(transform.localRotation, predictedRotation) > rotationTolerance;
        }

        /// <summary>
        /// Tracks how fast the transform is actually turning, per frame so fast spins don't alias
        /// </summary>
        private void MeasureSpin()
        {
            var rotation = transform.localRotation;
            var dt = Time.deltaTime;

            if (!spinTracking || dt <= 0f)
            {
                spinTracking = true;
                lastSpinRotation = rotation;
                return;
            }

            var delta = rotation * Quaternion.Inverse(lastSpinRotation);
            lastSpinRotation = rotation;

            var angle = 0f;
            var axis = Vector3.zero;
            delta.ToAngleAxis(out angle, out axis);
            if (angle > 180f)
                angle -= 360f;

            var frameSpin = Mathf.Abs(angle) < 0.001f ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / dt);

            // Smooths out frames where physics stepped zero or two times
            measuredSpin = Vector3.Lerp(measuredSpin, frameSpin, 1f - Mathf.Exp(-20f * dt));
        }

        private bool HeldOffsetChanged()
        {
            if (!lastSentHeld)
                return true;

            if ((measuredOffsetPosition - lastSentPosition).sqrMagnitude > HeldPositionTolerance * HeldPositionTolerance)
                return true;

            // VR controllers have constant micro-rotation, so don't gate on rotation there
            return !localPlayer.IsUserInVR() && Quaternion.Angle(measuredOffsetRotation, lastSentRotation) > HeldRotationTolerance;
        }

        private void UpdateHoldHand()
        {
            var hand = NoHand;
            if (heldByScript)
            {
                hand = currentOwnerHideOverride == LeftHand ? LeftHand : RightHand;

                // BodyMountedObject floats script-held objects in front of the head on desktop
                if (Utilities.IsValid(localPlayer) && !localPlayer.IsUserInVR())
                    hand = Head;
            }
            else if (Utilities.IsValid(pickup))
            {
                hand = (int)pickup.currentHand;
            }

            if (hand == holdHand)
                return;

            holdHand = hand;
            ownerHolding = hand != NoHand;
            if (ownerHolding)
                MeasureHandOffset();

            forceNextSync = true;
        }

        private void MeasureHandOffset()
        {
            if (!GetHandPose(localPlayer, holdHand))
                return;

            var inverseHand = Quaternion.Inverse(handRotation);
            measuredOffsetPosition = inverseHand * (transform.position - handPosition);
            measuredOffsetRotation = inverseHand * transform.rotation;
        }

        private void QueueGravitySend()
        {
            if (!this.LocalPlayerOwnsThisObject() || gravitySendQueued || !GravityChanged())
                return;

            var wait = lastGravitySendTime + GravitySendInterval - Time.time;
            if (wait > 0f)
            {
                gravitySendQueued = true;
                SendCustomEventDelayedSeconds(nameof(_SendGravity), wait);
                return;
            }

            _SendGravity();
        }

        public void _SendGravity()
        {
            gravitySendQueued = false;

            if (!this.LocalPlayerOwnsThisObject() || !GravityChanged())
                return;

            // Owner predicts with what remotes have, so continuously changing gravity just falls back to more packets
            customGravityActive = wantCustomGravity;
            customGravity = wantCustomGravity ? wantedGravity : Vector3.zero;
            lastGravitySendTime = Time.time;

            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(SetRemoteGravity), customGravityActive, customGravity);
            _RequestFastSync(forceSync: true);
        }

        private bool GravityChanged()
        {
            if (wantCustomGravity != customGravityActive)
                return true;

            return wantCustomGravity && (wantedGravity - customGravity).sqrMagnitude > GravityTolerance * GravityTolerance;
        }

        private void StartReturnTimer()
        {
            returnTime = Time.time + returnAfterDropTime;

            if (returnTimerRunning)
                return;

            returnTimerRunning = true;
            SendCustomEventDelayedSeconds(nameof(_ReturnAfterDropTimer), returnAfterDropTime);
        }

        public void _ReturnAfterDropTimer()
        {
            if (returnTime < 0 || !this.LocalPlayerOwnsThisObject())
            {
                returnTimerRunning = false;
                return;
            }

            var remaining = returnTime - Time.time;
            if (remaining > 0f)
            {
                SendCustomEventDelayedSeconds(nameof(_ReturnAfterDropTimer), remaining);
                return;
            }

            returnTimerRunning = false;
            returnTime = -1f;

            var held = heldByScript || (Utilities.IsValid(pickup) && pickup.currentHand != VRC_Pickup.PickupHand.None);
            if (!held)
                _Respawn();
        }

        #endregion

        #region Remote

        private void UpdateRemoteFree(float now, bool smooth)
        {
            if (bufferCount == 0)
            {
                remoteActive = false;
                return;
            }

            // Far away objects just show the latest state
            var renderTime = smooth ? now - interpolationDelay : now;

            while (bufferCount > 1 && bufferTime[1] <= renderTime)
                RemoveOldestSnapshot();

            Vector3 position;
            Quaternion rotation;
            var settled = false;

            if (bufferCount > 1 && renderTime > bufferTime[0])
            {
                // Hermite between the snapshots either side, so bounces follow the owner's path
                var span = bufferTime[1] - bufferTime[0];
                var u = Mathf.Clamp01((renderTime - bufferTime[0]) / span);
                position = Hermite(bufferPosition[0], bufferVelocity[0] * span, bufferPosition[1], bufferVelocity[1] * span, u);
                var fromRotation = ExtrapolateRotation(bufferRotation[0], bufferAngularVelocity[0], renderTime - bufferTime[0]);
                var toRotation = ExtrapolateRotation(bufferRotation[1], bufferAngularVelocity[1], renderTime - bufferTime[1]);
                rotation = Quaternion.Slerp(fromRotation, toRotation, u);
            }
            else
            {
                // Nothing newer yet - the owner only goes quiet while its last snapshot still predicts it
                var maxExtrapolation = GetMaxExtrapolation();
                var elapsed = bufferCount > 1 ? 0f : Mathf.Clamp(renderTime - bufferTime[0], 0f, maxExtrapolation);
                position = bufferPosition[0] + bufferVelocity[0] * elapsed;
                if (bufferAirborne[0])
                    position += GetLocalGravity() * (0.5f * elapsed * elapsed);
                rotation = ExtrapolateRotation(bufferRotation[0], bufferAngularVelocity[0], elapsed);

                var stationary = bufferVelocity[0] == Vector3.zero && bufferAngularVelocity[0] == Vector3.zero;
                settled = bufferCount == 1 && renderTime >= bufferTime[0] && (stationary || elapsed >= maxExtrapolation);
            }

            var dt = Time.deltaTime;
            renderVelocity = dt > 0f ? (position - transform.localPosition) / dt : Vector3.zero;

            transform.SetLocalPositionAndRotation(position, rotation);

            if (settled)
            {
                remoteActive = false;
                renderVelocity = Vector3.zero;
            }
        }

        private void AddSnapshot(float time, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity, bool airborne)
        {
            if (bufferCount == BufferSize)
                RemoveOldestSnapshot();

            bufferTime[bufferCount] = time;
            bufferPosition[bufferCount] = position;
            bufferRotation[bufferCount] = rotation;
            bufferVelocity[bufferCount] = velocity;
            bufferAngularVelocity[bufferCount] = angularVelocity;
            bufferAirborne[bufferCount] = airborne;
            bufferCount++;
        }

        private void RemoveOldestSnapshot()
        {
            for (var i = 1; i < bufferCount; i++)
                MoveSnapshot(i, i - 1);

            bufferCount--;
        }

        private void MoveSnapshot(int from, int to)
        {
            bufferTime[to] = bufferTime[from];
            bufferPosition[to] = bufferPosition[from];
            bufferRotation[to] = bufferRotation[from];
            bufferVelocity[to] = bufferVelocity[from];
            bufferAngularVelocity[to] = bufferAngularVelocity[from];
            bufferAirborne[to] = bufferAirborne[from];
        }

        private Vector3 Hermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float u)
        {
            var u2 = u * u;
            var u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * p0 + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * p1 + (u3 - u2) * m1;
        }

        private void UpdateRemoteHeld(bool smooth)
        {
            if (!GetHandPose(ownerPlayer, holdHand))
                return;

            if (smooth)
            {
                var k = 1f - Mathf.Exp(-HeldOffsetSharpness * Time.deltaTime);
                heldOffsetPosition = Vector3.Lerp(heldOffsetPosition, targetPosition, k);
                heldOffsetRotation = Quaternion.Slerp(heldOffsetRotation, targetRotation, k);
            }
            else
            {
                heldOffsetPosition = targetPosition;
                heldOffsetRotation = targetRotation;
            }

            transform.SetPositionAndRotation(handPosition + handRotation * heldOffsetPosition, handRotation * heldOffsetRotation);
        }

        private float GetRemoteUpdateDelay()
        {
            if (!Utilities.IsValid(localPlayer))
                return 0f;

            var distance = Vector3.Distance(transform.position, localPlayer.GetPosition());
            return Mathf.Clamp(remoteUpdateDistanceCurve.Evaluate(distance), 0f, 30f);
        }

        private void SetRemoteKinematic()
        {
            if (syncDynamicRigidbody && Utilities.IsValid(objectRB) && !objectRB.isKinematic)
                objectRB.isKinematic = true;
        }

        private void ApplyPickupState()
        {
            if (!Utilities.IsValid(pickup))
                return;

            var isOwner = this.LocalPlayerOwnsThisObject();

            // Someone else took it - let go locally
            if (!isOwner && pickup.currentHand != VRC_Pickup.PickupHand.None)
                pickup.Drop();

            if (!canManagePickupable)
                return;

            var canPickup = isOwner || !(pickup.DisallowTheft && holdHand != NoHand);
            if (pickup.pickupable != canPickup)
                pickup.pickupable = canPickup;
        }

        #endregion

        #region Helpers

        private void UpdateFastSyncInterval()
        {
            if (fastSyncIntervalCurve != null)
                fastSyncInterval = fastSyncIntervalCurve.Evaluate(VRCPlayerApi.GetPlayerCount());
        }

        private float GetHeartbeat()
        {
            return Mathf.Max(MinHeartbeat, fastSyncInterval);
        }

        private float GetMaxExtrapolation()
        {
            return GetHeartbeat() * 2f;
        }

        private bool RigidbodyIsDynamic()
        {
            return Utilities.IsValid(objectRB) && !objectRB.isKinematic;
        }

        /// <summary>
        /// Gravity in the parent's space, the same on the owner and remotes
        /// </summary>
        private Vector3 GetLocalGravity()
        {
            if (customGravityActive)
                return ToLocalVector(customGravity);

            return Utilities.IsValid(objectRB) && objectRB.useGravity ? ToLocalVector(Physics.gravity) : Vector3.zero;
        }

        private bool IsAirborne()
        {
            if (scriptGrounding)
                return !scriptGrounded;

            // Contacts report every physics step, so two missed steps means nothing is touching
            return Time.fixedTime - lastContactTime > Time.fixedDeltaTime * 2f;
        }

        private bool RigidbodyIsAwake()
        {
            return RigidbodyIsDynamic() && !objectRB.IsSleeping();
        }

        /// <summary>
        /// Writes the hand pose into handPosition/handRotation
        /// </summary>
        private bool GetHandPose(VRCPlayerApi player, int hand)
        {
            if (!Utilities.IsValid(player) || hand == NoHand)
                return false;

            if (hand == Head)
            {
                var headData = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                handPosition = headData.position;
                handRotation = headData.rotation;
                return true;
            }

            var left = hand == LeftHand;
            var bone = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var bonePosition = player.GetBonePosition(bone);
            if (bonePosition != Vector3.zero)
            {
                handPosition = bonePosition;
                handRotation = player.GetBoneRotation(bone);
                return true;
            }

            // No humanoid hand bone - use tracking data instead
            var trackingData = player.GetTrackingData(left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
            handPosition = trackingData.position;
            handRotation = trackingData.rotation;
            return true;
        }

        private Quaternion ExtrapolateRotation(Quaternion rotation, Vector3 angularVelocity, float time)
        {
            var speed = angularVelocity.magnitude;
            // Negative time rewinds, used when blending towards the next snapshot
            if (speed < 0.0001f || time == 0f)
                return rotation;

            return Quaternion.AngleAxis(speed * time * Mathf.Rad2Deg, angularVelocity / speed) * rotation;
        }

        private Vector3 ToLocalVector(Vector3 worldVector)
        {
            var parent = transform.parent;
            return Utilities.IsValid(parent) ? parent.InverseTransformVector(worldVector) : worldVector;
        }

        private Vector3 ToLocalDirection(Vector3 worldDirection)
        {
            var parent = transform.parent;
            return Utilities.IsValid(parent) ? parent.InverseTransformDirection(worldDirection) : worldDirection;
        }

        private Vector3 ToWorldVector(Vector3 localVector)
        {
            var parent = transform.parent;
            return Utilities.IsValid(parent) ? parent.TransformVector(localVector) : localVector;
        }

        private Vector3 ToWorldDirection(Vector3 localDirection)
        {
            var parent = transform.parent;
            return Utilities.IsValid(parent) ? parent.TransformDirection(localDirection) : localDirection;
        }

        #endregion

        #region Packing

        private int Quantize(float value, float range, int steps)
        {
            return Mathf.RoundToInt((Mathf.Clamp(value, -range, range) + range) / (2f * range) * steps);
        }

        private float Dequantize(int value, float range, int steps)
        {
            return value / (float)steps * 2f * range - range;
        }

        /// <summary>
        /// Square-root spacing - fine steps at low speed, ~1% steps near the top of the range
        /// </summary>
        private int QuantizeSqrt(float value, float range, int steps)
        {
            var normalized = Mathf.Clamp(value / range, -1f, 1f);
            var curved = Mathf.Sign(normalized) * Mathf.Sqrt(Mathf.Abs(normalized));
            return Mathf.RoundToInt((curved + 1f) * 0.5f * steps);
        }

        private float DequantizeSqrt(int value, float range, int steps)
        {
            var curved = value / (float)steps * 2f - 1f;
            return Mathf.Sign(curved) * curved * curved * range;
        }

        /// <summary>
        /// Smallest-three: 2 bits for the dropped component, 10 bits for each of the others
        /// </summary>
        private uint PackRotation(Quaternion q)
        {
            var largest = 0;
            var max = Mathf.Abs(q.x);
            if (Mathf.Abs(q.y) > max)
            {
                largest = 1;
                max = Mathf.Abs(q.y);
            }

            if (Mathf.Abs(q.z) > max)
            {
                largest = 2;
                max = Mathf.Abs(q.z);
            }

            if (Mathf.Abs(q.w) > max)
                largest = 3;

            var a = 0f;
            var b = 0f;
            var c = 0f;
            var dropped = 0f;
            switch (largest)
            {
                case 0:
                    a = q.y;
                    b = q.z;
                    c = q.w;
                    dropped = q.x;
                    break;
                case 1:
                    a = q.x;
                    b = q.z;
                    c = q.w;
                    dropped = q.y;
                    break;
                case 2:
                    a = q.x;
                    b = q.y;
                    c = q.w;
                    dropped = q.z;
                    break;
                default:
                    a = q.x;
                    b = q.y;
                    c = q.z;
                    dropped = q.w;
                    break;
            }

            if (dropped < 0f)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            return ((uint)largest << 30)
                   | ((uint)Quantize(a, RotationRange, RotationSteps) << 20)
                   | ((uint)Quantize(b, RotationRange, RotationSteps) << 10)
                   | (uint)Quantize(c, RotationRange, RotationSteps);
        }

        private Quaternion UnpackRotation(uint packed)
        {
            var largest = (int)(packed >> 30);
            var a = Dequantize((int)((packed >> 20) & 1023u), RotationRange, RotationSteps);
            var b = Dequantize((int)((packed >> 10) & 1023u), RotationRange, RotationSteps);
            var c = Dequantize((int)(packed & 1023u), RotationRange, RotationSteps);
            var d = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a - b * b - c * c));

            switch (largest)
            {
                case 0:
                    return new Quaternion(d, a, b, c);
                case 1:
                    return new Quaternion(a, d, b, c);
                case 2:
                    return new Quaternion(a, b, d, c);
                default:
                    return new Quaternion(a, b, c, d);
            }
        }

        /// <summary>
        /// 21 bits per axis
        /// </summary>
        private ulong PackVelocity(Vector3 v)
        {
            return (ulong)Quantize(v.x, MaxVelocity, VelocitySteps)
                   | ((ulong)Quantize(v.y, MaxVelocity, VelocitySteps) << 21)
                   | ((ulong)Quantize(v.z, MaxVelocity, VelocitySteps) << 42);
        }

        private Vector3 UnpackVelocity(ulong packed)
        {
            return new Vector3(
                Dequantize((int)(packed & 0x1FFFFFUL), MaxVelocity, VelocitySteps),
                Dequantize((int)((packed >> 21) & 0x1FFFFFUL), MaxVelocity, VelocitySteps),
                Dequantize((int)((packed >> 42) & 0x1FFFFFUL), MaxVelocity, VelocitySteps));
        }

        /// <summary>
        /// 10 bits per axis
        /// </summary>
        private uint PackAngularVelocity(Vector3 v)
        {
            return (uint)QuantizeSqrt(v.x, MaxAngularVelocity, AngularSteps)
                   | ((uint)QuantizeSqrt(v.y, MaxAngularVelocity, AngularSteps) << 10)
                   | ((uint)QuantizeSqrt(v.z, MaxAngularVelocity, AngularSteps) << 20);
        }

        private Vector3 UnpackAngularVelocity(uint packed)
        {
            return new Vector3(
                DequantizeSqrt((int)(packed & 1023u), MaxAngularVelocity, AngularSteps),
                DequantizeSqrt((int)((packed >> 10) & 1023u), MaxAngularVelocity, AngularSteps),
                DequantizeSqrt((int)((packed >> 20) & 1023u), MaxAngularVelocity, AngularSteps));
        }

        #endregion
    }
}
