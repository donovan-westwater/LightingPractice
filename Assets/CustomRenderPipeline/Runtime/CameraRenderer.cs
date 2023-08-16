using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
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

	ScriptableRenderContext context;

	Camera camera;

	CullingResults cullingResults;

	Lighting lighting = new Lighting(); //Provided by RP
	//Called by custom render pipeline to render new images onto the screen
	public void Render(
		ScriptableRenderContext context, Camera camera,
		bool useDynamicBatching, bool useGPUInstancing,
		ShadowSettings shadowSettings
	)
	{
		this.context = context;
		this.camera = camera;

		PrepareBuffer();
		PrepareForSceneWindow();
		if (!Cull(shadowSettings.maxDistance)) //Cull objects if they return false in cull function
		{
			return;
		}
		//Want to setup shadows first before drawing the actual objects
		buffer.BeginSample(SampleName);
		ExecuteBuffer();
		lighting.Setup(context, cullingResults, shadowSettings);
		buffer.EndSample(SampleName);
		Setup();
		DrawVisibleGeometry(useDynamicBatching, useGPUInstancing); //Skybox has its own dedicated command buffer
		//We want to handle material types not supported by our setup
		DrawUnsupportedShaders();
		//We want to be able to draw handles and gizmos
		DrawGizmos();
		lighting.Cleanup();
		//You need to submit the draw command to the command buffer
		Submit();
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
		//Make sure we are working with a clean frame
		buffer.ClearRenderTarget(
			flags <= CameraClearFlags.Depth,
			flags == CameraClearFlags.Color,
			flags == CameraClearFlags.Color ?
				camera.backgroundColor.linear : Color.clear
		);
		buffer.BeginSample(SampleName);
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

	void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
	{
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
			perObjectData = PerObjectData.Lightmaps | PerObjectData.OcclusionProbe
			| PerObjectData.ShadowMask
			| PerObjectData.LightProbe 
			| PerObjectData.LightProbeProxyVolume//Send the lightmaps and light probes assoiated with each object
			| PerObjectData.OcclusionProbeProxyVolume
		};
		drawingSettings.SetShaderPassName(1, litShaderTagId);

		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); //indicate which queues are allowed

		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);

		context.DrawSkybox(camera);
		//Time to draw transparent geometry. This makes sure the skybox doesn't draw over transparent geo
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;

		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}
}