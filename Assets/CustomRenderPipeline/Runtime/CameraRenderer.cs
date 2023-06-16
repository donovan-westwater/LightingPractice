using UnityEngine;
using UnityEngine.Rendering;
public class CameraRenderer {
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    string sampleName = bufferName;
    static ShaderTagId unlitShaderTag = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTag = new ShaderTagId("CustomLit");
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    CullingResults cullingResults;
#if UNITY_EDITOR
    static Material errorMaterial; //used for unsupported mats
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
#endif
    Lighting lighting = new Lighting();
    public void Render(ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings) //Provided by RP
    {
        this.context = context;
        this.camera = camera;
#if UNITY_EDITOR
        PrepareBuffer();
        PrepareForSceneWindow();
#endif
        if (!Cull(shadowSettings.maxDistance)) //Cull objects if they return false in cull function
        {
            return;
        }
        //Want to setup shadows first before drawing the actual objects
        buffer.BeginSample(sampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,shadowSettings);
        buffer.EndSample(sampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching,useGPUInstancing); //Skybox has its own dedicated command buffer
#if UNITY_EDITOR
        //We want to handle material types not supported by our setup (Legacy shaders)
        DrawUnsupportedShaders();
        //Draw gizmos in the editor
        DrawGizmos();
#endif
        //Cleanup tmp info used by lights and shaodws
        lighting.Cleanup();
        //You need to submit the draw command to the command buffer
        Submit();
    }
#if UNITY_EDITOR
    void DrawUnsupportedShaders()
    {
        //Setup error material to draw when bad shaders are used
        if (errorMaterial == null)
        {
            errorMaterial =
                new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        //We are drawing legacy shaders starting with the first legacy pass
        //using default sorting settings for the camera
        //We dont care about the settings since these are all invalid!
        var drawingSettings = new DrawingSettings(
            legacyShaderTagIds[0], new SortingSettings(camera)
        )
        {
            overrideMaterial = errorMaterial
        };
        //Add the rest of the passes to the drawing settings
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );
    }
    void DrawGizmos()
    {
        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
    void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            //UI wont show up in scene view without this!
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }
    //Make sure each camera gets their own scope
    //Want to make sure samples are attached to the correct cameras
    void PrepareBuffer()
    {
        buffer.name = sampleName = camera.name;
    }
#endif
        void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {

        //Invoke our draw renderers from our meshes and such
    var sortingSettings = new SortingSettings(camera);
        sortingSettings.criteria = SortingCriteria.CommonOpaque;
        var drawingSettings = new DrawingSettings(unlitShaderTag,sortingSettings); //Idicate which shader passes are allowed
        drawingSettings.enableDynamicBatching = useDynamicBatching; //Temproary, trying out another from of call bundling
        drawingSettings.enableInstancing = useGPUInstancing; //GPU instancing doesn't work with dynamic batching
        drawingSettings.SetShaderPassName(1, litShaderTag); //Setup custom lighting shader pass
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque); //Ideicate which queues are allowed
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);
        context.DrawSkybox(camera);
        //Time to draw transparent geometry. This makes sure the skybox doesn't draw over transparent geo
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);
    }
    void Setup()
    {
        context.SetupCameraProperties(camera);
        //Clear flags for cam layers and combining results for multiple cameras
        CameraClearFlags flags = camera.clearFlags;
        //Make suer we are workinngwith a clean frame
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(sampleName);
        ExecuteBuffer();

    }
    bool Cull(float maxDistance)
    {
        ScriptableCullingParameters p;
        if(camera.TryGetCullingParameters(out p))
        {
            p.shadowDistance = Mathf.Min(maxDistance,camera.farClipPlane); //Define max distance for shadows, cull beyond that
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
    void Submit()
    {
        buffer.EndSample(sampleName); //Finished storinng commands to submit
        ExecuteBuffer();
        context.Submit();
    }
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear(); //Clean buffer so draw commands don't pile up
    }
}
