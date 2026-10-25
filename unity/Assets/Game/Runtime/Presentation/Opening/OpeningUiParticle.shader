Shader "Zjcs/Opening/UIParticle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _ColorIntensity ("Color Intensity", Float) = 1
        _IntensityAffectAlpha ("Intensity Affects Alpha", Float) = 0
        _ClampMainUV ("Clamp Main UV", Float) = 0
        _WorldClipHeight ("World Clip Height", Float) = -1000000
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ZWrite ("Depth Write", Float) = 0
        [HideInInspector] _ZTest ("Depth Test", Float) = 4
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
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex OpeningUiVert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            float4 _ClipRect;
            #include "OpeningUiEffectCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainColor;
            float _ColorIntensity;
            float _IntensityAffectAlpha;
            float _ClampMainUV;
            float _WorldClipHeight;

            fixed4 frag(OpeningUiVertex input) : SV_Target
            {
                clip(input.worldPosition.y - _WorldClipHeight);
                float2 uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                if (_ClampMainUV != 0.0) uv = saturate(uv);
                fixed4 sampleColor = tex2D(_MainTex, uv) * input.color * _MainColor;
                float alpha = sampleColor.a * (_IntensityAffectAlpha * (_ColorIntensity - 1.0) + 1.0);
                clip(alpha - 0.001);
                fixed4 color = fixed4(sampleColor.rgb * _ColorIntensity, alpha);
                return OpeningUiApplyClip(color, input.worldPosition);
            }
            ENDCG
        }
    }
}
