Shader "Custom RP/Custom-Lit"
{
    //Tutorial for ref: https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color",Color) = (0.5,0.5,0.5,1.0)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
            [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0 //Want to  be able to disable alpha clipping
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
    }
    SubShader
    {
        Pass
        {
            //Indicate we are using custom lighting approach
            Tags {
                "LightMode" = "CustomLit"
            }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite[_ZWrite] //want to be able to disable writing to depth buffer for transparent rendering
            //Fun fact: you can put non HLSL code in here, so we need to tell Unity what lang we are using!
            HLSLPROGRAM
            #pragma shader_feature _CLIPPING
            #pragma multi_compile_instancing //Helps consolidate draw calls with objects of the same mesh
            #pragma vertex LitPassVertex //This is the name of the vertex step
            #pragma fragment LitPassFragment //This is the name of the frag step
            #include "LitPass.hlsl" //Defining vert and frag in seperate file
            ENDHLSL
        }
    }
}
