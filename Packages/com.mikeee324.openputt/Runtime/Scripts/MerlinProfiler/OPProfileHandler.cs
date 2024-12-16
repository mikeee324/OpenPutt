// GlobalProfileHandler.cs - https://gist.github.com/MerlinVR/2da80b29361588ddb556fd8d3f3f47b5

#define AVERAGE_OUTPUT

using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[DefaultExecutionOrder(1000000000)]
public class OPProfileHandler : UdonSharpBehaviour
{
    public TextMeshProUGUI _timeText;
    private OPProfileKickoff _kickoff;

    private void Start()
    {
        _kickoff = GetComponent<OPProfileKickoff>();
    }

    private int _currentFrame = -1;
    private float _elapsedTime;
#if AVERAGE_OUTPUT
    private float _measuredFrametimeTotal;
    private float _measuredTimeTotal;
    private int _measuredTimeFrameCount;
    private const int MEASURE_FRAME_AMOUNT = 45;
#endif

    private void FixedUpdate()
    {
        if (!Utilities.IsValid(_kickoff) || !Utilities.IsValid(_kickoff.stopwatch)) return;
        if (_currentFrame != Time.frameCount)
        {
            _elapsedTime = 0f;
            _currentFrame = Time.frameCount;
        }

        if (_kickoff)
            _elapsedTime += (float)_kickoff.stopwatch.Elapsed.TotalSeconds * 1000f;
    }

    private void Update()
    {
        if (!Utilities.IsValid(_kickoff) || !Utilities.IsValid(_kickoff.stopwatch)) return;
        if (_currentFrame != Time.frameCount) // FixedUpdate didn't run this frame, so reset the time
            _elapsedTime = 0f;

        _elapsedTime += (float)_kickoff.stopwatch.Elapsed.TotalSeconds * 1000f;
    }

    private void LateUpdate()
    {
        if (!Utilities.IsValid(_kickoff) || !Utilities.IsValid(_kickoff.stopwatch)) return;
        _elapsedTime += (float)_kickoff.stopwatch.Elapsed.TotalSeconds * 1000f;
    }

    float lastFrame;

    public override void PostLateUpdate()
    {
        if (!Utilities.IsValid(_kickoff) || !Utilities.IsValid(_kickoff.stopwatch)) return;
        _elapsedTime += (float)_kickoff.stopwatch.Elapsed.TotalSeconds * 1000f;
        var now = Time.timeSinceLevelLoad;
        _measuredFrametimeTotal += (now - lastFrame) * 1000f;
        lastFrame = now;

#if AVERAGE_OUTPUT
        if (_measuredTimeFrameCount >= MEASURE_FRAME_AMOUNT)
        {
            var frameTime = _measuredFrametimeTotal / _measuredTimeFrameCount;
            _timeText.text = $"Udon: {(_measuredTimeTotal / _measuredTimeFrameCount):F4}ms\nFrametime: {(frameTime):F4}ms ({(1000 / frameTime):F0}fps)";
            _measuredTimeTotal = 0f;
            _measuredFrametimeTotal = 0f;
            _measuredTimeFrameCount = 0;
        }

        _measuredTimeTotal += _elapsedTime;
        _measuredTimeFrameCount += 1;
#else
        _timeText.text = $"Update time:\n{_elapsedTime:F4}ms";
#endif
    }
}