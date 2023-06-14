using UnityEngine;
using UnityEngine.Rendering;

//Shadows get there own render pass like the lighting script
//This means its own command buffers, rendercontext, etc

public class Shadows
{
	const int maxShadowedDirectionalLightCount = 1; //Shadows are expensive so we want to limit the amount of shadows per light
	const string bufferName = "Shadows";
	//Command buffer used to setup and execute the shadow draw calls
	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	ScriptableRenderContext context;

	CullingResults cullingResults;

	ShadowSettings settings;
	//Store our dirctional shadow properties per light
	struct ShadowedDirectionalLight
    {
		public int visibleLightIndex;
    }
	ShadowedDirectionalLight[] shadowedDirectionalLights =
		new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
	//Used to determine which light(s) gets to have shadows
	int ShadowedDirectionalLightCount;
	
	public void Setup(
		ScriptableRenderContext context, CullingResults cullingResults,
		ShadowSettings settings
	)
	{
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
		ShadowedDirectionalLightCount = 0;
	}
	public void ReserveDirectionalShadows(Light light, int visibleLightIndex) {
		//If we have enough space, assign a light if the light has shadows enabled
		//We also want to make sure there are objects to cast shadows on as well
		if(ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
			light.shadows != LightShadows.None && light.shadowStrength > 0f &&
			cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        {
			shadowedDirectionalLights[ShadowedDirectionalLightCount++] =
				new ShadowedDirectionalLight
				{
					visibleLightIndex = visibleLightIndex
                };
        }
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

}
