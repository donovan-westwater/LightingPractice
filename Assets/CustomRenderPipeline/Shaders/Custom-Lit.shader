Shader "Custom RP/Custom-Lit"
{
    //Tutorial for ref: https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color",Color) = (0.5,0.5,0.5,1.0)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0 //Want to  be able to disable alpha clipping
        [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {} //Emission map for emissive materials
        [HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0) //Emission color
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0 //USed for transparent objects
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha("Premultiply Alpha", Float) = 0 //fade out diffuse while keeping specular
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
    }
    SubShader
    {
        Pass
        {
            HLSLINCLUDE
            #include "../ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/LitInput.hlsl"
            ENDHLSL
            //Indicate we are using custom lighting approach
            Tags {
                "LightMode" = "CustomLit"
            }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite[_ZWrite] //want to be able to disable writing to depth buffer for transparent rendering
            //Fun fact: you can put non HLSL code in here, so we need to tell Unity what lang we are using!
            HLSLPROGRAM
            #pragma target 3.5 //Helps with differences in webGL and OpenGL
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_instancing //Helps consolidate draw calls with objects of the same mesh
            #pragma vertex LitPassVertex //This is the name of the vertex step
            #pragma fragment LitPassFragment //This is the name of the frag step
            #include "LitPass.hlsl" //Defining vert and frag in seperate file
            ENDHLSL
        }
        //2nd pass to handle shadow casting
        Pass 
        {
            Tags{
                "LightMode" = "ShadowCaster"
            }

            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass 
        {
            Tags {
                "LightMode" = "Meta"
            }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "MetaPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}
