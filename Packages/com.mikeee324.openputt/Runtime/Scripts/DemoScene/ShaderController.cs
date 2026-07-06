using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

public class ShaderController : UdonSharpBehaviour
{
    [Header("Target Object")]
    public MeshRenderer targetRenderer;
    public string targetShaderName = "OpenPutt/GolfCourse/LinesStandard3DNoise";
    private Material targetMaterial;

    [Header("UI Sliders")]
    public Slider smallNoiseAmountSlider;
    public Slider bigNoiseAmountSlider;
    public Slider bigNoiseStrengthSlider;
    public Slider linesDarkenAmountSlider;
    public Slider allNoiseStrengthSlider;
    public Slider smoothstepMinSlider;
    public Slider smoothstepMaxSlider;
    public Slider heightOffsetSlider;
    public Slider blendMaxSlider;
    public Slider blendMinSlider;
    public Slider lineHeightCMSlider;

    private bool isInitializing = true;

    // Variables to store the original default values
    private float defSmallNoise, defBigNoise, defBigNoiseStrength, defLinesDarken;
    private float defAllNoise, defSmoothMin, defSmoothMax, defHeightOffset;
    private float defBlendMax, defBlendMin, defLineHeight;

    void Start()
    {
        Debug.Log("[ShaderController] Start initiated.");
        if (targetRenderer != null)
        {
            foreach (Material mat in targetRenderer.materials)
            {
                if (mat != null && mat.shader != null && mat.shader.name == targetShaderName)
                {
                    targetMaterial = mat;
                    Debug.Log("[ShaderController] Found matching material!");
                    break;
                }
            }
            
            if (targetMaterial != null) 
            {
                // 1. Memorize the starting values before the user touches anything
                StoreDefaultValues();
                // 2. Set the UI to match
                ReadMaterialValues();
            }
            else
            {
                Debug.LogError("[ShaderController] Could not find material!");
            }
        }
    }

    /// <summary>
    /// Saves the initial state of the material properties to hidden variables.
    /// </summary>
    private void StoreDefaultValues()
    {
        if (targetMaterial == null) return;

        defSmallNoise = targetMaterial.GetFloat("_SmallNoiseAmaount");
        defBigNoise = targetMaterial.GetFloat("_BigNoiseAmaount");
        defBigNoiseStrength = targetMaterial.GetFloat("_BigNoiseStrength");
        defLinesDarken = targetMaterial.GetFloat("_LinesDarkenAmount");
        defAllNoise = targetMaterial.GetFloat("_AllNoiseStrength");
        defSmoothMin = targetMaterial.GetFloat("_smooothstepmin");
        defSmoothMax = targetMaterial.GetFloat("_smooothstepmax");
        defHeightOffset = targetMaterial.GetFloat("_HeightOffset");
        defBlendMax = targetMaterial.GetFloat("_BlendMax");
        defBlendMin = targetMaterial.GetFloat("_BlendMin");
        defLineHeight = targetMaterial.GetFloat("_LineHeightCM");
        
        Debug.Log("[ShaderController] Default values stored in memory.");
    }

    /// <summary>
    /// Triggered by a UI Button. Pushes the memorized defaults back to the material and UI.
    /// </summary>
    public void ResetToDefault()
    {
        if (targetMaterial == null) return;

        Debug.Log("[ShaderController] Reset button clicked. Restoring defaults.");

        // Push the memorized values back into the material
        targetMaterial.SetFloat("_SmallNoiseAmaount", defSmallNoise);
        targetMaterial.SetFloat("_BigNoiseAmaount", defBigNoise);
        targetMaterial.SetFloat("_BigNoiseStrength", defBigNoiseStrength);
        targetMaterial.SetFloat("_LinesDarkenAmount", defLinesDarken);
        targetMaterial.SetFloat("_AllNoiseStrength", defAllNoise);
        targetMaterial.SetFloat("_smooothstepmin", defSmoothMin);
        targetMaterial.SetFloat("_smooothstepmax", defSmoothMax);
        targetMaterial.SetFloat("_HeightOffset", defHeightOffset);
        targetMaterial.SetFloat("_BlendMax", defBlendMax);
        targetMaterial.SetFloat("_BlendMin", defBlendMin);
        targetMaterial.SetFloat("_LineHeightCM", defLineHeight);

        // Tell the UI sliders to snap back to these updated material values
        ReadMaterialValues();
    }

    // Triggered by the UI Sliders
    public void UpdateShaderProperties()
    {
        if (isInitializing || targetMaterial == null) return;

        if (smallNoiseAmountSlider != null) targetMaterial.SetFloat("_SmallNoiseAmaount", smallNoiseAmountSlider.value);
        if (bigNoiseAmountSlider != null) targetMaterial.SetFloat("_BigNoiseAmaount", bigNoiseAmountSlider.value);
        if (bigNoiseStrengthSlider != null) targetMaterial.SetFloat("_BigNoiseStrength", bigNoiseStrengthSlider.value);
        if (linesDarkenAmountSlider != null) targetMaterial.SetFloat("_LinesDarkenAmount", linesDarkenAmountSlider.value);
        if (allNoiseStrengthSlider != null) targetMaterial.SetFloat("_AllNoiseStrength", allNoiseStrengthSlider.value);
        if (smoothstepMinSlider != null) targetMaterial.SetFloat("_smooothstepmin", smoothstepMinSlider.value);
        if (smoothstepMaxSlider != null) targetMaterial.SetFloat("_smooothstepmax", smoothstepMaxSlider.value);
        if (heightOffsetSlider != null) targetMaterial.SetFloat("_HeightOffset", heightOffsetSlider.value);
        if (blendMaxSlider != null) targetMaterial.SetFloat("_BlendMax", blendMaxSlider.value);
        if (blendMinSlider != null) targetMaterial.SetFloat("_BlendMin", blendMinSlider.value);
        if (lineHeightCMSlider != null) targetMaterial.SetFloat("_LineHeightCM", lineHeightCMSlider.value);
    }

    public void ReadMaterialValues()
    {
        if (targetMaterial == null) return;
        
        isInitializing = true; 
        
        if (smallNoiseAmountSlider != null) smallNoiseAmountSlider.value = targetMaterial.GetFloat("_SmallNoiseAmaount");
        if (bigNoiseAmountSlider != null) bigNoiseAmountSlider.value = targetMaterial.GetFloat("_BigNoiseAmaount");
        if (bigNoiseStrengthSlider != null) bigNoiseStrengthSlider.value = targetMaterial.GetFloat("_BigNoiseStrength");
        if (linesDarkenAmountSlider != null) linesDarkenAmountSlider.value = targetMaterial.GetFloat("_LinesDarkenAmount");
        if (allNoiseStrengthSlider != null) allNoiseStrengthSlider.value = targetMaterial.GetFloat("_AllNoiseStrength");
        if (smoothstepMinSlider != null) smoothstepMinSlider.value = targetMaterial.GetFloat("_smooothstepmin");
        if (smoothstepMaxSlider != null) smoothstepMaxSlider.value = targetMaterial.GetFloat("_smooothstepmax");
        if (heightOffsetSlider != null) heightOffsetSlider.value = targetMaterial.GetFloat("_HeightOffset");
        if (blendMaxSlider != null) blendMaxSlider.value = targetMaterial.GetFloat("_BlendMax");
        if (blendMinSlider != null) blendMinSlider.value = targetMaterial.GetFloat("_BlendMin");
        if (lineHeightCMSlider != null) lineHeightCMSlider.value = targetMaterial.GetFloat("_LineHeightCM");
        
        isInitializing = false; 
    }
}