
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttEventHandler : UdonSharpBehaviour
    {
        public OpenPutt openPutt;

        /// <summary>
        /// Called when OpenPutt has assigned and finished setting up a PlayerManager for a player
        /// </summary>
        /// <param name="player">The player that was initialized</param>
        /// <param name="playerManager">The PlayerManager that was assigned</param>
        public virtual void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {
            if (player.isLocal)
                openPutt.LocalPlayerManager = playerManager;

            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerInitialised(player, playerManager);
        }

        /// <summary>
        /// Called when a player hits their ball
        /// </summary>
        /// <param name="player">The player who hit the ball</param>
        /// <param name="speed">The velocity magnitude that was just applied to the ball</param>
        public virtual void OnPlayerBallHit(VRCPlayerApi player, float speed)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerBallHit(player, speed);
        }

        /// <summary>
        /// Called when a player's ball has stopped moving
        /// </summary>
        /// <param name="player">The player whose ball stopped</param>
        public virtual void OnPlayerBallStopped(VRCPlayerApi player)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerBallStopped(player);
        }

        /// <summary>
        /// Called when a player ball drops into a course hole
        /// </summary>
        /// <param name="player">The player who finished the course</param>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        /// <param name="score">The absolute score that the player got on the course</param>
        /// <param name="scoreRelativeToPar">The score relative to the par on this course</param>
        /// <param name="totalHits">The total number of hits the player took on this course</param>
        public virtual void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar, int totalHits)
        {
            if (Utilities.IsValid(openPutt.SFXController) && Utilities.IsValid(course) && Utilities.IsValid(hole))
                openPutt.SFXController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, !player.isLocal);

            if (totalHits == 1)
            {
                if (Utilities.IsValid(openPutt.SFXController) && Utilities.IsValid(hole))
                    openPutt.SFXController.PlayHoleInOneSoundAtPosition(hole.transform.position, !player.isLocal);
            }
            else
            {
                if (Utilities.IsValid(openPutt.SFXController) && Utilities.IsValid(hole))
                    openPutt.SFXController.PlayScoreSoundAtPosition(hole.transform.position, totalHits, scoreRelativeToPar, !player.isLocal);
            }

            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerFinishCourse(player, course, hole, score, scoreRelativeToPar, totalHits);
        }

        /// <summary>
        /// Called when a player resets their score
        /// </summary>
        /// <param name="player">The player who reset their score</param>
        public virtual void OnPlayerScoreReset(VRCPlayerApi player)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerScoreReset(player);
        }

        /// <summary>
        /// Called when a player's club type changes
        /// </summary>
        /// <param name="player">The player whose club type changed</param>
        /// <param name="newClubType">The new club type</param>
        public virtual void OnPlayerClubTypeChanged(VRCPlayerApi player, GolfClubType newClubType)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerClubTypeChanged(player, newClubType);
        }

        public virtual void OnPlayerHandednessChanged(VRCPlayerApi player, VRC_Pickup.PickupHand newHand)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerHandednessChanged(player, newHand);
        }

        public void OnPortableScoreboardOpened()
        {
            openPutt.hasUsedPortableScoreboard = true;

            if (Utilities.IsValid(openPutt.uiController))
                openPutt.uiController.UpdateButtonStates();
        }
    }
}
