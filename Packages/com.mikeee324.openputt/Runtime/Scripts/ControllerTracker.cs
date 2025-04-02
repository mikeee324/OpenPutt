using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ControllerTracker : UdonSharpBehaviour
{
    [Tooltip("Number of frames to store in history for velocity calculations")]
    [Range(2, 20)]
    public int bufferSize = 2;
    
    [Tooltip("Frames to look back for start point (0 = use newest frame)")]
    [Range(0, 5)]
    public int startOffset = 0;
    
    [Tooltip("Frames to look back for end point (1 = use previous frame)")]
    [Range(1, 19)]
    public int endOffset = 1;
    
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
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;
        
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
        
        // Increment frames filled if needed
        if (framesFilled < bufferSize)
        {
            framesFilled++;
        }
    }

    // Helper method to get offset index safely
    private int GetOffsetIndex(int offset)
    {
        if (offset >= bufferSize) offset = bufferSize - 1;
        return (currentIndex - offset + bufferSize) % bufferSize;
    }

    // Get linear velocity at the tracking point's position using configurable time window
    public Vector3 GetLinearVelocity(VRCPlayerApi.TrackingDataType trackingPoint)
    {
        // Make sure we have enough frames and valid offsets
        if (!initialized || framesFilled <= endOffset) return Vector3.zero;
        if (endOffset <= startOffset) return Vector3.zero;
        
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
        
        // Get start and end indices
        var startIndex = GetOffsetIndex(startOffset);
        var endIndex = GetOffsetIndex(endOffset);
        
        // Calculate delta time
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime; // Fallback
        
        // Calculate velocity
        return (positions[startIndex] - positions[endIndex]) / deltaTime;
    }

    // Get angular velocity for a specified tracking point using configurable time window
    public Vector3 GetAngularVelocity(VRCPlayerApi.TrackingDataType trackingPoint)
    {
        // Make sure we have enough frames and valid offsets
        if (!initialized || framesFilled <= endOffset) return Vector3.zero;
        if (endOffset <= startOffset) return Vector3.zero;
        
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
        
        var startIndex = GetOffsetIndex(startOffset);
        var endIndex = GetOffsetIndex(endOffset);
        
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime;
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
        // Make sure we have enough frames and valid offsets
        if (!initialized || framesFilled <= endOffset) return Vector3.zero;
        if (endOffset <= startOffset) return Vector3.zero;
        
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
        
        var startIndex = GetOffsetIndex(startOffset);
        var endIndex = GetOffsetIndex(endOffset);
        
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime;
        
        var localOffset = Quaternion.Inverse(rotations[startIndex]) * (worldPosition - positions[startIndex]);
        var endWorldPos = positions[endIndex] + rotations[endIndex] * localOffset;
        return (worldPosition - endWorldPos) / deltaTime;
    }
}