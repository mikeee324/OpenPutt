using System.Diagnostics;
using UdonSharp;
using UnityEngine;

namespace dev.mikeee324.OpenPutt
{
/*

// EXAMPLE USAGE OF - TimeSlicer
[UdonBehaviourSyncMode(BehaviourSyncMode.None), DefaultExecutionOrder(200)]
public class AgentUpdateController : TimeSlicer
{
    public AgentController[] agents;

    void Start()
    {
        numberOfObjects = agents.Length;
    }

    protected override void _OnUpdateStarted()
    {
    }

    protected override void _OnUpdateEnded()
    {
    }

    protected override void _StartUpdateFrame()
    {
    }

    protected override void _EndUpdateFrame()
    {
    }

    protected override void _OnUpdateItem(int index)
    {
        AgentController agent = agents[index];

        if (!agent.IsJumping)
            agent.UpdateAnimator();
    }
}

*/

    /// <summary>
    /// Base class for looping through an array of objects a bit at a time, spending at most timeAllowedPerFrame ms per frame
    /// </summary>
    public abstract class OpenPuttTimeSlicer : UdonSharpBehaviour
    {
        [Range(0f, 2f), Tooltip("How much time in milliseconds this TimeSlicer is allowed to run for")]
        public float timeAllowedPerFrame = .3f;

        /// <summary>
        /// Total number of objects that are in your array - could also be used as a progress indicator for a long running task
        /// </summary>
        [HideInInspector]
        public int numberOfObjects;

        public int _currentUpdateIndex;

        private Stopwatch _stopwatch = new Stopwatch();

        private void Update()
        {
            _StartUpdateFrame();

            if (_currentUpdateIndex == 0)
                _OnUpdateStarted();

            _stopwatch.Restart();

            if (numberOfObjects == 0)
            {
                _currentUpdateIndex = 0;
                return;
            }

            for (; _currentUpdateIndex < numberOfObjects && _stopwatch.Elapsed.TotalMilliseconds < timeAllowedPerFrame; _currentUpdateIndex++)
            {
                // if (_currentUpdateIndex >= numberOfObjects)
                // {
                //     _currentUpdateIndex = 0;
                //
                //     _OnUpdateEnded();
                //     _OnUpdateStarted();
                // }
                _OnUpdateItem(_currentUpdateIndex);
            }

            if (_currentUpdateIndex >= numberOfObjects)
            {
                _currentUpdateIndex = 0;

                _OnUpdateEnded();
            }

            _EndUpdateFrame();
        }

        /// <summary>
        /// Called at the start of each frame before any items are processed
        /// </summary>
        protected abstract void _StartUpdateFrame();

        /// <summary>
        /// Called at the end of each frame
        /// </summary>
        protected abstract void _EndUpdateFrame();

        /// <summary>
        /// Called per item and this is where you should do the work
        /// </summary>
        /// <param name="index">Index of the item or the current progress through the job</param>
        protected abstract void _OnUpdateItem(int index);

        /// <summary>
        /// Called when the time slicer has started again at the beginning of the array
        /// </summary>
        protected abstract void _OnUpdateStarted();

        /// <summary>
        /// Called when the time slicer reaches the end of the array
        /// </summary>
        protected abstract void _OnUpdateEnded();
    }
}