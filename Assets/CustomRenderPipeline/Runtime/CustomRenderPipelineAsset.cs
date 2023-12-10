using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset {
	[SerializeField]
	bool useDynamicBatching = true,
		useGPUInstancing = true,
		useSRPBatcher = true,
		useLightsPerObject = true;
	[SerializeField]
	ShadowSettings shadows = default;
	[SerializeField]
	PostFXSettings postFXSettings = default;
	[SerializeField]
	CameraBufferSettings cameraBuffer = new CameraBufferSettings
	{
		allowHDR = true,
		renderScale = 1f,
		fxaa = new CameraBufferSettings.FXAA
		{
			fixedThreshold = 0.0833f
        }
	};
	//Look up table setup to precalculate convertions between unaltered to altered colors
	//This saves a lot of calculation time for color grading
	public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64}
	[SerializeField]
	ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
	[SerializeField]
	Shader cameraRendererShader = default;
	protected override RenderPipeline CreatePipeline()
	{
		return new CustomRenderPipeline(cameraBuffer,useDynamicBatching, useGPUInstancing, useSRPBatcher,
			useLightsPerObject,shadows, postFXSettings, (int)colorLUTResolution,cameraRendererShader);
	}
}