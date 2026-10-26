Shader "Zjcs/Map10/Official Effect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _DistortionTex ("Distortion Texture", 2D) = "gray" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _Color ("Color", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _DissolveColor ("Dissolve Color", Color) = (1,1,1,1)
        _MainTex_MoveCenter ("Main UV Offset", Vector) = (0,0,0,0)
        _MaskTex_MoveCenter ("Mask UV Offset", Vector) = (0,0,0,0)
        _NoiseTex_MoveCenter ("Noise UV Offset", Vector) = (0,0,0,0)
        _DistortionTex_MoveCenter ("Distortion UV Offset", Vector) = (0,0,0,0)
        _ColorIntensity ("Color Intensity", Float) = 1
        _Glow ("Glow", Float) = 0
        _GlowGlobal ("Global Glow", Float) = 0
        _Distortion ("Distortion", Float) = 0
        _MaksPower ("Mask Power", Float) = 1
        _MaskRA ("Mask Alpha Channel", Float) = 0
        _MaskOn ("Use Mask", Float) = 0
        _DissolveOn ("Use Dissolve", Float) = 0
        _UseDissolve2 ("Use Secondary Dissolve", Float) = 0
        _DissolveControl ("Dissolve Threshold", Float) = 0.5
        _DissolveRange ("Dissolve Range", Float) = 0.5
        _DissolveSoftness ("Dissolve Softness", Float) = 0.05
        _DissolveProgress ("Dissolve Progress", Float) = 0
        _Maintex_Rota ("Main UV Rotation", Float) = 0
        _Maintex_Rotator ("Main UV Rotation Speed", Float) = 0
        _Mask_Rota ("Mask UV Rotation", Float) = 0
        _Mask_Rotator ("Mask UV Rotation Speed", Float) = 0
        _Noise_Rota ("Noise UV Rotation", Float) = 0
        _Noise_Rotator ("Noise UV Rotation Speed", Float) = 0
        _DistortionTex_Rota ("Distortion UV Rotation", Float) = 0
        _DistortionTex_Rotator ("Distortion UV Rotation Speed", Float) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _BlendOp ("Blend Operation", Float) = 0
        [HideInInspector] _ZWrite ("Depth Write", Float) = 0
        [HideInInspector] _ZTest ("Depth Test", Float) = 4
        [HideInInspector] _CullMode ("Cull Mode", Float) = 0
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _Stencil ("Stencil Reference", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull [_CullMode]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]

        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _NoiseTex;
            sampler2D _DistortionTex;
            float4 _MainTex_ST;
            float4 _MainColor;
            float4 _Color;
            float4 _GlowColor;
            float4 _DissolveColor;
            float4 _MainTex_MoveCenter;
            float4 _MaskTex_MoveCenter;
            float4 _NoiseTex_MoveCenter;
            float4 _DistortionTex_MoveCenter;
            float _ColorIntensity;
            float _Glow;
            float _GlowGlobal;
            float _Distortion;
            float _MaksPower;
            float _MaskRA;
            float _MaskOn;
            float _DissolveOn;
            float _UseDissolve2;
            float _DissolveControl;
            float _DissolveRange;
            float _DissolveSoftness;
            float _DissolveProgress;
            float _Maintex_Rota;
            float _Maintex_Rotator;
            float _Mask_Rota;
            float _Mask_Rotator;
            float _Noise_Rota;
            float _Noise_Rotator;
            float _DistortionTex_Rota;
            float _DistortionTex_Rotator;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float2 rotateUv(float2 uv, float radians, float2 pivot)
            {
                float sineValue;
                float cosineValue;
                sincos(radians, sineValue, cosineValue);
                uv -= pivot;
                return float2(uv.x * cosineValue - uv.y * sineValue,
                    uv.x * sineValue + uv.y * cosineValue) + pivot;
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 pivot = _MainTex_MoveCenter.xy;
                float2 mainUv = rotateUv(input.uv, _Maintex_Rota + _Maintex_Rotator * _Time.y, pivot);
                float2 distortionUv = rotateUv(input.uv + _DistortionTex_MoveCenter.xy,
                    _DistortionTex_Rota + _DistortionTex_Rotator * _Time.y, float2(0.5, 0.5));
                float2 distortion = (tex2D(_DistortionTex, distortionUv).rg * 2.0 - 1.0) * _Distortion;
                fixed4 textureColor = tex2D(_MainTex, mainUv + distortion);
                float2 maskUv = rotateUv(input.uv + _MaskTex_MoveCenter.xy,
                    _Mask_Rota + _Mask_Rotator * _Time.y, float2(0.5, 0.5));
                float mask = tex2D(_MaskTex, maskUv).r;
                float maskAlpha = tex2D(_MaskTex, maskUv).a;
                mask = lerp(mask, maskAlpha, saturate(_MaskRA));
                float2 noiseUv = rotateUv(input.uv + _NoiseTex_MoveCenter.xy,
                    _Noise_Rota + _Noise_Rotator * _Time.y, float2(0.5, 0.5));
                float noiseValue = tex2D(_NoiseTex, noiseUv).r;

                fixed4 color = textureColor * _MainColor * _Color * input.color;
                color.rgb *= max(_ColorIntensity, 0.0);
                color.a *= lerp(1.0, mask, saturate(_MaskOn * _MaksPower));
                if (_DissolveOn > 0.5 || _UseDissolve2 > 0.5)
                {
                    float threshold = lerp(_DissolveControl, _DissolveProgress, saturate(_DissolveProgress));
                    float dissolve = smoothstep(threshold - max(_DissolveSoftness, 0.001),
                        threshold + max(_DissolveSoftness, 0.001), noiseValue);
                    color.a *= dissolve;
                    color.rgb += _DissolveColor.rgb * dissolve * _DissolveRange;
                }
                color.rgb += textureColor.rgb * _GlowColor.rgb * max(_Glow * _GlowGlobal, 0.0) * 0.1;
                return color;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
