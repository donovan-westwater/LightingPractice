Shader "Custom RP/Custom-CrossHatchShader"
{
    Properties
    {
        _MainTex ("Texture", 2DArray) = "white" {}
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
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
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma multi_compile _ LOD_FADE_CROSSFADE //Enables cross fade for this shader for LOD
            #pragma multi_compile _ LIGHTMAP_ON
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
                float3 vertexW : VAR_POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float3 hashAndBlend : TEXCOORD1;
                float2 lightIndices : VAR_UV;
                float  dotVal : COLOR;
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
            //IDEA: Make simple surface and then do all the nessceary steps to get the shadow. Multiply result with dot
            //to get shadowed value
            v2f vert(appdata v)
            {
                v2f o;
                float3 p = TransformObjectToWorld(v.vertex);
                o.vertexW = p;
                o.vertex = TransformWorldToHClip(p);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                
                //Look to see what lights exist
                o.lightIndices = float2(-1, -1);                
                float d = -1;
                for (int i = 0; i < GetDirectionalLightCount(); i++) {
                    float prev = d;
                    d = max(d, dot(normalize(o.normal), normalize(_DirectionalLightDirectionsAndMasks[i])));
                    if (prev < d) o.lightIndices.x = i;
                }
                //Other point lights
                for (int i = 0; i < GetOtherLightCount(); i++) {
                    float prev = d;
                    float3 dir = _OtherLightPositions[i].xyz - p;
                    d = max(d, dot(normalize(o.normal), normalize(dir)));
                    if (prev < d) o.lightIndices.y = i;
                }
                d = max(0,d);
                float t = d;// saturate(1. - d) * 8.0;
                o.dotVal = t;
                /*
                float tf = frac(t);
                //Find the hash value for tone
                int index = floor(t);
                //When the frac starts to approch the next tone, the previous tone should have less of a weight!
                o.hashAndBlend = float3((float)index, 1.-tf, tf);
                if (t < 1.0) o.hashAndBlend.yz = float2(1,0);
                //Find the tangent vector [WIP: Goal: Find way to blend tang (found via vert norm plane) with other verts]
                */
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                //Populate surface
                Surface surface;
                surface.position = i.vertexW; //pixel position for shadows
                surface.normal = normalize(i.normal);
                surface.interpolatedNormal = surface.normal;
                surface.viewDirection = normalize(_WorldSpaceCameraPos - i.vertexW);
                surface.depth = -TransformWorldToView(i.vertexW).z;
                surface.color = float3(1,1,1);
                surface.alpha = 1.0;
                surface.metallic = 0; //we aren't bothering with specular lighting
                surface.smoothness = 0; //We aren't bothering with specular lighting
                surface.occlusion = 0;
                surface.fresnelStrength = 0;
                surface.dither = 0;
                surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
                //Get light info (if it exists
                ShadowData sd = GetShadowData(surface);
                int lightIndex = -1;
                Light dl;
                if (i.lightIndices.x >= 0) {
                    lightIndex = i.lightIndices.x;
                    dl = GetDirectionalLight(lightIndex, surface, sd);
                }
                if (i.lightIndices.y >= 0) {
                    lightIndex = i.lightIndices.y;
                    dl = GetOtherLight(lightIndex, surface, sd);
                }
                float testV = i.dotVal *dl.attenuation;
                i.dotVal = i.dotVal * dl.attenuation;
                i.dotVal = saturate(1. - i.dotVal) * 8.0;
                //Hash the dotVal
                float tf = frac(i.dotVal);
                //Find the hash value for tone
                int index = floor(i.dotVal);
                //When the frac starts to approch the next tone, the previous tone should have less of a weight!
                i.hashAndBlend = float3((float)index, 1. - tf, tf);
                //if (i.dotVal < 0.5) i.hashAndBlend.yz = float2(1,0);
                // sample the texture
                float4 col = float4(0,0,i.hashAndBlend.x/8.0,1);//tex2D(_MainTex, i.uv);
                //col.rgb = float4(1,1,1,1)* max(0
                //    , dot(normalize(i.normal), normalize(_DirectionalLightDirectionsAndMasks[0])));
                //col.rgb = normalize(i.normal).rgb;
                col = _MainTex.Sample(sampler_MainTex, float3(i.uv.x, i.uv.y, i.hashAndBlend.x-1))
                    *i.hashAndBlend.y;
                col += _MainTex.Sample(sampler_MainTex, float3(i.uv.x, i.uv.y, i.hashAndBlend.x))
                    * i.hashAndBlend.z;
                //col = float4(testV, 0, 0, 1);
                //col = i.adjColor;
                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
        ENDHLSL
        }
        Pass {
            Tags {
                "LightMode" = "ShadowCaster"
            }
            HLSLINCLUDE
            #include "../ShaderLibrary/Common.hlsl"
            #include "../ShaderLibrary/LitInput.hlsl"
            ENDHLSL
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            
            struct Attributes {
                float3 positionOS : POSITION;
                float2 baseUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            //Custom output
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 baseUV : VAR_BASE_UV; //Basically declaring that it has no special meaning
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            bool _ShadowPancaking;
            //output is for homogenous clip space
            Varyings ShadowCasterPassVertex(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input); //For GPU instancing
                UNITY_TRANSFER_INSTANCE_ID(input, output); //copy index from input to output for GPU instancing
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                if (_ShadowPancaking) {
                    //Prevents shadows from being clipped by the near plane of cam
                    #if UNITY_REVERSED_Z
                    output.positionCS.z =
                        min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                    #else
                    output.positionCS.z =
                        max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                    #endif
                }
                //float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
                output.baseUV = TransformBaseUV(input.baseUV); //Transform UVs
                return output;
            }

            void ShadowCasterPassFragment(Varyings input) {
                UNITY_SETUP_INSTANCE_ID(input);
                //float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV); //Samples texture
                //float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor); //Get color from instance
                InputConfig config = GetInputConfig(input.positionCS,input.baseUV);
                ClipLOD(config.fragment, unity_LODFade.x);
                float4 base = GetBase(config);

            }
            ENDHLSL
        }
    }
}
