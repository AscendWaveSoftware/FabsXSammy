Shader "Skybox/Dual Panoramic (URP Ready)" {
    Properties{
        _Tint1("Tint Color 1", Color) = (.5, .5, .5, .5)
        _Tint2("Tint Color 2", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure1("Exposure 1", Range(0, 8)) = 1.0
        [Gamma] _Exposure2("Exposure 2", Range(0, 8)) = 1.0
        _Rotation1("Rotation1", Range(0, 360)) = 0
        _Rotation2("Rotation2", Range(0, 360)) = 0
        [NoScaleOffset] _Texture1("Texture 1", 2D) = "grey" {}
        [NoScaleOffset] _Texture2("Texture 2", 2D) = "grey" {}
        [Enum(360 Degrees, 0, 180 Degrees, 1)] _ImageType("Image Type", Float) = 0
        [Toggle] _MirrorOnBack("Mirror on Back", Float) = 0
        [Enum(None, 0, Side by Side, 1, Over Under, 2)] _Layout("3D Layout", Float) = 0
        _Blend("Blend", Range(0.0, 1.0)) = 0.0
    }

    SubShader{
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local __ _MAPPING_6_FRAMES_LAYOUT

            #include "UnityCG.cginc"

            sampler2D _Texture1;
            sampler2D _Texture2;
            float4 _Texture1_TexelSize;

            CBUFFER_START(UnityPerMaterial)
            half4 _Texture1_HDR;
            half4 _Texture2_HDR;
            half4 _Tint1;
            half4 _Tint2;
            half _Exposure1;
            half _Exposure2;
            float _Rotation1;
            float _Rotation2;
            float _Blend;
            float _MirrorOnBack;
            int _ImageType;
            int _Layout;
            CBUFFER_END

            inline float2 ToRadialCoords(float3 coords)
            {
                float3 n = normalize(coords);
                float latitude = acos(n.y);
                float longitude = atan2(n.z, n.x);
                float2 uv = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - uv;
            }

            float3 RotateAroundYInDegrees(float3 v, float deg)
            {
                float a = deg * UNITY_PI / 180.0;
                float s, c;
                sincos(a, s, c);
                float2x2 m = float2x2(c, -s, s, c);
                return float3(mul(m, v.xz), v.y).xzy;
            }

            struct appdata {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                float2 image180ScaleAndCutoff : TEXCOORD1;
                float4 layout3DScaleAndOffset : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rotated = RotateAroundYInDegrees(v.vertex.xyz, _Rotation1);
                o.vertex = UnityObjectToClipPos(float4(rotated,1));
                o.texcoord = v.vertex.xyz;

                // 360 vs 180
                if (_ImageType == 0) o.image180ScaleAndCutoff = float2(1.0, 1.0);
                else                 o.image180ScaleAndCutoff = float2(2.0, (_MirrorOnBack > 0.5) ? 1.0 : 0.5);

                // 3D layout
                if (_Layout == 0)      o.layout3DScaleAndOffset = float4(0,0,1,1);
                else if (_Layout == 1) o.layout3DScaleAndOffset = float4(unity_StereoEyeIndex,0,0.5,1);
                else                   o.layout3DScaleAndOffset = float4(0, 1 - unity_StereoEyeIndex,1,0.5);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 tc = ToRadialCoords(i.texcoord);
                if (tc.x > i.image180ScaleAndCutoff.y) return half4(0,0,0,1);
                tc.x = fmod(tc.x * i.image180ScaleAndCutoff.x, 1);
                tc = (tc + i.layout3DScaleAndOffset.xy) * i.layout3DScaleAndOffset.zw;

                half4 t1 = tex2D(_Texture1, tc);
                tc.x = frac(tc.x + (_Rotation2 - _Rotation1) / 360.0);
                half4 t2 = tex2D(_Texture2, tc);

                half3 c1 = DecodeHDR(t1, _Texture1_HDR);
                half3 c2 = DecodeHDR(t2, _Texture2_HDR);

                c1 *= _Tint1.rgb * unity_ColorSpaceDouble.rgb * _Exposure1;
                c2 *= _Tint2.rgb * unity_ColorSpaceDouble.rgb * _Exposure2;
                half3 col = lerp(c1, c2, saturate(_Blend));

                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
