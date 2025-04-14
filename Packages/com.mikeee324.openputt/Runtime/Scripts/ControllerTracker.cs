using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(-999)]
public class ControllerTracker : UdonSharpBehaviour
{
    [Tooltip("Number of frames to store in history for velocity calculations")]
    [Range(2, 20)]
    public int bufferSize = 5;
    
    [Tooltip("Whether to use smoothing (multiple frames) or just use single frame for velocity")]
    public bool useSmoothing = true;
    
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

    // Helper method to get previous index safely
    private int GetPreviousIndex()
    {
        return (currentIndex - 1 + bufferSize) % bufferSize;
    }

    // Helper method to get smooth offset index 
    private int GetSmoothIndex()
    {
        // When smoothing, use the maximum available frames up to buffer size
        return (currentIndex - (bufferSize - 1) + framesFilled) % bufferSize;
    }

    // Get linear velocity at the tracking point's position
    public Vector3 GetLinearVelocity(VRCPlayerApi.TrackingDataType trackingPoint)
    {
        // Make sure we have enough frames
        if (!initialized || framesFilled < 2) return Vector3.zero;
        
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
        
        // Get indices based on smoothing preference
        int startIndex = currentIndex;
        int endIndex;
        
        if (useSmoothing && framesFilled >= bufferSize)
        {
            // Use full buffer for smoothing
            endIndex = GetSmoothIndex();
        }
        else
        {
            // Use just previous frame
            endIndex = GetPreviousIndex();
        }
        
        // Calculate delta time
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime; // Fallback
        
        // Calculate velocity
        return (positions[startIndex] - positions[endIndex]) / deltaTime;
    }

    // Get angular velocity for a specified tracking point
    public Vector3 GetAngularVelocity(VRCPlayerApi.TrackingDataType trackingPoint)
    {
        // Make sure we have enough frames
        if (!initialized || framesFilled < 2) return Vector3.zero;
        
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
        
        // Get indices based on smoothing preference
        int startIndex = currentIndex;
        int endIndex;
        
        if (useSmoothing && framesFilled >= bufferSize)
        {
            // Use full buffer for smoothing
            endIndex = GetSmoothIndex();
        }
        else
        {
            // Use just previous frame
            endIndex = GetPreviousIndex();
        }
        
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
        // Make sure we have enough frames
        if (!initialized || framesFilled < 2) return Vector3.zero;
        
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
        
        // Get indices based on smoothing preference
        int startIndex = currentIndex;
        int endIndex;
        
        if (useSmoothing && framesFilled >= bufferSize)
        {
            // Use full buffer for smoothing
            endIndex = GetSmoothIndex();
        }
        else
        {
            // Use just previous frame
            endIndex = GetPreviousIndex();
        }
        
        var deltaTime = timestamps[startIndex] - timestamps[endIndex];
        if (deltaTime <= 0.0001f) deltaTime = Time.deltaTime;
        
        var localOffset = Quaternion.Inverse(rotations[startIndex]) * (worldPosition - positions[startIndex]);
        var endWorldPos = positions[endIndex] + rotations[endIndex] * localOffset;
        return (worldPosition - endWorldPos) / deltaTime;
    }
}