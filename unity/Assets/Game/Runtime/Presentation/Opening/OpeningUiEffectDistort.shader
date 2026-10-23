Shader "Zjcs/Opening/UIEffectDistort"
{
    Properties
    {
        _Brightness ("Brightness", Float) = 1
        _Contrast ("Contrast", Float) = 1
        _MainColor ("Main Color", Color) = (1,1,1,1)
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _MainSpeed ("Main Speed", Vector) = (1,1,1,1)
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskPivot ("Mask Pivot", Vector) = (0.5,0.5,0,0)
        _MaskRotator ("Mask Rotator", Vector) = (0,1,0,0)
        _Speed ("Mask Speed", Vector) = (0.9,1,1,1)
        _DistortTex ("Distortion Texture", 2D) = "white" {}
        _Float2 ("Distortion Amount", Range(0,0.5)) = 0.062
        _DistortSpeed ("Distortion Speed", Vector) = (1,1,1,1)
        _DistortTexMask ("Distortion Mask", 2D) = "white" {}
        _MaskDistortSpeed ("Distortion Mask Speed", Vector) = (1,1,1,1)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use UI Alpha Clip", Float) = 0
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex OpeningUiVert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "OpeningUiEffectCommon.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _DistortTex;
            sampler2D _DistortTexMask;
            float4 _MainTex_ST;
            float4 _MaskTex_ST;
            float4 _DistortTex_ST;
            float4 _DistortTexMask_ST;
            float4 _MainColor;
            float2 _MainSpeed;
            float2 _Speed;
            float2 _MaskPivot;
            float2 _MaskRotator;
            float2 _DistortSpeed;
            float2 _MaskDistortSpeed;
            float _Float2;
            float _Brightness;
            float _Contrast;
            float4 _ClipRect;

            fixed4 frag(OpeningUiVertex input) : SV_Target
            {
                float2 maskOffset = input.uv * _MaskTex_ST.xy + _MaskTex_ST.zw - _MaskPivot;
                float2 maskUv = _MaskPivot + float2(
                    dot(maskOffset, _MaskRotator),
                    dot(maskOffset, float2(-_MaskRotator.y, _MaskRotator.x)));
                float mask = tex2D(_MaskTex, maskUv + _Speed * _Time.y).r;

                float2 distortUv = input.uv * _DistortTex_ST.xy + _DistortTex_ST.zw;
                float distortion = tex2D(_DistortTex, distortUv + _DistortSpeed * _Time.y).r * _Float2;
                float2 distortionMaskUv = input.uv * _DistortTexMask_ST.xy + _DistortTexMask_ST.zw;
                float distortionMask = tex2D(_DistortTexMask,
                    distortionMaskUv + _MaskDistortSpeed * _Time.y).r;
                float2 mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                mainUv += _MainSpeed * _Time.y + distortion * distortionMask;

                fixed4 sampleColor = tex2D(_MainTex, mainUv);
                float3 rgb = pow(abs(sampleColor.rgb), _Contrast) * _Brightness;
                float4 tint = input.color * _MainColor;
                float alpha = sampleColor.a * tint.a * mask;
                fixed4 color = fixed4(rgb * tint.rgb * alpha, alpha);
                return OpeningUiApplyClip(color, input.worldPosition);
            }
            ENDCG
        }
    }
}
