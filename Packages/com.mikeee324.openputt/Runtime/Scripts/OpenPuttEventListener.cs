using UdonSharp;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    public class OpenPuttEventListener : UdonSharpBehaviour
    {
        /// <summary>
        /// Called when OpenPutt has assigned and finished setting up a PlayerManager for a player
        /// </summary>
        /// <param name="player">The player that was initialized</param>
        /// <param name="playerManager">The PlayerManager that was assigned</param>
        public virtual void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {

        }

        /// <summary>
        /// Called when a player hits their ball
        /// </summary>
        /// <param name="player">The player who hit the ball</param>
        /// <param name="speed">The velocity magnitude that was just applied to the ball</param>
        public virtual void OnPlayerBallHit(VRCPlayerApi player, float speed)
        {

        }

        /// <summary>
        /// Called when a player's ball has stopped moving
        /// </summary>
        /// <param name="player">The player whose ball stopped</param>
        public virtual void OnPlayerBallStopped(VRCPlayerApi player)
        {

        }

        /// <summary>
        /// Called when a player ball drops into a course hole
        /// </summary>
        /// <param name="player">The player who finished the course</param>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        /// <param name="score">The absolute score that the player got on the course</param>
        /// <param name="scoreRelativeToPar">The score relative to the par on this course</param>
        public virtual void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar)
        {

        }

        /// <summary>
        /// Called when a player resets their score
        /// </summary>
        /// <param name="player">The player who reset their score</param>
        public virtual void OnPlayerScoreReset(VRCPlayerApi player)
        {

        }

        /// <summary>
        /// Called when a player's club type changes
        /// </summary>
        /// <param name="player">The player whose club type changed</param>
        /// <param name="newClubType">The new club type</param>
        public virtual void OnPlayerClubTypeChanged(VRCPlayerApi player, GolfClubType newClubType)
        {

        }

        /// <summary>
        /// Called when a player's left handed mode changes (or is set on initialisation/load)
        /// </summary>
        /// <param name="player">The player whose handedness changed</param>
        /// <param name="isLeftHanded">True if the player is now in left-handed mode</param>
        public virtual void OnPlayerHandednessChanged(VRCPlayerApi player, VRC_Pickup.PickupHand newHand)
        {

        }
    }
}