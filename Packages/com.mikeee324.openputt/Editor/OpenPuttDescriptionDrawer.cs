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
        var style = EditorStyles.helpBox;
        var height = style.CalcHeight(new GUIContent(Attr.Description), EditorGUIUtility.currentViewWidth - 19);
        return Mathf.Max(height, EditorGUIUtility.singleLineHeight * 2) + 6;
    }

    public override void OnGUI(Rect position)
    {
        position.height -= 4;
        EditorGUI.HelpBox(position, Attr.Description, MessageType.Info);
    }
}
#endif
