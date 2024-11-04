using UdonSharp;
using UnityEngine;

namespace dev.mikeee324.OpenPutt
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CourseStartPosition : UdonSharpBehaviour
    {
        public CourseManager courseManager;
        public Collider myCollider;
    }
}

