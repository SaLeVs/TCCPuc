Shader "Hidden/VHSEffectURP"
{
    Properties
    {
        _NoiseIntensity ("Noise Intensity", Range(0,1)) = 0.03
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.08
        _ScanlineCount ("Scanline Count", Float) = 800
        _ChromaticAberration ("Chromatic Aberration", Range(0,0.02)) = 0.0015
        _VignetteIntensity ("Vignette Intensity", Range(0,1)) = 0.25
        _ColorTint ("Color Tint", Color) = (1,0.95,0.9,1)
        _TrackingWobble ("Tracking Wobble", Range(0,0.05)) = 0.002
        _GlitchIntensity ("Glitch Intensity", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "VHSEffectPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _NoiseIntensity;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _ChromaticAberration;
            float _VignetteIntensity;
            float4 _ColorTint;
            float _TrackingWobble;
            float _GlitchIntensity;

            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // Tracking wobble
                float wobbleStrength = _TrackingWobble + _GlitchIntensity * 0.03;
                float wobble = sin(uv.y * 40.0 + _Time.y * 5.0) * wobbleStrength;
                wobble += (rand(float2(_Time.y, floor(uv.y * 20.0))) - 0.5) * _GlitchIntensity * 0.05;
                uv.x += wobble;

                // Chromatic aberration
                float caAmount = _ChromaticAberration + _GlitchIntensity * 0.01;
                float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(caAmount, 0)).r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(caAmount, 0)).b;
                half4 col = half4(r, g, b, 1);

                // Scanlines
                float scanline = sin(uv.y * _ScanlineCount) * 0.5 + 0.5;
                col.rgb -= scanline * _ScanlineIntensity;

                // Grain
                float noise = (rand(uv * _Time.y) - 0.5) * (_NoiseIntensity + _GlitchIntensity * 0.4);
                col.rgb += noise;

                // Tearing durante o glitch
                if (_GlitchIntensity > 0.01)
                {
                    float block = floor(uv.y * 20.0);
                    float tear = rand(float2(block, floor(_Time.y * 15.0)));
                    if (tear > 1.0 - _GlitchIntensity * 0.3)
                    {
                        float2 tornUV = uv;
                        tornUV.x = frac(uv.x + (tear - 0.5) * 0.2);
                        col.rgb = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, tornUV).rgb;
                    }
                }

                // Vinheta
                float2 centered = uv - 0.5;
                float vig = 1.0 - dot(centered, centered) * _VignetteIntensity * 2.0;
                col.rgb *= vig;

                // Tint
                col.rgb = lerp(col.rgb, col.rgb * _ColorTint.rgb, 0.3);

                return col;
            }
            ENDHLSL
        }
    }
}
