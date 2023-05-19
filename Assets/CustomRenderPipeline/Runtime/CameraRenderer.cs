using UnityEngine;
using UnityEngine.Rendering;
public class CameraRenderer {
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    static ShaderTagId unlitShaderTag = new ShaderTagId("SRPDefaultUnlit");
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
    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;
        if (!Cull()) //Cull objects if they return false in cull function
        {
            return;
        }
        Setup();
        DrawVisibleGeometry(); //Skybox has its own dedicated command buffer
#if UNITY_EDITOR
        //We want to handle material types not supported by our setup (Legacy shaders)
        DrawUnsupportedShaders();
        //Draw gizmos in the editor
        DrawGizmos();
#endif
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
#endif
    void DrawVisibleGeometry()
    {

        //Invoke our draw renderers from our meshes and such
        var sortingSettings = new SortingSettings(camera);
        sortingSettings.criteria = SortingCriteria.CommonOpaque;
        var drawingSettings = new DrawingSettings(unlitShaderTag,sortingSettings); //Idicate which shader passes are allowed
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
        //Make suer we are workinngwith a clean frame
        buffer.ClearRenderTarget(true, true, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

    }
    bool Cull()
    {
        ScriptableCullingParameters p;
        if(camera.TryGetCullingParameters(out p))
        {
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
    void Submit()
    {
        buffer.EndSample(bufferName); //Finished storinng commands to submit
        ExecuteBuffer();
        context.Submit();
    }
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear(); //Clean buffer so draw commands don't pile up
    }
}
