Shader "Zjcs/Opening/SpineHit"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _HitEffectColor ("Hit Color", Color) = (1,1,1,1)
        _HitEffectBlend ("Hit Blend", Float) = 0
        _HITEFFECT ("Hit Keyword", Float) = 0
        _SrcBlend ("Source Blend", Float) = 5
        _DstBlend ("Destination Blend", Float) = 10
        _ZWrite ("Z Write", Float) = 0
        _ZTest ("Z Test", Float) = 4
        _CullMode ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull [_CullMode]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _HitEffectColor;
            float _HitEffectBlend, _HITEFFECT;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * i.color;
                color.rgb = lerp(color.rgb, _HitEffectColor.rgb,
                    saturate(_HitEffectBlend) * _HITEFFECT * _HitEffectColor.a);
                return color;
            }
            ENDCG
        }
    }
}
