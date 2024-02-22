Shader "Custom RP/Custom-CrossHatchShader"
{
    Properties
    {
        _MainTex ("Texture", 2DArray) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        //We are going to start with a simple naive setup just to see if we can draw the textures on the triangles
        //Once that is done, we will restructure this to look like Custom-Lit or Custom-Unlit
        //This means making a cross-hatch vertex and fragment functions in a seperate hlsl file
        Pass
        {
            HLSLINCLUDE
            #include "../ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/CustomSurface.hlsl"
            #include "../ShaderLibrary/Shadows.hlsl"
            #include "../ShaderLibrary/CustomLight.hlsl"
            ENDHLSL
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            //#include "UnityCG.cginc"


            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float3 hashAndBlend : TEXCOORD1;
            };
            Texture2DArray _MainTex;
            SamplerState sampler_MainTex;
            float4 _MainTex_ST;

            //Store the vertex ID in the v2g output
            //also create a UAV which is populated over time by the vertex stage
            //Once we have all the vertices, we can figure out which ones are adj to 
            //NEW PLAN: For now, we will just have to create a script which at the start/ on awake, 
            //searches goes through all the vertices and finds the adjcent verts/triangles. This infomation should then be
            //send over to this shader as an UAV
            //Also add an editor feature which automatically adds the script to all objects that use the shader!
            //Maybe do it on awake or on start?
            v2f vert(appdata v)
            {
                v2f o;
                float3 p = TransformObjectToWorld(v.vertex);
                o.vertex = TransformWorldToHClip(p);
                o.normal = v.normal;// TransformObjectToWorld(v.normal);
                o.uv = v.uv;
                //Sticking to directional for now
                float d = max(0
                    ,dot(normalize(o.normal), normalize(_DirectionalLightDirectionsAndMasks[0])));
                float t = saturate(1.-d) * 7.0;
                float tf = frac(t);
                //Find the hash value for tone
                int index = floor(t);
                o.hashAndBlend = float3((float)index, tf, 1. - tf);
                if (t < 1.0) o.hashAndBlend.yz = float2(1,0);
                //Find the tangent vector [WIP: Goal: Find way to blend tang (found via vert norm plane) with other verts]
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // sample the texture
                float4 col = float4(0,0,i.hashAndBlend.x/8.0,1);//tex2D(_MainTex, i.uv);
                //col.rgb = float4(1,1,1,1)* max(0
                //    , dot(normalize(i.normal), normalize(_DirectionalLightDirectionsAndMasks[0])));
                //col.rgb = normalize(i.normal).rgb;
                col = _MainTex.Sample(sampler_MainTex, float3(i.uv.x, i.uv.y, i.hashAndBlend.x))
                    *i.hashAndBlend.y;
                col += _MainTex.Sample(sampler_MainTex, float3(i.uv.x, i.uv.y, i.hashAndBlend.x + 1))
                    * i.hashAndBlend.z;
                //col = i.adjColor;
                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
        ENDHLSL
        }

    }
}
