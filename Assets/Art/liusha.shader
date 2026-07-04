Shader "Custom/SandFall"
{
    Properties
    {
        _MainTex ("Noise (RGB = 3 layers)", 2D) = "white" {}

        _Color  ("Sand Light", Color) = (0.90,0.82,0.58,1)
        _Color2 ("Sand Deep",  Color) = (0.62,0.50,0.30,1)

        _Speed        ("Fall Speed", Float) = 0.35
        _Density      ("Density", Range(0,4)) = 1.6
        _Softness     ("Top Fade", Range(0.001,1)) = 0.25
        _BottomFade   ("Bottom Fade", Range(0.001,1)) = 0.45
        _EdgeFeather  ("Edge Feather", Range(0.001,1)) = 0.5
        _FlowStrength ("Flow Distortion", Range(0,0.5)) = 0.06
        _Wind         ("Wind Sway", Range(0,0.2)) = 0.02
        _Contrast     ("Softness / Contrast", Range(0.2,3)) = 1.0
        _Alpha        ("Overall Alpha", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;
            fixed4 _Color2;
            float _Speed;
            float _Density;
            float _Softness;
            float _BottomFade;
            float _EdgeFeather;
            float _FlowStrength;
            float _Wind;
            float _Contrast;
            float _Alpha;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;

                // wind: slow horizontal sway
                float wind = sin(_Time.y * 0.6) * _Wind;

                float2 uv = i.uv;

                // 4 scrolling noise layers (different scale / speed)
                float l1 = tex2D(_MainTex, uv * 1.0 + float2( wind,       -t * 1.00)).r;
                float l2 = tex2D(_MainTex, uv * 2.0 + float2(-wind * 0.7, -t * 1.70)).g;
                float l3 = tex2D(_MainTex, uv * 4.0 + float2( wind * 0.5, -t * 2.60)).b;

                // flow map: horizontal distortion from l2
                uv.x += (l2 - 0.5) * _FlowStrength;
                float l4 = tex2D(_MainTex, uv * 3.0 + float2(0.0, -t * 2.20)).r;

                // weighted fbm
                float n = (l1 * 0.50 + l2 * 0.28 + l3 * 0.14 + l4 * 0.24) / 1.16;

                // density + soft mapping (wide smoothstep = foggy)
                float sand = saturate(n * _Density);
                sand = smoothstep(0.5 - 0.5 * _Contrast, 0.5 + 0.5 * _Contrast, sand);

                // height fade: top fade in, bottom dissipate
                float fadeTop    = smoothstep(1.0, 1.0 - _Softness, i.uv.y);
                float fadeBottom = smoothstep(0.0, _BottomFade,     i.uv.y);
                float height = fadeTop * fadeBottom;

                // left / right edge feather
                float edge = smoothstep(0.0, _EdgeFeather, 1.0 - abs(i.uv.x - 0.5) * 2.0);

                float alpha = sand * height * edge * _Alpha;

                // color depth by density
                fixed3 col = lerp(_Color2.rgb, _Color.rgb, sand);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
