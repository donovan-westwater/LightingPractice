using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject {
    [SerializeField]
    //Custom triangle shader
    Shader shader = default;
	
	//Material for full screen triangle shader
	[System.NonSerialized]
	Material material;
	//Bloom settings
	[System.Serializable]
	public struct BloomSettings
	{

		[Range(0f, 16f)]
		public int maxIterations; //Controls how many layers we use

		[Min(1f)]
		public int downscaleLimit; //At what size do we stop sampling?

        public bool bicubicUpsampling; //Switches to a different filtering method for bloom

        [Min(0f)]
		public float threshold; //Controls cutoff point for bloom effect

		[Range(0f, 1f)]
		public float thresholdKnee; //Controls the steepness of the bloom cutoff curve or knee curve

		[Min(0f)]
		public float intensity; //Controls intensity of the effect

		public bool fadeFireflies; //Reduces the flickering caused by moving the camera around
		public enum Mode { Additive, Scattering } //What kind of bloom are we using?

		public Mode mode;

		[Range(0.05f, 0.95f)]
		public float scatter; //intensity of light scattering

		public bool ignoreRenderScale;
	}
	[System.Serializable]
	public struct ToneMappingSettings
    {
		public float exposureBias;
		public float whitePoint;
		public enum Mode {None, ACES,Neutral,Reinhard,NeutralCustom}
		public Mode mode;
    }

	[SerializeField]
	ToneMappingSettings toneMapping = new ToneMappingSettings
	{
		exposureBias = 0.02f,
		whitePoint = 5.3f
	};

	public ToneMappingSettings ToneMapping => toneMapping;

	[SerializeField]
	BloomSettings bloom = new BloomSettings
	{
		scatter = 0.7f
	};

	public BloomSettings Bloom => bloom;
	//Color grading settings below
	[Serializable]
	public struct ColorAdjustmentsSettings {
		public float postExposure; //Increases the exposure of the image (whiteness)
		[Range(-100f, 100f)]
		public float contrast; //Sharpens the image

		[ColorUsage(false, true)]
		public Color colorFilter;

		[Range(-180f, 180f)]
		public float hueShift; //'rotates' the colors on the color wheel (Same Saturation and Whiteness)

		[Range(-100f, 100f)]
		public float saturation; //Increases the saturation on the colors in the image
	}

	[SerializeField]
	ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings {
		colorFilter = Color.white
	};

	public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

	//Adjusts the color temperature of the image
	[Serializable]
	public struct WhiteBalanceSettings
    {
		[Range(-100f, 100f)]
		public float temperature, tint; //Cools or warms image, tweaks temp shifted color
    }

	[SerializeField]
	WhiteBalanceSettings whiteBalance = default;

	public WhiteBalanceSettings WhiteBalance => whiteBalance;

	//Split toning colors shadows and highlights a sperate color
	[Serializable]
	public struct SplitToningSettings
	{

		[ColorUsage(false)]
		public Color shadows, highlights; //The colors to be applied to shadows and highlights

		[Range(-100f, 100f)]
		public float balance; //The white balance of the split tones
	}

	[SerializeField]
	SplitToningSettings splitToning = new SplitToningSettings
	{
		shadows = Color.gray,
		highlights = Color.gray
	};

	public SplitToningSettings SplitToning => splitToning;

	//Mixes channels to make new colors
	[Serializable]
	public struct ChannelMixerSettings
	{

		public Vector3 red, green, blue;
	}

	[SerializeField]
	ChannelMixerSettings channelMixer = new ChannelMixerSettings
	{
		red = Vector3.right,
		green = Vector3.up,
		blue = Vector3.forward
	};

	public ChannelMixerSettings ChannelMixer => channelMixer;

	//Handles midtones alterations (similar to split toning)
	[Serializable]
	public struct ShadowsMidtonesHighlightsSettings
	{

		[ColorUsage(false, true)]
		public Color shadows, midtones, highlights;
		//Handles what ranges of intensity count as shadow, highlight, and mid tone
		[Range(0f, 2f)]
		public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
	}

	[SerializeField]
	ShadowsMidtonesHighlightsSettings
		shadowsMidtonesHighlights = new ShadowsMidtonesHighlightsSettings
		{
			shadows = Color.white,
			midtones = Color.white,
			highlights = Color.white,
			shadowsEnd = 0.3f,
			highlightsStart = 0.55f,
			highLightsEnd = 1f
		};

	public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights =>
		shadowsMidtonesHighlights;
	public Material Material
	{
		get
		{
			if (material == null && shader != null)
			{
				material = new Material(shader);
				material.hideFlags = HideFlags.HideAndDontSave;
			}
			return material;
		}
	}

}
