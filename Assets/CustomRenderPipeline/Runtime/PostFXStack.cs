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

	int fxSourceId = Shader.PropertyToID("_PostFxXSource"); //Used to access source image for post fx

	ScriptableRenderContext context;

	Camera camera;

	PostFXSettings settings;

	enum Pass
    {
		Copy
    }
	public bool IsActive => settings != null; //Keeps track of if there is post fx

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
		Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
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
}