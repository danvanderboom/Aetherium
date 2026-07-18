// The creature-memory uncertainty circle (EntityViewRegistry ghosts): an unlit,
// alpha-blended disc computed in the fragment shader from quad UVs — no texture.
//
// _Fuzz is the dispersion: near 0 the alpha holds solid to the rim then drops
// (a crisp, clear circle — "it is right here"); at 1 the alpha is a radial
// gradient peaked at the center (a diffuse probability cloud — "it could be
// anywhere in here"). The registry drives _Fuzz from 0 to 1 over the ghost's
// life, so the memory starts sharp and disperses like a spreading waveform.
//
// Deliberately a legacy-style unlit pass with no LightMode tag: URP renders it
// via SRPDefaultUnlit, so it works under URP (and built-in) with the blend
// state declared right here — no fragile runtime surface-type mutation.
// Cull Off so the disc can never vanish to backface culling.
Shader "Aetherium/GhostCircle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.15, 0.45)
        _Fuzz ("Fuzz", Range(0.01, 1)) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Fuzz;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // r: 0 at the quad center, 1 at the inscribed circle's rim.
                float r = distance(i.uv, float2(0.5, 0.5)) * 2.0;
                float edge0 = saturate(1.0 - _Fuzz);
                float a = 1.0 - smoothstep(edge0, 1.0, r);
                return fixed4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
}
