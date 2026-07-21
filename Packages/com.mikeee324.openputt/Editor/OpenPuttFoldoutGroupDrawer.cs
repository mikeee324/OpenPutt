#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using dev.mikeee324.OpenPutt;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders the collapsible foldout for <see cref="OpenPuttFoldoutGroupAttribute"/>. Applies automatically to any field with that attribute - no custom editor needed.<br/>
/// The first field declared in a group draws the foldout header and (when expanded) every field in the group; the rest of the group's fields draw nothing themselves.
/// </summary>
[CustomPropertyDrawer(typeof(OpenPuttFoldoutGroupAttribute))]
public class OpenPuttFoldoutGroupDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, bool> FoldoutStates = new Dictionary<string, bool>();

    // While the leader is drawing a sibling field, that sibling's own drawer instance gets invoked again by Unity
    // (it carries the same attribute). This flag tells that nested call to fall back to the field's normal/default
    // rendering instead of re-running the group logic (which would otherwise draw nothing for a non-leader field).
    private static bool drawingGroupMember;

    // Keys of type+field combinations we've already warned about, so the misconfiguration is logged once per
    // domain reload rather than every OnGUI frame.
    private static readonly HashSet<string> WarnedDescriptionCombos = new HashSet<string>();

    private OpenPuttFoldoutGroupAttribute Attr => (OpenPuttFoldoutGroupAttribute)attribute;


    // Unity never routes an attribute-based PropertyDrawer to an array/List field's own container property (only
    // to its elements), so grouping one alongside our leader would draw it once via the leader's manual loop and
    // then a second time via Unity's own default array UI in the normal top-level iteration. Excluding them here
    // means they just render at their normal default position instead - not grouped, but not duplicated either.
    private static bool IsListLike(FieldInfo f) => f.FieldType != typeof(string) && typeof(IList).IsAssignableFrom(f.FieldType);

    private List<FieldInfo> GetGroupFields(SerializedProperty property)
    {
        var targetType = property.serializedObject.targetObject.GetType();
        var groupFields = targetType
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => !IsListLike(f) && f.GetCustomAttribute<OpenPuttFoldoutGroupAttribute>()?.GroupName == Attr.GroupName)
            // Fields of non-serializable types (eg. MaterialPropertyBlock) have no backing SerializedProperty -
            // skip them here rather than crashing later when FindProperty comes back null.
            .Where(f => property.serializedObject.FindProperty(f.Name) != null)
            .ToList();

        // [OpenPuttDescription] is a DecoratorDrawer and always draws itself in Unity's normal top-level iteration -
        // it has no way to know its field was pulled into a foldout, so the help box ends up orphaned at the field's
        // natural position while the field itself is relocated under the header. The two attributes don't compose;
        // warn the developer (once) rather than silently rendering a broken inspector.
        foreach (var f in groupFields)
        {
            if (f.GetCustomAttribute<OpenPuttDescriptionAttribute>() == null)
                continue;
            var warnKey = $"{targetType.FullName}.{f.Name}";
            if (WarnedDescriptionCombos.Add(warnKey))
                Debug.LogWarning($"[OpenPutt] Field '{targetType.Name}.{f.Name}' has both [OpenPuttDescription] and [OpenPuttFoldoutGroup]. These don't compose - the description box will render detached from the foldout. Move the description to a field that isn't in a group.");
        }

        return groupFields;
    }

    private bool IsGroupLeader(List<FieldInfo> groupFields) => groupFields.Count > 0 && groupFields[0] == fieldInfo;

    private string StateKey(SerializedProperty property) => $"{property.serializedObject.targetObject.GetInstanceID()}_{Attr.GroupName}";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (drawingGroupMember)
            return EditorGUI.GetPropertyHeight(property, label, true);

        var groupFields = GetGroupFields(property);
        if (!IsGroupLeader(groupFields))
            return -EditorGUIUtility.standardVerticalSpacing;

        var headerHeight = EditorGUIUtility.singleLineHeight;
        if (!FoldoutStates.TryGetValue(StateKey(property), out var expanded))
            expanded = Attr.DefaultExpanded;
        if (!expanded)
            return headerHeight;

        var total = headerHeight;
        foreach (var f in groupFields)
            total += GetMemberHeight(property.serializedObject.FindProperty(f.Name)) + EditorGUIUtility.standardVerticalSpacing;
        return total;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (drawingGroupMember)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        var groupFields = GetGroupFields(property);
        if (!IsGroupLeader(groupFields))
            return;

        var key = StateKey(property);
        if (!FoldoutStates.TryGetValue(key, out var expanded))
            expanded = Attr.DefaultExpanded;

        var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var previousIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Mutate the shared style in place rather than copying it, so every layout metric (padding, margin, icon
        // rects) stays byte-identical to Unity's own native array foldouts - only the font weight changes.
        // The restore runs in finally: EditorStyles.foldout is global, so if Foldout throws we must not leak the
        // bold font onto every other foldout in the editor until the next domain reload.
        var previousFontStyle = EditorStyles.foldout.fontStyle;
        EditorStyles.foldout.fontStyle = FontStyle.Bold;
        try
        {
            expanded = EditorGUI.Foldout(headerRect, expanded, Attr.GroupName, true, EditorStyles.foldout);
        }
        finally
        {
            EditorStyles.foldout.fontStyle = previousFontStyle;
        }

        EditorGUI.indentLevel = previousIndent;
        FoldoutStates[key] = expanded;

        if (!expanded)
            return;

        var y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.indentLevel++;
        foreach (var f in groupFields)
        {
            var prop = property.serializedObject.FindProperty(f.Name);
            var height = GetMemberHeight(prop);
            DrawMember(new Rect(position.x, y, position.width, height), prop);
            y += height + EditorGUIUtility.standardVerticalSpacing;
        }
        EditorGUI.indentLevel--;
    }

    private float GetMemberHeight(SerializedProperty prop)
    {
        drawingGroupMember = true;
        try
        {
            return EditorGUI.GetPropertyHeight(prop, true);
        }
        finally
        {
            drawingGroupMember = false;
        }
    }

    private void DrawMember(Rect rect, SerializedProperty prop)
    {
        drawingGroupMember = true;
        try
        {
            EditorGUI.PropertyField(rect, prop, true);
        }
        finally
        {
            drawingGroupMember = false;
        }
    }
}
#endif
