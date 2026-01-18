Shader "Hidden/URP/MatteTonePost"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _Strength("Strength", Range(0,1)) = 1
        _Threshold("Highlight Threshold", Range(0,1)) = 0.7
        _Compression("Compression (lower = more crush)", Range(0.2,1)) = 0.6
        _Desaturate("Desaturate Highlights", Range(0,1)) = 0.6
        _FacetSteps("Posterize Steps (1 = off)", Range(1,16)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "MATTE_TONE"
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Strength;
            float _Threshold;
            float _Compression;
            float _Desaturate;
            float _FacetSteps;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float luminance(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float4 src = tex2D(_MainTex, uv);
                float3 col = src.rgb;

                float lum = luminance(col);

                // highlight mask (smooth)
                float mask = smoothstep(_Threshold, 1.0, lum);

                // compress highlights via power curve
                float compressedLum = pow(lum, _Compression);
                float scale = compressedLum / max(1e-5, lum);
                float3 crushed = col * scale;

                // desaturate highlights (blend towards gray)
                float3 gray = lum.xxx;
                crushed = lerp(crushed, gray, _Desaturate * mask);

                // compose: only affect highlights by mask and overall strength
                float3 outCol = lerp(col, crushed, _Strength * mask);

                // optional posterize / faceting on color to make edges 'squarer'
                if (_FacetSteps > 1.0)
                {
                    outCol = floor(outCol * _FacetSteps) / _FacetSteps;
                }

                return float4(outCol, src.a);
            }
            ENDHLSL
        }
    }
}
