Shader "Custom/URP/MatteHDR"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Wrap ("Wrap Strength", Range(0,0.9)) = 0.25
        _Ambient ("Ambient", Range(0,1)) = 0.2
        _Emission ("Emission Color", Color) = (0,0,0,0)
        _EmissionBoost ("Emission Boost", Range(0,10)) = 1
        _HDRMultiplier ("HDR Multiplier", Range(0,4)) = 1
        _FacetSteps ("Faceting Steps (1 = off)", Range(1,16)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            float4 _BaseColor;
            float _Wrap;
            float _Ambient;
            float4 _Emission;
            float _EmissionBoost;
            float _HDRMultiplier;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.normalWS = normalWS;
                OUT.tangentWS = tangentWS;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetMainCameraWorldPos() - worldPos;
                UNITY_TRANSFER_FOG(OUT, OUT.positionCS);
                return OUT;
            }

            // Simple wrap diffuse: reduces contrast of highlights and removes specular
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                // normal from normal map if present
                float3 normalWS = normalize(IN.normalWS);
                float3 nmap = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv).xyz * 2 - 1;
                // if normal map is provided (non-flat), use it in tangent space
                if (length(nmap) > 0.01)
                {
                    float3 t = normalize(IN.tangentWS);
                    float3 b = normalize(cross(IN.normalWS, t));
                    float3x3 TBN = float3x3(t, b, IN.normalWS);
                    normalWS = normalize(mul(nmap, TBN));
                }

                // Optional faceting: quantize the normal to reduce interpolation/aliasing
                if (_FacetSteps > 1.0)
                {
                    float3 q = round(normalWS * _FacetSteps) / _FacetSteps;
                    if (length(q) > 0.001)
                        normalWS = normalize(q);
                }

                // Use main directional light approximate from SH or main directional
                float3 lightDirWS = normalize(GetMainLightDirection());

                float nl = saturate(dot(normalWS, lightDirWS));
                // wrap diffuse: bring in some light to surfaces facing away
                float wrapped = saturate(nl + _Wrap);
                // scale to 0..1 range
                float diffuse = saturate(wrapped / (1 + _Wrap));

                float3 color = baseCol.rgb * (_Ambient + diffuse * (1 - _Ambient));

                // Emission drives HDR/bloom - multiply by boost
                float3 emission = _Emission.rgb * _EmissionBoost;

                float3 outCol = (color + emission) * _HDRMultiplier;

                UNITY_APPLY_FOG(IN, outCol);
                return half4(outCol, baseCol.a);
            }
            ENDHLSL
        }
    }

    FallBack "Diffuse"
}
