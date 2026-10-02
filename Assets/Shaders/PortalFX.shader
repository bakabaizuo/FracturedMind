Shader "AFM/PortalFX"
{
    Properties
    {
        _LightningColor ("Lightning Color", Color) = (0.35, 0.75, 1.0, 1.0)
        _CoreColor ("Core Color", Color) = (0.9, 1.0, 1.0, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 12)) = 4
        _Opacity ("Opacity", Range(0, 1)) = 1
        _TileX ("Lightning Tile X", Range(0.1, 8)) = 1
        _TileY ("Lightning Tile Y", Range(0.1, 8)) = 1
        _UVRotation ("UV Rotation", Range(-180, 180)) = 0
        _UVRotationSpeed ("UV Rotation Speed", Range(-360, 360)) = 0
        _Width ("Bolt Width", Range(0.002, 0.2)) = 0.035
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.035
        _Jaggedness ("Jaggedness", Range(0, 0.2)) = 0.06
        _BranchStrength ("Branch Strength", Range(0, 1)) = 0.35
        _ScrollSpeed ("Scroll Speed", Range(-8, 8)) = 2
        _FlickerSpeed ("Flicker Speed", Range(0, 30)) = 12
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _LightningColor;
            fixed4 _CoreColor;
            float _GlowIntensity;
            float _Opacity;
            float _TileX;
            float _TileY;
            float _UVRotation;
            float _UVRotationSpeed;
            float _Width;
            float _Softness;
            float _Jaggedness;
            float _BranchStrength;
            float _ScrollSpeed;
            float _FlickerSpeed;
            float _FlickerAmount;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = hash21(cell);
                float b = hash21(cell + float2(1, 0));
                float c = hash21(cell + float2(0, 1));
                float d = hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float boltDistance(float2 uv, float time, out float branchMask)
            {
                float y = uv.y;
                float noiseA = valueNoise(float2(y * 13.0, time * 0.35));
                float noiseB = valueNoise(float2(y * 31.0 + 7.0, time * 0.55));
                float center = 0.5
                    + (noiseA - 0.5) * _Jaggedness
                    + (noiseB - 0.5) * _Jaggedness * 0.45;

                float distanceToCore = abs(uv.x - center);
                float branchPhase = frac(y * 4.0 + time * 0.2);
                float branchWindow = smoothstep(0.02, 0.12, branchPhase)
                    * (1.0 - smoothstep(0.55, 0.95, branchPhase));
                float branchDirection = step(0.5, hash21(float2(floor(y * 4.0), 3.7))) * 2.0 - 1.0;
                float branchCenter = center + branchDirection * branchPhase * 0.22;
                float branchDistance = abs(uv.x - branchCenter);
                branchMask = branchWindow * exp(-branchDistance / max(_Width * 2.0, 0.001));
                return distanceToCore;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rotation = radians(_UVRotation + (_Time.y * _UVRotationSpeed));
                float2 centeredUV = i.uv - 0.5;
                float sine = sin(rotation);
                float cosine = cos(rotation);
                centeredUV = float2(
                    centeredUV.x * cosine - centeredUV.y * sine,
                    centeredUV.x * sine + centeredUV.y * cosine);
                i.uv = frac(centeredUV + 0.5);
                i.uv = frac(i.uv * float2(_TileX, _TileY));

                float time = _Time.y * _ScrollSpeed;
                float branchMask;
                float distanceToCore = boltDistance(i.uv, time, branchMask);
                float core = 1.0 - smoothstep(_Width, _Width + _Softness, distanceToCore);
                float branch = branchMask * _BranchStrength;
                float verticalFade = smoothstep(0.0, 0.08, i.uv.y)
                    * (1.0 - smoothstep(0.92, 1.0, i.uv.y));
                float flicker = 1.0 - _FlickerAmount
                    + sin(_Time.y * _FlickerSpeed + i.uv.y * 18.0) * _FlickerAmount;
                float intensity = saturate((core + branch) * verticalFade * flicker)
                    * _GlowIntensity * _Opacity;
                fixed3 color = lerp(_LightningColor.rgb, _CoreColor.rgb, saturate(core * 1.5));
                return fixed4(color * intensity, intensity);
            }
            ENDCG
        }
    }

    FallBack Off
}
