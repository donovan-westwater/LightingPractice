Shader "Unlit/Custom-Unlit"
{
    //Tutorial for ref: https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/
    Properties
    {
        
    }
    SubShader
    {
        Pass
        {
            //Fun fact: you can put non HLSL code in here, so we need to tell Unity what lang we are using!
            HLSLPROGRAM
            #pragma vertex UnlitPassVertex //This is the name of the vertex step
            #pragma fragment UnlitPassFragment //This is the name of the frag step
            #include "UnlitPass.hlsl" //Defining vert and frag in seperate file
            ENDHLSL
        }
    }
}
