
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// This is responsible for receiving events from OpenPutt and then passing them on to any OpenPuttEventListeners that are in the world.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttEventHandler : UdonSharpBehaviour
    {
        public OpenPutt openPutt;

        /// <summary>
        /// Called when OpenPutt has assigned and finished setting up a PlayerManager for a player
        /// <br/>
        /// <b>Fired for the local player only (for now)</b>
        /// </summary>
        /// <param name="player">The player that was initialized</param>
        /// <param name="playerManager">The PlayerManager that was assigned</param>
        public void OnPlayerInitialised(VRCPlayerApi player, PlayerManager playerManager)
        {
            if (player.isLocal)
                openPutt.LocalPlayerManager = playerManager;

            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerInitialised(player, playerManager);
        }

        /// <summary>
        /// Called when a player hits their ball
        /// <br/>
        /// <b>Fired for the local player only (for now)</b>
        /// </summary>
        /// <param name="player">The player who hit the ball</param>
        /// <param name="speed">The velocity magnitude that was just applied to the ball</param>
        public void OnPlayerBallHit(VRCPlayerApi player, float speed)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerBallHit(player, speed);
        }

        /// <summary>
        /// Called when a player hits the ball and their score for the current hole exceeds the course's max score
        /// <br/>
        /// <b>Fired for all players</b>
        /// </summary>
        /// <param name="player">The player who hit the max score</param>
        /// <param name="course">The course the player was playing on</param>
        public void OnPlayerHitCourseMaxScore(VRCPlayerApi player, CourseManager course)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerHitCourseMaxScore(player, course);
        }

        /// <summary>
        /// Called when a player's ball has stopped moving
        /// <br/>
        /// <b>Fired for the local player only (for now)</b>
        /// </summary>
        /// <param name="player">The player whose ball stopped</param>
        public void OnPlayerBallStopped(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player))
                return;

            // TODO: Possibly check here if the player hit max score instead, so we can wait until the ball stops and let the player see where the ball went after the last first

            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerBallStopped(player);
        }

        /// <summary>
        /// Called when a player ball drops into a course hole
        /// <br/>
        /// <b>Fired for all players</b>
        /// </summary>
        /// <param name="player">The player who finished the course</param>
        /// <param name="course">Which course the player was playing on</param>
        /// <param name="hole">The hole that the ball entered</param>
        /// <param name="score">The absolute score that the player got on the course</param>
        /// <param name="scoreRelativeToPar">The score relative to the par on this course</param>
        /// <param name="totalHits">The total number of hits the player took on this course</param>
        public void OnPlayerFinishCourse(VRCPlayerApi player, CourseManager course, CourseHole hole, int score, int scoreRelativeToPar, int totalHits)
        {
            if (Utilities.IsValid(openPutt.sfxController) && Utilities.IsValid(course) && Utilities.IsValid(hole))
                openPutt.sfxController.PlayBallHoleSoundAtPosition(course.holeNumber, hole.transform.position, !player.isLocal);

            if (Utilities.IsValid(openPutt.sfxController) && Utilities.IsValid(hole))
                openPutt.sfxController.PlayScoreSoundAtPosition(hole.transform.position, totalHits, scoreRelativeToPar, !player.isLocal);

            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerFinishCourse(player, course, hole, score, scoreRelativeToPar, totalHits);
        }

        /// <summary>
        /// Called when a player resets their score
        /// <br/>
        /// <b>Fired for the local player only (for now)</b>
        /// </summary>
        /// <param name="player">The player who reset their score</param>
        public void OnPlayerScoreReset(VRCPlayerApi player)
        {
            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerScoreReset(player);
        }

        /// <summary>
        /// Called when a player's club type changes
        /// <br/>
        /// <b>Fired for all players</b>
        /// </summary>
        /// <param name="player">The player whose club type changed</param>
        /// <param name="newClubType">The new club type</param>
        public void OnPlayerClubTypeChanged(VRCPlayerApi player, GolfClubType newClubType)
        {
            if (player.isLocal && Utilities.IsValid(openPutt.uiController))
                openPutt.uiController.UpdateButtonStates();

            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerClubTypeChanged(player, newClubType);
        }

        /// <summary>
        /// Called when a player's handedness/pickup hand changes
        /// <br/>
        /// <b>Fired for all players</b>
        /// </summary>
        public void OnPlayerHandednessChanged(VRCPlayerApi player, VRC_Pickup.PickupHand newHand)
        {
            if (player.isLocal && Utilities.IsValid(openPutt.uiController))
                openPutt.uiController.UpdateButtonStates();

            foreach (var listener in openPutt.eventListeners)
                if (Utilities.IsValid(listener))
                    listener.OnPlayerHandednessChanged(player, newHand);
        }

        /// <summary>
        /// Called when the portable scoreboard UI is opened by the local player
        /// <br/>
        /// <b>Fired for the local player only (for now)</b>
        /// </summary>
        public void OnPortableScoreboardOpened()
        {
            openPutt.hasUsedPortableScoreboard = true;

            if (Utilities.IsValid(openPutt.uiController))
                openPutt.uiController.UpdateButtonStates();
        }
    }
}
