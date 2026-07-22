#if UNITY_EDITOR
using dev.mikeee324.OpenPutt;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders the HelpBox for <see cref="OpenPuttDescriptionAttribute"/>. Applies automatically to any field with that attribute - no custom editor needed.
/// </summary>
[CustomPropertyDrawer(typeof(OpenPuttDescriptionAttribute))]
public class OpenPuttDescriptionDrawer : DecoratorDrawer
{
    private OpenPuttDescriptionAttribute Attr => (OpenPuttDescriptionAttribute)attribute;

    public override float GetHeight()
    {
        // When this field is a foldout group leader, the foldout drawer redraws it as the first member inside the
        // group. Collapse to zero there so the leader's help box only renders once, at its natural spot above the header.
        if (OpenPuttFoldoutGroupDrawer.DrawingGroupMember)
            return 0f;

        var style = EditorStyles.helpBox;
        var height = style.CalcHeight(new GUIContent(Attr.Description), EditorGUIUtility.currentViewWidth - 19);
        return Mathf.Max(height, EditorGUIUtility.singleLineHeight * 2) + 6;
    }

    public override void OnGUI(Rect position)
    {
        if (OpenPuttFoldoutGroupDrawer.DrawingGroupMember)
            return;

        position.height -= 4;
        EditorGUI.HelpBox(position, Attr.Description, MessageType.Info);
    }
}
#endif
