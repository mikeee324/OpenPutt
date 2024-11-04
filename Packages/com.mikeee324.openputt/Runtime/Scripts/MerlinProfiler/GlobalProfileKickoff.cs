// GlobalProfileKickoss.cs - https://gist.github.com/MerlinVR/2da80b29361588ddb556fd8d3f3f47b5

using System;
using System.Diagnostics;
using UdonSharp;
using UnityEngine;

[DefaultExecutionOrder(-1000000000)]
public class GlobalProfileKickoff : UdonSharpBehaviour
{
    [NonSerialized]
    public Stopwatch stopwatch;

    private void Start()
    {
        stopwatch = new Stopwatch();
    }

    private void FixedUpdate()
    {
        stopwatch.Restart();
    }

    private void Update()
    {
        stopwatch.Restart();
    }

    private void LateUpdate()
    {
        stopwatch.Restart();
    }

    public override void PostLateUpdate()
    {
        stopwatch.Restart();
    }
}