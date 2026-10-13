Shader "Custom/URP/Glass Configurable V2"
{
    Properties
    {
        [Header(Base)]
        _Tint ("Tint", Color) = (0.82, 0.92, 0.96, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.15

        [Header(Reflection)]
        [Toggle(_REFLECTION_ON)] _ReflectionOn ("Enable Reflection", Float) = 1
        _ReflectionStrength ("Reflection Strength", Range(0, 2)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95

        [Header(Distorted)]
        [Toggle(_DISTORTION_ON)] _DistortionOn ("Enable Distortion", Float) = 0
        [NoScaleOffset] _DistortionMap ("Distortion Normal Map", 2D) = "bump" {}
        _DistortionTiling ("Normal Tiling", Float) = 1
        _DistortionStrength ("Distortion Strength", Range(0, 0.15)) = 0.03

        [Header(Refraction)]
        [Toggle(_REFRACTION_ON)] _RefractionOn ("Enable Refraction (IOR)", Float) = 0
        _IOR ("IOR", Range(1, 2.5)) = 1.5
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.2

        [Header(Frosted)]
        [Toggle(_FROST_ON)] _FrostOn ("Enable Frosted", Float) = 0
        _Frost ("Frost Amount", Range(0, 1)) = 0.5
        _FrostRadius ("Frost Max Blur Radius", Range(0, 0.05)) = 0.015
        _FrostWhiten ("Frost Whiten", Range(0, 0.5)) = 0.08

        [Header(Cracks)]
        [Toggle(_CRACKS_ON)] _CracksOn ("Enable Cracks", Float) = 0
        _CrackMap ("Crack Mask (white = crack)", 2D) = "black" {}
        _CrackAmount ("Crack Amount", Range(0, 1)) = 1
        _CrackDistortion ("Crack Distortion", Range(0, 0.2)) = 0.04
        _CrackBrightness ("Crack Brightness", Range(0, 3)) = 1

        [Header(Debug)]
        [KeywordEnum(Off, SceneColor, Reflection, Fresnel)] _Debug ("Debug View", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GlassForward"
            Tags { "LightMode" = "UniversalForward" }

            // O vidro compõe o fundo sozinho (via Opaque Texture) e sai opaco.
            Blend Off
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _REFLECTION_ON
            #pragma shader_feature_local_fragment _DISTORTION_ON
            #pragma shader_feature_local_fragment _REFRACTION_ON
            #pragma shader_feature_local_fragment _FROST_ON
            #pragma shader_feature_local_fragment _CRACKS_ON
            #pragma shader_feature_local_fragment _DEBUG_OFF _DEBUG_SCENECOLOR _DEBUG_REFLECTION _DEBUG_FRESNEL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_DistortionMap);
            SAMPLER(sampler_DistortionMap);
            TEXTURE2D(_CrackMap);
            SAMPLER(sampler_CrackMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _TintStrength;
                half _ReflectionStrength;
                half _FresnelPower;
                half _Smoothness;
                half _DistortionTiling;
                half _DistortionStrength;
                half _IOR;
                half _RefractionStrength;
                half _Frost;
                half _FrostRadius;
                half _FrostWhiten;
                float4 _CrackMap_ST;
                float4 _CrackMap_TexelSize;
                half _CrackAmount;
                half _CrackDistortion;
                half _CrackBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                half3  normalWS    : TEXCOORD2;
                half4  tangentWS   : TEXCOORD3; // w = sinal do bitangent
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.tangentWS = half4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv = IN.uv;
                return OUT;
            }

            // Ruído barato por pixel, usado pra girar as amostras do blur
            float InterleavedNoise(float2 p)
            {
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                half3 normalWS = normalize(IN.normalWS);
                half3 viewDirWS = (half3)GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // ---------- DISTORTED (normal map) ----------
                #if defined(_DISTORTION_ON)
                    half3 nTS = UnpackNormal(SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, IN.uv * _DistortionTiling));
                    screenUV += nTS.xy * _DistortionStrength;

                    half3 bitangentWS = cross(normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
                    normalWS = normalize(IN.tangentWS.xyz * nTS.x + bitangentWS * nTS.y + normalWS * nTS.z);
                #endif

                // ---------- CRACKS ----------
                half crack = 0;
                #if defined(_CRACKS_ON)
                    float2 crackUV = TRANSFORM_TEX(IN.uv, _CrackMap);
                    float2 texel = _CrackMap_TexelSize.xy * 2.0;
                    half c0 = SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, crackUV).r;
                    half cx = SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, crackUV + float2(texel.x, 0)).r;
                    half cy = SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, crackUV + float2(0, texel.y)).r;

                    // Crack Amount revela as rachaduras das mais fortes (centro) pras mais fracas
                    crack = saturate((c0 - (1.0h - _CrackAmount)) * 8.0h);

                    // Nas bordas das linhas o fundo é deslocado, como cacos desalinhados
                    screenUV += half2(cx - c0, cy - c0) * _CrackDistortion * crack;
                #endif

                // ---------- REFRACTION (IOR) ----------
                #if defined(_REFRACTION_ON)
                    half3 incident = -viewDirWS;
                    half3 refracted = refract(incident, normalWS, rcp(max(_IOR, 1.0h)));
                    half3 incidentVS = TransformWorldToViewDir(incident);
                    half3 refractedVS = TransformWorldToViewDir(refracted);
                    screenUV += (refractedVS.xy - incidentVS.xy) * _RefractionStrength;
                #endif

                // Evita ler fora da tela
                screenUV = saturate(screenUV);

                // ---------- FUNDO (Opaque Texture) / FROSTED ----------
                half3 bg;
                #if defined(_FROST_ON)
                    float radius = _FrostRadius * _Frost;
                    float2 aspect = float2(_ScreenParams.y / _ScreenParams.x, 1.0); // blur redondo
                    float angle = InterleavedNoise(IN.positionHCS.xy) * 6.2831853;
                    float s, c;
                    sincos(angle, s, c);
                    float2x2 rot = float2x2(c, -s, s, c);

                    // 5 amostras (centro + 4). Pra mais qualidade, adicione mais taps.
                    const float2 taps[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };

                    bg = SampleSceneColor(screenUV);
                    [unroll] for (int i = 0; i < 4; i++)
                    {
                        float2 o = mul(rot, taps[i]) * radius * aspect;
                        bg += SampleSceneColor(saturate(screenUV + o));
                    }
                    bg *= 0.2h;
                    bg = lerp(bg, half3(1, 1, 1), _FrostWhiten * _Frost);
                #else
                    bg = SampleSceneColor(screenUV);
                #endif

                #if defined(_DEBUG_SCENECOLOR)
                    return half4(bg, 1);
                #endif

                // ---------- TINT ----------
                half3 col = bg * lerp(half3(1, 1, 1), _Tint.rgb, _TintStrength);

                #if defined(_CRACKS_ON)
                    col += crack * _CrackBrightness * half3(0.35, 0.40, 0.45);
                #endif

                half3 env = 0;
                half fresnel = 0;

                // ---------- REFLECTION ----------
                #if defined(_REFLECTION_ON)
                    half3 reflVec = reflect(-viewDirWS, normalWS);

                    // Frost deixa o reflexo mais difuso
                    half roughness = 1.0h - _Smoothness;
                    #if defined(_FROST_ON)
                        roughness = lerp(roughness, 1.0h, _Frost);
                    #endif

                    // mesmo cálculo da Unity (6 níveis de mip no cubemap de reflexo)
                    half mip = roughness * (1.7h - 0.7h * roughness) * 6.0h;
                    half4 encoded = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflVec, mip);
                    half hdrAlpha = unity_SpecCube0_HDR.w * (encoded.a - 1.0h) + 1.0h;
                    env = unity_SpecCube0_HDR.x * pow(max(hdrAlpha, 0.0001h), unity_SpecCube0_HDR.y) * encoded.rgb;
                    env = min(env, 3.0h); // evita estourar em branco com céu HDR

                    fresnel = pow(1.0h - saturate(abs(dot(normalWS, viewDirWS))), _FresnelPower);
                    col += env * fresnel * _ReflectionStrength;
                #endif

                #if defined(_DEBUG_REFLECTION)
                    return half4(env, 1);
                #elif defined(_DEBUG_FRESNEL)
                    return half4(fresnel.xxx, 1);
                #endif

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
