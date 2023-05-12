using UnityEngine;
using UnityEngine.Rendering;
public class CameraRenderer {
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;
        Setup();
        DrawVisibleGeometry(); //Skybox has its own dedicated command buffer
        //You need to submit the draw command to the command buffer
        Submit();
    }
    void DrawVisibleGeometry()
    {
        context.DrawSkybox(camera);
    }
    void Setup()
    {
        context.SetupCameraProperties(camera);
        //Make suer we are workinngwith a clean frame
        buffer.ClearRenderTarget(true, true, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

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
