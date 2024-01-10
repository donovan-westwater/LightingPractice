Shader "Unlit/Texture3DVIsualizer"
{
    Properties
    {
        _MainTex ("Texture", 2DArray) = "white" {}
        _SingleTex("Single Texture", 2D) = "white" {}
        _Slice("Slice",Int) = 0
        _MipLevel("Mip Level",Int) = 0
        [Toggle] _EnableSingle("Toggle Single",Int) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            UNITY_DECLARE_TEX2D(_SingleTex);
            float4 _MainTex_ST;
            float4 _SingleTex_ST;
            int _Slice;
            int _MipLevel;
            int _EnableSingle;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                float3 uvw = float3(i.uv.x,i.uv.y,_Slice);
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY_LOD(_MainTex, float3(i.uv, _Slice),_MipLevel);
                if (_EnableSingle) col = UNITY_SAMPLE_TEX2D_LOD(_SingleTex, float2(i.uv), _MipLevel);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
