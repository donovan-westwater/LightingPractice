using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public partial class PostFXStack
{
	//This class handles the Post FX settings, the fx stack buffer, and the caemra
	const string bufferName = "Post FX";
	bool useHDR;
	CameraSettings.FinalBlendMode finalBlendMode;
	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};
	//Layer ids
	int
		finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
		finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");
	//Bloom Ids
	int bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
	int fxSourceId = Shader.PropertyToID("_PostFXSource"); //Used to access source image for post fx
	int fxSource2Id = Shader.PropertyToID("_PostFXSource2"); //Used to upscale the image for post fx
	int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"); //Saves downscales by reducing resolution of pytramid
	int bloomThresholdId = Shader.PropertyToID("_BloomThreshold"); //Controls the point where the bloom effect is cutoff
	int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
	int bloomResultId = Shader.PropertyToID("_BloomResult");
	int exposureId = Shader.PropertyToID("_ExposureBias");
	int whitePointId = Shader.PropertyToID("_WhitePoint");
	//Color grading ids
	int colorLUTResolution;
	int colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLog");
	int colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters");
	int colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT");
	int colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments");
	int colorFilterId = Shader.PropertyToID("_ColorFilter");
	int whiteBalanceId = Shader.PropertyToID("_WhiteBalance");
	int splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows");
	int splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights");
	//Channel mixing ids
	int channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed");
	int channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen");
	int channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue");
	//Shadows midtones and highlights
	int smhShadowsId = Shader.PropertyToID("_SMHShadows");
	int smhMidtonesId = Shader.PropertyToID("_SMHMidtones");
	int smhHighlightsId = Shader.PropertyToID("_SMHHighlights");
	int smhRangeId = Shader.PropertyToID("_SMHRange");
	int finalResultId = Shader.PropertyToID("_FinalResult");
	int copyBicubicId = Shader.PropertyToID("_CopyBicubic");
	CameraBufferSettings.BicubicRescalingMode bicubicRescaling;
	ScriptableRenderContext context;
	Vector2Int bufferSize;
	static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
	CameraBufferSettings.FXAA fxaa;
	Camera camera;

	PostFXSettings settings;
	//Bloom post fx
	const int maxBloomPyramidLevels = 16; //The max number of mipmap layers used for the blurring process
	int bloomPyramidId;
	enum Pass
    {
		BloomHorizontal,
		BloomVertical,
		BloomAdd,
		BloomScatter,
		BloomScatterFinal,
		BloomPrefilter,
		BloomPrefilterFireflies,
		ColorGradingNone,
		ColorGradingACES,
		ColorGradingNeutral,
		ColorGradingReinhard,
		ColorGradingNeutralCustom,
		Copy,
		Final,
		FinalRescale
    }
	public bool IsActive => settings != null; //Keeps track of if there is post fx

	public PostFXStack()
    {
		//Bloom setup
		bloomPyramidId = Shader.PropertyToID("_BloomPyramid0"); //Only track the first, like we do with arrays
		for(int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
			Shader.PropertyToID("_BloomPyramid" + i); //We want to store each of the layers
        }
    }
	public void Setup(
		ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings, bool useHDR
	,int colorLUTResolution, CameraSettings.FinalBlendMode finalBlendMode,
		CameraBufferSettings.BicubicRescalingMode bicubicRescaling, CameraBufferSettings.FXAA fxaa)
	{
		this.bicubicRescaling = bicubicRescaling;
		this.finalBlendMode = finalBlendMode;
		this.useHDR = useHDR;
		this.context = context;
		this.camera = camera;
		this.bufferSize = bufferSize;
		this.colorLUTResolution = colorLUTResolution;
		this.fxaa = fxaa;
		//Only applies the Post Process effect for the assigned camera
		this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
		ApplySceneViewState();
	}
	public void Render(int sourceId)
    {
		//This combines the FX effect shader with the caemra and executes it
		//Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		//Pass results of the bloom to the tone mapper
		if (DoBloom(sourceId))
		{
			DoColorGradingAndToneMapping(bloomResultId);
			buffer.ReleaseTemporaryRT(bloomResultId);
		}
		else
		{
			DoColorGradingAndToneMapping(sourceId);
		}
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	//Draws the fullscreen triangle used for post processing
	void Draw(
		RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass
	)
	{
		buffer.SetGlobalTexture(fxSourceId, from);
		//Store the results in the render target
		buffer.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		//Tell the gpu to draw the full screen triangle
		buffer.DrawProcedural(
			Matrix4x4.identity, settings.Material, (int)pass,
			MeshTopology.Triangles, 3
		);
	}
	//Final Draw call for post processing
	void DrawFinal(
		RenderTargetIdentifier from, Pass pass
	)
	{
		buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
		buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);
		buffer.SetGlobalTexture(fxSourceId, from);
		//Store the results in the render target
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget
			, finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ?
				RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load
			, RenderBufferStoreAction.Store
		);
		buffer.SetViewport(camera.pixelRect); //set viewport to the current camera
		//Helps us avoid issues with split screen
		//Tell the gpu to draw the full screen triangle
		buffer.DrawProcedural(
			Matrix4x4.identity, settings.Material, (int)pass,
			MeshTopology.Triangles, 3
		);
	}
	//We send a command named bloom which which uses a series of lower detail images
	//and blurs them together
	bool DoBloom(int sourceId)
    {
        //buffer.BeginSample("Bloom");
		PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width , height;
        if (bloom.ignoreRenderScale)
        {
			width = camera.pixelWidth / 2;
			height = camera.pixelHeight / 2;
		}
		else
		{
			width = bufferSize.x / 2;
			height = bufferSize.y / 2;
		}
		//Just draw the image unaltered.
		if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
			height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
		{
			//Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			//buffer.EndSample("Bloom");
			return false;
		}
		buffer.BeginSample("Bloom");
		//Calculates cutoff vector for controling where the brightness is applied
		//[t,-t+tk,2tk,1/4tk+0.00001] is the sturcture (t = threshold, k = knee curve adjust)
		Vector4 threshold;
		threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
		threshold.y = threshold.x * bloom.thresholdKnee;
		threshold.z = 2f * threshold.y;
		threshold.w = 0.25f / (threshold.y + 0.00001f);
		threshold.y -= threshold.x;
		buffer.SetGlobalVector(bloomThresholdId, threshold);
		//Reducing the resolution of the layers so we don't sample as much
		RenderTextureFormat format = useHDR ?
			RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
		buffer.GetTemporaryRT(
			bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
		);
		Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ?
				Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
		width /= 2;
		height /= 2;

		int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
		//We go through each layer and blend neighboring pixels together
		//Then we use downsampling of each layer to add more detail
		//Copy our images into layers, lowering the detail by downsampling each time
		int i;
		for(i = 0; i < bloom.maxIterations; i++)
        {
			if(height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
				break;
            }
			//We need to use a intermediate texture to store combination of vertical and horizonal
			int midId = toId - 1;
			buffer.GetTemporaryRT(
				midId, width, height, 0, FilterMode.Bilinear, format
			);
			//This is the the part that blurs each layer with bilinear filtering
			//We are average the layer, then down scale, and copy the results
			//This causes the averaging the add up to a larger blur, resulting in bloom
			buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
			Draw(fromId, midId, Pass.BloomHorizontal);
			Draw(midId, toId, Pass.BloomVertical);
			fromId = toId;
			toId += 2;
			width /= 2;
			height /= 2;
		}
		//Releases the tmp texture used for halving the resolution
		buffer.ReleaseTemporaryRT(bloomPrefilterId);
		//Copy the resulting blurred image
		//Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		buffer.ReleaseTemporaryRT(fromId - 1);
		toId -= 5; //Release the last horizontal draw and move us up the pyramid
		float testf = bloom.bicubicUpsampling ? 1f : 0f;
		buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
		Pass combinePass, finalPass;
		float finalIntensity;
		if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
		{
			combinePass = finalPass = Pass.BloomAdd;
			buffer.SetGlobalFloat(bloomIntensityId, 1f);
			finalIntensity = bloom.intensity;
		}
		else
		{
			combinePass = Pass.BloomScatter;
			finalPass = Pass.BloomScatterFinal;
			buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
			finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
		}
		if (i > 1) { 
			//We release all of the reserved data now that we sent it to the postFX shader
			for (i -= 1; i > 0; i--)
			{
				//Go back through the layers and upsample to finish the bloom effect
				buffer.SetGlobalTexture(fxSource2Id, toId + 1);
				Draw(fromId, toId, combinePass);
				buffer.ReleaseTemporaryRT(fromId);
				buffer.ReleaseTemporaryRT(toId+1);
				fromId = toId;
				toId -= 2;

			}
        }
        else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		buffer.GetTemporaryRT(
			bloomResultId, bufferSize.x, bufferSize.y, 0,
			FilterMode.Bilinear, format
		);
		Draw(fromId, bloomResultId, finalPass);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
		return true;
    }
	void ConfigureColorAdjustments()
	{
		ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
		//Send a vector with the main color grading properties
		buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
			Mathf.Pow(2f, colorAdjustments.postExposure),
			colorAdjustments.contrast * 0.01f + 1f,
			colorAdjustments.hueShift * (1f / 360f),
			colorAdjustments.saturation * 0.01f + 1f
		));
		//Set the color filter
		buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
	}
	void ConfigureWhiteBalance()
    {
		WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
		//Uses the LMS color space, which models which colors our cones respond to
		buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(
			whiteBalance.temperature, whiteBalance.tint));
    }
	void ConfigureSplitToning()
	{
		SplitToningSettings splitToning = settings.SplitToning;
		Color splitColor = splitToning.shadows;
		splitColor.a = splitToning.balance * 0.01f; //Scale down to -1 to 1 range
		buffer.SetGlobalColor(splitToningShadowsId, splitColor);
		buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
	}
	void ConfigureChannelMixer()
	{
		ChannelMixerSettings channelMixer = settings.ChannelMixer;
		buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
		buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
		buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
	}
	//Convert the colors into linear space and then send ranges as a single vector
	void ConfigureShadowsMidtonesHighlights()
	{
		ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
		buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
		buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
		buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
		buffer.SetGlobalVector(smhRangeId, new Vector4(
			smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd
		));
	}
	void DoColorGradingAndToneMapping(int sourceId)
    {
		ConfigureColorAdjustments();
		ConfigureWhiteBalance();
		ConfigureSplitToning();
		ConfigureChannelMixer();
		ConfigureShadowsMidtonesHighlights();
		//Setup Color grading look up table
		//We are storing everything inside a '3D texture'
		//we cant use an actual 3d texture, so we are using a large 2d texture instead
		int lutHeight = colorLUTResolution;
		int lutWidth = lutHeight * lutHeight;
		buffer.GetTemporaryRT(colorGradingLUTId, lutWidth, lutHeight, 0
			, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
		buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
			lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
		));
		//Tone Mapping pass used if enabled
		ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
		Pass pass = mode < 0 ? Pass.Copy : Pass.ColorGradingNone + (int)mode;
		buffer.SetGlobalFloat(colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingNone
			? 1f : 0f);
		if(pass == Pass.ColorGradingNeutralCustom)
        {
			buffer.SetGlobalFloat(exposureId, settings.ToneMapping.exposureBias);
			buffer.SetGlobalFloat(whitePointId, settings.ToneMapping.whitePoint);
		}
		Draw(sourceId, colorGradingLUTId, pass);
		buffer.SetGlobalVector(colorGradingLUTParametersId,
			new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f)
		);
		if(bufferSize.x == camera.pixelWidth)
        {
			DrawFinal(sourceId,Pass.Final);
		}
        else
        {
			//Rescale the image in LDR before switching to HDR
			buffer.SetGlobalFloat(finalSrcBlendId, 1f);
			buffer.SetGlobalFloat(finalDstBlendId, 0f);
			buffer.GetTemporaryRT(
				finalResultId, bufferSize.x, bufferSize.y, 0,
				FilterMode.Bilinear, RenderTextureFormat.Default
			);
			Draw(sourceId, finalResultId, Pass.Final);
			bool bicubicSampling =
				bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
				bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
				bufferSize.x < camera.pixelWidth;
			buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);
			DrawFinal(finalResultId, Pass.FinalRescale);
			buffer.ReleaseTemporaryRT(finalResultId);
		}
		buffer.ReleaseTemporaryRT(colorGradingLUTId);
	}
}