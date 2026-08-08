using UnityEngine;

namespace dev.mikeee324.OpenPutt
{
    /// <summary>
    /// Put this on a script's first field to show a permanent description box above it in the inspector, explaining what the script does in plain language.<br/>
    /// Works like [Header] - no custom editor needed, drawn automatically by <see cref="OpenPuttFoldoutGroupDrawer"/>'s sibling decorator drawer.
    /// </summary>
    public class OpenPuttDescriptionAttribute : PropertyAttribute
    {
        public readonly string Description;

        public OpenPuttDescriptionAttribute(string description)
        {
            Description = description;
        }
    }
}
