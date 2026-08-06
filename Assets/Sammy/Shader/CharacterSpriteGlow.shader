Shader "Custom/CharacterSpriteAura"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (0.25, 0.9, 1.0, 1)
        _OutlineStrength ("Outline Strength", Range(0, 5)) = 2.0
        _OutlineSize ("Outline Size", Range(1, 8)) = 3

        _AuraColor ("Aura Color", Color) = (0.2, 0.75, 1.0, 1)
        _AuraStrength ("Aura Strength", Range(0, 8)) = 3.0
        _AuraSize ("Aura Size", Range(1, 20)) = 10

        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpriteAura"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float4 _Color;

            float4 _OutlineColor;
            float _OutlineStrength;
            float _OutlineSize;

            float4 _AuraColor;
            float _AuraStrength;
            float _AuraSize;

            float _PulseSpeed;
            float _PulseAmount;

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
                output.color = input.color * _Color;
                return output;
            }

            float SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            float GetAlphaAround(float2 uv, float distance)
            {
                float2 texel = _MainTex_TexelSize.xy * distance;

                float a = 0;
                a += SampleAlpha(uv + float2( texel.x, 0));
                a += SampleAlpha(uv + float2(-texel.x, 0));
                a += SampleAlpha(uv + float2(0,  texel.y));
                a += SampleAlpha(uv + float2(0, -texel.y));

                a += SampleAlpha(uv + float2( texel.x,  texel.y));
                a += SampleAlpha(uv + float2(-texel.x,  texel.y));
                a += SampleAlpha(uv + float2( texel.x, -texel.y));
                a += SampleAlpha(uv + float2(-texel.x, -texel.y));

                return saturate(a / 8.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float alpha = baseCol.a;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float outlineAlpha = GetAlphaAround(input.uv, _OutlineSize);
                outlineAlpha = saturate(outlineAlpha - alpha);

                float auraNear = GetAlphaAround(input.uv, _AuraSize * 0.35);
                float auraMid  = GetAlphaAround(input.uv, _AuraSize * 0.65);
                float auraFar  = GetAlphaAround(input.uv, _AuraSize);

                float auraAlpha = saturate(
                    auraNear * 0.6 +
                    auraMid  * 0.3 +
                    auraFar  * 0.15
                );

                auraAlpha = saturate(auraAlpha - alpha);
                auraAlpha *= pulse;

                float3 auraColor = _AuraColor.rgb * auraAlpha * _AuraStrength;
                float3 outlineColor = _OutlineColor.rgb * outlineAlpha * _OutlineStrength;

                float3 finalColor = baseCol.rgb;
                finalColor = lerp(finalColor, _OutlineColor.rgb * _OutlineStrength, outlineAlpha);
                finalColor += auraColor;

                float finalAlpha = saturate(alpha + outlineAlpha + auraAlpha * 0.6);

                return half4(finalColor, finalAlpha);
            }

            ENDHLSL
        }
    }
}