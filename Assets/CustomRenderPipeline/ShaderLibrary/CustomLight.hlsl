#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED
//Defines the directional light properties
struct Light {
	float3 color;
	float3 direction;
	float attenuation;
};
#define MAX_DIRECTIONAL_LIGHT_COUNT 4
//Want to send light data to GPU
//Getting Scene Data
CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END
//Get the directional light count
int GetDirectionalLightCount() {
	return _DirectionalLightCount;
}

//Gets shadow data for a light
DirectionalShadowData GetDirectionalShadowData(int lightIndex) {
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData[lightIndex].x;
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y;
	return data;
}

//Returns Directional Light
Light GetDirectionalLight(int index, Surface surfaceWS) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	DirectionalShadowData shadowData = GetDirectionalShadowData(index);
	light.attenuation = GetDirectionalShadowAttenuation(shadowData, surfaceWS);
	return light;
}

#endif