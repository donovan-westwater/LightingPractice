using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    CameraBufferSettings cameraBufferSettings;
    int colorLUTResolution;
    //Edit pipeline settings on construction
    public CustomRenderPipeline(CameraBufferSettings cameraBufferSettings,
        bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
        bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postfxSettings
        , int colorLUTResolution, Shader cameraRendererShader)
    {
        this.cameraBufferSettings = cameraBufferSettings;
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postfxSettings;
        this.colorLUTResolution = colorLUTResolution;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;//Enable batching to improve preformance
        GraphicsSettings.lightsUseLinearIntensity = true; //Want lights to use linear
        InitializeForEditor();
        camRenderer = new CameraRenderer(cameraRendererShader);
    }
    CameraRenderer camRenderer;// = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera c in cameras)
        {
            camRenderer.Render(context, c, cameraBufferSettings,useDynamicBatching, useGPUInstancing, useLightsPerObject
                ,shadowSettings,postFXSettings, colorLUTResolution);
        }
    }
    //Cleanup after once pipeline is deleted
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        camRenderer.Dispose();
    }
}
