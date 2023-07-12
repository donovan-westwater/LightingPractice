using UnityEngine;
using UnityEngine.Rendering;

//Shadows get there own render pass like the lighting script
//This means its own command buffers, rendercontext, etc

public class Shadows
{
	const int maxShadowedDirectionalLightCount = 4; //Shadows are expensive so we want to limit the amount of shadows per light
	const int maxCascades = 4; //How many passes for the shadows we use to create the full picture?
	const string bufferName = "Shadows";
	//Shader variant keywords
	static string[] directionalFilterKeywords = {
		"_DIRECTIONAL_PCF3",
		"_DIRECTIONAL_PCF5",
		"_DIRECTIONAL_PCF7",
	};
	//Cascade blending options
	static string[] cascadeBlendKeywords = {
		"_CASCADE_BLEND_SOFT",
		"_CASCADE_BLEND_DITHER"
	};
	static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
	static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
	static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
	static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
	static int cascadeCullingSphereId = Shader.PropertyToID("_CascadeCullingSpheres");
	static int cascadeDataId = Shader.PropertyToID("_CascadeData");
	static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

	//Culling sphers use xyz position for center and w for radius
	static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades]; //Culling sphere setup
	static Vector4[] cascadeData = new Vector4[maxCascades]; //Data used to handle shadow acne
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
		public float slopeScaleBias;
		public float nearPlaneOffset;
    }
	ShadowedDirectionalLight[] shadowedDirectionalLights =
		new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
	//Need matrices to sample the UVs of each tile in our atlas
	static Matrix4x4[]
		dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount*maxCascades];
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
	public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex) {
		//If we have enough space, assign a light if the light has shadows enabled
		//We also want to make sure there are objects to cast shadows on as well
		if(ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
			light.shadows != LightShadows.None && light.shadowStrength > 0f &&
			cullingResults.GetShadowCasterBounds(visibleLightIndex,out Bounds b))
        {
			shadowedDirectionalLights[ShadowedDirectionalLightCount] =
				new ShadowedDirectionalLight
				{
					visibleLightIndex = visibleLightIndex,
					slopeScaleBias = light.shadowBias,
					nearPlaneOffset = light.shadowNearPlane
                };
			return new Vector3(light.shadowStrength, settings.directional.cascadeCount*ShadowedDirectionalLightCount++
				,light.shadowNormalBias);
        }
		return Vector3.zero;
	}
	//Takes a light matrix and converts into shadow atlas tile space
	Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
		//Negate the z direction if revesred z buffer
		if (SystemInfo.usesReversedZBuffer)
		{
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}
		//Convert clip space to texure coords
		float scale = 1f / split;
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);
		return m;
    }
	//Build and Render the shadow map for all lights
	public void Render()
    {
		//We want to get a dummy texture when shadows aren't needed to avoid shader varients
		//We wont have to worry about releasing a texture when it wasnt reserved
		if(ShadowedDirectionalLightCount > 0)
        {
			RenderDirectionalShadows();
        }
        else
        {
			//1x1 dummy texture to prevent releasing nothing
			buffer.GetTemporaryRT(
				dirShadowAtlasId, 1, 1,
				32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }
	//Build and render the shadow map for directionalLights
	void RenderDirectionalShadows() {
		//The shadowmap is created by drawing shadow casting objects to a texture
		//We will use a render texture to store this calculation
		int atlasSize = (int)settings.directional.atlasSize;
		//We will need to edit the texture's channel settings to work better for a shadow map
		//We want a high depth buffer to test for shadows with
		buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
		//We are rendering the current image to the rendertexture
		buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		//Clear depth as that is what we are using for shadows
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();
		//Split each light into its own tile in the texture
		int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount; //All the tiles used during shadows, including tiles used for cascade
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;//calculate number of splits for each light
		int tileSize = atlasSize / split;
		//Render shadows for each of the directional lights in our setup
		for(int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
			RenderDirectionalShadows(i, split,tileSize); //Assign the size of the tile assoiated with the shadow
        }
		//Send shadow matrices to to GPU
		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
		buffer.SetGlobalVectorArray(cascadeCullingSphereId, cascadeCullingSpheres);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
		buffer.SetGlobalVector(
			shadowDistanceFadeId,
			new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade,1f/(1f-f*f))
		);
		SetKeywords(
			directionalFilterKeywords, (int)settings.directional.filter - 1
		);
		SetKeywords(
			cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1
		);
		buffer.SetGlobalVector(
			shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)
		);
		buffer.EndSample(bufferName);
		ExecuteBuffer();

	}
	//Shader Keyword setup
	void SetKeywords(string[] keywords, int enabledIndex)
	{
		//int enabledIndex = (int)settings.directional.filter - 1;
		for (int i = 0; i < keywords.Length; i++)
		{
			if (i == enabledIndex)
			{
				buffer.EnableShaderKeyword(keywords[i]);
			}
			else
			{
				buffer.DisableShaderKeyword(keywords[i]);
			}
		}
	}
	//Adjust render Viewport so we can split the rt into sections
	Vector2 SetTileViewport(int index, int split, float tileSize)
    {
		//Classic modlus, frac vector. Commonly used for creating grids in graphics
		Vector2 offset = new Vector2(index % split, index / split);
		buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
		return offset;
    }
	//Rendering a specifc shadow for a specific directional light
	void RenderDirectionalShadows(int index,int split, int tileSize)
    {
		ShadowedDirectionalLight light = shadowedDirectionalLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
		//Prepping for making matrices for each cascade
		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount;
		Vector3 ratios = settings.directional.CascadeRatios;
		for(int i = 0;i < cascadeCount; i++)
        {        
			//We want to render the scene as if the light is the camera and use the depth info to draw the shadows
			//Since directional lights have no position, we need to create a clip space cube based on the rotation
			//of the light
			//We are going to calculate that using a unity function
			//First arg: visible light index, 2-4 are for the shadow cascade, 5: texture size, 6: near plane
			//The remaining 3 are the output parameters for view matrix and projection matrix and Shadow split data.
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount
				, ratios, tileSize, light.nearPlaneOffset
				, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
			//Split data is for how shadow casting objects should be culled.
			//Save results to shadow settings
			shadowSettings.splitData = splitData;
			if (index == 0) {
				SetCascadeData(i, splitData.cullingSphere, tileSize);
			}
			//Need to get the indices for each tile in the atlas we want to render for the current light
			int tileIndex = tileOffset + i;
			//Create matrixx for converting world to projection space
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix,
				SetTileViewport(tileIndex, split, tileSize),
				split); //save the matrix we calculated so we can sample it later
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias); //Settup bias to help with shadow acne (global and slope scale)
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f); //Reset after shadows are done
		}
	}

	void SetCascadeData(int index,Vector4 cullingSphere, float tileSize)
    {
		float texelSize = 2f * cullingSphere.w / tileSize; //Blowing up samples to correct self shadowing
		float filterSize = texelSize * ((float)settings.directional.filter + 1f);
		//cascadeData[index].x = 1f / cullingSphere.w;
		cullingSphere.w -= filterSize;
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
		cascadeData[index] = new Vector4(
			1f / cullingSphere.w,
			filterSize*1.412136f);
	}
	public void Cleanup()
    {
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		ExecuteBuffer();
    }
	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

}
