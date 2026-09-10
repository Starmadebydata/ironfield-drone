Shader "Ironfield/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure", Float) = 1.0
        _Contrast ("Contrast", Float) = 1.06
        _Saturation ("Saturation", Float) = 1.04
        _Vignette ("Vignette", Float) = 0.30
        _LiftShadow ("Shadow tint", Color) = (0.03, 0.04, 0.06, 0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure, _Contrast, _Saturation, _Vignette;
            float4 _LiftShadow;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;

                col *= _Exposure;
                col += _LiftShadow.rgb * saturate(1.0 - dot(col, float3(0.6, 0.6, 0.6)));
                // gentle highlight soft-clip only (keeps mids punchy, no milky roll-off)
                col = col / (1.0 + max(col - 0.8, 0.0) * 0.6);

                // contrast around mid grey
                col = (col - 0.5) * _Contrast + 0.5;

                // saturation
                float l = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(float3(l, l, l), col, _Saturation);

                // vignette
                float2 d = i.uv - 0.5;
                float v = 1.0 - dot(d, d) * _Vignette * 2.4;
                col *= saturate(v);

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
