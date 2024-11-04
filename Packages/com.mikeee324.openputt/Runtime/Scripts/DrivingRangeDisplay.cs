using dev.mikeee324.OpenPutt;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

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
        if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(drivingRangeCourse) || !Utilities.IsValid(distanceLabel))
        {
            return;
        }
    }

    public override void PostLateUpdate()
    {
        if (!this.enabled)
            return;

        if (!Utilities.IsValid(playerManager))
            return;

        if (!Utilities.IsValid(golfBall))
            golfBall = playerManager.golfBall;
        if (!Utilities.IsValid(golfBall))
            return;

        if (playerManager.CurrentCourse == drivingRangeCourse)
        {
            if (!golfBall.BallIsMoving)
            {
                MonitoringDistance = false;
            }

            var distance = Mathf.FloorToInt(Vector3.Distance(golfBall.CurrentPosition, golfBall.respawnPosition));
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

    public override void OnLocalPlayerBallHit(float speed)
    {
        highestScoreSoFar = 0;

        if (!Utilities.IsValid(playerManager))
            playerManager = openPutt.LocalPlayerManager;
        if (!Utilities.IsValid(playerManager))
            return;

        if (playerManager.CurrentCourse == drivingRangeCourse)
        {
            MonitoringDistance = true;
        }
    }

    public override void OnLocalPlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
    {
    }

    public override void OnRemotePlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
    {
    }

    public override void OnLocalPlayerBallStopped()
    {
    }
}