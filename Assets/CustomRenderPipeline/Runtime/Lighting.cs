using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
	//Name of the command buffer used to send lighting data
	const string bufferName = "Lighting";
	//Command buffer to submit lighting data to GPU
	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};
	const int maxDirLightCount = 4;
	static int
		//dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
		//dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
		dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
	static Vector4[]
		dirLightColors = new Vector4[maxDirLightCount],
		dirLightDirections = new Vector4[maxDirLightCount];
	CullingResults cullingResults; //Need which visible spaces are going to be affected
	Shadows shadows = new Shadows(); //Class used to handle the shadow draw calls
	public void Setup(ScriptableRenderContext context, CullingResults cullingResults,ShadowSettings shadowSettings)
	{
		this.cullingResults = cullingResults;
		//Start command buffer setup
		buffer.BeginSample(bufferName);
		//Setup the directional light to submit
		//SetupDirectionalLight();
		shadows.Setup(context, cullingResults, shadowSettings);
		SetupLights();
		//Finish setting up command buffer
		buffer.EndSample(bufferName);
		//Submit buffer
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	void SetupLights()
    {
		//Retrive data relvant to the lights - Array allows for multiple lights
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		int dlCount = 0;
		for(int i = 0; i < visibleLights.Length; i++)
        {
			VisibleLight vL = visibleLights[i]; //Want to optimize memory via pass by ref
			if (vL.lightType == LightType.Directional)
            {
				SetupDirectionalLight(dlCount++, ref vL);
				if (dlCount >= maxDirLightCount) break; //Make sure we don't go over max light count
			}
        }
		//Send populated dir lights to shader
		buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
		buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
		buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
	}
    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
	{
		dirLightColors[index] = visibleLight.finalColor;
		//Negate forward vector for the light
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		//Reserve a shadow for the light if there is enough room
		shadows.ReserveDirectionalShadows(visibleLight.light, index);

	}
}
