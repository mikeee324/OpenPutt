using UnityEngine;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Put this on a field to draw it inside a collapsible foldout group with a header of the given name.<br/>
    /// Consecutive fields sharing the same group name are drawn together under the same foldout.<br/>
    /// Works automatically on any script via <see cref="OpenPuttFoldoutGroupDrawer"/> - no custom editor needed.
    /// </summary>
    public class OpenPuttFoldoutGroupAttribute : PropertyAttribute
    {
        public readonly string GroupName;
        public readonly bool DefaultExpanded;

        public OpenPuttFoldoutGroupAttribute(string groupName, bool defaultExpanded = true)
        {
            GroupName = groupName;
            DefaultExpanded = defaultExpanded;
        }
    }
}
