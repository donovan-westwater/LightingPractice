using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
	//This class handles the Post FX settings, the fx stack buffer, and the caemra
	const string bufferName = "Post FX";
	bool useHDR;
	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};
	int bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
	int fxSourceId = Shader.PropertyToID("_PostFXSource"); //Used to access source image for post fx
	int fxSource2Id = Shader.PropertyToID("_PostFXSource2"); //Used to upscale the image for post fx
	int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"); //Saves downscales by reducing resolution of pytramid
	int bloomThresholdId = Shader.PropertyToID("_BloomThreshold"); //Controls the point where the bloom effect is cutoff
	int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
	ScriptableRenderContext context;

	Camera camera;

	PostFXSettings settings;
	//Bloom post fx
	const int maxBloomPyramidLevels = 16; //The max number of mipmap layers used for the blurring process
	int bloomPyramidId;
	enum Pass
    {
		BloomHorizontal,
		BloomVertical,
		BloomCombine,
		BloomPrefilter,
		BloomPrefilterFireflies,
		Copy
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
		ScriptableRenderContext context, Camera camera, PostFXSettings settings, bool useHDR
	)
	{
		this.useHDR = useHDR;
		this.context = context;
		this.camera = camera;
		//Only applies the Post Process effect for the assigned camera
		this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
		ApplySceneViewState();
	}
	public void Render(int sourceId)
    {
		//This combines the FX effect shader with the caemra and executes it
		//Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		DoBloom(sourceId);
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
	//We send a command named bloom which which uses a series of lower detail images
	//and blurs them together
	void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");
		PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		//Just draw the image unaltered.
		if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
			height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
		{
			Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			buffer.EndSample("Bloom");
			return;
		}
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
		//buffer.SetGlobalFloat(bloomIntensityId, 1f);
		if (i > 1) { 
			//We release all of the reserved data now that we sent it to the postFX shader
			for (i -= 1; i > 0; i--)
			{
				//Go back through the layers and upsample to finish the bloom effect
				buffer.SetGlobalTexture(fxSource2Id, toId + 1);
				Draw(fromId, toId, Pass.BloomCombine);
				buffer.ReleaseTemporaryRT(fromId);
				buffer.ReleaseTemporaryRT(toId+1);
				fromId = toId;
				toId -= 2;

			}
        }
        else {
			buffer.ReleaseTemporaryRT(bloomPyramidId);
		}
		buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		Draw(bloomPyramidId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
    }
}