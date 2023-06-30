#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position; //Surface position for shadows
	float3 normal;
	float3 viewDirection;
	float3 color;
	float alpha;
	float metallic; //Is the surface perfectly diffuse or is it a perfect mirror?
	float smoothness; //If this surface is bumpy, then the light gets scattered, blurring the reflection of light
};
#endif