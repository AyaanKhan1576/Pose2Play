Shader "Pose2Play/MuscleBodyGlow"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (0.22,0.95,0.45,1)
        _GlowStrength ("Glow Strength", Range(0,4)) = 0.6
        _Softness ("Glow Softness", Range(0.2,4)) = 2.4
        _EdgeNoise ("Edge Noise", Range(0,1)) = 0.0
    }

    // URP path
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _GlowColor;
            float _GlowStrength;
            float _Softness;
            float _EdgeNoise;
            int _ZoneCount;
            float4 _ZoneCenters[24];
            float4 _ZoneParams[24];
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.worldPos);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;

                float influence = 0.0;
                [unroll]
                for (int i = 0; i < 24; i++)
                {
                    if (i >= _ZoneCount) break;

                    float3 center = _ZoneCenters[i].xyz;
                    float radius = max(_ZoneParams[i].x, 0.001);
                    float intensity = saturate(_ZoneParams[i].y);

                    float d = distance(IN.worldPos, center);
                    float normalized = d / radius;

                    float zone = exp(-pow(normalized, 2.0) * max(_Softness, 0.001));

                    float n = frac(sin(dot(IN.worldPos.xy + IN.worldPos.yz, float2(12.9898, 78.233))) * 43758.5453);
                    float edgePerturb = lerp(1.0, n, _EdgeNoise);
                    zone *= edgePerturb;

                    float broad = exp(-pow(normalized, 2.0) * max(_Softness * 0.45, 0.001)) * 0.35;
                    influence += (zone + broad) * intensity * 0.45;
                }

                influence = saturate(influence);

                float3 glow = _GlowColor.rgb * influence * _GlowStrength;
                float3 diffuseTint = lerp(baseCol.rgb, baseCol.rgb * (1.0 + glow * 0.12), influence * 0.75);
                float3 litBase = lerp(baseCol.rgb, diffuseTint, influence * 0.85);

                return half4(litBase + glow * 0.05, 1.0);
            }
            ENDHLSL
        }
    }

    // Built-in fallback path
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _GlowColor;
        float _GlowStrength;
        float _Softness;
        float _EdgeNoise;

        int _ZoneCount;
        float4 _ZoneCenters[24];
        float4 _ZoneParams[24];

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 baseCol = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            float influence = 0.0;
            [unroll]
            for (int i = 0; i < 24; i++)
            {
                if (i >= _ZoneCount) break;

                float3 center = _ZoneCenters[i].xyz;
                float radius = max(_ZoneParams[i].x, 0.001);
                float intensity = saturate(_ZoneParams[i].y);
                float normalized = distance(IN.worldPos, center) / radius;
                float zone = exp(-pow(normalized, 2.0) * max(_Softness, 0.001));
                influence += zone * intensity * 0.45;
            }

            influence = saturate(influence);
            float3 glow = _GlowColor.rgb * influence * _GlowStrength;
            float3 litBase = lerp(baseCol.rgb, baseCol.rgb * (1.0 + glow * 0.12), influence * 0.8);

            o.Albedo = litBase;
            o.Metallic = 0.03;
            o.Smoothness = 0.45;
            o.Emission = glow * 0.05;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Standard"
}
