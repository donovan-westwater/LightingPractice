#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED
//Does a basic directional light calculation with surface normal
float3 IncomingLight(Surface surface, Light light) {
	//saturate clamps value between 0 and 1
	float lIn = saturate(dot(surface.normal, light.direction)
	*light.attenuation) * light.color;
	//lIn = lerp(0.5,1,ceil(lIn)); Cell Shading mode!
	return lIn * light.color;
}
//Specular global illumination
float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular) {
	float fresnelStrength = 
		Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
	float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
	//Roughness scatters the reflection
	reflection /= brdf.roughness * brdf.roughness + 1.0;
	return diffuse * brdf.diffuse + reflection;
}
//Calculates lighting based on light source
float3 GetLighting(Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}
//Calculates lighting using the surface normals
float3 GetLighting(Surface surfaceWS, BRDF brdf,GI gi) {
	ShadowData shadowData = GetShadowData(surfaceWS); //shadow data canceling out multiple lights
	shadowData.shadowMask = gi.shadowMask;
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		//light.attenuation = 1; Shadow attenuation calc not working with multiple dir lights
		color += GetLighting(surfaceWS, brdf,light);
	}
	return color;
}


#endif