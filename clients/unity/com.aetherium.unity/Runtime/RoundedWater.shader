Shader "Aetherium/RoundedWater"
{
    // Unlit, transparent URP water. Reads a signed distance-to-coastline baked into
    // UV1.x by RegionMeshBuilder: it carves a smooth curved edge (discard outside the
    // coast), fades shallows->deep, and draws an animated foam band hugging the shore.
    // _BandAlpha carries an optional depth falloff; _AmbientTint the frame's mood.
    Properties
    {
        _DeepColor    ("Deep Water",     Color) = (0.10, 0.34, 0.52, 1)
        _ShallowColor ("Shallow Water",  Color) = (0.28, 0.62, 0.75, 1)
        _FoamColor    ("Foam",           Color) = (0.94, 0.98, 1.00, 1)
        _ShoreWidth   ("Shallows Width", Float) = 2.0
        _FoamWidth    ("Foam Width",     Float) = 0.35
        _FoamSpeed    ("Foam Speed",     Float) = 1.5
        _WaveScale    ("Wave Scale",     Float) = 5.0
        _WaveSpeed    ("Wave Speed",     Float) = 0.8
        _WaveStrength ("Wave Strength",  Range(0,1)) = 0.10
        _EdgeSoftness ("Edge Softness",  Float) = 0.06
        _BlendWidth   ("Shore Blend",    Float) = 0.6
        _WaterAlpha   ("Water Alpha",    Range(0,1)) = 0.90
        _BandAlpha    ("Band Alpha",     Range(0,1)) = 1.0
        _AmbientTint  ("Ambient Tint",   Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
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
                float2 uv0        : TEXCOORD0; // grid XY (wave sampling)
                float2 uv1        : TEXCOORD1; // x = signed distance to coastline (+ inside)
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 worldUv     : TEXCOORD0;
                float  signedDist  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _FoamColor;
                float  _ShoreWidth;
                float  _FoamWidth;
                float  _FoamSpeed;
                float  _WaveScale;
                float  _WaveSpeed;
                float  _WaveStrength;
                float  _EdgeSoftness;
                float  _BlendWidth;
                float  _WaterAlpha;
                float  _BandAlpha;
                float4 _AmbientTint;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldUv     = IN.uv0;
                OUT.signedDist  = IN.uv1.x;
                return OUT;
            }

            // Cheap sum-of-sines ripple field.
            float Waves(float2 p, float t)
            {
                return sin(p.x * _WaveScale + t) * 0.5
                     + sin((p.x + p.y) * _WaveScale * 0.6 - t * 1.3) * 0.3
                     + sin(p.y * _WaveScale * 0.8 + t * 0.7) * 0.2;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // Perturb the coast distance with the wave field so foam + edge shimmer.
                float wave = Waves(IN.worldUv, t * _WaveSpeed);
                float d = IN.signedDist + wave * _WaveStrength;

                // Smooth curved coastline: fade the water outward over _BlendWidth so it melts
                // onto the beach (soft, tunable) instead of ending on a crisp line. Inside the
                // coast (d >= _EdgeSoftness) it's fully present; the fade lives in the shore band.
                float edge = smoothstep(-(_BlendWidth + _EdgeSoftness), _EdgeSoftness, d);
                if (edge <= 0.001)
                    discard;

                // Shallow -> deep gradient by distance from shore.
                float shallowT = saturate(d / max(_ShoreWidth, 1e-3));
                float3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, shallowT);

                // Animated foam band hugging the shore.
                float foamEdge = _FoamWidth *
                    (0.75 + 0.25 * sin(IN.worldUv.x * 3.0 + IN.worldUv.y * 3.0 + t * _FoamSpeed));
                float foam = saturate(1.0 - smoothstep(0.0, max(foamEdge, 1e-3), d));

                float3 col = lerp(water, _FoamColor.rgb, foam);
                col += saturate(wave) * 0.05 * (1.0 - foam); // faint open-water sparkle
                col *= _AmbientTint.rgb;                     // per-frame atmosphere

                float alpha = saturate(_WaterAlpha * _BandAlpha * edge + foam * 0.15 * _BandAlpha);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
