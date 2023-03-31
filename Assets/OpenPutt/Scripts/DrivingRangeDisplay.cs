
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
    private float resetTimer = -1f;
    private int highestScoreSoFar = 0;
    private bool monitorDistance = false;
    private float initTimer = 0f;

    void Start()
    {
        if (openPutt == null || drivingRangeCourse == null || distanceLabel == null)
        {
            this.enabled = false;
            return;
        }
    }

    private void LateUpdate()
    {
        if (initTimer > 5f)
        {
            if (playerManager == null)
                playerManager = openPutt.LocalPlayerManager;
            if (playerManager == null)
                return;
            if (golfBall == null)
                golfBall = playerManager.golfBall;
            if (golfBall == null)
                return;

            if (!monitorDistance)
                return;

            if (playerManager.CurrentCourse == drivingRangeCourse)
            {
                if (!golfBall.BallIsMoving)
                {
                    monitorDistance = false;
                }

                int distance = Mathf.FloorToInt(Vector3.Distance(golfBall.transform.position, golfBall.respawnPosition));
                if (distance > highestScoreSoFar)
                {
                    highestScoreSoFar = distance;
                    distanceLabel.text = $"{distance}m";
                }
            }
            else
            {
                monitorDistance = false;
            }
        }
        else
        {
            initTimer += Time.deltaTime;
        }
    }

    public override void OnHoleInOne(CourseManager course, CourseHole hole)
    {

    }

    public override void OnBallEnterHole(CourseManager course, CourseHole hole)
    {

    }

    public override void OnLocalPlayerBallHit()
    {
        highestScoreSoFar = 0;

        if (playerManager == null)
            playerManager = openPutt.LocalPlayerManager;
        if (playerManager == null)
            return;

        if (playerManager.CurrentCourse == drivingRangeCourse)
            monitorDistance = true;
    }
}
