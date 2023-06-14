using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    bool useDynamicBatching, useGPUInstancing;
    ShadowSettings shadowSettings;
    //Edit pipeline settings on construction
    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
        ShadowSettings shadowSettings)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;//Enable batching to improve preformance
        GraphicsSettings.lightsUseLinearIntensity = true; //Want lights to use linear
        this.shadowSettings = shadowSettings;
    }
    CameraRenderer camRenderer = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera c in cameras)
        {
            camRenderer.Render(context, c, useDynamicBatching, useGPUInstancing,shadowSettings);
        }
    }
}
