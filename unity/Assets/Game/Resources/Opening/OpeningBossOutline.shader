// Pinned Spine outline pass: original 8-neighbour alpha, noise UV and stencil.
Shader "Zjcs/Opening/BossOutline"
{
    Properties
    {
        _MainTex ("Atlas",2D)="white" {}
        _NoiseTex ("Noise",2D)="white" {}
        _NoiseTex_MoveCenter ("Noise move/center",Vector)=(0,0,.5,.5)
        _Noise_Rota ("Noise rotation",Float)=0
        _Noise_Rotator ("Noise rotation speed",Float)=0
        _UseNoise ("Use noise",Float)=0
        _OutlineWidth ("Width",Float)=1
        _OutlineReferenceTexWidth ("Reference width",Float)=512
        _OutlineMipLevel ("Mip",Float)=0
        _ThresholdEnd ("Threshold",Float)=.1
        _OutlineSmoothness ("Smoothness",Float)=1
        _OutlineOpaqueAlpha ("Opaque alpha",Float)=1
        _OutlineColor ("Outline",Color)=(1,1,1,1)
        _ColorIntensity ("Intensity",Float)=1
        _StencilRef ("Stencil ID",Float)=0
        _StencilComp ("Comparison",Float)=6
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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_NoiseTex;
            float4 _MainTex_TexelSize,_NoiseTex_ST,_NoiseTex_MoveCenter,_OutlineColor;
            float _Noise_Rota,_Noise_Rotator,_UseNoise,_OutlineWidth,_OutlineReferenceTexWidth;
            float _OutlineMipLevel,_ThresholdEnd,_OutlineSmoothness,_OutlineOpaqueAlpha,_ColorIntensity;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float alpha:COLOR; float2 uv:TEXCOORD0; float2 noise:TEXCOORD1; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.alpha=v.color.a;
                float angle=(_Noise_Rotator*(frac(_Noise_Rota*_Time.y)*6.28299999-_Noise_Rota)+_Noise_Rota)*6.28318548;
                float2 p=v.uv*_NoiseTex_ST.xy+_NoiseTex_ST.zw-_NoiseTex_MoveCenter.zw;
                o.noise=float2(dot(p,float2(cos(angle),sin(angle))),dot(p,float2(-sin(angle),cos(angle))))
                    +_NoiseTex_MoveCenter.zw+_Time.yy*_NoiseTex_MoveCenter.xy;
                return o;
            }
            float SampleAlpha(float2 uv) { return tex2Dlod(_MainTex,float4(uv,0,_OutlineMipLevel)).a; }
            fixed4 frag(Output i):SV_Target
            {
                float2 offset=_OutlineWidth/(_OutlineReferenceTexWidth*_MainTex_TexelSize.x)*_MainTex_TexelSize.xy;
                float sum=SampleAlpha(i.uv+float2(0,-offset.y))+SampleAlpha(i.uv+float2(0,offset.y))
                    +SampleAlpha(i.uv+float2(-offset.x,0))+SampleAlpha(i.uv+float2(offset.x,0))
                    +SampleAlpha(i.uv+offset*float2(-.7,.7))+SampleAlpha(i.uv+offset*float2(.7,.7))
                    +SampleAlpha(i.uv+offset*float2(-.7,-.7))+SampleAlpha(i.uv+offset*float2(.7,-.7));
                float alpha=tex2D(_MainTex,i.uv).a;
                float a=saturate((sum*i.alpha*.125-(1-_OutlineSmoothness)*_ThresholdEnd)/(_ThresholdEnd*_OutlineSmoothness));
                a=saturate(a-alpha);
                float4 color=alpha>_OutlineOpaqueAlpha?0:a*_OutlineColor;
                color.rgb*=color.a*_ColorIntensity;
                if(_UseNoise>0) color*=tex2D(_NoiseTex,i.noise);
                clip(color.a-.01);
                return color;
            }
            ENDCG
        }
    }
}
