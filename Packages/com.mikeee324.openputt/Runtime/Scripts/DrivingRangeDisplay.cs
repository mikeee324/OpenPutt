
using mikeee324.OpenPutt;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DrivingRangeDisplay : OpenPuttEventListener
{
    public OpenPutt openPutt;
    public CourseManager drivingRangeCourse;
    public TextMeshProUGUI distanceLabel;
    private PlayerManager playerManager;
    private GolfBallController golfBall;
    private int highestScoreSoFar = 0;
    private bool MonitoringDistance
    {
        set => this.enabled = value;
    }

    void Start()
    {
        // Disable Update() and LateUpdate() calls - we will enable things as when we need to monitor the ball distance
        this.enabled = false;
        if (openPutt == null || drivingRangeCourse == null || distanceLabel == null)
        {
            return;
        }
    }

    public override void PostLateUpdate()
    {
        if (!this.enabled)
            return;

        if (playerManager == null)
            return;

        if (golfBall == null)
            golfBall = playerManager.golfBall;
        if (golfBall == null)
            return;

        if (playerManager.CurrentCourse == drivingRangeCourse)
        {
            if (!golfBall.BallIsMoving)
            {
                MonitoringDistance = false;
            }

            int distance = Mathf.FloorToInt(Vector3.Distance(golfBall.GetPosition(false), golfBall.respawnPosition));
            if (distance > highestScoreSoFar)
            {
                highestScoreSoFar = distance;
                distanceLabel.text = $"{distance}m";
            }
        }
        else
        {
            MonitoringDistance = false;
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == Networking.LocalPlayer)
        {
            distanceLabel.text = $"-";
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == Networking.LocalPlayer)
            this.enabled = false;
    }

    public override void OnLocalPlayerBallHit()
    {
        highestScoreSoFar = 0;

        if (playerManager == null)
            playerManager = openPutt.LocalPlayerManager;
        if (playerManager == null)
            return;

        if (playerManager.CurrentCourse == drivingRangeCourse)
        {
            MonitoringDistance = true;
        }
    }

    public override void OnRemotePlayerHoleInOne(CourseManager course, CourseHole hole)
    {

    }

    public override void OnRemotePlayerBallEnterHole(CourseManager course, CourseHole hole)
    {

    }

    public override void OnLocalPlayerHoleInOne(CourseManager course, CourseHole hole)
    {

    }

    public override void OnLocalPlayerBallEnterHole(CourseManager course, CourseHole hole)
    {

    }
}
