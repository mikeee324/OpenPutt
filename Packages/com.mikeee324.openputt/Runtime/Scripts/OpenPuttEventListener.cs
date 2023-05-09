using UdonSharp;

namespace mikeee324.OpenPutt
{
    public abstract class OpenPuttEventListener : UdonSharpBehaviour
    {
        /// <summary>
        /// Called when a remote player gets a hole in one
        /// </summary>
        /// <param name="course">Which course the player got a hole in one on</param>
        /// <param name="hole">The hole that the ball entered</param>
        public abstract void OnRemotePlayerHoleInOne(CourseManager course, CourseHole hole);

        /// <summary>
        /// Called when a remote player ball drops into a course hole
        /// </summary>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        public abstract void OnRemotePlayerBallEnterHole(CourseManager course, CourseHole hole);

        /// <summary>
        /// Called when the local player gets a hole in one
        /// </summary>
        /// <param name="course">Which course the player got a hole in one on</param>
        /// <param name="hole">The hole that the ball entered</param>
        public abstract void OnLocalPlayerHoleInOne(CourseManager course, CourseHole hole);

        /// <summary>
        /// Called when the local player ball drops into a course hole
        /// </summary>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        public abstract void OnLocalPlayerBallEnterHole(CourseManager course, CourseHole hole);

        /// <summary>
        /// Called when local player hits their ball
        /// </summary>
        public abstract void OnLocalPlayerBallHit();
    }
}