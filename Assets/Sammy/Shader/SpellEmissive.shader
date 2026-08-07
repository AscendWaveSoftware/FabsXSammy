Shader "Sammy/SpellEmissive"
{
    // Sprite shader whose tint may exceed 1. That is the whole point: a plain
    // SpriteRenderer stores its colour as a Color32 vertex attribute, so it can
    // never leave the 0..1 range and can never cross the bloom threshold. The
    // tint lives in a shader property instead, where full float precision
    // survives and a MaterialPropertyBlock can push it well above white.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Colour", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpellEmissive"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float4 _EmissionColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 tinted = texel * input.color * _EmissionColor;

                // Only the colour is allowed past 1. Alpha stays in range, so the
                // blend keeps behaving and the pixel art keeps its hard edges.
                return half4(tinted.rgb, saturate(tinted.a));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
