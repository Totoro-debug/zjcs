Shader "Zjcs/Combat/TimelineEffectDistort"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _DistortTex ("Distortion Texture", 2D) = "white" {}
        _DistortTexMask ("Distortion Mask", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _MainSpeed ("Main Speed", Vector) = (1,1,1,1)
        _MaskPivot ("Mask Pivot", Vector) = (.5,.5,0,0)
        _MaskRotator ("Mask Rotation", Vector) = (0,1,0,0)
        _Speed ("Mask Speed", Vector) = (.9,1,1,1)
        _Float2 ("Distortion Amount", Float) = .062
        _DistortSpeed ("Distortion Speed", Vector) = (1,1,1,1)
        _MaskDistortSpeed ("Distortion Mask Speed", Vector) = (1,1,1,1)
        _Brightness ("Brightness", Float) = 1
        _Contrast ("Contrast", Float) = 1
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _MaskTex, _DistortTex, _DistortTexMask;
            half4 _MainTex_ST, _MaskTex_ST, _DistortTex_ST, _DistortTexMask_ST, _MainColor;
            half2 _MainSpeed, _MaskPivot, _MaskRotator, _Speed, _DistortSpeed, _MaskDistortSpeed;
            half _Float2, _Brightness, _Contrast;
            float2 _GlobalMipBias;
            struct Input { float4 vertex : POSITION; half4 color : COLOR; half2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; half4 color : COLOR; half2 uv : TEXCOORD0; half2 mainUv : TEXCOORD1; };
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                output.mainUv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }
            half4 frag(Output input) : SV_Target
            {
                half2 maskOffset = input.uv * _MaskTex_ST.xy + _MaskTex_ST.zw - _MaskPivot;
                half2 maskUv = _MaskPivot + half2(dot(maskOffset, _MaskRotator),
                    dot(maskOffset, half2(-_MaskRotator.y, _MaskRotator.x)));
                half mask = tex2Dbias(_MaskTex, float4(maskUv + _Speed * _Time.y, 0, _GlobalMipBias.x)).r;
                half2 distortUv = input.uv * _DistortTex_ST.xy + _DistortTex_ST.zw;
                half distortion = tex2Dbias(_DistortTex, float4(distortUv + _DistortSpeed * _Time.y, 0, _GlobalMipBias.x)).r * _Float2;
                half2 distortionMaskUv = input.uv * _DistortTexMask_ST.xy + _DistortTexMask_ST.zw;
                half distortionMask = tex2Dbias(_DistortTexMask,
                    float4(distortionMaskUv + _MaskDistortSpeed * _Time.y, 0, _GlobalMipBias.x)).r;
                half4 sampleColor = tex2Dbias(_MainTex,
                    float4(input.mainUv + _MainSpeed * _Time.y + distortion * distortionMask, 0, _GlobalMipBias.x));
                half4 tint = input.color * _MainColor;
                half4 color = half4(pow(abs(sampleColor.rgb), _Contrast) * _Brightness * tint.rgb,
                    sampleColor.a * tint.a) * mask;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
