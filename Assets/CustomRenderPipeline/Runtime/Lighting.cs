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
		otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
		otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
		otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");

	static Vector4[]
		otherLightColors = new Vector4[maxOtherLightCount],
		otherLightPositions = new Vector4[maxOtherLightCount],
		otherLightDirections = new Vector4[maxOtherLightCount],
		otherLightSpotAngles = new Vector4[maxOtherLightCount];
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
			//Adds a new light if there is enough room in the light buffer
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
				case LightType.Spot:
					if (olCount < maxOtherLightCount)
					{
						SetupSpotLight(olCount++, ref vL);
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
			buffer.SetGlobalVectorArray(
				otherLightDirectionsId, otherLightDirections
			);
			buffer.SetGlobalVectorArray(
				otherLightSpotAnglesId, otherLightSpotAngles
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
		//Setup light range to cutoff the intensity if the light is too far
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w =
			1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		//Ensures spotlight calcualtion doesnt effect point lights
		otherLightSpotAngles[index] = new Vector4(0f, 1f);
	}
	//Spotlight setup. Sends info to shader for lighting calculations like with point light and directional light
	void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		//Calculate the spot angle for the spotlight
		//Calculates both inner and outer angles
		Light light = visibleLight.light;
		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
		float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		otherLightSpotAngles[index] = new Vector4(
			angleRangeInv, -outerCos * angleRangeInv
		);
	}
}
