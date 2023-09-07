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
	const int maxDirLightCount = 4, maxOtherLightCount = 64;
	//Point and SpotLight vars
	static int
		otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
		otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
		otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");

	static Vector4[]
		otherLightColors = new Vector4[maxOtherLightCount],
		otherLightPositions = new Vector4[maxOtherLightCount];
	//Directional Light vars
	static int
		//dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
		//dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
		dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
		dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
	static Vector4[]
		dirLightColors = new Vector4[maxDirLightCount],
		dirLightDirections = new Vector4[maxDirLightCount],
		dirLightShadowData = new Vector4[maxDirLightCount];
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
		shadows.Render();
		//Finish setting up command buffer
		buffer.EndSample(bufferName);
		//Submit buffer
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	public void Cleanup()
    {
		shadows.Cleanup();
    }
	void SetupLights()
    {
		//Retrive data relvant to the lights - Array allows for multiple lights
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		int dlCount = 0, olCount = 0;
		for(int i = 0; i < visibleLights.Length; i++)
        {
			VisibleLight vL = visibleLights[i]; //Want to optimize memory via pass by ref
            switch (vL.lightType)
            {
				case LightType.Directional:
					if (dlCount < maxDirLightCount)
					{
						SetupDirectionalLight(dlCount++, ref vL);
					}
					break;
				case LightType.Point:
					if(olCount < maxOtherLightCount)
                    {
						SetupPointLight(olCount++, ref vL);
                    }
					break;
			}
			
        }
		//Send populated dir lights to shader
		buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
		if(dlCount > 0) { 
			buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
			buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
			buffer.SetGlobalVectorArray(dirLightShadowDataId,dirLightShadowData);
		}
		//Send populated spot and point lights to shader
		buffer.SetGlobalInt(otherLightCountId, olCount);
		if (olCount > 0)
		{
			buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
			buffer.SetGlobalVectorArray(
				otherLightPositionsId, otherLightPositions
			);
		}
	}
    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
	{
		dirLightColors[index] = visibleLight.finalColor;
		//Negate forward vector for the light
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		//Reserve a shadow for the light if there is enough room
		dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);

	}
	//Setup a point light
	void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
		otherLightColors[index] = visibleLight.finalColor;
		otherLightPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
	}
}
