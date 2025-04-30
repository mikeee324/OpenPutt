using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(-999)]
public class ControllerTracker : UdonSharpBehaviour
{
    [Tooltip("Number of frames to store in history for velocity calculations")] [Range(2, 60)]
    public int bufferSize = 20;

    // Removed defaultFrameOffset and defaultSmoothingFrames variables.
    // Velocity calculation will now use a fixed number of frames for smoothing (defaulting to 5).

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

    // Define a fixed number of frames to use for velocity calculation
    [FormerlySerializedAs("FIXED_LOOKBACK_FRAMES")] [SerializeField]
    public int lookbackFrames = 5; // Number of frames to look back for calculating the velocity difference

    // Define a fixed offset from the most current frame to use as the 'end' point for velocity calculation
    [FormerlySerializedAs("VELOCITY_END_OFFSET")]
    [Tooltip("Offset from the most current frame (currentIndex) to use as the end point for velocity calculation. 0 means the current frame.")]
    [SerializeField, Range(0, 5)] // Added a range for the inspector, though it's a const here.
    public int endOffset = 0; // Using the most current frame as the end point by default

    void Start()
    {
        // Ensure buffer size is at least enough for the fixed lookback frames + the end offset + 1
        bufferSize = Mathf.Max(lookbackFrames + endOffset + 1, bufferSize);

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
    /// Gets the linear velocity of a tracking point using a fixed number of frames for calculation.
    /// Uses VELOCITY_END_OFFSET to determine the end frame and FIXED_LOOKBACK_FRAMES for the start frame.
    /// Returns Vector3.zero if the buffer is not yet initialized or if deltaTime is zero.
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <returns>The calculated linear velocity in meters per second.</returns>
    public Vector3 GetVelocity(VRCPlayerApi.TrackingDataType dataType)
    {
        if (!initialized) return Vector3.zero;

        // Calculate the index of the end frame for the velocity calculation
        var newestIdx = GetIndexWithOffset(endOffset);
        // Calculate the index of the start frame based on the fixed lookback frames
        var oldestIdx = GetIndexWithOffset(endOffset + lookbackFrames);

        // Ensure we have enough frames in the buffer for the requested lookback
        if (!initialized && framesFilled < endOffset + lookbackFrames + 1) return Vector3.zero;

        var newestPos = GetPosition(dataType, newestIdx);
        var oldestPos = GetPosition(dataType, oldestIdx);
        var totalDeltaTime = timestamps[newestIdx] - timestamps[oldestIdx];

        // Avoid division by zero or negative time differences
        if (totalDeltaTime <= 0) return Vector3.zero;

        return (newestPos - oldestPos) / totalDeltaTime;
    }

     /// <summary>
    /// Gets the linear velocity of a point offset from a tracking point using a fixed number of frames for calculation.
    /// The offset is treated as a local offset relative to the tracking point's rotation.
    /// Uses VELOCITY_END_OFFSET to determine the end frame and FIXED_LOOKBACK_FRAMES for the start frame.
    /// Returns Vector3.zero if the buffer is not yet initialized or if deltaTime is zero.
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <param name="localOffset">The offset vector in the local space of the tracking point.</param>
    /// <returns>The calculated linear velocity of the offset point in meters per second.</returns>
    public Vector3 GetVelocityAtOffset(VRCPlayerApi.TrackingDataType dataType, Vector3 localOffset)
    {
        if (!initialized) return Vector3.zero;

        // Calculate the index of the end frame for the velocity calculation
        var newestIdx = GetIndexWithOffset(endOffset);
        // Calculate the index of the start frame based on the fixed lookback frames
        var oldestIdx = GetIndexWithOffset(endOffset + lookbackFrames);

        // Ensure we have enough frames in the buffer for the requested lookback
        if (!initialized && framesFilled < endOffset + lookbackFrames + 1) return Vector3.zero;

        var newestPos = GetPosition(dataType, newestIdx);
        var newestRot = GetRotation(dataType, newestIdx);
        var oldestPos = GetPosition(dataType, oldestIdx);
        var oldestRot = GetRotation(dataType, oldestIdx);
        var totalDeltaTime = timestamps[newestIdx] - timestamps[oldestIdx];

        // Avoid division by zero or negative time differences
        if (totalDeltaTime <= 0) return Vector3.zero;

        // Calculate the world position of the offset point at the newest and oldest frames
        var offsetPointNewestPos = newestPos + (newestRot * localOffset);
        var offsetPointOldestPos = oldestPos + (oldestRot * localOffset);

        // Calculate velocity of the offset point over the calculation window
        return (offsetPointNewestPos - offsetPointOldestPos) / totalDeltaTime;
    }


    /// <summary>
    /// Gets the angular velocity of a tracking point using a fixed number of frames for calculation.
    /// Uses VELOCITY_END_OFFSET to determine the end frame and FIXED_LOOKBACK_FRAMES for the start frame.
    /// Returns Vector3.zero if the buffer is not yet initialized or if deltaTime is zero.
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <returns>The calculated angular velocity in degrees per second (Euler angles).</returns>
    public Vector3 GetAngularVelocity(VRCPlayerApi.TrackingDataType dataType, int smoothingFrames)
    {
        if (!initialized) return Vector3.zero;

        if (smoothingFrames == -1)
            smoothingFrames = lookbackFrames;

        // Calculate the index of the end frame for the velocity calculation
        var newestIdx = GetIndexWithOffset(endOffset);
        // Calculate the index of the start frame based on the fixed lookback frames
        var oldestIdx = GetIndexWithOffset(endOffset + smoothingFrames);

        // Ensure we have enough frames in the buffer for the requested lookback
        if (!initialized && framesFilled < endOffset + smoothingFrames + 1) return Vector3.zero;

        // Calculate angular velocity between the oldest and newest rotations in the calculation window
        var currentRot = GetRotation(dataType, newestIdx);
        var previousRot = GetRotation(dataType, oldestIdx);
        var deltaTime = timestamps[newestIdx] - timestamps[oldestIdx];

        // Avoid division by zero or negative time differences
        if (deltaTime <= 0) return Vector3.zero;

        // Calculate the difference in rotation as a delta quaternion (rotation from previous to current)
        var deltaRotation = Quaternion.Inverse(previousRot) * currentRot;

        // Convert the delta quaternion to axis-angle representation
        // The angle is in degrees here, as per Unity's ToAngleAxis
        deltaRotation.ToAngleAxis(out var angle, out var axis);

        // Ensure the angle represents the shortest rotation path (between -180 and 180 degrees)
        // This is important for consistent angular velocity direction
        if (angle > 180f) angle -= 360f;
        else if (angle < -180f) angle += 360f;

        // Angular velocity is the angle rotated per second along the rotation axis
        // Convert angle to radians if needed for other calculations, but for a Vector3 representation
        // where magnitude is speed in deg/sec and direction is axis, this is appropriate.
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
    /// Calculates the local offset of a world position relative to a tracking point.
    /// This offset can then be used with GetVelocityAtOffset to find the velocity of that world point.
    /// Returns Vector3.zero if the buffer is not yet initialized.
    /// </summary>
    /// <param name="dataType">The tracking data type (Head, LeftHand, RightHand).</param>
    /// <param name="worldPosition">The world position to calculate the offset for.</param>
    /// <returns>The local offset vector relative to the tracking point.</returns>
    public Vector3 CalculateLocalOffsetFromWorldPosition(VRCPlayerApi.TrackingDataType dataType, Vector3 worldPosition)
    {
        if (!initialized) return Vector3.zero;

        // Get the current world position and rotation of the tracking point
        // Note: This calculation uses the *most current* tracking data, not the VELOCITY_END_OFFSET frame.
        // This is because you typically want the offset relative to the current position/rotation for attachment purposes.
        var trackingPointWorldPos = GetPosition(dataType, currentIndex);
        var trackingPointWorldRot = GetRotation(dataType, currentIndex);

        // Calculate the local offset using the inverse transformation
        return Quaternion.Inverse(trackingPointWorldRot) * (worldPosition - trackingPointWorldPos);
    }
        
    /// <summary>
    /// Gets an array of historical world positions for a point offset from a tracking point, starting from a specified offset.
    /// The offset is treated as a local offset relative to the tracking point's rotation at each historical frame.
    /// Returns an empty array if the buffer is not initialized or the requested range is invalid.
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
