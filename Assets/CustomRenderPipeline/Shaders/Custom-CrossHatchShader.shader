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
            #pragma geometry geom
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            //#include "UnityCG.cginc"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct g2f
            {
                float2 uv : TEXCOORD0;
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2g vert(appdata v)
            {
                v2g o;
                float3 p = TransformObjectToWorld(v.vertex);
                o.vertex = TransformWorldToHClip(p);
                    
                o.uv = v.uv;
                return o;
            }
            //In the future, we will need to figure out a way to determine which hatching coord to use
            // in the case of multiple lights!
            //Or do multiple passes.
            //IDEA: use the dot product to check which of the light sources has the most impact
            //use that to determine which direction the hatches should point to with multiple sources!
            //(The idea is that the larger dot production of the sources should determine which direction to point to)
            //STUDY HOW CROSS HATCHING IN REALITY IS DONE!
            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
            {
                g2f o;
                float3 norm = cross(IN[1].vertex - IN[0].vertex, IN[2].vertex - IN[0].vertex);
                norm = normalize(norm);
                //Directional Lights
                int dirCount = GetDirectionalLightCount();
                //Assume for now there is only 1 directional light
                float3 lightDir = -_DirectionalLightDirectionsAndMasks[0];
                //for (int d = 0; d < dirCount; d++) {
                //    _DirectionalLightDirectionsAndMasks[d];
                //}
                //Positional + spotlights [DO LATER!]
                int otherCount = GetOtherLightCount();

                for (int i = 0; i < 3; i++)
                {
                    float3 projLight = (lightDir - norm) * dot(lightDir, norm);
                    float3 tangent = cross(projLight, norm);

                    o.vertex = IN[i].vertex;// UnityObjectToClipPos(IN[i].vertex);

                    o.uv = float2(dot(tangent, IN[i].vertex), dot(projLight, IN[i].vertex));
                    triStream.Append(o);
                }

                triStream.RestartStrip();
            }

            float4 frag(g2f i) : SV_Target
            {
                // sample the texture
                float4 col = float4(i.uv.x,i.uv.y,0,1);//tex2D(_MainTex, i.uv);
                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
        ENDHLSL
        }

    }
}
