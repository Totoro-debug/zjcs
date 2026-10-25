// Pinned monster_3803 mask pass, translated from the official GLES program.
Shader "Zjcs/Opening/BossMask"
{
    Properties
    {
        _MainTex ("Atlas",2D)="white" {}
        _StencilRef ("Stencil ID",Float)=0
        _StencilComp ("Comparison",Float)=8
        _StencilOp ("Operation",Float)=0
        _ColorMask ("Color Mask",Float)=15
        _ZWrite ("Z Write",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite [_ZWrite] ZTest LEqual
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilOp] }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                float3 color=v.color.a==0?v.color.rgb:v.color.rgb/v.color.a;
                o.color=float4(GammaToLinearSpaceExact(color.r),GammaToLinearSpaceExact(color.g),GammaToLinearSpaceExact(color.b),v.color.a);
                return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                if(i.color.a!=0) clip(c.a-.1);
                c.rgb*=c.a;
                return c;
            }
            ENDCG
        }
    }
}
