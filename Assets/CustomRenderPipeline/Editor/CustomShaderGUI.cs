using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
	MaterialEditor editor; //Underlying editor object rsponsible for showing/editing materials
	Object[] materials; //The materials being edited. OBject array because targets property returns objects
	MaterialProperty[] properties; //The properties of selected materials
	bool showPresets;
	bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");
	enum ShadowMode
    {
		On,Clip,Dither,Off
    }
	ShadowMode Shadows
    {
		set
		{
			if (SetProperty("_Shadows", (float)value))
			{
				SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
				SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
			}
		}
	}
	//RenderQueue setter for assigning renderQueue property
	RenderQueue RenderQueue
	{
		set
		{
			foreach (Material m in materials)
			{
				m.renderQueue = (int)value;
			}
		}
	}
	public override void OnGUI(
		MaterialEditor materialEditor, MaterialProperty[] properties
	)
	{
		EditorGUI.BeginChangeCheck();
		base.OnGUI(materialEditor, properties);
		editor = materialEditor;
		materials = materialEditor.targets;
		this.properties = properties;
		BakedEmission(); //Enable baked emission
		EditorGUILayout.Space();
		showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
		if (showPresets)
		{
			OpaquePreset();
			ClipPreset();
			FadePreset();
			TransparentPreset();
		}
        if (EditorGUI.EndChangeCheck())
        {
			SetShadowCasterPass();
			CopyLightMappingProperties();
        }
	}
	//Copy lightmap properties to make sure Main and Base use the same textures and colors
	void CopyLightMappingProperties()
    {
		MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
		MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
		if (mainTex != null && baseMap != null)
		{
			mainTex.textureValue = baseMap.textureValue;
			mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
		}
		MaterialProperty color = FindProperty("_Color", properties, false);
		MaterialProperty baseColor =
			FindProperty("_BaseColor", properties, false);
		if (color != null && baseColor != null)
		{
			color.colorValue = baseColor.colorValue;
		}
	}
	void BakedEmission()
    {
		EditorGUI.BeginChangeCheck();
		editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
			foreach(Material m in editor.targets)
            {
				m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
	}
	bool SetProperty(string name, float value)
    {
		//Find the properties in the shader and assign the correct values
		MaterialProperty property = FindProperty(name, properties);
		if(property != null)
        {
			property.floatValue = value;
			return true;
        }
		return false;
    }
	//Handles property-keyword combos
	void SetProperty(string name, string keyword, bool value)
    {
		if (SetProperty(name, value ? 1f : 0f)){
			SetKeyword(keyword, value);
		}
	}
	void SetKeyword(string keyword,bool enabled)
    {
        //if enabled, enabled all keywords, else disable all keywords
        if (enabled)
        {
			foreach(Material m in materials)
            {
				m.EnableKeyword(keyword);
            }
        }
        else
        {
			foreach(Material m in materials)
            {
				m.DisableKeyword(keyword);
            }
        }
    }
	bool HasProperty(string name) =>
		FindProperty(name, properties, false) != null;
	//A Button created via GUILayout.Button to handle preset settings
	bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
			editor.RegisterPropertyChangeUndo(name);
			return true;
        }
		return false;
    }
	//Presets
	void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
			Clipping = false;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.Zero;
			ZWrite = true;
			RenderQueue = RenderQueue.Geometry;
        }
    }
	void ClipPreset()
    {
		if (PresetButton("Clip"))
		{
			Clipping = true;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.Zero;
			ZWrite = true;
			RenderQueue = RenderQueue.AlphaTest;
		}
	}
	void FadePreset()
    {
		if (PresetButton("Fade"))
		{
			Clipping = false;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.SrcAlpha;
			DstBlend = BlendMode.OneMinusSrcAlpha;
			ZWrite = false;
			RenderQueue = RenderQueue.Transparent;
		}
	}
	void TransparentPreset()
    {
		if (HasPremultiplyAlpha && PresetButton("Transparent"))
		{
			Clipping = false;
			PremultiplyAlpha = true;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.OneMinusSrcAlpha;
			ZWrite = false;
			RenderQueue = RenderQueue.Transparent;
		}
	}
	//Simple setter properties
	bool Clipping
	{
		set => SetProperty("_Clipping", "_CLIPPING", value);
	}

	bool PremultiplyAlpha
	{
		set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
	}

	BlendMode SrcBlend
	{
		set => SetProperty("_SrcBlend", (float)value);
	}

	BlendMode DstBlend
	{
		set => SetProperty("_DstBlend", (float)value);
	}

	bool ZWrite
	{
		set => SetProperty("_ZWrite", value ? 1f : 0f);
	}
	void SetShadowCasterPass()
	{
		MaterialProperty shadows = FindProperty("_Shadows", properties, false);
		if (shadows == null || shadows.hasMixedValue)
		{
			return;
		}
		bool enabled = shadows.floatValue < (float)ShadowMode.Off;
		foreach (Material m in materials)
		{
			m.SetShaderPassEnabled("ShadowCaster", enabled);
		}
	}
}