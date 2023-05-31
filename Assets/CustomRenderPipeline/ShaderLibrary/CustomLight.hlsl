#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED
//Defines the directional light properties
struct Light {
	float3 color;
	float3 direction;
};
//Returns directional light
Light GetDirectionalLight() {
	Light light;
	light.color = 1.0;
	light.direction = float3(0.0, 1.0, 0.0);
	return light;
}

#endif