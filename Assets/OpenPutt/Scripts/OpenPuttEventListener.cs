using UnityEngine;
using UdonSharp;

namespace mikeee324.OpenPutt
{
    public abstract class OpenPuttEventListener : UdonSharpBehaviour
    {
        /// <summary>
        /// Called when any player gets a hole in one
        /// </summary>
        /// <param name="course">Which course the player got a hole in one on</param>
        /// <param name="hole">The hole that the ball entered</param>
        public abstract void OnHoleInOne(CourseManager course, CourseHole hole);

        /// <summary>
        /// Called when any player ball drops into a course hole
        /// </summary>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        public abstract void OnBallEnterHole(CourseManager course, CourseHole hole);
    }
}