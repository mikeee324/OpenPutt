using UdonSharp;
using UnityEngine;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CourseStartPosition : UdonSharpBehaviour
    {
        [OpenPuttDescription("Marks a valid starting spot for this course. Players are placed here when they begin or reset the hole.")]
        public CourseManager courseManager;
        public Collider myCollider;
    }
}