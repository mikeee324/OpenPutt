using UnityEngine;
using UnityEditor;

public class LinesUnityShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // 1. Fetch properties
        MaterialProperty albedo = FindProperty("_Albedo", properties);
        MaterialProperty albedoColor = FindProperty("_Color", properties); 
        MaterialProperty normal = FindProperty("_Normal", properties);
        MaterialProperty metallicMap = FindProperty("_Metallic", properties);
        MaterialProperty metallicVal = FindProperty("_MetallicValue", properties);
        MaterialProperty smoothMap = FindProperty("_Smoothness", properties);
        MaterialProperty smoothVal = FindProperty("_SmoothnessValue", properties);
        
        MaterialProperty emissionMap = FindProperty("_EmissionMap", properties);
        MaterialProperty emissionColor = FindProperty("_EmissionColor", properties);
        MaterialProperty emissionIntensity = FindProperty("_EmissionIntensity", properties);
        
        MaterialProperty roughnessToggle = FindProperty("_RoughnessToggle", properties);
        
        MaterialProperty tiling = FindProperty("_TextureTiling", properties);
        MaterialProperty offset = FindProperty("_TextureOffset", properties);
        
        MaterialProperty darkenAmount = FindProperty("_DarkenAmount", properties);
        MaterialProperty lineHeight = FindProperty("_LineHeightCM", properties);
        MaterialProperty heightOffset = FindProperty("_HeightOffset", properties);
        MaterialProperty blendMin = FindProperty("_BlendMin", properties);
        MaterialProperty blendMax = FindProperty("_BlendMax", properties);

        // Fetch the new forward rendering properties created by Amplify
        MaterialProperty specularHighlights = FindProperty("_SpecularHighlights", properties);
        MaterialProperty glossyReflections = FindProperty("_GlossyReflections", properties);

        // 2. Draw Standard PBR Texture Settings
        GUILayout.Label("Standard Surface Maps", EditorStyles.boldLabel);
        
        materialEditor.TexturePropertySingleLine(new GUIContent("Albedo"), albedo, albedoColor);
        materialEditor.TexturePropertySingleLine(new GUIContent("Normal"), normal);
        materialEditor.TexturePropertySingleLine(new GUIContent("Metallic"), metallicMap, metallicVal);
        materialEditor.TexturePropertySingleLine(new GUIContent("Smooth/Rough Map"), smoothMap, smoothVal);

        EditorGUI.indentLevel += 2;
        materialEditor.ShaderProperty(roughnessToggle, new GUIContent("Map is Roughness (Invert)", "Check this if your map is a Roughness map instead of Smoothness."));
        EditorGUI.indentLevel -= 2;
        
        EditorGUILayout.Space(5);
        
        GUILayout.Label("Emission Settings", EditorStyles.boldLabel);
        materialEditor.TexturePropertySingleLine(new GUIContent("Emission Map"), emissionMap, emissionColor);
        materialEditor.ShaderProperty(emissionIntensity, new GUIContent("Intensity"));
        
        EditorGUILayout.Space(5);

        // Draw Tiling and Offset
        EditorGUI.BeginChangeCheck();
        Vector2 currentTiling = new Vector2(tiling.vectorValue.x, tiling.vectorValue.y);
        Vector2 currentOffset = new Vector2(offset.vectorValue.x, offset.vectorValue.y);
        
        currentTiling = EditorGUILayout.Vector2Field("Texture Tiling", currentTiling);
        currentOffset = EditorGUILayout.Vector2Field("Texture Offset", currentOffset);
        
        if (EditorGUI.EndChangeCheck())
        {
            tiling.vectorValue = new Vector4(currentTiling.x, currentTiling.y, 0, 0);
            offset.vectorValue = new Vector4(currentOffset.x, currentOffset.y, 0, 0);
        }

        EditorGUILayout.Space(10);

        // 3. Draw Custom Line Settings
        GUILayout.Label("Line Generation Settings", EditorStyles.boldLabel);
        materialEditor.ShaderProperty(lineHeight, new GUIContent("Line Height (cm)", "How thick every line is in centimeters."));
        materialEditor.ShaderProperty(heightOffset, new GUIContent("Height Offset", "Scrolls the lines up and down in world space."));
        materialEditor.ShaderProperty(darkenAmount, new GUIContent("Darken Amount", "How much the lines darken the underlying Albedo."));

        EditorGUILayout.Space(10);

        // 4. Draw Advanced Blending Options
        GUILayout.Label("Advanced Blending", EditorStyles.boldLabel);
        materialEditor.ShaderProperty(blendMin, new GUIContent("Blend Min", "Adjusts the sharpness of the line's inner edge."));
        materialEditor.ShaderProperty(blendMax, new GUIContent("Blend Max", "Adjusts the sharpness of the line's outer edge."));

        EditorGUILayout.Space(10);

        // 5. Draw Forward Rendering Options
        materialEditor.ShaderProperty(specularHighlights, new GUIContent("Specular Highlights"));
        materialEditor.ShaderProperty(glossyReflections, new GUIContent("Reflections"));
    }
}