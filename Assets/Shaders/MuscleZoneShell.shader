Shader "Pose2Play/MuscleZoneShell"
{
    Properties
    {
        _GlowColor ("Glow Color", Color) = (0.15,0.95,0.35,1)
        _GlowStrength ("Glow Strength", Range(0,4)) = 1.2
        _Opacity ("Opacity", Range(0,1)) = 0.45
        _ShellOffset ("Shell Offset", Range(0,0.01)) = 0.0018
        _Softness ("Softness", Range(0.2,4)) = 2.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _GlowColor;
            float _GlowStrength;
            float _Opacity;
            float _ShellOffset;
            float _Softness;
            int _ZoneCount;
            float4 _ZoneCenters[24];
            float4 _ZoneParams[24];
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 worldNormal = normalize(TransformObjectToWorldNormal(input.normalOS));
                worldPos += worldNormal * _ShellOffset;
                output.worldPos = worldPos;
                output.positionHCS = TransformWorldToHClip(worldPos);
                return output;
            }

            half4 frag(Varyings IN) : SV_Target
            {
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
                    influence += zone * intensity;
                }

                influence = saturate(influence);
                float alpha = influence * _Opacity;
                float3 col = _GlowColor.rgb * influence * _GlowStrength;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _GlowColor;
            float _GlowStrength;
            float _Opacity;
            float _ShellOffset;
            float _Softness;
            int _ZoneCount;
            float4 _ZoneCenters[24];
            float4 _ZoneParams[24];

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                worldPos += worldNormal * _ShellOffset;
                o.worldPos = worldPos;
                o.vertex = UnityWorldToClipPos(worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float influence = 0.0;

                [unroll]
                for (int idx = 0; idx < 24; idx++)
                {
                    if (idx >= _ZoneCount) break;

                    float3 center = _ZoneCenters[idx].xyz;
                    float radius = max(_ZoneParams[idx].x, 0.001);
                    float intensity = saturate(_ZoneParams[idx].y);
                    float normalized = distance(i.worldPos, center) / radius;
                    float zone = exp(-pow(normalized, 2.0) * max(_Softness, 0.001));
                    influence += zone * intensity;
                }

                influence = saturate(influence);
                float alpha = influence * _Opacity;
                float3 col = _GlowColor.rgb * influence * _GlowStrength;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
