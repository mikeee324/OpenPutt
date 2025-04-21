using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(-999)]
public class ControllerTracker : UdonSharpBehaviour
{
    [Tooltip("Number of frames to store in history for velocity calculations")]
    [Range(2, 60)]
    public int bufferSize = 20;
    
    [Tooltip("Whether to use smoothing (multiple frames) or just use single frame for velocity")]
    public bool useSmoothing = true;

    [Range(0,5), Tooltip("How many frames to backtrack")]
    public int defaultFrameOffset = 1;
    
    [Tooltip("Default number of frames to use for smoothing")]
    [Range(2, 20)]
    public int defaultSmoothingFrames = 5;
    
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
        
        if (framesFilled < bufferSize)
            framesFilled++;
        else
            initialized = true;
    }

    // Helper method to get index with offset
    private int GetIndexWithOffset(int offset)
    {
        return (currentIndex - offset + bufferSize) % bufferSize;
    }

    // Get linear velocity at the tracking point's position
    public Vector3 GetLinearVelocity(VRCPlayerApi.TrackingDataType trackingPoint)
    {
        // Use default settings (no offset, default smoothing frames)
        return GetLinearVelocityWithOffset(trackingPoint, defaultFrameOffset, useSmoothing ? defaultSmoothingFrames : 1);
    }

    // Get linear velocity with frame offset and frame count
    public Vector3 GetLinearVelocityWithOffset(VRCPlayerApi.TrackingDataType trackingPoint, int frameOffset, int frameCount)
    {
        // Make sure we have enough frames and valid parameters
        if (!initialized) return Vector3.zero;
        
        // Clamp frameOffset to valid range
        frameOffset = Mathf.Clamp(frameOffset, 0, framesFilled - 1);
        
        // Clamp frameCount to ensure we don't exceed available frames
        frameCount = Mathf.Clamp(frameCount, 1, framesFilled - frameOffset);
        
        Vector3[] positions;
        
        // Select the appropriate position array
        switch (trackingPoint)
        {
            case VRCPlayerApi.TrackingDataType.LeftHand:
                positions = leftHandPositions;
                break;
                
            case VRCPlayerApi.TrackingDataType.RightHand:
                positions = rightHandPositions;
                break;
                
            case VRCPlayerApi.TrackingDataType.Head:
            default:
                positions = headPositions;
                break;
        }
        
        // Get indices based on offset and frame count
        var startIndex = GetIndexWithOffset(frameOffset);
        var endIndex = GetIndexWithOffset(frameOffset + frameCount - 1);
        
        // Calculate delta time
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime * frameCount; // Fallback
        
        // Calculate velocity
        return (positions[startIndex] - positions[endIndex]) / deltaTime;
    }

    // Get angular velocity for a specified tracking point
    public Vector3 GetAngularVelocity(VRCPlayerApi.TrackingDataType trackingPoint)
    {
        // Use default settings (no offset, default smoothing frames)
        return GetAngularVelocityWithOffset(trackingPoint, defaultFrameOffset, useSmoothing ? defaultSmoothingFrames : 1);
    }

    // Get angular velocity with frame offset and frame count
    public Vector3 GetAngularVelocityWithOffset(VRCPlayerApi.TrackingDataType trackingPoint, int frameOffset, int frameCount)
    {
        // Make sure we have enough frames and valid parameters
        if (!initialized) return Vector3.zero;
        
        // Clamp frameOffset to valid range
        frameOffset = Mathf.Clamp(frameOffset, 0, framesFilled - 1);
        
        // Clamp frameCount to ensure we don't exceed available frames
        frameCount = Mathf.Clamp(frameCount, 1, framesFilled - frameOffset);
        
        Quaternion[] rotations;
        
        switch (trackingPoint)
        {
            case VRCPlayerApi.TrackingDataType.LeftHand:
                rotations = leftHandRotations;
                break;
                
            case VRCPlayerApi.TrackingDataType.RightHand:
                rotations = rightHandRotations;
                break;
                
            case VRCPlayerApi.TrackingDataType.Head:
            default:
                rotations = headRotations;
                break;
        }
        
        // Get indices based on offset and frame count
        var startIndex = GetIndexWithOffset(frameOffset);
        var endIndex = GetIndexWithOffset(frameOffset + frameCount - 1);
        
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime * frameCount;
        
        var deltaRotation = Quaternion.Inverse(rotations[endIndex]) * rotations[startIndex];
        deltaRotation.ToAngleAxis(out var angle, out var axis);
        angle *= Mathf.Deg2Rad;
        
        // Fix for when angle > 180 degrees
        if (angle > Mathf.PI)
        {
            angle -= 2 * Mathf.PI;
        }
        
        // Return angular velocity vector (axis * radians/second)
        return axis.normalized * (angle / deltaTime);
    }
    
    // Calculate the velocity at a world position as if it was attached to the tracking point
    public Vector3 GetVelocityAtPoint(VRCPlayerApi.TrackingDataType trackingPoint, Vector3 worldPosition)
    {
        // Use default settings (no offset, default smoothing frames)
        return GetVelocityAtPointWithOffset(trackingPoint, worldPosition, defaultFrameOffset, useSmoothing ? defaultSmoothingFrames : 1);
    }
    
    // Calculate the velocity at a world position with frame offset and frame count
    public Vector3 GetVelocityAtPointWithOffset(VRCPlayerApi.TrackingDataType trackingPoint, Vector3 worldPosition, int frameOffset, int frameCount)
    {
        // Make sure we have enough frames and valid parameters
        if (!initialized) return Vector3.zero;
        
        // Clamp frameOffset to valid range
        frameOffset = Mathf.Clamp(frameOffset, 0, framesFilled - 1);
        
        // Clamp frameCount to ensure we don't exceed available frames
        frameCount = Mathf.Clamp(frameCount, 1, framesFilled - frameOffset);
        
        Vector3[] positions;
        Quaternion[] rotations;
        
        switch (trackingPoint)
        {
            case VRCPlayerApi.TrackingDataType.LeftHand:
                positions = leftHandPositions;
                rotations = leftHandRotations;
                break;
                
            case VRCPlayerApi.TrackingDataType.RightHand:
                positions = rightHandPositions;
                rotations = rightHandRotations;
                break;
                
            case VRCPlayerApi.TrackingDataType.Head:
            default:
                positions = headPositions;
                rotations = headRotations;
                break;
        }
        
        // Get indices based on offset and frame count
        var startIndex = GetIndexWithOffset(frameOffset);
        var endIndex = GetIndexWithOffset(frameOffset + frameCount - 1);
        
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime * frameCount;
        
        var localOffset = Quaternion.Inverse(rotations[startIndex]) * (worldPosition - positions[startIndex]);
        var endWorldPos = positions[endIndex] + rotations[endIndex] * localOffset;
        return (worldPosition - endWorldPos) / deltaTime;
    }
    
    // Get angular velocity at a specific world position relative to a tracking point
    public Vector3 GetAngularVelocityAtPoint(VRCPlayerApi.TrackingDataType trackingPoint, Vector3 worldPosition)
    {
        // Use default settings (no offset, default smoothing frames)
        return GetAngularVelocityAtPointWithOffset(trackingPoint, worldPosition, defaultFrameOffset, useSmoothing ? defaultSmoothingFrames : 1);
    }
    
    // Get angular velocity at a specific world position with frame offset and frame count
    public Vector3 GetAngularVelocityAtPointWithOffset(VRCPlayerApi.TrackingDataType trackingPoint, Vector3 worldPosition, int frameOffset, int frameCount)
    {
        // Make sure we have enough frames and valid parameters
        if (!initialized) return Vector3.zero;
        
        // Get the angular velocity vector
        var angularVelocity = GetAngularVelocityWithOffset(trackingPoint, frameOffset, frameCount);
        
        // Get the current position of the tracking point
        Vector3 trackingPointPosition;
        
        var currentFrameIndex = GetIndexWithOffset(frameOffset);
        
        switch (trackingPoint)
        {
            case VRCPlayerApi.TrackingDataType.LeftHand:
                trackingPointPosition = leftHandPositions[currentFrameIndex];
                break;
                
            case VRCPlayerApi.TrackingDataType.RightHand:
                trackingPointPosition = rightHandPositions[currentFrameIndex];
                break;
                
            case VRCPlayerApi.TrackingDataType.Head:
            default:
                trackingPointPosition = headPositions[currentFrameIndex];
                break;
        }
        
        // Get the direction from the tracking point to the world position
        var directionToPoint = worldPosition - trackingPointPosition;
        
        // Return the cross product of the angular velocity vector and the direction vector
        // This gives us the tangential velocity vector that would be caused by the angular velocity at the specified point
        return Vector3.Cross(angularVelocity, directionToPoint);
    }
}