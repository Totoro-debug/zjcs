Shader "Zjcs/Combat/TimelineParticleMainTex"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _MainTex_MoveCenter ("Main UV Motion And Center", Vector) = (0,0,0,0)
        _Maintex_Rota ("Main Rotation", Float) = 0
        _Maintex_Rotator ("Main Rotation Speed", Float) = 0
        _UseCustomData ("Use Custom UV", Float) = 0
        _ClampMainUV ("Clamp Main UV", Float) = 0
        _ColorIntensity ("Color Intensity", Float) = 1
        _IntensityAffectAlpha ("Intensity Affects Alpha", Float) = 0
        _WorldClipHeight ("World Clip Height", Float) = -1000000
        _BlendOp ("Blend Operation", Float) = 0
        _SrcBlend ("Source Blend", Float) = 5
        _DstBlend ("Destination Blend", Float) = 10
        _CullMode ("Cull", Float) = 0
        _ZWrite ("Z Write", Float) = 0
        _ZTest ("Z Test", Float) = 4
        _ColorMask ("Color Mask", Float) = 14
        _Stencil ("Stencil", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend]
        Cull [_CullMode]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        ColorMask [_ColorMask]
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            // Exact USE_MAINTEX variant of the original first particle pass.
            sampler2D _MainTex;
            float4 _MainTex_ST, _MainTex_MoveCenter, _MainColor;
            float _Maintex_Rota, _Maintex_Rotator, _UseCustomData, _ClampMainUV, _WorldClipHeight;
            half _ColorIntensity, _IntensityAffectAlpha;
            float2 _GlobalMipBias;
            struct Input { float4 vertex : POSITION; float4 uv : TEXCOORD0; float4 custom : TEXCOORD1; float4 color : COLOR; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float worldY : TEXCOORD1; };
            Output vert(Input input)
            {
                Output output;
                float4 world = mul(unity_ObjectToWorld, float4(input.vertex.xyz, 1));
                output.vertex = mul(UNITY_MATRIX_VP, world);
                output.worldY = world.y;
                float2 uv = input.uv.xy + (_UseCustomData != 0 ? input.custom.xy : float2(0,0));
                uv = uv * _MainTex_ST.xy + _MainTex_ST.zw - _MainTex_MoveCenter.zw;
                float angle = (_Maintex_Rotator * (frac(_Time.y * _Maintex_Rota) * 6.28299999 - _Maintex_Rota) + _Maintex_Rota) * 6.28318548;
                float sine = sin(angle), cosine = cos(angle);
                uv = float2(dot(uv, float2(cosine, sine)), dot(uv, float2(-sine, cosine)));
                output.uv = uv + _MainTex_MoveCenter.zw + _Time.y * _MainTex_MoveCenter.xy;
                output.color = input.color * _MainColor;
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                clip(input.worldY - _WorldClipHeight);
                float2 uv = _ClampMainUV != 0 ? saturate(input.uv) : input.uv;
                half4 sampleColor = tex2Dbias(_MainTex, float4(uv, 0, _GlobalMipBias.x));
                float4 color = sampleColor * input.color;
                half alphaIntensity = _IntensityAffectAlpha * (_ColorIntensity - 1) + 1;
                color.a *= alphaIntensity;
                clip(color.a - .00100000005);
                color.rgb *= _ColorIntensity;
                return color;
            }
            ENDCG
        }
    }
}
