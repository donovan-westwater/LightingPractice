using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType; //Prevents type clash with global illumination

//This is the editor file which describes changes to the UI and to the pipeline itself
public partial class CustomRenderPipeline
{
	partial void InitializeForEditor();
#if UNITY_EDITOR
	//We want to fix the light mapping so that it is not using the legacy light mapping
	//Spefically, it is using the wrong light fallout. This delegate will correct that
	static Lightmapping.RequestLightsDelegate lightsDelegate =
		(Light[] lights, NativeArray<LightDataGI> output) => {
			var lightData = new LightDataGI();
			for(int i = 0;i < lights.Length; i++)
            {
				Light light = lights[i];
				//Get the light data for each differnt type of light
                switch (light.type)
                {
					case LightType.Directional:
						var directionalLight = new DirectionalLight();
						LightmapperUtils.Extract(light, ref directionalLight);
						lightData.Init(ref directionalLight);
						break;
					case LightType.Point:
						var pointLight = new PointLight();
						LightmapperUtils.Extract(light, ref pointLight);
						lightData.Init(ref pointLight);
						break;
					case LightType.Spot:
						var spotLight = new SpotLight();
						LightmapperUtils.Extract(light, ref spotLight);
						//Set inner and outer spot angle fall off
						spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
						spotLight.angularFalloff =
							AngularFalloffType.AnalyticAndInnerAngle;
						lightData.Init(ref spotLight);
						break;
					case LightType.Area:
						var rectangleLight = new RectangleLight();
						LightmapperUtils.Extract(light, ref rectangleLight);
						//We dont have any real time area lights so force them to be baked
						rectangleLight.mode = LightMode.Baked;
						lightData.Init(ref rectangleLight);
						break;
					default:
						//If there is no light type, then don't bake it!
						lightData.InitNoBake(light.GetInstanceID());
						break;
                }
				//Force the lights to use an inverse squared fall off
				lightData.falloff = FalloffType.InverseSquared;
				output[i] = lightData;
            }
		};
	//Make sure the editor is using the correct light fallofff
	partial void InitializeForEditor()
	{
		Lightmapping.SetDelegate(lightsDelegate);
	}
	//Cleanup delegate once pipeline is deleted
	protected override void Dispose (bool disposing) {
		base.Dispose(disposing);
		Lightmapping.ResetDelegate();
	}
#endif
}