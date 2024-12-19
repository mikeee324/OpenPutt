using UdonSharp;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttInternalEvents : OpenPuttEventListener
    {
        public SFXController sfxController;
        
        public override void OnLocalPlayerInitialised(PlayerManager localPlayerManager)
        {
        }
        
        public override void OnLocalPlayerBallHit(float speed)
        {
        }

        public override void OnLocalPlayerBallStopped()
        {
        }

        public override void OnLocalPlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
            if (Utilities.IsValid(sfxController) && Utilities.IsValid(course) && Utilities.IsValid(hole))
                sfxController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position);

            if (score == 1)
            {
                if (Utilities.IsValid(sfxController) && Utilities.IsValid(hole))
                    sfxController.PlayHoleInOneSoundAtPosition(hole.transform.position);
            }
            else
            {
                if (Utilities.IsValid(sfxController) && Utilities.IsValid(hole))
                    sfxController.PlayScoreSoundAtPosition(hole.transform.position, scoreRelativeToPar);
            }
        }

        public override void OnRemotePlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {
            if (Utilities.IsValid(sfxController) && Utilities.IsValid(course) && Utilities.IsValid(hole))
                sfxController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, true);

            if (score == 1)
            {
                if (Utilities.IsValid(sfxController) && Utilities.IsValid(hole))
                    sfxController.PlayHoleInOneSoundAtPosition(hole.transform.position, true);
            }
        }
    }
}