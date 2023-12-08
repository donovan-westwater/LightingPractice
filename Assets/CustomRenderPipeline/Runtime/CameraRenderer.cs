using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
	//Custom Camera settings for the camera
	static CameraSettings defaultCameraSettings = new CameraSettings();
	//Name of the command buffer's used for this
	const string bufferName = "Render Camera";
	//The unlit and lit tags used for our pipeline
	static ShaderTagId
		unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
		litShaderTagId = new ShaderTagId("CustomLit");

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	//static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer"); //We want to read from a frame that isn't being written on
	//We want to seperate the color and depth buffers so we can sample them seperately
	static int colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
	static int depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment");
	static int depthTextureId = Shader.PropertyToID("_CameraDepthTexture");
	static int colorTextureId = Shader.PropertyToID("_CameraColorTexture");
	static int sourceTextureId = Shader.PropertyToID("_SourceTexture");
	static int srcBlendId = Shader.PropertyToID("_CameraSrcBlend");
	static int dstBlendId = Shader.PropertyToID("_CameraDstBlend");
	static int bufferSizeId = Shader.PropertyToID("_CameraBufferSize"); //We want to tell the shader the correct buffersizes
	ScriptableRenderContext context;

	Camera camera;

	CullingResults cullingResults;

	Lighting lighting = new Lighting(); //Provided by RP

	PostFXStack postFXStack = new PostFXStack(); //Controls what effects will be applied

	bool useHDR;

	bool useScaledRendering;

	bool useDepthTexture;

	bool useColorTexture;

	bool useIntermediateBuffer;
	
	Material material;

	Texture2D missingTexture;

	Vector2Int bufferSize; //Size of the render buffer of the camera

	static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None; //Copy Texture not supported in WebGL 2.0

	static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);

	public const float renderScaleMin = 0.1f, renderScaleMax = 2f;
	public CameraRenderer(Shader shader)
	{
		material = CoreUtils.CreateEngineMaterial(shader);
		//Create a texture to be rendered when depth doesn't exist
		missingTexture = new Texture2D(1, 1)
		{
			hideFlags = HideFlags.HideAndDontSave,
			name = "Missing"
		};
		missingTexture.SetPixel(0, 0, Color.white * 0.5f);
		missingTexture.Apply(true, true);
	}
	public void Dispose()
	{
		CoreUtils.Destroy(material);
		CoreUtils.Destroy(missingTexture);
	}
	//Called by custom render pipeline to render new images onto the screen
	public void Render(
		ScriptableRenderContext context, Camera camera,
		CameraBufferSettings bufferSettings,
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
		ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution
	)
	{
		this.context = context;
		this.camera = camera;
		//Assign Custom Camera Settings
		var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
		//use our default if nothing is found
		CameraSettings cameraSettings =
	crpCamera ? crpCamera.Settings : defaultCameraSettings;
		//Do we use the depth sample pass?
		if(camera.cameraType == CameraType.Reflection)
        {
			useDepthTexture = bufferSettings.copyDepthReflections;
			useColorTexture = bufferSettings.copyColorReflections;
        }
        else
        {
			useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
			useDepthTexture = bufferSettings.copyDepth && bufferSettings.copyDepth;
        }
        if (cameraSettings.overridePostFX)
        {
			postFXSettings = cameraSettings.postFXSettings;
        }
		//Check to see if we want to use render scale
		float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
		useScaledRendering = Mathf.Abs(renderScale-1f) > 0.01f; //Slight variations won't do anything
		PrepareBuffer();
		PrepareForSceneWindow();
		if (!Cull(shadowSettings.maxDistance)) //Cull objects if they return false in cull function
		{
			return;
		}
        //Apply render scale
        if (useScaledRendering)
        {
			renderScale = Mathf.Clamp(renderScale,renderScaleMin, renderScaleMax);
			bufferSize.x = (int)(camera.pixelWidth * renderScale);
			bufferSize.y = (int)(camera.pixelHeight * renderScale);
		}
		else
		{
			bufferSize.x = camera.pixelWidth;
			bufferSize.y = camera.pixelHeight;
		}
		useHDR = bufferSettings.allowHDR && camera.allowHDR;
		//Want to setup shadows first before drawing the actual objects
		buffer.BeginSample(SampleName);
		buffer.SetGlobalVector(bufferSizeId, new Vector4(1f / bufferSize.x, 1f / bufferSize.y
			, bufferSize.x, bufferSize.y)); //We use the same format as _ScreenParams that unity sends
		ExecuteBuffer();
		lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject,
			cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
		postFXStack.Setup(context, camera, bufferSize,postFXSettings,useHDR, colorLUTResolution,
			cameraSettings.finalBlendMode);
		buffer.EndSample(SampleName);
		Setup();
		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing,useLightsPerObject
			,cameraSettings.renderingLayerMask); //Skybox has its own dedicated command buffer
		//We want to handle material types not supported by our setup
		DrawUnsupportedShaders();
		//We want to be able to draw handles and gizmos
		DrawGizmosBeforeFX();
		//Render the Post FX at the very end
		if (postFXStack.IsActive)
		{
			postFXStack.Render(colorAttachmentId);
		}else if (useIntermediateBuffer) //Draw our final output into the buffer for sampling depth for particles
        {
			DrawFinal(cameraSettings.finalBlendMode);
			ExecuteBuffer();
        }
		DrawGizmosAfterFX();
		Cleanup();
		//You need to submit the draw command to the command buffer
		Submit();
	}
	void Cleanup()
	{
		lighting.Cleanup();
		if (useIntermediateBuffer)
		{
			buffer.ReleaseTemporaryRT(colorAttachmentId);
			buffer.ReleaseTemporaryRT(depthAttachmentId);
            if (useColorTexture)
            {
				buffer.ReleaseTemporaryRT(colorTextureId);
            }

			if (useDepthTexture)
			{
				buffer.ReleaseTemporaryRT(depthTextureId);
			}
		}
	}
	bool Cull(float maxShadowDistance)
	{
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
		{
			p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
			cullingResults = context.Cull(ref p);
			return true;
		}
		return false;
	}

	void Setup()
	{
		context.SetupCameraProperties(camera);
		//Clear flags for cam layers and combining results for multiple cameras
		CameraClearFlags flags = camera.clearFlags;
		//Make sure we can use the depth buffer even if the postfx stack is off
		useIntermediateBuffer = useColorTexture || useDepthTexture || postFXStack.IsActive
			|| useScaledRendering;
        if (useIntermediateBuffer)
        {
			//Clear the  frame buffer
			if(flags > CameraClearFlags.Color)
            {
				flags = CameraClearFlags.Color;
            }
			//Store the current frame in the frame buffer for reading
			//First we sample the color 
			buffer.GetTemporaryRT(
				colorAttachmentId, bufferSize.x, bufferSize.y,
				0, FilterMode.Bilinear, useHDR ?
					RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
			);
			//Then sample depth
			buffer.GetTemporaryRT(
				depthAttachmentId, bufferSize.x, bufferSize.y,
				32, FilterMode.Point, RenderTextureFormat.Depth
			);
			buffer.SetRenderTarget(
				colorAttachmentId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
				depthAttachmentId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
			);
		}
		//Make sure we are working with a clean frame
		buffer.ClearRenderTarget(
			flags <= CameraClearFlags.Depth,
			flags == CameraClearFlags.Color,
			flags == CameraClearFlags.Color ?
				camera.backgroundColor.linear : Color.clear
		);
		buffer.BeginSample(SampleName);
		buffer.SetGlobalTexture(depthTextureId, missingTexture);
		buffer.SetGlobalTexture(colorTextureId, missingTexture);
		ExecuteBuffer();
	}

	void Submit()
	{
		buffer.EndSample(SampleName);
		ExecuteBuffer();
		context.Submit();
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing,bool useLightsPerObject,
		int renderingLayerMask)
	{
		//PerObjectData for objects
		PerObjectData lightsPerObjectFlags = useLightsPerObject ?
			PerObjectData.LightData | PerObjectData.LightIndices :
			PerObjectData.None; //Do we use per object data?
		//Don't use per object data for Large objects sicne there are limits
		//To how many light sources can illuminate them and gets buggy 
		//Invoke our draw renderers from our meshes and such
		var sortingSettings = new SortingSettings(camera)
		{
			criteria = SortingCriteria.CommonOpaque
		};
		var drawingSettings = new DrawingSettings(
			unlitShaderTagId, sortingSettings
		)//indicate which shader passes are allowed
		{
			enableDynamicBatching = useDynamicBatching, //Bundles draw calls together
			enableInstancing = useGPUInstancing, //Turns objects into instances
			perObjectData = PerObjectData.Lightmaps 
			| PerObjectData.ReflectionProbes
			| PerObjectData.OcclusionProbe
			| PerObjectData.ShadowMask
			| PerObjectData.LightProbe 
			| PerObjectData.LightProbeProxyVolume//Send the lightmaps and light probes assoiated with each object
			| PerObjectData.OcclusionProbeProxyVolume
			| lightsPerObjectFlags
		};
		drawingSettings.SetShaderPassName(1, litShaderTagId);

		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque
			, renderingLayerMask: (uint) renderingLayerMask); //indicate which queues are allowed

		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);

		context.DrawSkybox(camera);
		if(useColorTexture || useDepthTexture)
        {
			CopyAttachments();
		}
		//Time to draw transparent geometry. This makes sure the skybox doesn't draw over transparent geo
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;

		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}
	//Draw into a render texture from another texture
	void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
		buffer.SetGlobalTexture(sourceTextureId, from);
		buffer.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.DrawProcedural(
			Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3
		);
	}
	//Copy of the PostFX draw function where we set the blend mode back to 1 - 0 
	//This allows the function to not effect other copy issues
	//This means we can copy to an intermediate texture and handle multiple cameras
	void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
	{
		//Copy over to the from the camera rt to the intermediate rt
		buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
		buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
		buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ?
				RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
			RenderBufferStoreAction.Store
		);
		buffer.SetViewport(camera.pixelRect);
		buffer.DrawProcedural(
			Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3
		);
		//reset the blending so that they aren't effected by the distortion blending
		buffer.SetGlobalFloat(srcBlendId, 1f);
		buffer.SetGlobalFloat(dstBlendId, 0f);
	}
	//Copy our depth and color buffers
	void CopyAttachments()
    {
        if (useColorTexture)
        {
			buffer.GetTemporaryRT(colorTextureId, bufferSize.x, bufferSize.y, 0
				, FilterMode.Bilinear,
				useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            if (copyTextureSupported)
            {
				buffer.CopyTexture(colorAttachmentId, colorTextureId);
            }
            else
            {
				Draw(colorAttachmentId, colorTextureId);
            }
        }
        if (useDepthTexture)
        {
			buffer.GetTemporaryRT(depthTextureId, bufferSize.x, bufferSize.y,
				32, FilterMode.Point, RenderTextureFormat.Depth);
            if (copyTextureSupported)
            {
				buffer.CopyTexture(depthAttachmentId, depthTextureId);
            }
            else
            {
				Draw(depthAttachmentId, depthTextureId,true);
				//We are drawing to the wrong render target if we leave it there
				//move it back
				//buffer.SetRenderTarget(
				//	colorAttachmentId,
				//	RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
				//	depthAttachmentId,
				//	RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
				//);
			}
		}
		if (!copyTextureSupported)
		{
			buffer.SetRenderTarget(
				colorAttachmentId,
				RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
				depthAttachmentId,
				RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
			);
		}
		ExecuteBuffer();
	}
}