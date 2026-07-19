Shader "Aetherium/RoundedTerrain"
{
    // Unlit, transparent URP terrain surface — the generic cousin of Aetherium/RoundedWater
    // for solid ground (grass, sand, forest, hills, road…). Reads a signed distance-to-boundary
    // baked into UV1.x by RegionMeshBuilder: the interior is fully opaque, and the edge feathers
    // OUTWARD over _BlendWidth so each terrain bleeds softly onto its lower-priority neighbours
    // instead of ending on a hard square step. Layer order (which terrain bleeds over which) is
    // set by the renderer via a small per-priority y-lift. A faint value-noise breaks up the flat.
    Properties
    {
        _Color         ("Color",         Color) = (0.42, 0.58, 0.30, 1)
        _BlendWidth    ("Blend Width",   Float) = 0.6
        _EdgeSoftness  ("Edge Softness", Float) = 0.04
        _DetailStrength("Detail",        Range(0,0.4)) = 0.06
        _DetailScale   ("Detail Scale",  Float) = 6.0
        _BandAlpha     ("Band Alpha",    Range(0,1)) = 1.0
        _AmbientTint   ("Ambient Tint",  Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0; // grid XY (detail sampling)
                float2 uv1        : TEXCOORD1; // x = signed distance to boundary (+ inside)
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 gridUv      : TEXCOORD0;
                float  signedDist  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _BlendWidth;
                float  _EdgeSoftness;
                float  _DetailStrength;
                float  _DetailScale;
                float  _BandAlpha;
                float4 _AmbientTint;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.gridUv      = IN.uv0;
                OUT.signedDist  = IN.uv1.x;
                return OUT;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(41.317, 289.107))) * 43758.5453);
            }

            // Value noise (smooth) for a gentle ground texture so big regions aren't dead-flat.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float dist = IN.signedDist;

                // Opaque interior (dist >= 0), feathering outward to nothing over _BlendWidth so the
                // terrain fades onto its neighbour instead of stepping. Higher-priority terrain is
                // drawn on top (renderer y-lift), so this outward fade blends over the lower one.
                float alpha = smoothstep(-(_BlendWidth + _EdgeSoftness), -_EdgeSoftness, dist);
                if (alpha <= 0.003)
                    discard;

                float n = ValueNoise(IN.gridUv * _DetailScale) - 0.5;
                float3 col = _Color.rgb * (1.0 + _DetailStrength * n);
                col *= _AmbientTint.rgb;

                return half4(col, saturate(alpha) * _BandAlpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
