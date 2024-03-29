Shader "Unlit/Particles/Custom-Unlit"
{
    //Tutorial for ref: https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        [HDR] _BaseColor("Color",Color) = (1.0,1.0,0.0,1.0)
        [Toggle(_VERTEX_COLORS)] _VertexColors("Vertex Colors", Float) = 0
        [Toggle(_FLIPBOOK_BLENDING)] _FlippbookBlending("Flipbook Blending",Float) = 0
        [Toggle(_SOFT_PARTICLES)] _SoftParticles("Enable Soft Particles",Float ) = 0
        _SoftParticlesDistance("Soft Particles Distance", Range(0.0,10.0)) = 0
        _SoftParticlesRange("Soft Particles Range", Range(0.01,10.0)) = 1
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
            [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0 //Want to  be able to disable alpha clipping
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
        //Distance based fading
        [Toggle(_NEAR_FADE)] _NearFade("Near Fade", Float) = 0
        _NearFadeDistance("Near Fade Distance", Range(0.0, 10.0)) = 1
        _NearFadeRange("Near Fade Range", Range(0.01, 10.0)) = 1
        //Distortion Map
        [Toggle(_DISTORTION)] _Distortion("Distortion", Float) = 0
        [NoScaleOffset] _DistortionMap("Distortion Vectors", 2D) = "bumb" {}
        _DistortionStrength("Distortion Strength", Range(0.0, 0.2)) = 0.1
        _DistortionBlend("Distortion Blend", Range(0.0,1.0)) = 1
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../ShaderLibrary/UnlitInput.hlsl"
        ENDHLSL
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite[_ZWrite] //want to be able to disable writing to depth buffer for transparent rendering
            //Fun fact: you can put non HLSL code in here, so we need to tell Unity what lang we are using!
            HLSLPROGRAM
            #pragma target 3.5 //Helps with differences in webGL and OpenGL
            #pragma shader_feature _DISTORTION
            #pragma shader_feature _FLIPBOOK_BLENDING
            #pragma shader_feature _VERTEX_COLORS
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _NEAR_FADE
            #pragma shader_feature _SOFT_PARTICLES
            #pragma multi_compile_instancing //Helps consolidate draw calls with objects of the same mesh
            #pragma vertex UnlitPassVertex //This is the name of the vertex step
            #pragma fragment UnlitPassFragment //This is the name of the frag step
            #include "UnlitPass.hlsl" //Defining vert and frag in seperate file
            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}
