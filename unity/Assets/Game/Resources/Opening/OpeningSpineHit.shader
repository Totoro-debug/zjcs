Shader "Zjcs/Opening/SpineHit"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _HitEffectColor ("Hit Color", Color) = (1,1,1,1)
        _HitEffectBlend ("Hit Blend", Float) = 0
        _HITEFFECT ("Hit Keyword", Float) = 0
        [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Input",Float)=0
        _StencilRef ("Stencil ID",Float)=1
        _StencilComp ("Stencil Comparison",Float)=8
        _StencilOp ("Stencil Operation",Float)=0
        _ColorMask ("Color Mask",Float)=15
        _SrcBlend ("Source Blend", Float) = 1
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
        ColorMask [_ColorMask]
        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilOp] }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _STRAIGHT_ALPHA_INPUT
            #include "UnityCG.cginc"
            #include "../../../Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"
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
                o.color = PMAGammaToTargetSpace(v.color) * _Color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // SkeletonEffect-2D's native _HITEFFECT GLES variant clips transparent
                // texels, blends against hitColor * alpha, then premultiplies output.
                // Tinting transparent texels with an opaque RGB value creates a large quad.
                if (_HITEFFECT > .5)
                {
                    float alpha = tex.a * i.color.a;
                    if (i.color.a != 0) clip(alpha - .01);
                    return fixed4(lerp(tex.rgb, _HitEffectColor.rgb * alpha,
                        _HitEffectBlend * _HitEffectColor.a) * alpha, alpha);
                }
                #if defined(_STRAIGHT_ALPHA_INPUT)
                tex.rgb *= tex.a;
                #endif
                fixed4 color = tex * i.color;
                return color;
            }
            ENDCG
        }
    }
}
