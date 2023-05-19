using UnityEngine;
using UnityEngine.Rendering;
public class CameraRenderer {
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    static ShaderTagId unlitShaderTag = new ShaderTagId("SRPDefaultUnlit");
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    CullingResults cullingResults;
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
        //You need to submit the draw command to the command buffer
        Submit();
    }
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
