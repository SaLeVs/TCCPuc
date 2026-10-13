Shader "TCC/PlacementGhost"
{
    Properties
    {
        [Tooltip(Tint only. Opacity is shaped by the hologram itself.)]
        _Color ("Color", Color) = (0.25, 1, 0.4, 1)

        [Header(Hologram)]
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _FillAlpha ("Fill Opacity", Range(0, 1)) = 0.06
        _ScanDensity ("Scanlines Per Meter", Float) = 70
        _ScanSpeed ("Scanline Speed", Float) = 2
        _ScanAlpha ("Scanline Opacity", Range(0, 1)) = 0.1

        [Header(Footprint)]
        _LineWidth ("Line Width (m)", Float) = 0.022
        _DashLength ("Dash Length (m)", Float) = 0.07
        _DashGap ("Dash Gap (m)", Float) = 0.05
        _CornerLength ("Corner Length (m)", Float) = 0.09
        _MarchSpeed ("March Speed (m/s)", Float) = 0.05
        _FootprintWhiteness ("Whiteness", Range(0, 1)) = 0.55

        // Set per renderer by PlacementGhost.
        [HideInInspector] _Footprint ("Footprint", Float) = 0
        [HideInInspector] _FootprintSize ("Footprint Size", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _RimPower;
            half _FillAlpha;
            float _ScanDensity;
            float _ScanSpeed;
            half _ScanAlpha;
            float _LineWidth;
            float _DashLength;
            float _DashGap;
            float _CornerLength;
            float _MarchSpeed;
            half _FootprintWhiteness;
            float _Footprint;
            float4 _FootprintSize;
        CBUFFER_END
        ENDHLSL

        // Depth first, so only the outer surface gets tinted: without it every face behind the
        // front ones shows through and a box reads as a tangle of edges.
        Pass
        {
            Name "GhostDepth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                // The footprint lies flat on the floor and must not hide anything: collapse it.
                if (_Footprint > 0.5) return 0;

                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "GhostColor"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);

                Varyings output;
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(position.positionWS);
                output.uv = input.uv;
                return output;
            }

            /// Almost empty inside, a bright silhouette, scanlines climbing up.
            half4 Hologram(Varyings input)
            {
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0h - facing, _RimPower);
                half scan = step(0.55, frac(input.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed));

                half alpha = saturate(_FillAlpha + scan * _ScanAlpha + rim * 0.95h);
                half3 color = _Color.rgb * (0.8h + rim * 1.6h) + scan * 0.08h;

                return half4(color, alpha);
            }

            /// A dashed rectangle around the item's base, solid at the corners, the dashes marching
            /// round it. The UVs are in meters, so dashes keep their size on any footprint.
            half4 Footprint(Varyings input)
            {
                float2 uv = input.uv;
                float2 size = _FootprintSize.xy;

                float bottom = uv.y;
                float right = size.x - uv.x;
                float top = size.y - uv.y;
                float left = uv.x;
                float toEdge = min(min(bottom, top), min(left, right));

                float lineAa = fwidth(toEdge);
                float onLine = 1 - smoothstep(_LineWidth - lineAa, _LineWidth + lineAa, toEdge);

                // Distance along the perimeter, counter-clockwise, so the dashes run all the way round.
                float along;
                if (toEdge == bottom) along = uv.x;
                else if (toEdge == right) along = size.x + uv.y;
                else if (toEdge == top) along = size.x + size.y + (size.x - uv.x);
                else along = 2 * size.x + size.y + (size.y - uv.y);

                float period = _DashLength + _DashGap;
                float dashPos = frac((along - _Time.y * _MarchSpeed) / period) * period;
                float dashAa = max(fwidth(along), 1e-4);
                float dash = 1 - smoothstep(_DashLength - dashAa, _DashLength + dashAa, dashPos);

                float2 toCorner = min(uv, size - uv);
                float corner = step(toCorner.x, _CornerLength) * step(toCorner.y, _CornerLength);

                float visible = onLine * max(dash, corner);
                clip(visible - 0.01);

                half3 color = lerp(_Color.rgb, half3(1, 1, 1), _FootprintWhiteness);
                return half4(color, visible * 0.9h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // A branch, not ?: - HLSL evaluates both sides of ?:, and the footprint's clip would
                // cut holes in the hologram.
                UNITY_BRANCH
                if (_Footprint > 0.5) return Footprint(input);

                return Hologram(input);
            }
            ENDHLSL
        }
    }
}
