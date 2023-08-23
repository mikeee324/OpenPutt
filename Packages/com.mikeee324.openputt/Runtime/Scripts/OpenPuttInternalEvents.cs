using UdonSharp;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttInternalEvents : OpenPuttEventListener
    {
        public SFXController sfxController;

        public override void OnRemotePlayerBallEnterHole(CourseManager course, CourseHole hole)
        {
            if (sfxController != null && course != null && hole != null)
                sfxController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, true);
        }

        public override void OnRemotePlayerHoleInOne(CourseManager course, CourseHole hole)
        {
            if (sfxController != null && hole != null)
                sfxController.PlayHoleInOneSoundAtPosition(hole.transform.position, true);
        }

        public override void OnLocalPlayerBallHit()
        {

        }

        public override void OnLocalPlayerHoleInOne(CourseManager course, CourseHole hole)
        {
            if (sfxController != null && hole != null)
                sfxController.PlayHoleInOneSoundAtPosition(hole.transform.position, false);
        }

        public override void OnLocalPlayerBallEnterHole(CourseManager course, CourseHole hole)
        {
            if (sfxController != null && course != null && hole != null)
                sfxController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, false);
        }
    }
}