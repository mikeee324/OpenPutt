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
    private int highestScoreSoFar;

    private bool MonitoringDistance
    {
        set => enabled = value;
    }

    void Start()
    {
        // Disable Update() and LateUpdate() calls - we will enable things as when we need to monitor the ball distance
        enabled = false;
    }

    public override void PostLateUpdate()
    {
        if (!enabled)
            return;

        if (!Utilities.IsValid(playerManager))
            return;
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
            distanceLabel.text = "-";
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == Networking.LocalPlayer)
            enabled = false;
    }

    public override void OnLocalPlayerInitialised(PlayerManager localPlayerManager)
    {
        playerManager = localPlayerManager;

        if (!Utilities.IsValid(playerManager)) return;
        
        golfBall = playerManager.golfBall;
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