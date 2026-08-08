using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Displays the hole number/name on a marker.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CourseMarker : UdonSharpBehaviour
    {
        [OpenPuttDescription("Shows the hole number/name and par score on a sign or marker, using the settings from the course it is linked to.")]
        [Tooltip("The course that this marker is attached to")]
        public CourseManager courseManager;

        [OpenPuttFoldoutGroup("UI References")]
        public TextMeshProUGUI topText;

        [OpenPuttFoldoutGroup("UI References")]
        public TextMeshProUGUI bottomText;

        void Start()
        {
        }

        public void ResetUI()
        {
            if (!Utilities.IsValid(courseManager)) return;

            if (Utilities.IsValid(topText))
            {
                if (courseManager.scoreboardLongName.Length > 0)
                    topText.text = courseManager.scoreboardLongName;
                else
                    topText.text = $"Hole {courseManager.holeNumber + 1}";
            }

            if (Utilities.IsValid(bottomText))
            {
                if (courseManager.parScore > 0 && courseManager.courseType == CourseType.Standard)
                {
                    bottomText.text = $"<size=60%>Par</size>\r\n{courseManager.parScore}";
                }
                else
                {
                    bottomText.text = "";
                }
            }
        }
    }
}