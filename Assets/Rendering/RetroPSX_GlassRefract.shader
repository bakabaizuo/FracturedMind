Shader "Retro/PSX_VertexSnap_Glass_Refraction_Safe"
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

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Smoothness;
        float _Metallic;
        float _RefractionStrength;
        fixed4 _RimColor;
        float _RimPower;

        float _PSXSnapNear;
        float _PSXSnapFar;
        float _PSXNearPlane;
        float _PSXFarPlane;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
        };

        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;

            // Safe clamp for planes
            float nearPlane = max(_PSXNearPlane, 0.001);
            float farPlane = max(_PSXFarPlane, nearPlane + 0.001);

            float dist = length(viewPos);
            float t = saturate((dist - nearPlane) / (farPlane - nearPlane));

            // Safe snap step
            float snapStep = lerp(_PSXSnapNear, _PSXSnapFar, t);
            snapStep = max(snapStep, 0.0001);

            // Snap in view space (PSX-style)
            viewPos = floor(viewPos / snapStep) * snapStep;

            // Retro-style fake refraction (offset by view direction)
            float3 viewDir = normalize(viewPos);
            viewPos += viewDir * _RefractionStrength * dist;

            // Transform back to object space
            v.vertex = mul(unity_WorldToObject, mul(UNITY_MATRIX_I_V, float4(viewPos, 1.0)));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = c.a;

            // Rim light highlight for edges
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower);
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
