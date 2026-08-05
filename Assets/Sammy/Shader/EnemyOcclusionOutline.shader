Shader "Sammy/Enemy Occlusion Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.16, 0.05, 1)
        _OutlineWidth ("Outline Width", Range(0.002, 0.05)) = 0.018
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _UVExpansion ("UV Expansion", Vector) = (0.02, 0.02, 0, 0)
        [HideInInspector] _BoundsCenter ("Bounds Center", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "EnemyOcclusionOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _SpriteUVRect;
                float4 _UVExpansion;
                float4 _BoundsCenter;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float2 fromCenter = positionOS.xy - _BoundsCenter.xy;
                positionOS.xy += sign(fromCenter) * _OutlineWidth;

                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                return output;
            }

            half SampleSpriteAlpha(float2 uv)
            {
                float inside = step(_SpriteUVRect.x, uv.x) *
                               step(_SpriteUVRect.y, uv.y) *
                               step(uv.x, _SpriteUVRect.z) *
                               step(uv.y, _SpriteUVRect.w);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * inside;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvCenter = (_SpriteUVRect.xy + _SpriteUVRect.zw) * 0.5;
                float2 expandedUv = uvCenter +
                    (input.uv - uvCenter) * (1.0 + 2.0 * _UVExpansion.xy);
                float2 uvOffset = (_SpriteUVRect.zw - _SpriteUVRect.xy) * _UVExpansion.xy;

                half centerAlpha = SampleSpriteAlpha(expandedUv);
                half neighbourAlpha = 0.0h;
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2( uvOffset.x, 0.0)));
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2(-uvOffset.x, 0.0)));
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2(0.0,  uvOffset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2(0.0, -uvOffset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2( uvOffset.x,  uvOffset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2(-uvOffset.x,  uvOffset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2( uvOffset.x, -uvOffset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleSpriteAlpha(expandedUv + float2(-uvOffset.x, -uvOffset.y)));

                half outlineAlpha = saturate(neighbourAlpha - centerAlpha) * _OutlineColor.a;
                clip(outlineAlpha - 0.001h);
                return half4(_OutlineColor.rgb, outlineAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
