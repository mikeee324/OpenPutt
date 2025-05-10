using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace dev.mikeee324.OpenPutt
{
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

        private void OnTriggerEnter(Collider other)
        {
            // Already waiting for a teleport to happen
            if (velocityBeforeTeleport >= 0f) return;

            var ball = other.gameObject.GetComponent<GolfBallController>();
            if (Utilities.IsValid(ball) && ball.playerManager.Owner == Networking.LocalPlayer)
            {
                golfBall = ball;

                velocityBeforeTeleport = ball.ballRigidbody.velocity.magnitude;

                golfBall.BallIsMoving = false;

                var oldPos = golfBall.respawnPosition;
                golfBall.SetPosition(hideBallDuringDelay ? Vector3.up * (golfBall.openPuttSync.autoRespawnHeight * .95f) : targetPosition.position);
                golfBall.SetRespawnPosition(oldPos);

                golfBall = ball;

                golfBall.isHeldInTeleporter = true;

                if (Utilities.IsValid(teleportEnterAudio))
                {
                    if (networkAudio)
                        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayEnterAudio));
                    else
                        PlayEnterAudio();
                }

                SendCustomEventDelayedSeconds(nameof(Teleport), teleportDelay);
            }
        }

        public void PlayEnterAudio()
        {
            if (!Utilities.IsValid(teleportEnterAudio)) return;

            teleportEnterAudio.Play();
        }

        public void PlayExitAudio()
        {
            if (!Utilities.IsValid(teleportExitAudio)) return;

            teleportExitAudio.Play();
        }

        public void Teleport()
        {
            if (!Utilities.IsValid(launchPosition) || !Utilities.IsValid(targetPosition)) return;

            var newMagnitude = updateSpeedAfterTeleport ? speedAfterTeleport : velocityBeforeTeleport;

            if (Utilities.IsValid(golfBall))
            {
                var oldPos = golfBall.respawnPosition;
                golfBall.isHeldInTeleporter = false;
                golfBall.SetPosition(targetPosition.position);
                golfBall.SetRespawnPosition(oldPos);
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

            // Draw the transparent box for the collider
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);                 // Green and semi-transparent
            Gizmos.matrix = localCollider.transform.localToWorldMatrix; // Apply the collider's transform
            Gizmos.DrawCube(localCollider.center, localCollider.size);
            Gizmos.color = Color.green; // Reset color for the wireframe
            Gizmos.DrawWireCube(localCollider.center, localCollider.size);

            Gizmos.matrix = Matrix4x4.identity;

            // Draw transparent cube from the center of the collider to the target position
            DrawCubeArrow(localCollider.transform.TransformPoint(localCollider.center), targetPosition.position, Color.yellow);

            // Draw transparent cube at the target position to show the direction the ball will roll out
            var directionStart = targetPosition.position;
            var directionEnd = launchPosition.position;

            DrawCubeArrow(directionStart, directionEnd, new Color(.4f, .4f, 0.4f, 0.5f));

            // Draw spheres at target and launch positions
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(targetPosition.position, 0.0225f);
            Gizmos.DrawWireSphere(targetPosition.position, 0.0225f);

            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(launchPosition.position, 0.0225f);
            Gizmos.DrawWireSphere(launchPosition.position, 0.0225f);
        }

        private void DrawCubeArrow(Vector3 directionStart, Vector3 directionEnd, Color color)
        {
            DrawTransparentCubeLine(directionStart, directionEnd, color, 0.0225f); // Yellow and semi-transparent, thin cube

            // Draw the arrowhead using thin transparent cubes
            var arrowDir = (directionEnd - directionStart).normalized;
            var arrowHeadLength = 0.2f;
            var arrowHeadAngle = 30f;

            // Calculate directions for the arrowhead lines
            var left = Quaternion.LookRotation(arrowDir) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
            var right = Quaternion.LookRotation(arrowDir) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;

            DrawTransparentCubeLine(directionEnd, directionEnd + left * arrowHeadLength, color, 0.0225f);  // Cyan and semi-transparent, thin cube
            DrawTransparentCubeLine(directionEnd, directionEnd + right * arrowHeadLength, color, 0.0225f); // Cyan and semi-transparent, thin cube
        }

        // Helper method to draw a transparent cube representing a line segment
        private void DrawTransparentCubeLine(Vector3 start, Vector3 end, Color color, float thickness)
        {
            var direction = end - start;
            var distance = direction.magnitude;
            if (distance <= 0.0001f) return; // Avoid drawing tiny or zero-length cubes

            var center = (start + end) / 2f;
            var rotation = Quaternion.LookRotation(direction);
            var scale = new Vector3(thickness, thickness, distance);

            Gizmos.color = color;

            // Apply position, rotation, and scale
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, scale);

            // Draw the cube
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

            // Reset the matrix
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}