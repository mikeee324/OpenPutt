using UdonSharp;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OpenPuttInternalEvents : OpenPuttEventListener
    {

        public override void OnBallEnterHole(CourseManager course, CourseHole hole)
        {
            if (course != null && course.openPutt != null && course.openPutt.SFXController != null)
                course.openPutt.SFXController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position);
        }

        public override void OnHoleInOne(CourseManager course, CourseHole hole)
        {
            if (course != null && course.openPutt != null && course.openPutt.SFXController != null)
                course.openPutt.SFXController.PlayHoleInOneSoundAtPosition(hole.transform.position);
        }

    }
}