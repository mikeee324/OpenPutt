
using TMPro;
using UdonSharp;
using UnityEngine;

namespace mikeee324.OpenPutt
{
    /// <summary>
    /// This script doesn't do much other than display the hole number or name right now.. I intend this to allow people to summon their ball or golf club using the course markers in the future.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CourseMarker : UdonSharpBehaviour
    {
        [Tooltip("The course that this marker is attached to")]
        public CourseManager courseManager;

        public TextMeshProUGUI topText;
        public TextMeshProUGUI bottomText;

        void Start()
        {
        }

        public void ResetUI()
        {
            if (courseManager == null) return;

            if (topText != null)
            {
                if (courseManager.scoreboardLongName.Length > 0)
                    topText.text = courseManager.scoreboardLongName;
                else
                    topText.text = $"Hole {courseManager.holeNumber + 1}";
            }

            if (bottomText != null)
            {
                if (courseManager.parScore > 0 && !courseManager.drivingRangeMode)
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