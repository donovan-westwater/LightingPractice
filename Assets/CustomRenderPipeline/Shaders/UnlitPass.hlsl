//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED
#include "../ShaderLibrary/Common.hlsl"
//output is for homogenous clip space
float4 UnlitPassVertex(float3 positionOS: POSITION) : SV_POSITION{
	float3 positionWS = TransformObjectToWorld(positionOS);
	float4 positionView = TransformWorldToView(positionWS);
	return positionView;
}
//: XXXXX statements indicates what we mean with the value we return
//In this case, output is for render target
float4 UnlitPassFragment() : SV_TARGET{
	return float4(0.0, 0.0, 0.0, 0.0);
}

#endif