using UnityEditor;
using UnityEngine;

public class GolfCourseLinesMasterGUI : ShaderGUI
{
    // Foldout states
    bool showSurface = true;
    bool showLines = true;
    bool showNoise = true;
    bool showForward = false;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();

        // The script modularly attempts to draw each section. 
        // If the required properties don't exist in the selected shader, the section quietly skips itself.
        DrawSurfaceSection(materialEditor, properties);
        DrawLinesSection(materialEditor, properties);
        DrawNoiseSection(materialEditor, properties);
        DrawForwardSection(materialEditor, properties);

        if (EditorGUI.EndChangeCheck())
        {
            materialEditor.PropertiesChanged();
        }
    }

    private void DrawSurfaceSection(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // We look for _Albedo to determine if we are using the Standard PBR layout or the simpler layout
        MaterialProperty albedoMap = FindProperty("_Albedo", properties, false);

        GUILayout.Space(5);
        showSurface = EditorGUILayout.BeginFoldoutHeaderGroup(showSurface, "Surface Properties");
        if (showSurface)
        {
            EditorGUI.indentLevel++;

            if (albedoMap != null)
            {
                // --- STANDARD PBR LAYOUT ---
                MaterialProperty color = FindProperty("_Color", properties, false);
                MaterialProperty normal = FindProperty("_Normal", properties, false);
                MaterialProperty metallicMap = FindProperty("_Metallic", properties, false);
                MaterialProperty metallicVal = FindProperty("_MetallicValue", properties, false);
                MaterialProperty smoothMap = FindProperty("_Smoothness", properties, false);
                MaterialProperty smoothVal = FindProperty("_SmoothnessValue", properties, false);
                MaterialProperty roughnessToggle = FindProperty("_RoughnessToggle", properties, false);
                
                MaterialProperty emissionMap = FindProperty("_EmissionMap", properties, false);
                MaterialProperty emissionColor = FindProperty("_EmissionColor", properties, false);
                MaterialProperty emissionIntensity = FindProperty("_EmissionIntensity", properties, false);
                
                MaterialProperty tiling = FindProperty("_TextureTiling", properties, false);
                MaterialProperty offset = FindProperty("_TextureOffset", properties, false);

                materialEditor.TexturePropertySingleLine(new GUIContent("Albedo"), albedoMap, color);
                materialEditor.TexturePropertySingleLine(new GUIContent("Normal"), normal);
                materialEditor.TexturePropertySingleLine(new GUIContent("Metallic"), metallicMap, metallicVal);
                materialEditor.TexturePropertySingleLine(new GUIContent("Smooth/Rough Map"), smoothMap, smoothVal);

                if (roughnessToggle != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(roughnessToggle, new GUIContent("Map is Roughness (Invert)"));
                    EditorGUI.indentLevel--;
                }
                
                GUILayout.Space(5);
                materialEditor.TexturePropertySingleLine(new GUIContent("Emission Map"), emissionMap, emissionColor);
                if (emissionIntensity != null) materialEditor.ShaderProperty(emissionIntensity, new GUIContent("Emission Intensity"));
                
                GUILayout.Space(10);
                if (tiling != null && offset != null)
                {
                    Vector2 currentTiling = new Vector2(tiling.vectorValue.x, tiling.vectorValue.y);
                    Vector2 currentOffset = new Vector2(offset.vectorValue.x, offset.vectorValue.y);
                    currentTiling = EditorGUILayout.Vector2Field("Texture Tiling", currentTiling);
                    currentOffset = EditorGUILayout.Vector2Field("Texture Offset", currentOffset);
                    tiling.vectorValue = new Vector4(currentTiling.x, currentTiling.y, 0, 0);
                    offset.vectorValue = new Vector4(currentOffset.x, currentOffset.y, 0, 0);
                }
            }
            else
            {
                // --- SIMPLER UNLIT/NOISE LAYOUT ---
                MaterialProperty color0 = FindProperty("_Color0", properties, false);
                MaterialProperty metallicFloat = FindProperty("_Metallic", properties, false);
                MaterialProperty smoothnessFloat = FindProperty("_Smoothness", properties, false);

                if (color0 != null) materialEditor.ShaderProperty(color0, new GUIContent("Base Color"));
                if (metallicFloat != null) materialEditor.ShaderProperty(metallicFloat, new GUIContent("Metallic"));
                if (smoothnessFloat != null) materialEditor.ShaderProperty(smoothnessFloat, new GUIContent("Smoothness"));
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawLinesSection(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Every shader has Line Height, so we use it as the anchor for this section
        MaterialProperty lineHeight = FindProperty("_LineHeightCM", properties, false);
        if (lineHeight == null) return; 

        MaterialProperty heightOffset = FindProperty("_HeightOffset", properties, false);
        MaterialProperty blendMin = FindProperty("_BlendMin", properties, false);
        MaterialProperty blendMax = FindProperty("_BlendMax", properties, false);
        
        // Handle naming discrepancy between shaders
        MaterialProperty darkenAmount = FindProperty("_DarkenAmount", properties, false) ?? FindProperty("_LinesDarkenAmount", properties, false);

        GUILayout.Space(10);
        showLines = EditorGUILayout.BeginFoldoutHeaderGroup(showLines, "Topographical Lines");
        if (showLines)
        {
            EditorGUI.indentLevel++;
            materialEditor.ShaderProperty(lineHeight, new GUIContent("Line Height (cm)", "Physical height distance between each line."));
            if (heightOffset != null) materialEditor.ShaderProperty(heightOffset, new GUIContent("Height Offset"));
            if (darkenAmount != null) materialEditor.ShaderProperty(darkenAmount, new GUIContent("Darken Amount"));

            if (blendMin != null && blendMax != null)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Line Blending Limits", EditorStyles.boldLabel);
                materialEditor.ShaderProperty(blendMin, new GUIContent("Blend Min"));
                materialEditor.ShaderProperty(blendMax, new GUIContent("Blend Max"));
                
                if (blendMin.floatValue >= blendMax.floatValue)
                {
                    EditorGUILayout.HelpBox("Blend Min should be strictly less than Blend Max.", MessageType.Warning);
                }
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawNoiseSection(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Check for both the standard naming convention and the appended "1" convention from LinesStandard3DNoise
        MaterialProperty lockNoise = FindProperty("_LockNoiseToObject", properties, false) ?? FindProperty("_LockNoiseToObject1", properties, false);
        if (lockNoise == null) return;

        MaterialProperty allNoiseStr = FindProperty("_AllNoiseStrength", properties, false) ?? FindProperty("_AllNoiseStrength1", properties, false);
        MaterialProperty bigNoiseStr = FindProperty("_BigNoiseStrength", properties, false) ?? FindProperty("_BigNoiseStrength1", properties, false);
        MaterialProperty bigNoiseAmt = FindProperty("_BigNoiseAmaount", properties, false) ?? FindProperty("_BigNoiseAmaount1", properties, false); 
        MaterialProperty smallNoiseAmt = FindProperty("_SmallNoiseAmaount", properties, false) ?? FindProperty("_SmallNoiseAmaount1", properties, false); 
        MaterialProperty smoothMin = FindProperty("_smooothstepmin", properties, false) ?? FindProperty("_smooothstepmin1", properties, false); 
        MaterialProperty smoothMax = FindProperty("_smooothstepmax", properties, false) ?? FindProperty("_smooothstepmax1", properties, false);

        GUILayout.Space(10);
        showNoise = EditorGUILayout.BeginFoldoutHeaderGroup(showNoise, "Noise & Variation");
        if (showNoise)
        {
            EditorGUI.indentLevel++;
            materialEditor.ShaderProperty(lockNoise, new GUIContent("Lock Noise to Object Space"));

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Strength", EditorStyles.boldLabel);
            if (allNoiseStr != null) materialEditor.ShaderProperty(allNoiseStr, new GUIContent("Overall Noise Strength"));
            if (bigNoiseStr != null) materialEditor.ShaderProperty(bigNoiseStr, new GUIContent("Big Noise Strength"));

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Scale", EditorStyles.boldLabel);
            if (bigNoiseAmt != null) materialEditor.ShaderProperty(bigNoiseAmt, new GUIContent("Big Noise Scale"));
            if (smallNoiseAmt != null) materialEditor.ShaderProperty(smallNoiseAmt, new GUIContent("Small Noise Scale"));

            if (smoothMin != null && smoothMax != null)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Smoothstep Mapping", EditorStyles.boldLabel);
                materialEditor.ShaderProperty(smoothMin, new GUIContent("Smoothstep Min"));
                materialEditor.ShaderProperty(smoothMax, new GUIContent("Smoothstep Max"));

                if (smoothMin.floatValue >= smoothMax.floatValue)
                {
                    EditorGUILayout.HelpBox("Smoothstep Min should be strictly less than Smoothstep Max.", MessageType.Warning);
                }
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawForwardSection(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Check for standard Amplify forward rendering toggles
        MaterialProperty specularHighlights = FindProperty("_SpecularHighlights", properties, false);
        if (specularHighlights == null) return;

        MaterialProperty glossyReflections = FindProperty("_GlossyReflections", properties, false);

        GUILayout.Space(10);
        showForward = EditorGUILayout.BeginFoldoutHeaderGroup(showForward, "Forward Rendering Options");
        if (showForward)
        {
            EditorGUI.indentLevel++;
            materialEditor.ShaderProperty(specularHighlights, new GUIContent("Specular Highlights"));
            if (glossyReflections != null) materialEditor.ShaderProperty(glossyReflections, new GUIContent("Reflections"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}