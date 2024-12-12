using UdonSharp;

namespace dev.mikeee324.OpenPutt
{
    public abstract class OpenPuttEventListener : UdonSharpBehaviour
    {
        /// <summary>
        /// Called when OpenPutt has assigned and finished setting up a PlayerManager for the local player
        /// </summary>
        /// <param name="localPlayerManager">The PlayerManager that was assigned</param>
        public abstract void OnLocalPlayerInitialised(PlayerManager localPlayerManager);
        
        /// <summary>
        /// Called when local player hits their ball
        /// </summary>
        /// <param name="speed">The velocity magnitude that was just applied to the ball</param>
        public abstract void OnLocalPlayerBallHit(float speed);

        /// <summary>
        /// Called when the local players ball has stopped moving
        /// </summary>
        public abstract void OnLocalPlayerBallStopped();

        /// <summary>
        /// Called when the local player ball drops into a course hole
        /// </summary>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        /// <param name="score">The abolsute score that the local player got on the course</param>
        /// <param name="scoreRelativeToPar">The score relative to the par on this course</param>
        public abstract void OnLocalPlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar);

        /// <summary>
        /// Called when a remote player ball drops into a course hole
        /// </summary>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        /// <param name="score">Will only return 1 if it was a hole in one, -1 otherwise as we don't have an easy way of sending scores with events yet</param>
        /// <param name="scoreRelativeToPar">Only works if the remote player got a hole in 1 for now</param>
        public abstract void OnRemotePlayerFinishCourse(CourseManager course, CourseHole hole, int score, int scoreRelativeToPar);
    }
}