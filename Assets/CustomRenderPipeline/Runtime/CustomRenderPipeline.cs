using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    bool useDynamicBatching, useGPUInstancing, useLightsPerObject,allowHDR;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    //Edit pipeline settings on construction
    public CustomRenderPipeline(bool allowHDR,bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
        bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postfxSettings)
    {
        this.allowHDR = allowHDR;
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postfxSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;//Enable batching to improve preformance
        GraphicsSettings.lightsUseLinearIntensity = true; //Want lights to use linear
        InitializeForEditor();

    }
    CameraRenderer camRenderer = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera c in cameras)
        {
            camRenderer.Render(context, c, allowHDR,useDynamicBatching, useGPUInstancing, useLightsPerObject
                ,shadowSettings,postFXSettings);
        }
    }
}
