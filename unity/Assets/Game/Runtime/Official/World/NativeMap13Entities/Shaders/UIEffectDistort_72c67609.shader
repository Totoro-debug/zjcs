Shader "Zjcs/Official/World/Map13/Entity/UIEffectDistort_72c67609" {
Properties {
_Brightness ("Brightness", Float) = 1.0
_Contrast ("Contrast", Float) = 1.0
_MainColor ("主图颜色", Color) = (1.0,1.0,1.0,1.0)
_MainTex ("主贴图", 2D) = "white" {}
_MainSpeed ("主帖图流动速度XY", Vector) = (1.0,1.0,1.0,1.0)
_MaskTex ("遮罩贴图", 2D) = "white" {}
_MaskPivot ("遮罩旋转锚点", Vector) = (0.5,0.5,0.0,0.0)
_MaskRotator ("Mask旋转角度", Vector) = (0.0,1.0,0.0,0.0)
_Speed ("流动速度XY", Vector) = (0.8999999761581421,1.0,1.0,1.0)
_DistortTex ("扰动贴图", 2D) = "white" {}
_Float2 ("扰动数量", Range(0.0,0.5)) = 0.06199999898672104
_DistortSpeed ("扰动速度XY", Vector) = (1.0,1.0,1.0,1.0)
_DistortTexMask ("扰动贴图遮罩", 2D) = "white" {}
_MaskDistortSpeed ("遮罩流动速度XY", Vector) = (1.0,1.0,1.0,1.0)
[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8.0
_Stencil ("Stencil ID", Float) = 0.0
[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0.0
_StencilWriteMask ("Stencil Write Mask", Float) = 255.0
_StencilReadMask ("Stencil Read Mask", Float) = 255.0
[KeywordEnum(Add, Blend)] _BlendMode ("Blend Mode", Float) = 1.0
_SrcBlend ("SrcBlend", Float) = 5.0
_DstBlend ("DstBlend", Float) = 10.0
[Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 15.0
[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0.0
}
SubShader {
Tags { "IGNOREPROJECTOR"="true" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest [unity_GUIZTestMode]
Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
BlendOp Add, Add
ColorMask [_ColorMask]
Stencil { Ref [_Stencil] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] Comp [_StencilComp] Pass [_StencilOp] Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local __ UNITY_UI_ALPHACLIP
#pragma multi_compile_local __ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#if !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 color:COLOR; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
float4 Xhlslcc_UnusedXunity_LODFade;
float4 Xhlslcc_UnusedXunity_LightmapST;
float4 Xhlslcc_UnusedXunity_MotionVectorsParams;
float4 Xhlslcc_UnusedXunity_ProbesOcclusion;
float4 Xhlslcc_UnusedXunity_RendererBounds_Max;
float4 Xhlslcc_UnusedXunity_RendererBounds_Min;
float4 Xhlslcc_UnusedXunity_RenderingLayer;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube0_ProbePosition;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube1_ProbePosition;
float4 _ClipRect;
half _Brightness;
half _Contrast;
half _Float2;
half2 _DistortSpeed;
half2 _MainSpeed;
half2 _MaskDistortSpeed;
half2 _MaskPivot;
half2 _MaskRotator;
half2 _Speed;
half4 Xhlslcc_UnusedXunity_LightData;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SHAb;
half4 Xhlslcc_UnusedXunity_SHAg;
half4 Xhlslcc_UnusedXunity_SHAr;
half4 Xhlslcc_UnusedXunity_SHBb;
half4 Xhlslcc_UnusedXunity_SHBg;
half4 Xhlslcc_UnusedXunity_SHBr;
half4 Xhlslcc_UnusedXunity_SHC;
half4 Xhlslcc_UnusedXunity_SpecCube0_HDR;
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _DistortTexMask_ST;
half4 _DistortTex_ST;
half4 _MainColor;
half4 _MainTex_ST;
half4 _MaskTex_ST;
sampler2D _DistortTex;
sampler2D _DistortTexMask;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float2 u_xlat5;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    output.world.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = input.uv.xy;
    u_xlat5.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat5.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat5.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat1.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    u_xlat0.xy = (-u_xlat1.zw) + u_xlat1.xy;
    output.mask = u_xlat0;
    output.color = input.color;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half u_xlat16_1;
half4 u_xlat16_2;
half4 u_xlat16_3;
half2 u_xlat16_4;
float2 u_xlat5;
half u_xlat16_5;
half2 u_xlat16_8;
float2 u_xlat9;

    u_xlat16_0.xy = float2(_MaskRotator.y, _MaskRotator.x) * float2(-1.0, 1.0);
    u_xlat16_8.xy = input.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    u_xlat16_8.xy = u_xlat16_8.xy + (-_MaskPivot.xy);
    u_xlat16_0.y = dot(u_xlat16_8.xy, u_xlat16_0.xy);
    u_xlat16_0.x = dot(u_xlat16_8.xy, float2(_MaskRotator.x, _MaskRotator.y));
    u_xlat16_0.xy = u_xlat16_0.xy + _MaskPivot.xy;
    u_xlat1.xy = _Speed.xy * _Time.yy + u_xlat16_0.xy;
    u_xlat16_1 = tex2Dbias(_MaskTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.xy = input.uv.xy * _DistortTex_ST.xy + _DistortTex_ST.zw;
    u_xlat5.xy = _Time.yy * _DistortSpeed.xy + u_xlat16_0.xy;
    u_xlat16_5 = tex2Dbias(_DistortTex, float4(u_xlat5.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.x = u_xlat16_5 * _Float2;
    u_xlat16_4.xy = input.uv.xy * _DistortTexMask_ST.xy + _DistortTexMask_ST.zw;
    u_xlat5.xy = _Time.yy * float2(_MaskDistortSpeed.x, _MaskDistortSpeed.y) + u_xlat16_4.xy;
    u_xlat16_5 = tex2Dbias(_DistortTexMask, float4(u_xlat5.xy, 0, _GlobalMipBias.x)).x;
    u_xlat9.xy = _MainSpeed.xy * _Time.yy + input.world.xy;
    u_xlat5.xy = u_xlat16_0.xx * float2(u_xlat16_5,u_xlat16_5) + u_xlat9.xy;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat5.xy, 0, _GlobalMipBias.x));
    u_xlat16_2.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(float3(_Contrast, _Contrast, _Contrast));
    u_xlat16_2.xyz = exp2(u_xlat16_2.xyz);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_Brightness,_Brightness,_Brightness);
    u_xlat16_3 = input.color * _MainColor;
    u_xlat16_2.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.w = u_xlat16_0.w * u_xlat16_3.w;
    u_xlat16_0 = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * u_xlat16_2;
    result.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 color:COLOR; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
float4 Xhlslcc_UnusedXunity_LODFade;
float4 Xhlslcc_UnusedXunity_LightmapST;
float4 Xhlslcc_UnusedXunity_MotionVectorsParams;
float4 Xhlslcc_UnusedXunity_ProbesOcclusion;
float4 Xhlslcc_UnusedXunity_RendererBounds_Max;
float4 Xhlslcc_UnusedXunity_RendererBounds_Min;
float4 Xhlslcc_UnusedXunity_RenderingLayer;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube0_ProbePosition;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube1_ProbePosition;
float4 _ClipRect;
half _Brightness;
half _Contrast;
half _Float2;
half2 _DistortSpeed;
half2 _MainSpeed;
half2 _MaskDistortSpeed;
half2 _MaskPivot;
half2 _MaskRotator;
half2 _Speed;
half4 Xhlslcc_UnusedXunity_LightData;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SHAb;
half4 Xhlslcc_UnusedXunity_SHAg;
half4 Xhlslcc_UnusedXunity_SHAr;
half4 Xhlslcc_UnusedXunity_SHBb;
half4 Xhlslcc_UnusedXunity_SHBg;
half4 Xhlslcc_UnusedXunity_SHBr;
half4 Xhlslcc_UnusedXunity_SHC;
half4 Xhlslcc_UnusedXunity_SpecCube0_HDR;
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _DistortTexMask_ST;
half4 _DistortTex_ST;
half4 _MainColor;
half4 _MainTex_ST;
half4 _MaskTex_ST;
sampler2D _DistortTex;
sampler2D _DistortTexMask;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float2 u_xlat5;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    output.world.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = input.uv.xy;
    u_xlat5.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat5.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat5.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat1.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    u_xlat0.xy = (-u_xlat1.zw) + u_xlat1.xy;
    output.mask = u_xlat0;
    output.color = input.color;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half u_xlat16_1;
bool u_xlatb1;
half4 u_xlat16_2;
half4 u_xlat16_3;
half3 u_xlat16_4;
half2 u_xlat16_5;
float2 u_xlat6;
half u_xlat16_6;
half2 u_xlat16_10;
float2 u_xlat11;

    u_xlat16_0.xy = float2(_MaskRotator.y, _MaskRotator.x) * float2(-1.0, 1.0);
    u_xlat16_10.xy = input.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    u_xlat16_10.xy = u_xlat16_10.xy + (-_MaskPivot.xy);
    u_xlat16_0.y = dot(u_xlat16_10.xy, u_xlat16_0.xy);
    u_xlat16_0.x = dot(u_xlat16_10.xy, float2(_MaskRotator.x, _MaskRotator.y));
    u_xlat16_0.xy = u_xlat16_0.xy + _MaskPivot.xy;
    u_xlat1.xy = _Speed.xy * _Time.yy + u_xlat16_0.xy;
    u_xlat16_1 = tex2Dbias(_MaskTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.xy = input.uv.xy * _DistortTex_ST.xy + _DistortTex_ST.zw;
    u_xlat6.xy = _Time.yy * _DistortSpeed.xy + u_xlat16_0.xy;
    u_xlat16_6 = tex2Dbias(_DistortTex, float4(u_xlat6.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.x = u_xlat16_6 * _Float2;
    u_xlat16_5.xy = input.uv.xy * _DistortTexMask_ST.xy + _DistortTexMask_ST.zw;
    u_xlat6.xy = _Time.yy * float2(_MaskDistortSpeed.x, _MaskDistortSpeed.y) + u_xlat16_5.xy;
    u_xlat16_6 = tex2Dbias(_DistortTexMask, float4(u_xlat6.xy, 0, _GlobalMipBias.x)).x;
    u_xlat11.xy = _MainSpeed.xy * _Time.yy + input.world.xy;
    u_xlat6.xy = u_xlat16_0.xx * float2(u_xlat16_6,u_xlat16_6) + u_xlat11.xy;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat6.xy, 0, _GlobalMipBias.x));
    u_xlat16_2 = input.color * _MainColor;
    u_xlat16_3.w = u_xlat16_0.w * u_xlat16_2.w;
    u_xlat16_4.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_4.xyz = u_xlat16_4.xyz * float3(float3(_Contrast, _Contrast, _Contrast));
    u_xlat16_4.xyz = exp2(u_xlat16_4.xyz);
    u_xlat16_4.xyz = u_xlat16_4.xyz * float3(_Brightness,_Brightness,_Brightness);
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_4.xyz;
    u_xlat16_0 = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * u_xlat16_3;
    u_xlat16_2.x = u_xlat16_3.w * u_xlat16_1 + -0.00100000005;
    u_xlatb1 = u_xlat16_2.x<0.0;
    if(u_xlatb1){clip(-1);}
    result.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 color:COLOR; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
float4 Xhlslcc_UnusedXunity_LODFade;
float4 Xhlslcc_UnusedXunity_LightmapST;
float4 Xhlslcc_UnusedXunity_MotionVectorsParams;
float4 Xhlslcc_UnusedXunity_ProbesOcclusion;
float4 Xhlslcc_UnusedXunity_RendererBounds_Max;
float4 Xhlslcc_UnusedXunity_RendererBounds_Min;
float4 Xhlslcc_UnusedXunity_RenderingLayer;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube0_ProbePosition;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube1_ProbePosition;
float4 _ClipRect;
half _Brightness;
half _Contrast;
half _Float2;
half2 _DistortSpeed;
half2 _MainSpeed;
half2 _MaskDistortSpeed;
half2 _MaskPivot;
half2 _MaskRotator;
half2 _Speed;
half4 Xhlslcc_UnusedXunity_LightData;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SHAb;
half4 Xhlslcc_UnusedXunity_SHAg;
half4 Xhlslcc_UnusedXunity_SHAr;
half4 Xhlslcc_UnusedXunity_SHBb;
half4 Xhlslcc_UnusedXunity_SHBg;
half4 Xhlslcc_UnusedXunity_SHBr;
half4 Xhlslcc_UnusedXunity_SHC;
half4 Xhlslcc_UnusedXunity_SpecCube0_HDR;
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _DistortTexMask_ST;
half4 _DistortTex_ST;
half4 _MainColor;
half4 _MainTex_ST;
half4 _MaskTex_ST;
sampler2D _DistortTex;
sampler2D _DistortTexMask;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float2 u_xlat5;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    output.world.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = input.uv.xy;
    u_xlat5.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat5.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat5.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat1.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    u_xlat0.xy = (-u_xlat1.zw) + u_xlat1.xy;
    output.mask = u_xlat0;
    output.color = input.color;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half u_xlat16_1;
half4 u_xlat16_2;
half4 u_xlat16_3;
half2 u_xlat16_4;
float2 u_xlat5;
half u_xlat16_5;
half2 u_xlat16_8;
float2 u_xlat9;

    u_xlat16_0.xy = float2(_MaskRotator.y, _MaskRotator.x) * float2(-1.0, 1.0);
    u_xlat16_8.xy = input.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    u_xlat16_8.xy = u_xlat16_8.xy + (-_MaskPivot.xy);
    u_xlat16_0.y = dot(u_xlat16_8.xy, u_xlat16_0.xy);
    u_xlat16_0.x = dot(u_xlat16_8.xy, float2(_MaskRotator.x, _MaskRotator.y));
    u_xlat16_0.xy = u_xlat16_0.xy + _MaskPivot.xy;
    u_xlat1.xy = _Speed.xy * _Time.yy + u_xlat16_0.xy;
    u_xlat16_1 = tex2Dbias(_MaskTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.xy = input.uv.xy * _DistortTex_ST.xy + _DistortTex_ST.zw;
    u_xlat5.xy = _Time.yy * _DistortSpeed.xy + u_xlat16_0.xy;
    u_xlat16_5 = tex2Dbias(_DistortTex, float4(u_xlat5.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.x = u_xlat16_5 * _Float2;
    u_xlat16_4.xy = input.uv.xy * _DistortTexMask_ST.xy + _DistortTexMask_ST.zw;
    u_xlat5.xy = _Time.yy * float2(_MaskDistortSpeed.x, _MaskDistortSpeed.y) + u_xlat16_4.xy;
    u_xlat16_5 = tex2Dbias(_DistortTexMask, float4(u_xlat5.xy, 0, _GlobalMipBias.x)).x;
    u_xlat9.xy = _MainSpeed.xy * _Time.yy + input.world.xy;
    u_xlat5.xy = u_xlat16_0.xx * float2(u_xlat16_5,u_xlat16_5) + u_xlat9.xy;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat5.xy, 0, _GlobalMipBias.x));
    u_xlat16_2.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(float3(_Contrast, _Contrast, _Contrast));
    u_xlat16_2.xyz = exp2(u_xlat16_2.xyz);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_Brightness,_Brightness,_Brightness);
    u_xlat16_3 = input.color * _MainColor;
    u_xlat16_2.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.w = u_xlat16_0.w * u_xlat16_3.w;
    u_xlat16_0 = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * u_xlat16_2;
    u_xlat1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat1.xy = u_xlat1.xy + -abs(input.mask.xy);
    u_xlat1.xy = u_xlat1.xy * input.mask.zw;
    u_xlat1.xy = clamp(u_xlat1.xy, 0.0, 1.0);
    u_xlat16_2.x = u_xlat1.y * u_xlat1.x;
    u_xlat16_2.x = u_xlat16_0.w * u_xlat16_2.x;
    result.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx;
    result.w = u_xlat16_2.x;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 color:COLOR; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
float4 Xhlslcc_UnusedXunity_LODFade;
float4 Xhlslcc_UnusedXunity_LightmapST;
float4 Xhlslcc_UnusedXunity_MotionVectorsParams;
float4 Xhlslcc_UnusedXunity_ProbesOcclusion;
float4 Xhlslcc_UnusedXunity_RendererBounds_Max;
float4 Xhlslcc_UnusedXunity_RendererBounds_Min;
float4 Xhlslcc_UnusedXunity_RenderingLayer;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube0_ProbePosition;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube1_ProbePosition;
float4 _ClipRect;
half _Brightness;
half _Contrast;
half _Float2;
half2 _DistortSpeed;
half2 _MainSpeed;
half2 _MaskDistortSpeed;
half2 _MaskPivot;
half2 _MaskRotator;
half2 _Speed;
half4 Xhlslcc_UnusedXunity_LightData;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SHAb;
half4 Xhlslcc_UnusedXunity_SHAg;
half4 Xhlslcc_UnusedXunity_SHAr;
half4 Xhlslcc_UnusedXunity_SHBb;
half4 Xhlslcc_UnusedXunity_SHBg;
half4 Xhlslcc_UnusedXunity_SHBr;
half4 Xhlslcc_UnusedXunity_SHC;
half4 Xhlslcc_UnusedXunity_SpecCube0_HDR;
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _DistortTexMask_ST;
half4 _DistortTex_ST;
half4 _MainColor;
half4 _MainTex_ST;
half4 _MaskTex_ST;
sampler2D _DistortTex;
sampler2D _DistortTexMask;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float2 u_xlat5;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    output.world.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = input.uv.xy;
    u_xlat5.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat5.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat5.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat1.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    u_xlat0.xy = (-u_xlat1.zw) + u_xlat1.xy;
    output.mask = u_xlat0;
    output.color = input.color;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half u_xlat16_1;
bool u_xlatb1;
half4 u_xlat16_2;
half4 u_xlat16_3;
half2 u_xlat16_4;
float2 u_xlat5;
half u_xlat16_5;
half u_xlat16_6;
half2 u_xlat16_8;
float2 u_xlat9;

    u_xlat16_0.xy = float2(_MaskRotator.y, _MaskRotator.x) * float2(-1.0, 1.0);
    u_xlat16_8.xy = input.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    u_xlat16_8.xy = u_xlat16_8.xy + (-_MaskPivot.xy);
    u_xlat16_0.y = dot(u_xlat16_8.xy, u_xlat16_0.xy);
    u_xlat16_0.x = dot(u_xlat16_8.xy, float2(_MaskRotator.x, _MaskRotator.y));
    u_xlat16_0.xy = u_xlat16_0.xy + _MaskPivot.xy;
    u_xlat1.xy = _Speed.xy * _Time.yy + u_xlat16_0.xy;
    u_xlat16_1 = tex2Dbias(_MaskTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.xy = input.uv.xy * _DistortTex_ST.xy + _DistortTex_ST.zw;
    u_xlat5.xy = _Time.yy * _DistortSpeed.xy + u_xlat16_0.xy;
    u_xlat16_5 = tex2Dbias(_DistortTex, float4(u_xlat5.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.x = u_xlat16_5 * _Float2;
    u_xlat16_4.xy = input.uv.xy * _DistortTexMask_ST.xy + _DistortTexMask_ST.zw;
    u_xlat5.xy = _Time.yy * float2(_MaskDistortSpeed.x, _MaskDistortSpeed.y) + u_xlat16_4.xy;
    u_xlat16_5 = tex2Dbias(_DistortTexMask, float4(u_xlat5.xy, 0, _GlobalMipBias.x)).x;
    u_xlat9.xy = _MainSpeed.xy * _Time.yy + input.world.xy;
    u_xlat5.xy = u_xlat16_0.xx * float2(u_xlat16_5,u_xlat16_5) + u_xlat9.xy;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat5.xy, 0, _GlobalMipBias.x));
    u_xlat16_2.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(float3(_Contrast, _Contrast, _Contrast));
    u_xlat16_2.xyz = exp2(u_xlat16_2.xyz);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_Brightness,_Brightness,_Brightness);
    u_xlat16_3 = input.color * _MainColor;
    u_xlat16_2.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.w = u_xlat16_0.w * u_xlat16_3.w;
    u_xlat16_0 = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * u_xlat16_2;
    u_xlat1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat1.xy = u_xlat1.xy + -abs(input.mask.xy);
    u_xlat1.xy = u_xlat1.xy * input.mask.zw;
    u_xlat1.xy = clamp(u_xlat1.xy, 0.0, 1.0);
    u_xlat16_2.x = u_xlat1.y * u_xlat1.x;
    u_xlat16_6 = u_xlat16_0.w * u_xlat16_2.x + -0.00100000005;
    u_xlat16_2.x = u_xlat16_0.w * u_xlat16_2.x;
    result.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx;
    result.w = u_xlat16_2.x;
    u_xlatb1 = u_xlat16_6<0.0;
    if(u_xlatb1){clip(-1);}
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
