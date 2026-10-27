Shader "Zjcs/Official/World/Map13/Entity/UIEffectSequeue_ff983b36" {
Properties {
_Brightness ("Brightness", Float) = 1.0
_Contrast ("Contrast", Float) = 1.0
_MainColor ("Main Color", Color) = (1.0,1.0,1.0,1.0)
_MainTex ("Main Tex", 2D) = "white" {}
_MainPanner ("Main Panner XY", Vector) = (0.0,0.0,0.0,0.0)
[Header(SquenceMove)] _SquenceRow ("序列图行数", Float) = 1.0
_SquenceColumn ("序列图列数", Float) = 1.0
[Toggle(IsSqueueRecycle)] IsSqueueRecycle ("Is Squeue Recycle", Float) = 1.0
_SquenceTimes ("序列图播放次数", Float) = 1.0
_SquenceRate ("序列图播放进度", Range(0.0,1.0)) = 0.0
_SquenceSpeed ("序列图播放速度", Float) = 1.0
_Rotator ("旋转角度", Vector) = (0.0,0.0,0.0,0.0)
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
#pragma multi_compile __ IsSqueueRecycle
#pragma multi_compile_local __ UNITY_UI_ALPHACLIP
#pragma multi_compile_local __ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#if !defined(IsSqueueRecycle) && !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceRate;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float4 u_xlat2;
int u_xlati2;
half3 u_xlat16_3;
half u_xlat16_5;
half u_xlat16_8;
int u_xlati10;
half u_xlat16_12;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat16_8 = max(_SquenceRate, 0.0);
    u_xlat16_8 = min(u_xlat16_8, 0.999000013);
    u_xlati2 = _SquenceColumn * _SquenceRow;
    u_xlat16_12 = float(u_xlati2);
    u_xlat16_1.x = u_xlat16_12 * u_xlat16_8;
    u_xlat2.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat2.xy = float2(1.0, 1.0) / u_xlat2.xy;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    u_xlat16_1.x = floor(u_xlat16_1.x);
    u_xlati10 = int(u_xlat16_1.x);
    u_xlati10 = u_xlati10 * _SquenceColumn;
    u_xlat16_5 = float(u_xlati10);
    u_xlat16_8 = u_xlat16_8 * u_xlat16_12 + (-u_xlat16_5);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_3.x = u_xlat2.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat2.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_1.x) * u_xlat2.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat2.xy + u_xlat16_3.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_1.w = u_xlat16_0.w * _MainColor.w;
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_1.xyz = exp2(u_xlat16_1.xyz);
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_3.xyz;
    u_xlat2 = u_xlat16_1.wxyz * input.color.wxyz;
    result.xyz = u_xlat2.xxx * u_xlat2.yzw;
    result.w = u_xlat2.x;
    return result;

}
#elif defined(IsSqueueRecycle) && !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceSpeed;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float4 u_xlat2;
half3 u_xlat16_3;
float2 u_xlat6;
int u_xlati6;
half u_xlat16_8;
half u_xlat16_12;
int u_xlati14;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat2.x = _Time.y * _SquenceSpeed;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlati6 = _SquenceColumn * _SquenceRow;
    u_xlat16_8 = float(u_xlati6);
    u_xlat16_12 = u_xlat16_8 * u_xlat2.x;
    u_xlat6.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat6.xy = float2(1.0, 1.0) / u_xlat6.xy;
    u_xlat16_12 = u_xlat16_12 * u_xlat6.x;
    u_xlat16_12 = floor(u_xlat16_12);
    u_xlati14 = int(u_xlat16_12);
    u_xlati14 = u_xlati14 * _SquenceColumn;
    u_xlat16_1.x = float(u_xlati14);
    u_xlat16_8 = u_xlat2.x * u_xlat16_8 + (-u_xlat16_1.x);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_1.x = u_xlat6.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat6.y) + 1.0;
    u_xlat16_1.y = (-u_xlat16_12) * u_xlat6.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat6.xy + u_xlat16_1.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_1.w = u_xlat16_0.w * _MainColor.w;
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_1.xyz = exp2(u_xlat16_1.xyz);
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_3.xyz;
    u_xlat2 = u_xlat16_1.wxyz * input.color.wxyz;
    result.xyz = u_xlat2.xxx * u_xlat2.yzw;
    result.w = u_xlat2.x;
    return result;

}
#elif !defined(IsSqueueRecycle) && defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceRate;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half3 u_xlat16_1;
float2 u_xlat2;
int u_xlati2;
half3 u_xlat16_3;
half3 u_xlat16_5;
float3 u_xlat6;
bool u_xlatb6;
half u_xlat16_8;
int u_xlati10;
half u_xlat16_12;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat16_8 = max(_SquenceRate, 0.0);
    u_xlat16_8 = min(u_xlat16_8, 0.999000013);
    u_xlati2 = _SquenceColumn * _SquenceRow;
    u_xlat16_12 = float(u_xlati2);
    u_xlat16_1.x = u_xlat16_12 * u_xlat16_8;
    u_xlat2.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat2.xy = float2(1.0, 1.0) / u_xlat2.xy;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    u_xlat16_1.x = floor(u_xlat16_1.x);
    u_xlati10 = int(u_xlat16_1.x);
    u_xlati10 = u_xlati10 * _SquenceColumn;
    u_xlat16_5.x = float(u_xlati10);
    u_xlat16_8 = u_xlat16_8 * u_xlat16_12 + (-u_xlat16_5.x);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_3.x = u_xlat2.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat2.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_1.x) * u_xlat2.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat2.xy + u_xlat16_3.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = u_xlat16_0.w * _MainColor.w;
    u_xlat16_5.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_5.xyz = exp2(u_xlat16_5.xyz);
    u_xlat16_3.x = u_xlat16_1.x * input.color.w + -0.00100000005;
    u_xlat2.x = u_xlat16_1.x * input.color.w;
    u_xlatb6 = u_xlat16_3.x<0.0;
    if(u_xlatb6){clip(-1);}
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_1.xyz = u_xlat16_5.xyz * u_xlat16_3.xyz;
    u_xlat6.xyz = u_xlat16_1.xyz * input.color.xyz;
    result.xyz = u_xlat2.xxx * u_xlat6.xyz;
    result.w = u_xlat2.x;
    return result;

}
#elif defined(IsSqueueRecycle) && defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceSpeed;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half3 u_xlat16_1;
float2 u_xlat2;
half3 u_xlat16_3;
half3 u_xlat16_5;
float3 u_xlat6;
int u_xlati6;
bool u_xlatb6;
half u_xlat16_8;
half u_xlat16_12;
int u_xlati14;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat2.x = _Time.y * _SquenceSpeed;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlati6 = _SquenceColumn * _SquenceRow;
    u_xlat16_8 = float(u_xlati6);
    u_xlat16_12 = u_xlat16_8 * u_xlat2.x;
    u_xlat6.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat6.xy = float2(1.0, 1.0) / u_xlat6.xy;
    u_xlat16_12 = u_xlat16_12 * u_xlat6.x;
    u_xlat16_12 = floor(u_xlat16_12);
    u_xlati14 = int(u_xlat16_12);
    u_xlati14 = u_xlati14 * _SquenceColumn;
    u_xlat16_1.x = float(u_xlati14);
    u_xlat16_8 = u_xlat2.x * u_xlat16_8 + (-u_xlat16_1.x);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_1.x = u_xlat6.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat6.y) + 1.0;
    u_xlat16_1.y = (-u_xlat16_12) * u_xlat6.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat6.xy + u_xlat16_1.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = u_xlat16_0.w * _MainColor.w;
    u_xlat16_5.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_5.xyz = exp2(u_xlat16_5.xyz);
    u_xlat16_3.x = u_xlat16_1.x * input.color.w + -0.00100000005;
    u_xlat2.x = u_xlat16_1.x * input.color.w;
    u_xlatb6 = u_xlat16_3.x<0.0;
    if(u_xlatb6){clip(-1);}
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_1.xyz = u_xlat16_5.xyz * u_xlat16_3.xyz;
    u_xlat6.xyz = u_xlat16_1.xyz * input.color.xyz;
    result.xyz = u_xlat2.xxx * u_xlat6.xyz;
    result.w = u_xlat2.x;
    return result;

}
#elif !defined(IsSqueueRecycle) && !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceRate;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float4 u_xlat2;
int u_xlati2;
half3 u_xlat16_3;
half u_xlat16_5;
half u_xlat16_8;
int u_xlati10;
half u_xlat16_12;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat16_8 = max(_SquenceRate, 0.0);
    u_xlat16_8 = min(u_xlat16_8, 0.999000013);
    u_xlati2 = _SquenceColumn * _SquenceRow;
    u_xlat16_12 = float(u_xlati2);
    u_xlat16_1.x = u_xlat16_12 * u_xlat16_8;
    u_xlat2.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat2.xy = float2(1.0, 1.0) / u_xlat2.xy;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    u_xlat16_1.x = floor(u_xlat16_1.x);
    u_xlati10 = int(u_xlat16_1.x);
    u_xlati10 = u_xlati10 * _SquenceColumn;
    u_xlat16_5 = float(u_xlati10);
    u_xlat16_8 = u_xlat16_8 * u_xlat16_12 + (-u_xlat16_5);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_3.x = u_xlat2.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat2.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_1.x) * u_xlat2.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat2.xy + u_xlat16_3.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_1.w = u_xlat16_0.w * _MainColor.w;
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_1.xyz = exp2(u_xlat16_1.xyz);
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_3.xyz;
    u_xlat2 = u_xlat16_1.wxyz * input.color.wxyz;
    u_xlat16_1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_1.xy = u_xlat16_1.xy + -abs(input.mask.xy);
    u_xlat16_1.xy = u_xlat16_1.xy * input.mask.zw;
    u_xlat16_1.xy = clamp(u_xlat16_1.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.y * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    result.xyz = u_xlat16_1.xxx * u_xlat2.yzw;
    result.w = u_xlat16_1.x;
    return result;

}
#elif defined(IsSqueueRecycle) && !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceSpeed;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float4 u_xlat2;
half3 u_xlat16_3;
float2 u_xlat6;
int u_xlati6;
half u_xlat16_8;
half u_xlat16_12;
int u_xlati14;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat2.x = _Time.y * _SquenceSpeed;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlati6 = _SquenceColumn * _SquenceRow;
    u_xlat16_8 = float(u_xlati6);
    u_xlat16_12 = u_xlat16_8 * u_xlat2.x;
    u_xlat6.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat6.xy = float2(1.0, 1.0) / u_xlat6.xy;
    u_xlat16_12 = u_xlat16_12 * u_xlat6.x;
    u_xlat16_12 = floor(u_xlat16_12);
    u_xlati14 = int(u_xlat16_12);
    u_xlati14 = u_xlati14 * _SquenceColumn;
    u_xlat16_1.x = float(u_xlati14);
    u_xlat16_8 = u_xlat2.x * u_xlat16_8 + (-u_xlat16_1.x);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_1.x = u_xlat6.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat6.y) + 1.0;
    u_xlat16_1.y = (-u_xlat16_12) * u_xlat6.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat6.xy + u_xlat16_1.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_1.w = u_xlat16_0.w * _MainColor.w;
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_1.xyz = exp2(u_xlat16_1.xyz);
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_3.xyz;
    u_xlat2 = u_xlat16_1.wxyz * input.color.wxyz;
    u_xlat16_1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_1.xy = u_xlat16_1.xy + -abs(input.mask.xy);
    u_xlat16_1.xy = u_xlat16_1.xy * input.mask.zw;
    u_xlat16_1.xy = clamp(u_xlat16_1.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.y * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    result.xyz = u_xlat16_1.xxx * u_xlat2.yzw;
    result.w = u_xlat16_1.x;
    return result;

}
#elif !defined(IsSqueueRecycle) && defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceRate;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half2 u_xlat16_1;
float3 u_xlat2;
int u_xlati2;
bool u_xlatb2;
half3 u_xlat16_3;
half3 u_xlat16_5;
half u_xlat16_8;
int u_xlati10;
half u_xlat16_12;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat16_8 = max(_SquenceRate, 0.0);
    u_xlat16_8 = min(u_xlat16_8, 0.999000013);
    u_xlati2 = _SquenceColumn * _SquenceRow;
    u_xlat16_12 = float(u_xlati2);
    u_xlat16_1.x = u_xlat16_12 * u_xlat16_8;
    u_xlat2.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat2.xy = float2(1.0, 1.0) / u_xlat2.xy;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    u_xlat16_1.x = floor(u_xlat16_1.x);
    u_xlati10 = int(u_xlat16_1.x);
    u_xlati10 = u_xlati10 * _SquenceColumn;
    u_xlat16_5.x = float(u_xlati10);
    u_xlat16_8 = u_xlat16_8 * u_xlat16_12 + (-u_xlat16_5.x);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_3.x = u_xlat2.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat2.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_1.x) * u_xlat2.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat2.xy + u_xlat16_3.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = u_xlat16_0.w * _MainColor.w;
    u_xlat16_5.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_5.xyz = exp2(u_xlat16_5.xyz);
    u_xlat2.x = u_xlat16_1.x * input.color.w;
    u_xlat16_3.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_3.xy = u_xlat16_3.xy + -abs(input.mask.xy);
    u_xlat16_3.xy = u_xlat16_3.xy * input.mask.zw;
    u_xlat16_3.xy = clamp(u_xlat16_3.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_3.y * u_xlat16_3.x;
    u_xlat16_3.x = u_xlat2.x * u_xlat16_1.x + -0.00100000005;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    u_xlatb2 = u_xlat16_3.x<0.0;
    if(u_xlatb2){clip(-1);}
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_5.xyz = u_xlat16_5.xyz * u_xlat16_3.xyz;
    u_xlat2.xyz = u_xlat16_5.xyz * input.color.xyz;
    result.xyz = u_xlat16_1.xxx * u_xlat2.xyz;
    result.w = u_xlat16_1.x;
    return result;

}
#elif defined(IsSqueueRecycle) && defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

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
half _Brightness;
half _Contrast;
half _SquenceSpeed;
half2 _MainPanner;
half2 _Rotator;
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
half4 _ClipRect;
half4 _MainColor;
half4 _MainTex_ST;
int _SquenceColumn;
int _SquenceRow;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half2 u_xlat16_1;
float3 u_xlat2;
bool u_xlatb2;
half3 u_xlat16_3;
half3 u_xlat16_5;
float2 u_xlat6;
int u_xlati6;
half u_xlat16_8;
half u_xlat16_12;
int u_xlati14;

    u_xlat16_0.xy = input.uv.xy + float2(-0.5, -0.5);
    u_xlat16_8 = u_xlat16_0.y * _Rotator.y;
    u_xlat16_1.x = u_xlat16_0.x * _Rotator.x + (-u_xlat16_8);
    u_xlat16_1.y = dot(u_xlat16_0.yx, _Rotator.xy);
    u_xlat16_0.xy = u_xlat16_1.xy + float2(0.5, 0.5);
    u_xlat2.x = _Time.y * _SquenceSpeed;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlati6 = _SquenceColumn * _SquenceRow;
    u_xlat16_8 = float(u_xlati6);
    u_xlat16_12 = u_xlat16_8 * u_xlat2.x;
    u_xlat6.xy = float2(int2(_SquenceColumn, _SquenceRow));
    u_xlat6.xy = float2(1.0, 1.0) / u_xlat6.xy;
    u_xlat16_12 = u_xlat16_12 * u_xlat6.x;
    u_xlat16_12 = floor(u_xlat16_12);
    u_xlati14 = int(u_xlat16_12);
    u_xlati14 = u_xlati14 * _SquenceColumn;
    u_xlat16_1.x = float(u_xlati14);
    u_xlat16_8 = u_xlat2.x * u_xlat16_8 + (-u_xlat16_1.x);
    u_xlat16_8 = trunc(u_xlat16_8);
    u_xlat16_1.x = u_xlat6.x * u_xlat16_8;
    u_xlat16_8 = (-u_xlat6.y) + 1.0;
    u_xlat16_1.y = (-u_xlat16_12) * u_xlat6.y + u_xlat16_8;
    u_xlat2.xy = u_xlat16_0.xy * u_xlat6.xy + u_xlat16_1.xy;
    u_xlat2.xy = float2(_MainPanner.x, _MainPanner.y) * _Time.yy + u_xlat2.xy;
    u_xlat2.xy = u_xlat2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat2.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = u_xlat16_0.w * _MainColor.w;
    u_xlat16_5.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(_Contrast,_Contrast,_Contrast);
    u_xlat16_5.xyz = exp2(u_xlat16_5.xyz);
    u_xlat2.x = u_xlat16_1.x * input.color.w;
    u_xlat16_3.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_3.xy = u_xlat16_3.xy + -abs(input.mask.xy);
    u_xlat16_3.xy = u_xlat16_3.xy * input.mask.zw;
    u_xlat16_3.xy = clamp(u_xlat16_3.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_3.y * u_xlat16_3.x;
    u_xlat16_3.x = u_xlat2.x * u_xlat16_1.x + -0.00100000005;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat2.x;
    u_xlatb2 = u_xlat16_3.x<0.0;
    if(u_xlatb2){clip(-1);}
    u_xlat16_3.xyz = float3(_Brightness,_Brightness,_Brightness) * _MainColor.xyz;
    u_xlat16_5.xyz = u_xlat16_5.xyz * u_xlat16_3.xyz;
    u_xlat2.xyz = u_xlat16_5.xyz * input.color.xyz;
    result.xyz = u_xlat16_1.xxx * u_xlat2.xyz;
    result.w = u_xlat16_1.x;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
