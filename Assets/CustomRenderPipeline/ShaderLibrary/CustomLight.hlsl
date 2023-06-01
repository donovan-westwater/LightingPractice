#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED
//Defines the directional light properties
struct Light {
	float3 color;
	float3 direction;
};
//Want to send light data to GPU
//Getting Scene Data
CBUFFER_START(_CustomLight)
	float3 _DirectionalLightColor;
	float3 _DirectionalLightDirection;
CBUFFER_END

//Returns Directional Light
Light GetDirectionalLight() {
	Light light;
	light.color = _DirectionalLightColor;
	light.direction = _DirectionalLightDirection;
	return light;
}

#endif