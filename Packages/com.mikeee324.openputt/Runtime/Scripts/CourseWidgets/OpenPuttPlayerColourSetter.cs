using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>Sets the local player's ball colour, either from the inspector field or a value passed in code.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OpenPuttPlayerColourSetter : UdonSharpBehaviour
    {
        #region Public Settings

        [OpenPuttDescription("Applies a colour to the local player's ball (and any other renderers listed below), either automatically on start, when interacted with, or when the ball/club touches this object.")]
        public OpenPutt openPutt;

        [Tooltip("The colour that will be applied to the local player's ball when this behaviour runs")]
        public Color ballColour = Color.white;

        [Tooltip("Additional renderers that will also have this colour applied to their _Color/_EmissionColor properties")]
        public Renderer[] meshesToColour;

        [OpenPuttFoldoutGroup("Activation Settings")]
        [Tooltip("If enabled the colour will be applied when this object is interacted with")]
        public bool enableInteract = true;

        [OpenPuttFoldoutGroup("Activation Settings")]
        [Tooltip("If enabled the colour will be applied when the local player's ball or club enters this object's trigger collider")]
        public bool enableTriggerCollision = true;

        #endregion

        #region Internal Vars

        private MaterialPropertyBlock propertyBlock;

        #endregion

        #region Public API

        /// <summary>Applies the inspector-set ballColour to the local player's ball.</summary>
        public void SetColour()
        {
            SetColourTo(ballColour);
        }

        /// <summary>Applies the given colour to the local player's ball.</summary>
        /// <param name="colour">The colour to apply to the local player's ball.</param>
        public void SetColourTo(Color colour)
        {
            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager))
                return;

            var playerManager = openPutt.LocalPlayerManager;

            playerManager.BallColor = colour;

            playerManager._RequestSync(syncNow: true);
        }

        /// <summary>Applies the given colour to each renderer in meshesToColour.</summary>
        /// <param name="colour">The colour to apply to each renderer.</param>
        private void ApplyColourToMeshes(Color colour)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            foreach (var renderer in meshesToColour)
            {
                if (!Utilities.IsValid(renderer)) continue;

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", colour);
                propertyBlock.SetColor("_EmissionColor", colour);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        #endregion

        #region Unity Events

        private void Start()
        {
            ApplyColourToMeshes(ballColour);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enableTriggerCollision) return;

            if (!Utilities.IsValid(openPutt) || !Utilities.IsValid(openPutt.LocalPlayerManager))
                return;

            var playerManager = openPutt.LocalPlayerManager;

            var golfBall = playerManager.golfBall;
            if (Utilities.IsValid(golfBall) && other.gameObject == golfBall.gameObject)
            {
                SetColour();
                return;
            }

            var golfClub = playerManager.golfClub;
            if (Utilities.IsValid(golfClub) && (other == golfClub.handleCollider || other == golfClub.shaftCollider))
                SetColour();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        // Lets meshesToColour preview ballColour in the editor without entering play mode
        private void OnValidate()
        {
            ApplyColourToMeshes(ballColour);
        }
#endif

        #endregion
    }
}
