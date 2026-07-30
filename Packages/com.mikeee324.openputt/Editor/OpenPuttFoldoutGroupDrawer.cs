#if UNITY_EDITOR
using System;
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
    // OpenPuttDescriptionDrawer also reads it (via DrawingGroupMember) to suppress its help box while a member is
    // being drawn inside the group, so the leader's description doesn't render a second time under the header.
    private static bool drawingGroupMember;

    internal static bool DrawingGroupMember => drawingGroupMember;

    // Keys of type+field combinations we've already warned about, so the misconfiguration is logged once per
    // domain reload rather than every OnGUI frame.
    private static readonly HashSet<string> WarnedDescriptionCombos = new HashSet<string>();

    private OpenPuttFoldoutGroupAttribute Attr => (OpenPuttFoldoutGroupAttribute)attribute;

    // Unity keeps one drawer instance per property, so anything that depends only on this drawer's field and its
    // declaring type can be worked out once and reused for the lifetime of the instance. resolvedForType guards the
    // (unlikely) case of an instance being recycled onto a different type.
    private Type resolvedForType;
    private bool resolvedIsListLike;
    private bool resolvedIsLeader;
    private List<FieldInfo> resolvedGroupFields;

    // $"{id}_{group}" was being rebuilt on every GetPropertyHeight and OnGUI for every grouped field - 138 throwaway
    // strings per event on Scoreboard. The instance id only changes when the inspector switches objects.
    private int resolvedStateKeyId;
    private string resolvedStateKey;

    private GUIContent headerLabel;

    // The leader needs a SerializedProperty and a height for each of its members, in both GetPropertyHeight and the
    // OnGUI that immediately follows it. FindProperty is a linear walk of the property tree, so doing it 50 times in
    // each of those two calls is the bulk of what is left. Build the list once in GetPropertyHeight and reuse it in
    // OnGUI - the two run back to back within a single event, so the properties cannot go stale in between.
    private SerializedObject memberCacheOwner;
    private readonly List<SerializedProperty> memberProperties = new List<SerializedProperty>();
    private readonly List<float> memberHeights = new List<float>();


    // Unity never routes an attribute-based PropertyDrawer to an array/List field's own container property (only
    // to its elements), so grouping one alongside our leader would draw it once via the leader's manual loop and
    // then a second time via Unity's own default array UI in the normal top-level iteration. Excluding them here
    // means they just render at their normal default position instead - not grouped, but not duplicated either.
    private static bool IsListLike(FieldInfo f) => f.FieldType != typeof(string) && typeof(IList).IsAssignableFrom(f.FieldType);

    // Both GetPropertyHeight and OnGUI run for every grouped field on every IMGUI event, and IMGUI fires several
    // events per frame - so the reflection scan below used to run thousands of times a frame on big components like
    // Scoreboard (69 grouped fields). Which fields carry the attribute is fixed for a type until the next domain
    // reload (which clears these statics), so the whole result is cached per type + group name.
    private static readonly Dictionary<Type, Dictionary<string, List<FieldInfo>>> GroupFieldCache = new Dictionary<Type, Dictionary<string, List<FieldInfo>>>();

    private List<FieldInfo> GetGroupFields(SerializedProperty property)
    {
        var targetType = property.serializedObject.targetObject.GetType();

        if (!GroupFieldCache.TryGetValue(targetType, out var groupsForType))
        {
            groupsForType = new Dictionary<string, List<FieldInfo>>();
            GroupFieldCache[targetType] = groupsForType;
        }

        if (groupsForType.TryGetValue(Attr.GroupName, out var cached))
            return cached;

        var groupFields = targetType
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => !IsListLike(f) && f.GetCustomAttribute<OpenPuttFoldoutGroupAttribute>()?.GroupName == Attr.GroupName)
            // Fields of non-serializable types (eg. MaterialPropertyBlock) have no backing SerializedProperty -
            // skip them here rather than crashing later when FindProperty comes back null. Whether a field is
            // serialized is a property of the type, so this stays valid for every instance we cache against.
            .Where(f => property.serializedObject.FindProperty(f.Name) != null)
            .ToList();

        groupsForType[Attr.GroupName] = groupFields;

        // [OpenPuttDescription] is a DecoratorDrawer and always draws itself in Unity's normal top-level iteration.
        // On the group LEADER that's exactly right: the field's natural position is the top of the component, so the
        // help box lands directly above the foldout header (OpenPuttDescriptionDrawer suppresses the duplicate that
        // would otherwise appear when the leader is redrawn as a member - see DrawingGroupMember). This composes, so
        // it's the common, supported pattern and we don't warn.
        // On a NON-leader member it can't compose: the decorator draws at that member's natural top-level slot while
        // the field itself is relocated under the header, orphaning the help box. Warn (once) for that case only.
        for (var i = 1; i < groupFields.Count; i++)
        {
            var f = groupFields[i];
            if (f.GetCustomAttribute<OpenPuttDescriptionAttribute>() == null)
                continue;
            var warnKey = $"{targetType.FullName}.{f.Name}";
            if (WarnedDescriptionCombos.Add(warnKey))
                Debug.LogWarning($"[OpenPutt] Field '{targetType.Name}.{f.Name}' has [OpenPuttDescription] but isn't the first field in its '{Attr.GroupName}' group, so the description box renders detached from the foldout. Put the description on the group's first field, or on a field that isn't in a group.");
        }

        return groupFields;
    }

    // Works out (once per drawer instance) whether this field is list-like and whether it leads its group.
    private void Resolve(SerializedProperty property)
    {
        var targetType = property.serializedObject.targetObject.GetType();
        if (resolvedForType == targetType)
            return;

        resolvedForType = targetType;
        resolvedIsListLike = IsListLike(fieldInfo);
        resolvedGroupFields = resolvedIsListLike ? null : GetGroupFields(property);
        resolvedIsLeader = !resolvedIsListLike && resolvedGroupFields.Count > 0 && resolvedGroupFields[0] == fieldInfo;
    }

    private string StateKey(SerializedProperty property)
    {
        var id = property.serializedObject.targetObject.GetInstanceID();
        if (resolvedStateKey == null || resolvedStateKeyId != id)
        {
            resolvedStateKeyId = id;
            resolvedStateKey = $"{id}_{Attr.GroupName}";
        }

        return resolvedStateKey;
    }

    private bool IsExpanded(SerializedProperty property) => FoldoutStates.TryGetValue(StateKey(property), out var expanded) ? expanded : Attr.DefaultExpanded;

    // Fills memberProperties/memberHeights for the group and returns the total height of the members (excluding
    // the header). Only the leader ever calls this, and only while the group is expanded.
    private float BuildMemberCache(SerializedProperty property)
    {
        var serializedObject = property.serializedObject;
        memberCacheOwner = serializedObject;
        memberProperties.Clear();
        memberHeights.Clear();

        var total = 0f;
        drawingGroupMember = true;
        try
        {
            foreach (var f in resolvedGroupFields)
            {
                var prop = serializedObject.FindProperty(f.Name);
                var height = EditorGUI.GetPropertyHeight(prop, true);
                memberProperties.Add(prop);
                memberHeights.Add(height);
                total += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        finally
        {
            drawingGroupMember = false;
        }

        return total;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (drawingGroupMember)
            return EditorGUI.GetPropertyHeight(property, label, true);

        Resolve(property);

        // [OpenPuttFoldoutGroup] can't group an array/List field itself (see IsListLike) - if it's mistakenly
        // put on one anyway, ignore it and fall back to the field's normal rendering rather than drawing nothing.
        if (resolvedIsListLike)
            return EditorGUI.GetPropertyHeight(property, label, true);

        if (!resolvedIsLeader)
            return -EditorGUIUtility.standardVerticalSpacing;

        var headerHeight = EditorGUIUtility.singleLineHeight;
        if (!IsExpanded(property))
        {
            memberCacheOwner = null;
            return headerHeight;
        }

        return headerHeight + BuildMemberCache(property);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (drawingGroupMember)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        Resolve(property);

        if (resolvedIsListLike)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        if (!resolvedIsLeader)
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
            if (headerLabel == null)
                headerLabel = new GUIContent(Attr.GroupName);
            expanded = EditorGUI.Foldout(headerRect, expanded, headerLabel, true, EditorStyles.foldout);
        }
        finally
        {
            EditorStyles.foldout.fontStyle = previousFontStyle;
        }

        EditorGUI.indentLevel = previousIndent;
        FoldoutStates[key] = expanded;

        if (!expanded)
            return;

        // Normally GetPropertyHeight has just run for this same event and left the members cached. It won't have if
        // the click above is what expanded the group, so rebuild in that case (the rects will be a frame stale either
        // way - the next repaint sorts it out, exactly as before).
        if (memberCacheOwner != property.serializedObject)
            BuildMemberCache(property);

        var y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.indentLevel++;
        drawingGroupMember = true;
        try
        {
            for (var i = 0; i < memberProperties.Count; i++)
            {
                var height = memberHeights[i];
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), memberProperties[i], true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        finally
        {
            drawingGroupMember = false;
        }

        EditorGUI.indentLevel--;
    }
}
#endif
