using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using dev.mikeee324.OpenPutt;

[UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(-999)]
public class ControllerTracker : UdonSharpBehaviour
{
    [OpenPuttDescription("Keeps a short history of the local player's head and hand movements each frame, so other scripts can calculate how fast they are moving (used for club swing speed).")]
    [OpenPuttFoldoutGroup("Buffer Settings")]
    [Tooltip("Number of frames of history to store. Needs to cover the longest sampling window at the highest framerate you expect - 30 frames is ~200ms at 144Hz.")] [Range(2, 60)]
    public int bufferSize = 30;

    // History arrays for each tracking point
    private Vector3[] headPositions;
    private Quaternion[] headRotations;
    private Vector3[] leftHandPositions;
    private Quaternion[] leftHandRotations;
    private Vector3[] rightHandPositions;
    private Quaternion[] rightHandRotations;
    private float[] timestamps;

    private int currentIndex = 0;
    private bool initialized = false;
    private int framesFilled = 0;

    // How much of the recent hand movement gets averaged into one velocity reading.
    //
    // TUNING: raise it for smoother, more repeatable shots; lower it for a more responsive read of
    // the exact instant of contact. Too high flattens the peak of a fast swing, so drives lose power,
    // and it starts measuring the chord of the swing arc rather than the tangent at contact, which
    // pulls the aim toward where the club was heading earlier in the arc. Too low lets tracking
    // jitter through as inconsistent shot speed.
    //
    // Affects everything that reads hand movement - club hits, club throws, ball throws, menu throws.
    //
    // Time based rather than frame based so a swing measures the same on a 72Hz headset as on a
    // 144Hz PC. A fixed frame count would smooth over 3x more of the swing on the slower device.
    [OpenPuttFoldoutGroup("Buffer Settings")]
    [Tooltip("How much recent hand movement is averaged into one velocity reading. Higher = smoother and more repeatable, but flattens the peak of fast swings and drags the aim toward the swing arc. Lower = more responsive but noisier.")]
    [SerializeField] [Range(0f, 0.1f)]
    public float trackingSmoothingSeconds = 0.022f;

    // Shifts the whole sampling window back in time, so we measure the swing slightly before 'now'.
    //
    // TUNING: leave at 0 unless the newest tracking frame is untrustworthy (extrapolation artifacts
    // at the very latest sample). Raising it does NOT delay anything - the hit still fires
    // immediately, it just gets measured from older frames.
    [OpenPuttFoldoutGroup("Buffer Settings")]
    [Tooltip("Rewinds the measurement this far before reading it. Adds no delay to the shot itself - only changes which frames get measured. Leave at 0 unless the newest tracking frame is unreliable.")]
    [SerializeField, Range(0f, 0.05f)]
    public float trackingRewindSeconds = 0f;

    // Resolved window from the last ResolveSampleWindow call. Fields rather than out params because Udon
    // doesn't handle out parameters on user methods.
    private int windowNewestIdx;
    private int windowOldestIdx;
    private float windowDeltaTime;

    void Start()
    {
        // Initialize arrays based on the buffer size
        InitializeArrays();
    }

    private void InitializeArrays()
    {
        headPositions = new Vector3[bufferSize];
        headRotations = new Quaternion[bufferSize];
        leftHandPositions = new Vector3[bufferSize];
        leftHandRotations = new Quaternion[bufferSize];
        rightHandPositions = new Vector3[bufferSize];
        rightHandRotations = new Quaternion[bufferSize];
        timestamps = new float[bufferSize];

        // Reset tracking
        currentIndex = 0;
        framesFilled = 0;
        initialized = false;

        // Fill initial history with current data to avoid zero velocity on start
        var player = Networking.LocalPlayer;
        if (player != null)
        {
            var currentHeadPos = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            var currentHeadRot = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
            var currentLeftPos = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
            var currentLeftRot = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;
            var currentRightPos = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
            var currentRightRot = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;
            var currentTime = Time.time;

            for (var i = 0; i < bufferSize; i++)
            {
                headPositions[i] = currentHeadPos;
                headRotations[i] = currentHeadRot;
                leftHandPositions[i] = currentLeftPos;
                leftHandRotations[i] = currentLeftRot;
                rightHandPositions[i] = currentRightPos;
                rightHandRotations[i] = currentRightRot;
                timestamps[i] = currentTime;
            }

            initialized = true; // Consider initialized immediately if buffer is pre-filled
            framesFilled = bufferSize;
        }
    }

    void Update()
    {
        var player = Networking.LocalPlayer;
        if (player == null) return;

        // Update the current index
        currentIndex = (currentIndex + 1) % bufferSize;

        // Record current time
        timestamps[currentIndex] = Time.time;

        // Record position and rotation for each tracking point
        headPositions[currentIndex] = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        headRotations[currentIndex] = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;

        leftHandPositions[currentIndex] = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
        leftHandRotations[currentIndex] = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;

        rightHandPositions[currentIndex] = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        rightHandRotations[currentIndex] = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;

        // If not initialized yet, increment framesFilled
        if (!initialized)
        {
            if (framesFilled < bufferSize)
                framesFilled++;
            else
                initialized = true; // Buffer is now full
        }
    }

    // Helper method to get index with offset, wrapping around the buffer size
    public int GetIndexWithOffset(int offset)
    {
        // Ensure offset is not negative and wraps correctly
        return (currentIndex - offset % bufferSize + bufferSize) % bufferSize;
    }

    /// <summary>
    /// Finds the newest buffered frame that is at least secondsBack older than the current frame.<br/>
    /// Walks backwards and stops when it runs out of real history, so asking for more time than the
    /// buffer holds clamps to the oldest valid frame instead of wrapping around onto a newer one.
    /// </summary>
    public int GetIndexSecondsAgo(float secondsBack)
    {
        var idx = currentIndex;
        if (secondsBack <= 0f) return idx;

        var targetTime = timestamps[currentIndex] - secondsBack;
        var previousTime = timestamps[idx];

        for (var i = 0; i < bufferSize - 1; i++)
        {
            var nextIdx = (idx - 1 + bufferSize) % bufferSize;
            var nextTime = timestamps[nextIdx];

            // Timestamps stopped going backwards, so we've wrapped onto stale data (or an
            // unwritten slot while the buffer is still filling) - keep what we already have
            if (nextTime <= 0f || nextTime >= previousTime) break;

            idx = nextIdx;
            previousTime = nextTime;

            // Walked far enough back to cover the requested window
            if (nextTime <= targetTime) break;
        }

        return idx;
    }

    /// <summary>
    /// Resolves a sampling window into windowNewestIdx / windowOldestIdx / windowDeltaTime.<br/>
    /// Returns false only when there is genuinely no usable history.
    /// </summary>
    private bool ResolveSampleWindow(float endSecondsBack, float windowSeconds)
    {
        windowNewestIdx = GetIndexSecondsAgo(endSecondsBack);
        windowOldestIdx = GetIndexSecondsAgo(endSecondsBack + Mathf.Max(windowSeconds, 0f));
        windowDeltaTime = timestamps[windowNewestIdx] - timestamps[windowOldestIdx];

        // Window collapsed onto a single frame (window shorter than one frame, or we ran out of
        // history). Widen by one frame rather than reporting zero velocity - a collapsed window
        // used to return Vector3.zero here, which showed up in game as a completely dead shot.
        if (windowDeltaTime <= 0f)
        {
            var widerIdx = (windowNewestIdx - 1 + bufferSize) % bufferSize;
            var widerTime = timestamps[widerIdx];

            if (widerTime > 0f && widerTime < timestamps[windowNewestIdx])
            {
                windowOldestIdx = widerIdx;
                windowDeltaTime = timestamps[windowNewestIdx] - widerTime;
            }
        }

        return windowDeltaTime > 0f;
    }

    /// <summary>
    /// Gets the linear velocity of a tracking point over the lookback window
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <returns>Linear velocity in meters per second.</returns>
    public Vector3 GetVelocity(VRCPlayerApi.TrackingDataType dataType)
    {
        if (!initialized) return Vector3.zero;

        if (!ResolveSampleWindow(trackingRewindSeconds, trackingSmoothingSeconds)) return Vector3.zero;

        var newestPos = GetPosition(dataType, windowNewestIdx);
        var oldestPos = GetPosition(dataType, windowOldestIdx);

        return (newestPos - oldestPos) / windowDeltaTime;
    }

     /// <summary>
    /// Gets the linear velocity of a point offset (in local space) from a tracking point
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <param name="localOffset">The offset vector in the local space of the tracking point.</param>
    /// <returns>Linear velocity of the offset point in meters per second.</returns>
    public Vector3 GetVelocityAtOffset(VRCPlayerApi.TrackingDataType dataType, Vector3 localOffset)
    {
        return SampleVelocityAtOffset(dataType, localOffset, trackingRewindSeconds, trackingSmoothingSeconds);
    }

    /// <summary>
    /// Gets the linear velocity of an offset point over an explicit window, ending endSecondsBack
    /// before the current frame. Lets a caller sample the swing around the moment of contact rather
    /// than at whatever later frame it happens to be applying the hit on.
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <param name="localOffset">The offset vector in the local space of the tracking point.</param>
    /// <param name="endSecondsBack">How far before the current frame the window should end.</param>
    /// <param name="windowSeconds">How far back from that end point the window reaches.</param>
    /// <returns>Linear velocity of the offset point in meters per second.</returns>
    public Vector3 SampleVelocityAtOffset(VRCPlayerApi.TrackingDataType dataType, Vector3 localOffset, float endSecondsBack, float windowSeconds)
    {
        if (!initialized) return Vector3.zero;

        if (!ResolveSampleWindow(endSecondsBack, windowSeconds)) return Vector3.zero;

        var newestPos = GetPosition(dataType, windowNewestIdx);
        var newestRot = GetRotation(dataType, windowNewestIdx);
        var oldestPos = GetPosition(dataType, windowOldestIdx);
        var oldestRot = GetRotation(dataType, windowOldestIdx);

        // Calculate the world position of the offset point at the newest and oldest frames
        var offsetPointNewestPos = newestPos + (newestRot * localOffset);
        var offsetPointOldestPos = oldestPos + (oldestRot * localOffset);

        // Calculate velocity of the offset point over the calculation window
        return (offsetPointNewestPos - offsetPointOldestPos) / windowDeltaTime;
    }

    /// <summary>
    /// Returns the two world-space endpoints of the current velocity window for an offset point,
    /// as {newest, oldest}. Used by the club visualiser to draw the window it actually measured.
    /// </summary>
    public Vector3[] GetVelocityWindowEndpointsAtOffset(VRCPlayerApi.TrackingDataType dataType, Vector3 localOffset)
    {
        if (!initialized || !ResolveSampleWindow(trackingRewindSeconds, trackingSmoothingSeconds))
            return new Vector3[0];

        var newestPos = GetPosition(dataType, windowNewestIdx) + (GetRotation(dataType, windowNewestIdx) * localOffset);
        var oldestPos = GetPosition(dataType, windowOldestIdx) + (GetRotation(dataType, windowOldestIdx) * localOffset);

        return new Vector3[] { newestPos, oldestPos };
    }


    /// <summary>
    /// Gets the angular velocity of a tracking point over the lookback window
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <param name="smoothingSeconds">Window length in seconds, or -1 to use trackingSmoothingSeconds.</param>
    /// <returns>Angular velocity in degrees per second (Euler angles).</returns>
    public Vector3 GetAngularVelocity(VRCPlayerApi.TrackingDataType dataType, float smoothingSeconds)
    {
        if (!initialized) return Vector3.zero;

        if (smoothingSeconds < 0f)
            smoothingSeconds = trackingSmoothingSeconds;

        if (!ResolveSampleWindow(trackingRewindSeconds, smoothingSeconds)) return Vector3.zero;

        // Calculate angular velocity between the oldest and newest rotations in the calculation window
        var currentRot = GetRotation(dataType, windowNewestIdx);
        var previousRot = GetRotation(dataType, windowOldestIdx);
        var deltaTime = windowDeltaTime;

        // Calculate the difference in rotation as a delta quaternion, expressed in world space
        // (previousRot on the right so the result rotates previousRot into currentRot around a world-space axis)
        var deltaRotation = currentRot * Quaternion.Inverse(previousRot);

        // Convert the delta quaternion to axis-angle representation (degrees)
        deltaRotation.ToAngleAxis(out var angle, out var axis);

        // Keep the angle in the shortest rotation path (-180 to 180 degrees)
        if (angle > 180f) angle -= 360f;
        else if (angle < -180f) angle += 360f;

        // Angular velocity is the angle rotated per second along the rotation axis
        return (axis * angle) / deltaTime;
    }

    // Helper methods to get position and rotation from history arrays based on index
    private Vector3 GetPosition(VRCPlayerApi.TrackingDataType dataType, int index)
    {
        switch (dataType)
        {
            case VRCPlayerApi.TrackingDataType.Head:
                return headPositions[index];
            case VRCPlayerApi.TrackingDataType.LeftHand:
                return leftHandPositions[index];
            case VRCPlayerApi.TrackingDataType.RightHand:
                return rightHandPositions[index];
            default:
                return Vector3.zero; // Should not happen with valid input
        }
    }

    public Quaternion GetRotation(VRCPlayerApi.TrackingDataType dataType, int index)
    {
        switch (dataType)
        {
            case VRCPlayerApi.TrackingDataType.Head:
                return headRotations[index];
            case VRCPlayerApi.TrackingDataType.LeftHand:
                return leftHandRotations[index];
            case VRCPlayerApi.TrackingDataType.RightHand:
                return rightHandRotations[index];
            default:
                return Quaternion.identity; // Should not happen with valid input
        }
    }

    /// <summary>
    /// Calculates the local offset of a world position relative to a tracking point, for use with GetVelocityAtOffset
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <param name="worldPosition">The world position to calculate the offset for.</param>
    /// <returns>The local offset vector relative to the tracking point.</returns>
    public Vector3 CalculateLocalOffsetFromWorldPosition(VRCPlayerApi.TrackingDataType dataType, Vector3 worldPosition)
    {
        if (!initialized) return Vector3.zero;

        // Uses the most current tracking data (not the delayed sampling frame) for attachment purposes
        var trackingPointWorldPos = GetPosition(dataType, currentIndex);
        var trackingPointWorldRot = GetRotation(dataType, currentIndex);

        // Calculate the local offset using the inverse transformation
        return Quaternion.Inverse(trackingPointWorldRot) * (worldPosition - trackingPointWorldPos);
    }
        
    /// <summary>
    /// Gets an array of historical world positions for a point offset (in local space) from a tracking point
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <param name="localOffset">The offset vector in the local space of the tracking point.</param>
    /// <param name="startOffset">The offset from the current frame to start retrieving history (0 is the current frame).</param>
    /// <param name="numberOfFrames">The number of historical frames to retrieve, starting from the startOffset.</param>
    /// <returns>An array of historical world positions of the offset point.</returns>
    public Vector3[] GetHistoricalPositionsArrayAtOffset(VRCPlayerApi.TrackingDataType dataType, Vector3 localOffset, int startOffset, int numberOfFrames)
    {
        // Return an empty array if not initialized or requested range is invalid
        if (!initialized || startOffset < 0 || numberOfFrames <= 0 || startOffset + numberOfFrames > bufferSize)
        {
            return new Vector3[0];
        }

        var historicalPositions = new Vector3[numberOfFrames];

        for (var i = 0; i < numberOfFrames; i++)
        {
            // Calculate the offset for the current frame in the loop relative to the overall history
            var currentOffset = startOffset + i;
            var historicalIndex = GetIndexWithOffset(currentOffset);

            // Get the historical position and rotation of the tracking point
            var trackingPointHistoricalPos = GetPosition(dataType, historicalIndex);
            var trackingPointHistoricalRot = GetRotation(dataType, historicalIndex);

            // Calculate the world position of the offset point at this historical frame
            historicalPositions[i] = trackingPointHistoricalPos + (trackingPointHistoricalRot * localOffset);
        }

        return historicalPositions;
    }
}
