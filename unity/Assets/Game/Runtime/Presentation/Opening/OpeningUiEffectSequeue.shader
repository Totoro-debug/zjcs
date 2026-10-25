Shader "Zjcs/Opening/UIEffectSequeue"
{
    Properties
    {
        _Brightness ("Brightness", Float) = 1
        _Contrast ("Contrast", Float) = 1
        _MainColor ("Main Color", Color) = (1,1,1,1)
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _MainPanner ("Main Panner", Vector) = (0,0,0,0)
        _Rotator ("Rotation", Vector) = (1,0,0,0)
        _SquenceColumn ("Sequence Columns", Float) = 1
        _SquenceRow ("Sequence Rows", Float) = 1
        _SquenceRate ("Sequence Rate", Range(0,1)) = 0
        _SquenceSpeed ("Sequence Speed", Float) = 0
        _SquenceTimes ("Sequence Times", Float) = 1
        IsSqueueRecycle ("Recycle Sequence", Float) = 0
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
            float4 _ClipRect;
            #include "OpeningUiEffectCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainColor;
            float4 _MainPanner;
            float2 _Rotator;
            float _SquenceColumn;
            float _SquenceRow;
            float _SquenceRate;
            float _Brightness;
            float _Contrast;

            fixed4 frag(OpeningUiVertex input) : SV_Target
            {
                float2 centeredUv = input.uv - 0.5;
                float2 rotatedUv = float2(
                    centeredUv.x * _Rotator.x - centeredUv.y * _Rotator.y,
                    centeredUv.y * _Rotator.x + centeredUv.x * _Rotator.y) + 0.5;
                float columns = max(1.0, floor(_SquenceColumn));
                float rows = max(1.0, floor(_SquenceRow));
                float rate = clamp(_SquenceRate, 0.0, 0.999000013);
                float frame = floor(columns * rows * rate);
                float columnIndex = floor(frame / columns);
                float rowIndex = frame - columnIndex * columns;
                float2 uv = rotatedUv / float2(columns, rows)
                    + float2(rowIndex / columns, (rows - 1.0 - columnIndex) / rows);
                uv = (uv + _MainPanner.xy * _Time.y) * _MainTex_ST.xy + _MainTex_ST.zw;

                fixed4 sampleColor = tex2D(_MainTex, uv);
                float3 rgb = pow(abs(sampleColor.rgb), _Contrast) * _Brightness * _MainColor.rgb;
                float alpha = sampleColor.a * _MainColor.a * input.color.a;
                fixed4 color = fixed4(rgb * input.color.rgb * alpha, alpha);
                return OpeningUiApplyClip(color, input.worldPosition);
            }
            ENDCG
        }
    }
}
