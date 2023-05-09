using UdonSharp;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttInternalEvents : OpenPuttEventListener
    {

        public override void OnRemotePlayerBallEnterHole(CourseManager course, CourseHole hole)
        {
            if (course != null && course.openPutt != null && course.openPutt.SFXController != null)
                course.openPutt.SFXController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, true);
        }

        public override void OnRemotePlayerHoleInOne(CourseManager course, CourseHole hole)
        {
            if (course != null && course.openPutt != null && course.openPutt.SFXController != null)
                course.openPutt.SFXController.PlayHoleInOneSoundAtPosition(hole.transform.position, true);
        }

        public override void OnLocalPlayerBallHit()
        {

        }

        public override void OnLocalPlayerHoleInOne(CourseManager course, CourseHole hole)
        {
            if (course != null && course.openPutt != null && course.openPutt.SFXController != null)
                course.openPutt.SFXController.PlayHoleInOneSoundAtPosition(hole.transform.position, false);
        }

        public override void OnLocalPlayerBallEnterHole(CourseManager course, CourseHole hole)
        {
            if (course != null && course.openPutt != null && course.openPutt.SFXController != null)
                course.openPutt.SFXController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, false);
        }
    }
}