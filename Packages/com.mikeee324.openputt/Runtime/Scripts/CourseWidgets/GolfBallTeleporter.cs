using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class GolfBallTeleporter : UdonSharpBehaviour
    {
        public BoxCollider localCollider;

        [Header("Ball Teleports To (Yellow sphere gizmo)"), Tooltip("Where the ball will be teleported to")]
        public Transform targetPosition;

        [Header("Ball Rolls Towards (Gray sphere gizmo)"), Tooltip("Where the ball will go towards after being teleported")]
        public Transform launchPosition;

        [Header("Settings"), Tooltip("How long to wait before teleporting in seconds")]
        public float teleportDelay = 0f;

        [Tooltip("Toggles if ths ball is hidden during the teleport delay or if it just gets frozen at the target position")]
        public bool hideBallDuringDelay = false;

        [Tooltip("Toggles whether the speed of the ball will be set to targetSpeed after it is teleported")]
        public bool updateSpeedAfterTeleport = false;

        [Tooltip("The speed of the ball after it is launched (m/s) - !Only applies if updateSpeedAfterTeleport is enabled!")]
        public float speedAfterTeleport = 0.5f;

        public bool networkAudio = false;
        public AudioSource teleportEnterAudio;
        public AudioSource teleportExitAudio;

        private float velocityBeforeTeleport = -1f;
        private GolfBallController golfBall;
        private Vector3 respawnPositionBeforeTeleport;

        private void OnTriggerEnter(Collider other)
        {
            // Already waiting for a teleport to happen
            if (velocityBeforeTeleport >= 0f) return;

            var ball = other.gameObject.GetComponent<GolfBallController>();
            if (Utilities.IsValid(ball) && ball.playerManager.Owner == Networking.LocalPlayer)
            {
                golfBall = ball;

                velocityBeforeTeleport = ball.ballRigidbody.velocity.magnitude;

                // Capture the player's last-hit respawn position before BallIsMoving's setter has a chance to
                // overwrite it with the teleporter entrance position
                respawnPositionBeforeTeleport = golfBall.respawnWorldPosition;

                golfBall.BallIsMoving = false;

                golfBall._SetPosition(hideBallDuringDelay ? Vector3.up * (golfBall.openPuttSync.autoRespawnHeight * .95f) : targetPosition.position);
                golfBall._SetRespawnPosition(respawnPositionBeforeTeleport);

                golfBall = ball;

                golfBall.isHeldInTeleporter = true;

                if (Utilities.IsValid(teleportEnterAudio))
                {
                    if (networkAudio)
                        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayEnterAudio));
                    else
                        PlayEnterAudio();
                }

                SendCustomEventDelayedSeconds(nameof(_Teleport), teleportDelay);
            }
        }

        [NetworkCallable(maxEventsPerSecond: 5)]
        public void PlayEnterAudio()
        {
            if (!Utilities.IsValid(teleportEnterAudio)) return;

            teleportEnterAudio.Play();
        }

        [NetworkCallable(maxEventsPerSecond: 5)]
        public void PlayExitAudio()
        {
            if (!Utilities.IsValid(teleportExitAudio)) return;

            teleportExitAudio.Play();
        }

        public void _Teleport()
        {
            if (!Utilities.IsValid(launchPosition) || !Utilities.IsValid(targetPosition)) return;

            var newMagnitude = updateSpeedAfterTeleport ? speedAfterTeleport : velocityBeforeTeleport;

            if (Utilities.IsValid(golfBall))
            {
                golfBall.isHeldInTeleporter = false;
                golfBall._SetPosition(targetPosition.position);
                golfBall._SetRespawnPosition(respawnPositionBeforeTeleport);
                golfBall.BallIsMoving = true;
                golfBall.ballRigidbody.velocity = (launchPosition.position - targetPosition.position).normalized * newMagnitude;
            }

            if (Utilities.IsValid(teleportExitAudio))
            {
                if (networkAudio)
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayExitAudio));
                else
                    PlayExitAudio();
            }

            golfBall = null;
            velocityBeforeTeleport = -1f;
        }


        private void OnDrawGizmos()
        {
            if (targetPosition == null || launchPosition == null || localCollider == null) return; // Added null check for localCollider

            OpenPuttGizmoUtils.DrawSolidAndWireBoxCollider(localCollider, new Color(0f, 1f, 0f, 0.3f), Color.green);

            // Draw transparent cube from the center of the collider to the target position
            OpenPuttGizmoUtils.DrawCubeArrow(localCollider.transform.TransformPoint(localCollider.center), targetPosition.position, Color.yellow, 0.0225f);

            // Draw transparent cube at the target position to show the direction the ball will roll out
            var directionStart = targetPosition.position;
            var directionEnd = launchPosition.position;

            OpenPuttGizmoUtils.DrawCubeArrow(directionStart, directionEnd, new Color(.4f, .4f, 0.4f, 0.5f), 0.0225f);

            // Draw spheres at target and launch positions
            OpenPuttGizmoUtils.DrawSphereMarker(targetPosition.position, 0.0225f, Color.yellow);
            OpenPuttGizmoUtils.DrawSphereMarker(launchPosition.position, 0.0225f, Color.gray);
        }
    }
}