#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position; //Surface position for shadows
	float3 normal;
	float3 interpolatedNormal; //Normal for handling shadow bias
	float3 viewDirection;
	float depth;
	float3 color;
	float alpha;
	float metallic; //Is the surface perfectly diffuse or is it a perfect mirror?
	float occlusion; //What are the gaps and occluded parts of the surface?
	float smoothness; //If this surface is bumpy, then the light gets scattered, blurring the reflection of light
	float fresnelStrength; //How powerful is the fresnel reflection for this surface
	float dither;
};
#endif