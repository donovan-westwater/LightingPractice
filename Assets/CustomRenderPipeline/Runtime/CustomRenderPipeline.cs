using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    //Edit pipeline settings on construction
    public CustomRenderPipeline()
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;//Enable batching to improve preformance
    }
    CameraRenderer camRenderer = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera c in cameras)
        {
            camRenderer.Render(context, c);
        }
    }
}
