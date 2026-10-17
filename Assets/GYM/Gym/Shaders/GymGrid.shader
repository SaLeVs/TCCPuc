// Blockout material: a world-space grid, 1 m minor lines and a bold line every 5 m, so every
// surface in the GYM doubles as a ruler. Projected on the face's dominant axis, so it stays square
// however the cube is scaled.
Shader "TCC/LevelDesign/GymGrid"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.55, 0.56, 0.6, 1)
        _LineColor ("Linha 1 m", Color) = (0.35, 0.36, 0.4, 1)
        _MajorColor ("Linha 5 m", Color) = (0.2, 0.2, 0.24, 1)
        _CellSize ("Célula (m)", Float) = 1
        _MajorEvery ("Linha forte a cada", Float) = 5
        _LineWidth ("Espessura (px)", Range(0.5, 4)) = 1.2
        _MinAmbient ("Luz ambiente mínima", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                half4 _MajorColor;
                float _CellSize;
                float _MajorEvery;
                float _LineWidth;
                half _MinAmbient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // 1 on a line, 0 between lines; width in screen pixels so it reads at any distance.
            float GridLines(float2 coord, float width)
            {
                float2 derivative = max(fwidth(coord), 1e-4);
                float2 distanceToLine = abs(frac(coord - 0.5) - 0.5) / derivative;
                return 1.0 - saturate(min(distanceToLine.x, distanceToLine.y) - width * 0.5);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 a = abs(n);
                float2 plane = a.y >= a.x && a.y >= a.z ? input.positionWS.xz
                             : a.x >= a.z ? input.positionWS.zy
                             : input.positionWS.xy;

                float minor = GridLines(plane / _CellSize, _LineWidth);
                float major = GridLines(plane / (_CellSize * _MajorEvery), _LineWidth * 1.8);

                half3 albedo = _BaseColor.rgb;
                albedo = lerp(albedo, _LineColor.rgb, minor);
                albedo = lerp(albedo, _MajorColor.rgb, major);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half lambert = saturate(dot(n, mainLight.direction)) * mainLight.shadowAttenuation;
                // A generated scene has no baked ambient probe, so faces turned away from the sun
                // would go black. Keep a floor, slightly brighter on top faces so shapes still read.
                half3 ambient = max(SampleSH(n), _MinAmbient * (0.8 + 0.2 * saturate(n.y)));
                half3 color = albedo * (ambient + mainLight.color * lambert);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
