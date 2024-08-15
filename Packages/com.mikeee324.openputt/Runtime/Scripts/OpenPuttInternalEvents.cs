using UdonSharp;

namespace mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttInternalEvents : OpenPuttEventListener
    {
        public SFXController sfxController;

        public override void OnLocalPlayerBallHit(float speed)
        {

        }

        public override void OnLocalPlayerBallStopped()
        {
            
        }

        public override void OnLocalPlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
            if (sfxController != null && course != null && hole != null)
                sfxController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, false);

            if (score == 1)
            {
                if (sfxController != null && hole != null)
                    sfxController.PlayHoleInOneSoundAtPosition(hole.transform.position, false);
            }
            else
            {
                if (sfxController != null && hole != null)
                    sfxController.PlayScoreSoundAtPosition(hole.transform.position, scoreRelativeToPar, false);
            }
        }

        public override void OnRemotePlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
            if (sfxController != null && course != null && hole != null)
                sfxController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, true);

            if (score == 1)
            {
                if (sfxController != null && hole != null)
                    sfxController.PlayHoleInOneSoundAtPosition(hole.transform.position, true);
            }
        }
    }
}