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

//Calculates lighting based on light source
float3 GetLighting(Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}
//Calculates lighting using the surface normals
float3 GetLighting(Surface surfaceWS, BRDF brdf,GI gi) {
	//Per object mode
	#if defined(_LIGHTS_PER_OBJECT)
	for (int j = 0; j < min(unity_LightData.y,8); j++) {
		int lightIndex = unity_LightIndices[j / 4][j % 4];
		Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	#else
	ShadowData shadowData = GetShadowData(surfaceWS); //shadow data canceling out multiple lights
	shadowData.shadowMask = gi.shadowMask;
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		//light.attenuation = 1; Shadow attenuation calc not working with multiple dir lights
		color += GetLighting(surfaceWS, brdf,light);
	}
	#endif
	//Get the color that is affecting the surface from the point lights
	for (int j = 0; j < GetOtherLightCount(); j++) {
		Light light = GetOtherLight(j, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	return color;

}


#endif