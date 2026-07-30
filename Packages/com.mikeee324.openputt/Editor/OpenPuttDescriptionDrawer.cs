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

    // GetHeight runs on every IMGUI event, and CalcHeight does a full text layout pass. The result only changes when
    // the inspector is resized, so keep the last one and the width it was measured at.
    private GUIContent cachedContent;
    private float cachedWidth = -1f;
    private float cachedHeight;

    public override float GetHeight()
    {
        // When this field is a foldout group leader, the foldout drawer redraws it as the first member inside the
        // group. Collapse to zero there so the leader's help box only renders once, at its natural spot above the header.
        if (OpenPuttFoldoutGroupDrawer.DrawingGroupMember)
            return 0f;

        var width = EditorGUIUtility.currentViewWidth - 19;
        if (cachedContent == null)
            cachedContent = new GUIContent(Attr.Description);
        else if (Mathf.Approximately(width, cachedWidth))
            return cachedHeight;

        var height = EditorStyles.helpBox.CalcHeight(cachedContent, width);
        cachedWidth = width;
        cachedHeight = Mathf.Max(height, EditorGUIUtility.singleLineHeight * 2) + 6;
        return cachedHeight;
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
