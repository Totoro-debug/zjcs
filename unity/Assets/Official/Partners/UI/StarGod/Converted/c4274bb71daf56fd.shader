Shader "UI/EffectDissolve2" {
Properties {

_BaseColor ("基础颜色", Color) = (1,1,1,1)

_MainTex ("基础贴图", 2D) = "white" { }

_DissolveTex ("溶解贴图", 2D) = "white" { }

_DissolveMultX ("对X轴定向溶解", Range(-1, 1)) = 0.0

_DissolveMultY ("对Y轴定向溶解", Range(-1, 1)) = 0.0

_DissolveRotator ("溶解旋转", Vector) = (0,0,0,0)

_DissolvePanner ("溶解贴图流动XY", Vector) = (0,0,0,0)

_DisSoftness ("溶解虚边", Range(0.1, 2)) = 0.10000000149011612

_AnDissolve ("溶解强度(用于k帧)", Float) = 0.0

_EdgeWidth ("边缘宽度 _EdgeWidth", Range(0, 1)) = 0.5

_EdgeColor1 ("边缘颜色1 _EdgeColor1", Color) = (1,1,1,1)

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
LOD 0
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Name "EffectDissolve"
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderType"="Transparent" }
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
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 color:COLOR0; float4 mask:TEXCOORD2; };

float _AnDissolve;
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
half _DisSoftness;
half _DissolveMultX;
half _DissolveMultY;
half _EdgeWidth;
half2 _DissolvePanner;
half2 _DissolveRotator;
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
half4 _BaseColor;
half4 _ClipRect;
half4 _DissolveTex_ST;
half4 _EdgeColor1;
half4 _MainTex_ST;
sampler2D _DissolveTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

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
    output.color = input.color;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX,_UIMaskSoftnessY) * float2(0.25,0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25,0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0,2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half4 u_xlat16_0;
float2 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
bool u_xlatb4;
half u_xlat16_6;
float2 u_xlat8;
half u_xlat16_10;

    u_xlat0.xy = _DissolvePanner.xy * _Time.yy + input.uv.xy;
    u_xlat0.xy = u_xlat0.xy + float2(-0.5,-0.5);
    u_xlat8.x = u_xlat0.y * _DissolveRotator.xxxy.w;
    u_xlat1.x = u_xlat0.x * _DissolveRotator.xxxy.z + (-u_xlat8.x);
    u_xlat1.y = dot(u_xlat0.yx, float2(_DissolveRotator.x,_DissolveRotator.y));
    u_xlat0.xy = u_xlat1.xy + float2(0.5,0.5);
    u_xlat0.xy = u_xlat0.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
    u_xlat16_0.xy = tex2Dbias(_DissolveTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).xw;
    u_xlat16_2.xy = ceil(float2(_DissolveMultX,_DissolveMultY));
    u_xlat8.xy = u_xlat16_2.xy * input.uv.xy;
    u_xlat8.x = u_xlat8.x * abs(_DissolveMultX);
    u_xlat16_2.x = u_xlat16_0.x * u_xlat16_0.y + (-u_xlat8.x);
    u_xlat16_2.x = -abs(_DissolveMultY) * u_xlat8.y + u_xlat16_2.x;
    u_xlat0.x = u_xlat16_2.x + (-_AnDissolve);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _DisSoftness;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat16_2.x = u_xlat0.x + -0.00100000005;
    u_xlatb4 = u_xlat16_2.x<0.0;
    if(u_xlatb4){clip(-1);}
    u_xlat16_2.x = float(1.0) / _EdgeWidth;
    u_xlat16_2.x = u_xlat0.x * u_xlat16_2.x;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.world.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = input.color * _BaseColor;
    u_xlat16_6 = u_xlat16_0.w * u_xlat16_1.w;
    u_xlat16_6 = u_xlat16_6 * _EdgeColor1.w;
    u_xlat16_10 = u_xlat16_0.w * u_xlat16_1.w + (-u_xlat16_6);
    u_xlat16_6 = u_xlat16_2.x * u_xlat16_10 + u_xlat16_6;
    u_xlat16_3.xyz = u_xlat16_0.xyz * u_xlat16_1.xyz + (-_EdgeColor1.xyz);
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_3.xyz + _EdgeColor1.xyz;
    result.xyz = (float3)(u_xlat16_6) * u_xlat16_2.xzw;
    result.w = u_xlat16_6;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 color:COLOR0; float4 mask:TEXCOORD2; };

float _AnDissolve;
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
half _DisSoftness;
half _DissolveMultX;
half _DissolveMultY;
half _EdgeWidth;
half2 _DissolvePanner;
half2 _DissolveRotator;
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
half4 _BaseColor;
half4 _ClipRect;
half4 _DissolveTex_ST;
half4 _EdgeColor1;
half4 _MainTex_ST;
sampler2D _DissolveTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

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
    output.color = input.color;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX,_UIMaskSoftnessY) * float2(0.25,0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25,0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0,2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
float2 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
bool u_xlatb4;
half u_xlat16_6;
float2 u_xlat8;
half u_xlat16_10;

    u_xlat0.xy = _DissolvePanner.xy * _Time.yy + input.uv.xy;
    u_xlat0.xy = u_xlat0.xy + float2(-0.5,-0.5);
    u_xlat8.x = u_xlat0.y * _DissolveRotator.xxxy.w;
    u_xlat1.x = u_xlat0.x * _DissolveRotator.xxxy.z + (-u_xlat8.x);
    u_xlat1.y = dot(u_xlat0.yx, float2(_DissolveRotator.x,_DissolveRotator.y));
    u_xlat0.xy = u_xlat1.xy + float2(0.5,0.5);
    u_xlat0.xy = u_xlat0.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
    u_xlat16_0.xy = tex2Dbias(_DissolveTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).xw;
    u_xlat16_2.xy = ceil(float2(_DissolveMultX,_DissolveMultY));
    u_xlat8.xy = u_xlat16_2.xy * input.uv.xy;
    u_xlat8.x = u_xlat8.x * abs(_DissolveMultX);
    u_xlat16_2.x = u_xlat16_0.x * u_xlat16_0.y + (-u_xlat8.x);
    u_xlat16_2.x = -abs(_DissolveMultY) * u_xlat8.y + u_xlat16_2.x;
    u_xlat0.x = u_xlat16_2.x + (-_AnDissolve);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _DisSoftness;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat16_2.x = u_xlat0.x + -0.00100000005;
    u_xlatb4 = u_xlat16_2.x<0.0;
    if(u_xlatb4){clip(-1);}
    u_xlat16_2.x = float(1.0) / _EdgeWidth;
    u_xlat16_2.x = u_xlat0.x * u_xlat16_2.x;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.world.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = input.color * _BaseColor;
    u_xlat16_6 = u_xlat16_0.w * u_xlat16_1.w;
    u_xlat16_6 = u_xlat16_6 * _EdgeColor1.w;
    u_xlat16_10 = u_xlat16_0.w * u_xlat16_1.w + (-u_xlat16_6);
    u_xlat16_6 = u_xlat16_2.x * u_xlat16_10 + u_xlat16_6;
    u_xlat16_3.xyz = u_xlat16_0.xyz * u_xlat16_1.xyz + (-_EdgeColor1.xyz);
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_3.xyz + _EdgeColor1.xyz;
    result.xyz = (float3)(u_xlat16_6) * u_xlat16_2.xzw;
    u_xlat16_2.x = u_xlat16_6 + -0.00100000005;
    result.w = u_xlat16_6;
    u_xlatb0 = u_xlat16_2.x<0.0;
    if(u_xlatb0){clip(-1);}
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 color:COLOR0; float4 mask:TEXCOORD2; };

float _AnDissolve;
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
half _DisSoftness;
half _DissolveMultX;
half _DissolveMultY;
half _EdgeWidth;
half2 _DissolvePanner;
half2 _DissolveRotator;
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
half4 _BaseColor;
half4 _ClipRect;
half4 _DissolveTex_ST;
half4 _EdgeColor1;
half4 _MainTex_ST;
sampler2D _DissolveTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

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
    output.color = input.color;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX,_UIMaskSoftnessY) * float2(0.25,0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25,0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0,2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half4 u_xlat16_0;
float2 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
bool u_xlatb4;
half u_xlat16_6;
float2 u_xlat8;
half u_xlat16_10;

    u_xlat0.xy = _DissolvePanner.xy * _Time.yy + input.uv.xy;
    u_xlat0.xy = u_xlat0.xy + float2(-0.5,-0.5);
    u_xlat8.x = u_xlat0.y * _DissolveRotator.xxxy.w;
    u_xlat1.x = u_xlat0.x * _DissolveRotator.xxxy.z + (-u_xlat8.x);
    u_xlat1.y = dot(u_xlat0.yx, float2(_DissolveRotator.x,_DissolveRotator.y));
    u_xlat0.xy = u_xlat1.xy + float2(0.5,0.5);
    u_xlat0.xy = u_xlat0.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
    u_xlat16_0.xy = tex2Dbias(_DissolveTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).xw;
    u_xlat16_2.xy = ceil(float2(_DissolveMultX,_DissolveMultY));
    u_xlat8.xy = u_xlat16_2.xy * input.uv.xy;
    u_xlat8.x = u_xlat8.x * abs(_DissolveMultX);
    u_xlat16_2.x = u_xlat16_0.x * u_xlat16_0.y + (-u_xlat8.x);
    u_xlat16_2.x = -abs(_DissolveMultY) * u_xlat8.y + u_xlat16_2.x;
    u_xlat0.x = u_xlat16_2.x + (-_AnDissolve);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _DisSoftness;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat16_2.x = u_xlat0.x + -0.00100000005;
    u_xlatb4 = u_xlat16_2.x<0.0;
    if(u_xlatb4){clip(-1);}
    u_xlat16_2.x = float(1.0) / _EdgeWidth;
    u_xlat16_2.x = u_xlat0.x * u_xlat16_2.x;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.world.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = input.color * _BaseColor;
    u_xlat16_6 = u_xlat16_0.w * u_xlat16_1.w;
    u_xlat16_6 = u_xlat16_6 * _EdgeColor1.w;
    u_xlat16_10 = u_xlat16_0.w * u_xlat16_1.w + (-u_xlat16_6);
    u_xlat16_6 = u_xlat16_2.x * u_xlat16_10 + u_xlat16_6;
    u_xlat16_3.xyz = u_xlat16_0.xyz * u_xlat16_1.xyz + (-_EdgeColor1.xyz);
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_3.xyz + _EdgeColor1.xyz;
    u_xlat16_3.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_3.xy = u_xlat16_3.xy + -abs(input.mask.xy);
    u_xlat16_3.xy = u_xlat16_3.xy * input.mask.zw;
    u_xlat16_3.xy = clamp(u_xlat16_3.xy, 0.0, 1.0);
    u_xlat16_3.x = u_xlat16_3.y * u_xlat16_3.x;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_3.x;
    result.xyz = (float3)(u_xlat16_6) * u_xlat16_2.xzw;
    result.w = u_xlat16_6;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 color:COLOR0; float4 mask:TEXCOORD2; };

float _AnDissolve;
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
half _DisSoftness;
half _DissolveMultX;
half _DissolveMultY;
half _EdgeWidth;
half2 _DissolvePanner;
half2 _DissolveRotator;
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
half4 _BaseColor;
half4 _ClipRect;
half4 _DissolveTex_ST;
half4 _EdgeColor1;
half4 _MainTex_ST;
sampler2D _DissolveTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

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
    output.color = input.color;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX,_UIMaskSoftnessY) * float2(0.25,0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25,0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0,2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
float2 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
bool u_xlatb4;
half u_xlat16_6;
half u_xlat16_7;
float2 u_xlat8;
half u_xlat16_10;

    u_xlat0.xy = _DissolvePanner.xy * _Time.yy + input.uv.xy;
    u_xlat0.xy = u_xlat0.xy + float2(-0.5,-0.5);
    u_xlat8.x = u_xlat0.y * _DissolveRotator.xxxy.w;
    u_xlat1.x = u_xlat0.x * _DissolveRotator.xxxy.z + (-u_xlat8.x);
    u_xlat1.y = dot(u_xlat0.yx, float2(_DissolveRotator.x,_DissolveRotator.y));
    u_xlat0.xy = u_xlat1.xy + float2(0.5,0.5);
    u_xlat0.xy = u_xlat0.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
    u_xlat16_0.xy = tex2Dbias(_DissolveTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).xw;
    u_xlat16_2.xy = ceil(float2(_DissolveMultX,_DissolveMultY));
    u_xlat8.xy = u_xlat16_2.xy * input.uv.xy;
    u_xlat8.x = u_xlat8.x * abs(_DissolveMultX);
    u_xlat16_2.x = u_xlat16_0.x * u_xlat16_0.y + (-u_xlat8.x);
    u_xlat16_2.x = -abs(_DissolveMultY) * u_xlat8.y + u_xlat16_2.x;
    u_xlat0.x = u_xlat16_2.x + (-_AnDissolve);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _DisSoftness;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat16_2.x = u_xlat0.x + -0.00100000005;
    u_xlatb4 = u_xlat16_2.x<0.0;
    if(u_xlatb4){clip(-1);}
    u_xlat16_2.x = float(1.0) / _EdgeWidth;
    u_xlat16_2.x = u_xlat0.x * u_xlat16_2.x;
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_6 = u_xlat16_2.x * -2.0 + 3.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_6;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.world.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = input.color * _BaseColor;
    u_xlat16_6 = u_xlat16_0.w * u_xlat16_1.w;
    u_xlat16_6 = u_xlat16_6 * _EdgeColor1.w;
    u_xlat16_10 = u_xlat16_0.w * u_xlat16_1.w + (-u_xlat16_6);
    u_xlat16_6 = u_xlat16_2.x * u_xlat16_10 + u_xlat16_6;
    u_xlat16_3.xyz = u_xlat16_0.xyz * u_xlat16_1.xyz + (-_EdgeColor1.xyz);
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_3.xyz + _EdgeColor1.xyz;
    u_xlat16_3.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_3.xy = u_xlat16_3.xy + -abs(input.mask.xy);
    u_xlat16_3.xy = u_xlat16_3.xy * input.mask.zw;
    u_xlat16_3.xy = clamp(u_xlat16_3.xy, 0.0, 1.0);
    u_xlat16_3.x = u_xlat16_3.y * u_xlat16_3.x;
    u_xlat16_7 = u_xlat16_6 * u_xlat16_3.x + -0.00100000005;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_3.x;
    u_xlatb0 = u_xlat16_7<0.0;
    if(u_xlatb0){clip(-1);}
    result.xyz = (float3)(u_xlat16_6) * u_xlat16_2.xzw;
    result.w = u_xlat16_6;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
