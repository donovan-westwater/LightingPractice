//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED
float4x4 unity_ObjectToWorld; //Matrix for local to world space transforms
float4x4 unity_MatrixVP; //View matrix or world to camera matrix
#endif