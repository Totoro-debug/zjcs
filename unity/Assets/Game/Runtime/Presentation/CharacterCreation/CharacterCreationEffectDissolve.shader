Shader "Zjcs/CharacterCreation/EffectDissolve"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture", 2D) = "white" {}
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        _DissolveMultX ("Dissolve X", Range(-1, 1)) = 0
        _DissolveMultY ("Dissolve Y", Range(-1, 1)) = 0
        [HideInInspector] _DissolveRotator ("Dissolve Rotation", Vector) = (1,0,0,0)
        _DissolvePanner ("Dissolve Panner", Vector) = (0,0,0,0)
        _DisSoftness ("Dissolve Softness", Range(0.1, 2)) = 0.1
        _AnDissolve ("Dissolve Amount", Float) = 0
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.5
        _EdgeColor1 ("Edge Color 1", Color) = (1,1,1,1)
        _EdgeColor1Intensity ("Edge Color 1 Intensity", Float) = 1
        _EdgeColor2 ("Edge Color 2", Color) = (1,1,1,1)
        _EdgeColor2Intensity ("Edge Color 2 Intensity", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        [KeywordEnum(Add, Blend)] [HideInInspector] _BlendMode ("Blend Mode", Float) = 1
        [HideInInspector] _SrcBlend ("SrcBlend", Float) = 5
        [HideInInspector] _DstBlend ("DstBlend", Float) = 10
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use UI Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
        }

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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _DissolveTex;
            float4 _MainTex_ST;
            float4 _DissolveTex_ST;
            float4 _BaseColor;
            float4 _DissolveRotator;
            float4 _DissolvePanner;
            float4 _EdgeColor1;
            float4 _EdgeColor2;
            float _DissolveMultX;
            float _DissolveMultY;
            float _DisSoftness;
            float _AnDissolve;
            float _EdgeWidth;
            float _EdgeColor1Intensity;
            float _EdgeColor2Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centeredUv = input.uv + _DissolvePanner.xy * _Time.y - 0.5;
                float2 rotatedUv = float2(
                    centeredUv.x * _DissolveRotator.x - centeredUv.y * _DissolveRotator.y,
                    centeredUv.y * _DissolveRotator.x + centeredUv.x * _DissolveRotator.y);
                float2 dissolveUv = (rotatedUv + 0.5) * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
                float4 dissolveSample = tex2D(_DissolveTex, dissolveUv);
                float dissolve = dissolveSample.r * dissolveSample.a
                    - ceil(_DissolveMultX) * abs(_DissolveMultX) * input.uv.x
                    - ceil(_DissolveMultY) * abs(_DissolveMultY) * input.uv.y
                    - _AnDissolve;
                float edge = pow(saturate(dissolve), _DisSoftness);
                clip(edge - _EdgeWidth);

                float edgeAmount = saturate((edge - 0.5) / (_EdgeWidth * 0.5));
                edgeAmount = 1.0 - (3.0 - 2.0 * edgeAmount) * edgeAmount * edgeAmount;
                float2 mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                fixed4 color = tex2D(_MainTex, mainUv) * _BaseColor * input.color;
                #if defined(UNITY_UI_ALPHACLIP)
                    clip(color.a - 0.001);
                #endif

                float3 edgeColor = lerp(_EdgeColor1.rgb * _EdgeColor1Intensity,
                    _EdgeColor2.rgb * _EdgeColor2Intensity, edgeAmount);
                color.rgb = (color.rgb + edgeColor * edgeAmount) * color.a;
                return color;
            }
            ENDCG
        }
    }
}
