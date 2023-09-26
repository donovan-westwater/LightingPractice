using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
	//This class handles the Post FX settings, the fx stack buffer, and the caemra
	const string bufferName = "Post FX";

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	int fxSourceId = Shader.PropertyToID("_PostFXSource"); //Used to access source image for post fx
	int fxSource2Id = Shader.PropertyToID("_PostFXSource2"); //Used to upscale the image for post fx
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
		ScriptableRenderContext context, Camera camera, PostFXSettings settings
	)
	{
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
		if (bloom.maxIterations == 0 || height < bloom.downscaleLimit
			|| width < bloom.downscaleLimit)
		{
			Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
			buffer.EndSample("Bloom");
			return;
		}
		RenderTextureFormat format = RenderTextureFormat.Default;
		int fromId = sourceId, toId = bloomPyramidId + 1;
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
		//Copy the resulting blurred image
		//Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		buffer.ReleaseTemporaryRT(fromId - 1);
		toId -= 5; //Release the last horizontal draw and move us up the pyramid
		if(i > 1) { 
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
		buffer.SetGlobalTexture(fxSource2Id, sourceId);
		Draw(bloomPyramidId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		buffer.ReleaseTemporaryRT(fromId);
		buffer.EndSample("Bloom");
    }
}