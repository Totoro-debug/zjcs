Shader "Zjcs/Opening/UIEffectDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        _DissolvePanner ("Dissolve Panner", Vector) = (0,0,0,0)
        _DissolveRotator ("Dissolve Rotator", Vector) = (1,0,0,0)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _DissolveMultX ("Dissolve X Tiling", Float) = 0
        _DissolveMultY ("Dissolve Y Tiling", Float) = 1
        _AnDissolve ("Dissolve Amount", Float) = 0
        _DisSoftness ("Dissolve Softness", Float) = 1
        _EdgeWidth ("Edge Width", Float) = 0.1
        _EdgeColor1 ("Edge Color 1", Color) = (1,1,1,1)
        _EdgeColor2 ("Edge Color 2", Color) = (1,1,1,1)
        _EdgeColor1Intensity ("Edge Color 1 Intensity", Float) = 1
        _EdgeColor2Intensity ("Edge Color 2 Intensity", Float) = 1
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
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
            float4 _ClipRect;
            #include "OpeningUiEffectCommon.cginc"

            sampler2D _MainTex;
            sampler2D _DissolveTex;
            float4 _MainTex_ST;
            float4 _DissolveTex_ST;
            float4 _BaseColor;
            float4 _DissolvePanner;
            float4 _DissolveRotator;
            float4 _EdgeColor1;
            float4 _EdgeColor2;
            float _DissolveMultX;
            float _DissolveMultY;
            float _AnDissolve;
            float _DisSoftness;
            float _EdgeWidth;
            float _EdgeColor1Intensity;
            float _EdgeColor2Intensity;

            fixed4 frag(OpeningUiVertex input) : SV_Target
            {
                float2 uv = input.uv + _DissolvePanner.xy * _Time.y - 0.5;
                float2 rotatedUv = float2(
                    uv.x * _DissolveRotator.x - uv.y * _DissolveRotator.y,
                    uv.y * _DissolveRotator.x + uv.x * _DissolveRotator.y) + 0.5;
                rotatedUv = rotatedUv * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
                fixed4 dissolveSample = tex2D(_DissolveTex, rotatedUv);
                float tiledX = ceil(_DissolveMultX) * input.uv.x * abs(_DissolveMultX);
                float tiledY = ceil(_DissolveMultY) * input.uv.y * abs(_DissolveMultY);
                float dissolve = pow(saturate(dissolveSample.r * dissolveSample.a
                    - tiledX - tiledY - _AnDissolve), _DisSoftness);
                clip(dissolve - _EdgeWidth);

                float edgeAmount = saturate((dissolve - 0.5) / (_EdgeWidth * 0.5));
                edgeAmount = 1.0 - edgeAmount * edgeAmount * (3.0 - 2.0 * edgeAmount);
                float3 edgeColor = lerp(_EdgeColor1.rgb * _EdgeColor1Intensity,
                    _EdgeColor2.rgb * _EdgeColor2Intensity, edgeAmount);

                float2 mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                fixed4 sampleColor = tex2D(_MainTex, mainUv);
                float4 mainColor = sampleColor * _BaseColor * input.color;
                float3 rgb = mainColor.rgb + edgeColor * edgeAmount;
                fixed4 color = fixed4(rgb * mainColor.a, mainColor.a);
                return OpeningUiApplyClip(color, input.worldPosition);
            }
            ENDCG
        }
    }
}
