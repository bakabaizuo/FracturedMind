Shader "Retro/PSX_VertexSnap_Glass_Refraction_URP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,0.5)
        _Smoothness ("Smoothness", Range(0,1)) = 0.8
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _RefractionStrength ("Refraction Strength", Range(0,0.1)) = 0.03
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(1,8)) = 3

        // PSX Snapping
        _PSXSnapNear ("Snap Near", Range(0.0001, 0.1)) = 0.01
        _PSXSnapFar ("Snap Far", Range(0.0001, 0.5)) = 0.05
        _PSXNearPlane ("Near Plane", Float) = 0.1
        _PSXFarPlane ("Far Plane", Float) = 100.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            sampler2D _MainTex;
            float4 _Color;
            float _Smoothness;
            float _Metallic;
            float _RefractionStrength;
            float4 _RimColor;
            float _RimPower;

            float _PSXSnapNear;
            float _PSXSnapFar;
            float _PSXNearPlane;
            float _PSXFarPlane;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                // fog coords removed – handled by URP automatic fog or omitted
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos,1)).xyz;

                // Safe clamp for planes
                float nearPlane = max(_PSXNearPlane, 0.001);
                float farPlane = max(_PSXFarPlane, nearPlane + 0.001);

                float dist = length(viewPos);
                float t = saturate((dist - nearPlane) / (farPlane - nearPlane));

                float snapStep = lerp(_PSXSnapNear, _PSXSnapFar, t);
                snapStep = max(snapStep, 0.0001);

                viewPos = floor(viewPos / snapStep) * snapStep;
                float3 viewDir = normalize(viewPos);
                viewPos += viewDir * _RefractionStrength * dist;

                float4 offsetWorld = mul(UNITY_MATRIX_I_V, float4(viewPos,1));
                float3 displacedOS = mul(unity_WorldToObject, offsetWorld).xyz;
                v.positionOS.xyz = displacedOS;

                o.worldPos = TransformObjectToWorld(v.positionOS.xyz);
                o.worldNormal = TransformObjectToWorldDir(v.normalOS);
                o.uv = v.uv;

                o.positionCS = TransformWorldToHClip(o.worldPos);
                o.viewDirWS = normalize(_WorldSpaceCameraPos - o.worldPos);

                // UNITY_TRANSFER_FOG(o, o.positionCS); // fog not used here
                return o;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                // basic textured output plus rim emission; no GI/lighting to avoid URP-specific types
                half4 col = tex2D(_MainTex, IN.uv) * _Color;

                float rim = 1.0 - saturate(dot(normalize(IN.viewDirWS), normalize(IN.worldNormal)));
                float3 emission = _RimColor.rgb * pow(rim, _RimPower);

                half3 rgb = col.rgb + emission;
                half alpha = col.a;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}