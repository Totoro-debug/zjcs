Shader "Zjcs/Official/World/Map13/BoundEntity/UniversalRenderPipeline2DSpriteLitFording_e257809f" {
Properties {
_MainTex ("Diffuse", 2D) = "white" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
_RendererColor ("RendererColor", Color) = (1.0,1.0,1.0,1.0)
_Flip ("Flip", Vector) = (1.0,1.0,1.0,1.0)
_AlphaTex ("External Alpha", 2D) = "white" {}
_EnableExternalAlpha ("Enable External Alpha", Float) = 0.0
_ZWrite ("Z Write", Float) = 0.0
_LightAtten ("Light Atten", Range(0.0,1.0)) = 0.0
[Toggle(_DISSOLVE)] _DissolveEnable ("Dissolve", Float) = 0.0
_DissolveTex ("DissolveTex", 2D) = "black" {}
_DissolveThreshold ("Threshold", Range(0.0,1.0)) = 0.0
_DissolveLineWidth ("Line Width", Range(0.0,0.20000000298023224)) = 0.10000000149011612
_DissolveColor ("Dissolve Color", Color) = (0.0,0.0,1.0,1.0)
_DissolveOffsetThreshold ("Vertex Offset Threshold", Range(0.0,1.0)) = 0.5
_DissolveOffset ("Vertex Offset", Vector) = (0.0,0.0,0.0,0.0)
_HitEffectColor ("Hit Effect Color", Color) = (1.0,1.0,1.0,1.0)
_HitEffectBlend ("Hit Effect Blend", Range(0.0,1.0)) = 0.0
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite [_ZWrite]
ZTest LEqual
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#pragma multi_compile_local __ _DISSOLVE
#pragma multi_compile_local __ _HITEFFECT
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_DISSOLVE) && !defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

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
float4 _FordingParam;
half _FordingLevel;
half _HDREmulationScale;
half _LightAtten;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _FordingTex;
sampler2D _MainTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half3 u_xlat16_1;
half4 u_xlat16_2;
float2 u_xlat3;
half u_xlat16_3;
half3 u_xlat16_4;
float u_xlat5;
float u_xlat8;
float u_xlat10;
float u_xlat15;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb15 = u_xlat16_2.x<0.0;
    if(u_xlatb15){clip(-1);}
    u_xlatb15 = u_xlat1.w==0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat15 = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat3.x = 0.0;
    u_xlat3.y = _Time.y * _FordingParam.y;
    u_xlat3.xy = input.mask.xy * _FordingParam.xx + u_xlat3.xy;
    u_xlat16_3 = tex2Dbias(_FordingTex, float4(u_xlat3.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_3 * 2.0 + -1.0;
    u_xlat3.x = u_xlat16_2.x * _FordingParam.z + _FordingLevel;
    u_xlat8 = (-u_xlat15) + u_xlat3.x;
    u_xlat8 = u_xlat8 / u_xlat3.x;
    u_xlat8 = clamp(u_xlat8, 0.0, 1.0);
    u_xlat16_2.xyz = float3(_DeepColor.x, _DeepColor.y, _DeepColor.z) + (-_ShallowColor.xyz);
    u_xlat16_2.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_2.xyz + _ShallowColor.xyz;
    u_xlat16_2.xyz = (-input.color.xyz) * u_xlat16_0.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_2.xyz + u_xlat1.xyz;
    u_xlatb0.x = u_xlat3.x>=u_xlat15;
    u_xlatb0.y = _FordingParam.w>=u_xlat8;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat5 = float(1.0) / _FordingParam.w;
    u_xlat5 = u_xlat5 * u_xlat8;
    u_xlat5 = clamp(u_xlat5, 0.0, 1.0);
    u_xlat10 = u_xlat5 * -2.0 + 3.0;
    u_xlat5 = u_xlat5 * u_xlat5;
    u_xlat5 = u_xlat5 * u_xlat10;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb15 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xxx;
    u_xlat16_2.xyz = (bool(u_xlatb15)) ? u_xlat16_2.xyz : u_xlat16_1.xyz;
    u_xlat16_4.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.xxyx.yyy;
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y);
    u_xlat16_2.xyz = u_xlat0.xyz * u_xlat16_4.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat0.xyz);
    u_xlat16_2.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlat16_2.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_4.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_DISSOLVE) && defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

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
float4 _FordingParam;
half _FordingLevel;
half _HDREmulationScale;
half _HitEffectBlend;
half _LightAtten;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _HitEffectColor;
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _FordingTex;
sampler2D _MainTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half3 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat4;
half u_xlat16_4;
half3 u_xlat16_6;
float u_xlat8;
bool u_xlatb12;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb12 = u_xlat16_2.x<0.0;
    if(u_xlatb12){clip(-1);}
    u_xlatb12 = u_xlat1.w==0.0;
    if(u_xlatb12){clip(-1);}
    u_xlat16_2.x = _HitEffectColor.w * _HitEffectBlend;
    u_xlat16_6.xyz = (-input.color.xyz) * u_xlat16_0.xyz + _HitEffectColor.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xxx * u_xlat16_6.xyz + u_xlat1.xyz;
    u_xlat0.x = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _FordingParam.y;
    u_xlat4.xy = input.mask.xy * _FordingParam.xx + u_xlat1.xy;
    u_xlat16_4 = tex2Dbias(_FordingTex, float4(u_xlat4.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_14 = u_xlat16_4 * 2.0 + -1.0;
    u_xlat4.x = u_xlat16_14 * _FordingParam.z + _FordingLevel;
    u_xlat8 = (-u_xlat0.x) + u_xlat4.x;
    u_xlat8 = u_xlat8 / u_xlat4.x;
    u_xlat8 = clamp(u_xlat8, 0.0, 1.0);
    u_xlat16_3.xyz = float3(_DeepColor.x, _DeepColor.y, _DeepColor.z) + (-_ShallowColor.xyz);
    u_xlat16_3.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_3.xyz + _ShallowColor.xyz;
    u_xlat16_3.xyz = (-u_xlat16_2.xyz) + u_xlat16_3.xyz;
    u_xlat16_2.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_3.xyz + u_xlat16_2.xyz;
    u_xlatb0.x = u_xlat4.x>=u_xlat0.x;
    u_xlatb0.y = _FordingParam.w>=u_xlat8;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat4.x = float(1.0) / _FordingParam.w;
    u_xlat4.x = u_xlat4.x * u_xlat8;
    u_xlat4.x = clamp(u_xlat4.x, 0.0, 1.0);
    u_xlat8 = u_xlat4.x * -2.0 + 3.0;
    u_xlat4.x = u_xlat4.x * u_xlat4.x;
    u_xlat4.x = u_xlat4.x * u_xlat8;
    u_xlat0.x = u_xlat4.x * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb12 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xxx;
    u_xlat16_2.xyz = (bool(u_xlatb12)) ? u_xlat16_2.xyz : u_xlat16_1.xyz;
    u_xlat16_3.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.xxyx.yyy;
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y);
    u_xlat16_2.xyz = u_xlat0.xyz * u_xlat16_3.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat0.xyz);
    u_xlat16_2.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlat16_2.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_3.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_3.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_DISSOLVE) && !defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

float _DissolveLineWidth;
float _DissolveThreshold;
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
float4 _DissolveColor;
float4 _FordingParam;
half _FordingLevel;
half _HDREmulationScale;
half _LightAtten;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _DissolveTex;
sampler2D _FordingTex;
sampler2D _MainTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half3 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;
bool u_xlatb3;
half3 u_xlat16_4;
float u_xlat5;
float u_xlat6;
float u_xlat10;
float u_xlat15;
half u_xlat16_15;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb15 = u_xlat16_2.x<0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat16_15 = tex2Dbias(_DissolveTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat15 = u_xlat16_15 + (-_DissolveThreshold);
    u_xlatb3 = u_xlat15<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlatb3 = u_xlat1.w==0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat3 = float(1.0) / _DissolveLineWidth;
    u_xlat15 = u_xlat15 * u_xlat3;
    u_xlat15 = clamp(u_xlat15, 0.0, 1.0);
    u_xlat3 = u_xlat15 * -2.0 + 3.0;
    u_xlat15 = u_xlat15 * u_xlat15;
    u_xlat15 = (-u_xlat3) * u_xlat15 + 1.0;
    u_xlatb3 = _DissolveThreshold>=0.00100000005;
    u_xlat3 = u_xlatb3 ? 1.0 : float(0.0);
    u_xlat15 = u_xlat15 * u_xlat3;
    u_xlat0.xyz = (-input.color.xyz) * u_xlat16_0.xyz + _DissolveColor.xyz;
    u_xlat0.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat0.xyz + u_xlat1.xyz;
    u_xlat15 = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _FordingParam.y;
    u_xlat1.xy = input.mask.xy * _FordingParam.xx + u_xlat1.xy;
    u_xlat16_1.x = tex2Dbias(_FordingTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_1.x * 2.0 + -1.0;
    u_xlat1.x = u_xlat16_2.x * _FordingParam.z + _FordingLevel;
    u_xlat6 = (-u_xlat15) + u_xlat1.x;
    u_xlat6 = u_xlat6 / u_xlat1.x;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat16_2.xyz = float3(_DeepColor.x, _DeepColor.y, _DeepColor.z) + (-_ShallowColor.xyz);
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + _ShallowColor.xyz;
    u_xlat16_2.xyz = (-u_xlat0.xyz) + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlatb0.x = u_xlat1.x>=u_xlat15;
    u_xlatb0.y = _FordingParam.w>=u_xlat6;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat5 = float(1.0) / _FordingParam.w;
    u_xlat5 = u_xlat5 * u_xlat6;
    u_xlat5 = clamp(u_xlat5, 0.0, 1.0);
    u_xlat10 = u_xlat5 * -2.0 + 3.0;
    u_xlat5 = u_xlat5 * u_xlat5;
    u_xlat5 = u_xlat5 * u_xlat10;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb15 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xxx;
    u_xlat16_2.xyz = (bool(u_xlatb15)) ? u_xlat16_2.xyz : u_xlat16_1.xyz;
    u_xlat16_4.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.xxyx.yyy;
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y);
    u_xlat16_2.xyz = u_xlat0.xyz * u_xlat16_4.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat0.xyz);
    u_xlat16_2.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlat16_2.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_4.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_DISSOLVE) && defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

float _DissolveLineWidth;
float _DissolveThreshold;
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
float4 _DissolveColor;
float4 _FordingParam;
half _FordingLevel;
half _HDREmulationScale;
half _HitEffectBlend;
half _LightAtten;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _HitEffectColor;
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _DissolveTex;
sampler2D _FordingTex;
sampler2D _MainTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half3 u_xlat16_1;
half4 u_xlat16_2;
bool u_xlatb3;
half3 u_xlat16_4;
float3 u_xlat5;
bool u_xlatb5;
float u_xlat6;
half3 u_xlat16_7;
float u_xlat10;
float u_xlat15;
half u_xlat16_15;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb15 = u_xlat16_2.x<0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat16_15 = tex2Dbias(_DissolveTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat15 = u_xlat16_15 + (-_DissolveThreshold);
    u_xlatb3 = u_xlat15<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlatb3 = u_xlat1.w==0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat16_2.x = _HitEffectColor.w * _HitEffectBlend;
    u_xlat16_7.xyz = (-input.color.xyz) * u_xlat16_0.xyz + _HitEffectColor.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xxx * u_xlat16_7.xyz + u_xlat1.xyz;
    u_xlat0.x = float(1.0) / _DissolveLineWidth;
    u_xlat0.x = u_xlat0.x * u_xlat15;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat5.x = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = (-u_xlat5.x) * u_xlat0.x + 1.0;
    u_xlatb5 = _DissolveThreshold>=0.00100000005;
    u_xlat5.x = u_xlatb5 ? 1.0 : float(0.0);
    u_xlat0.x = u_xlat5.x * u_xlat0.x;
    u_xlat5.xyz = (-u_xlat16_2.xyz) + _DissolveColor.xyz;
    u_xlat0.xyz = u_xlat0.xxx * u_xlat5.xyz + u_xlat16_2.xyz;
    u_xlat15 = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _FordingParam.y;
    u_xlat1.xy = input.mask.xy * _FordingParam.xx + u_xlat1.xy;
    u_xlat16_1.x = tex2Dbias(_FordingTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_1.x * 2.0 + -1.0;
    u_xlat1.x = u_xlat16_2.x * _FordingParam.z + _FordingLevel;
    u_xlat6 = (-u_xlat15) + u_xlat1.x;
    u_xlat6 = u_xlat6 / u_xlat1.x;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat16_2.xyz = float3(_DeepColor.x, _DeepColor.y, _DeepColor.z) + (-_ShallowColor.xyz);
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + _ShallowColor.xyz;
    u_xlat16_2.xyz = (-u_xlat0.xyz) + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlatb0.x = u_xlat1.x>=u_xlat15;
    u_xlatb0.y = _FordingParam.w>=u_xlat6;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat5.x = float(1.0) / _FordingParam.w;
    u_xlat5.x = u_xlat5.x * u_xlat6;
    u_xlat5.x = clamp(u_xlat5.x, 0.0, 1.0);
    u_xlat10 = u_xlat5.x * -2.0 + 3.0;
    u_xlat5.x = u_xlat5.x * u_xlat5.x;
    u_xlat5.x = u_xlat5.x * u_xlat10;
    u_xlat0.x = u_xlat5.x * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb15 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xxx;
    u_xlat16_2.xyz = (bool(u_xlatb15)) ? u_xlat16_2.xyz : u_xlat16_1.xyz;
    u_xlat16_4.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.xxyx.yyy;
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y);
    u_xlat16_2.xyz = u_xlat0.xyz * u_xlat16_4.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat0.xyz);
    u_xlat16_2.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlat16_2.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_4.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_DISSOLVE) && !defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

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
float4 _FordingParam;
half _FordingLevel;
half _LightAtten;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _MainTex_ST;
sampler2D _FordingTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half3 u_xlat16_2;
float2 u_xlat3;
half u_xlat16_3;
half3 u_xlat16_4;
float u_xlat5;
float u_xlat8;
float u_xlat10;
float u_xlat15;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb15 = u_xlat16_2.x<0.0;
    if(u_xlatb15){clip(-1);}
    u_xlatb15 = u_xlat1.w==0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat15 = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat3.x = 0.0;
    u_xlat3.y = _Time.y * _FordingParam.y;
    u_xlat3.xy = input.mask.xy * _FordingParam.xx + u_xlat3.xy;
    u_xlat16_3 = tex2Dbias(_FordingTex, float4(u_xlat3.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_3 * 2.0 + -1.0;
    u_xlat3.x = u_xlat16_2.x * _FordingParam.z + _FordingLevel;
    u_xlat8 = (-u_xlat15) + u_xlat3.x;
    u_xlat8 = u_xlat8 / u_xlat3.x;
    u_xlat8 = clamp(u_xlat8, 0.0, 1.0);
    u_xlat16_2.xyz = _DeepColor.xyz + (-_ShallowColor.xyz);
    u_xlat16_2.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_2.xyz + _ShallowColor.xyz;
    u_xlat16_2.xyz = (-input.color.xyz) * u_xlat16_0.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_2.xyz + u_xlat1.xyz;
    u_xlatb0.x = u_xlat3.x>=u_xlat15;
    u_xlatb0.y = _FordingParam.w>=u_xlat8;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat5 = float(1.0) / _FordingParam.w;
    u_xlat5 = u_xlat5 * u_xlat8;
    u_xlat5 = clamp(u_xlat5, 0.0, 1.0);
    u_xlat10 = u_xlat5 * -2.0 + 3.0;
    u_xlat5 = u_xlat5 * u_xlat5;
    u_xlat5 = u_xlat5 * u_xlat10;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = max(u_xlat0.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_4.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_DISSOLVE) && defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

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
float4 _FordingParam;
half _FordingLevel;
half _HitEffectBlend;
half _LightAtten;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _HitEffectColor;
half4 _MainTex_ST;
sampler2D _FordingTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat4;
half u_xlat16_4;
half3 u_xlat16_6;
float u_xlat8;
bool u_xlatb12;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb12 = u_xlat16_2.x<0.0;
    if(u_xlatb12){clip(-1);}
    u_xlatb12 = u_xlat1.w==0.0;
    if(u_xlatb12){clip(-1);}
    u_xlat16_2.x = _HitEffectColor.w * _HitEffectBlend;
    u_xlat16_6.xyz = (-input.color.xyz) * u_xlat16_0.xyz + _HitEffectColor.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xxx * u_xlat16_6.xyz + u_xlat1.xyz;
    u_xlat0.x = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _FordingParam.y;
    u_xlat4.xy = input.mask.xy * _FordingParam.xx + u_xlat1.xy;
    u_xlat16_4 = tex2Dbias(_FordingTex, float4(u_xlat4.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_14 = u_xlat16_4 * 2.0 + -1.0;
    u_xlat4.x = u_xlat16_14 * _FordingParam.z + _FordingLevel;
    u_xlat8 = (-u_xlat0.x) + u_xlat4.x;
    u_xlat8 = u_xlat8 / u_xlat4.x;
    u_xlat8 = clamp(u_xlat8, 0.0, 1.0);
    u_xlat16_3.xyz = _DeepColor.xyz + (-_ShallowColor.xyz);
    u_xlat16_3.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_3.xyz + _ShallowColor.xyz;
    u_xlat16_3.xyz = (-u_xlat16_2.xyz) + u_xlat16_3.xyz;
    u_xlat16_2.xyz = float3(u_xlat8,u_xlat8,u_xlat8) * u_xlat16_3.xyz + u_xlat16_2.xyz;
    u_xlatb0.x = u_xlat4.x>=u_xlat0.x;
    u_xlatb0.y = _FordingParam.w>=u_xlat8;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat4.x = float(1.0) / _FordingParam.w;
    u_xlat4.x = u_xlat4.x * u_xlat8;
    u_xlat4.x = clamp(u_xlat4.x, 0.0, 1.0);
    u_xlat8 = u_xlat4.x * -2.0 + 3.0;
    u_xlat4.x = u_xlat4.x * u_xlat4.x;
    u_xlat4.x = u_xlat4.x * u_xlat8;
    u_xlat0.x = u_xlat4.x * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = max(u_xlat0.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_3.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_3.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_DISSOLVE) && !defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

float _DissolveLineWidth;
float _DissolveThreshold;
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
float4 _DissolveColor;
float4 _FordingParam;
half _FordingLevel;
half _LightAtten;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _MainTex_ST;
sampler2D _DissolveTex;
sampler2D _FordingTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half u_xlat16_1;
half3 u_xlat16_2;
float u_xlat3;
bool u_xlatb3;
half3 u_xlat16_4;
float u_xlat5;
float u_xlat6;
float u_xlat10;
float u_xlat15;
half u_xlat16_15;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb15 = u_xlat16_2.x<0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat16_15 = tex2Dbias(_DissolveTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat15 = u_xlat16_15 + (-_DissolveThreshold);
    u_xlatb3 = u_xlat15<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlatb3 = u_xlat1.w==0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat3 = float(1.0) / _DissolveLineWidth;
    u_xlat15 = u_xlat15 * u_xlat3;
    u_xlat15 = clamp(u_xlat15, 0.0, 1.0);
    u_xlat3 = u_xlat15 * -2.0 + 3.0;
    u_xlat15 = u_xlat15 * u_xlat15;
    u_xlat15 = (-u_xlat3) * u_xlat15 + 1.0;
    u_xlatb3 = _DissolveThreshold>=0.00100000005;
    u_xlat3 = u_xlatb3 ? 1.0 : float(0.0);
    u_xlat15 = u_xlat15 * u_xlat3;
    u_xlat0.xyz = (-input.color.xyz) * u_xlat16_0.xyz + _DissolveColor.xyz;
    u_xlat0.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat0.xyz + u_xlat1.xyz;
    u_xlat15 = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _FordingParam.y;
    u_xlat1.xy = input.mask.xy * _FordingParam.xx + u_xlat1.xy;
    u_xlat16_1 = tex2Dbias(_FordingTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_1 * 2.0 + -1.0;
    u_xlat1.x = u_xlat16_2.x * _FordingParam.z + _FordingLevel;
    u_xlat6 = (-u_xlat15) + u_xlat1.x;
    u_xlat6 = u_xlat6 / u_xlat1.x;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat16_2.xyz = _DeepColor.xyz + (-_ShallowColor.xyz);
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + _ShallowColor.xyz;
    u_xlat16_2.xyz = (-u_xlat0.xyz) + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlatb0.x = u_xlat1.x>=u_xlat15;
    u_xlatb0.y = _FordingParam.w>=u_xlat6;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat5 = float(1.0) / _FordingParam.w;
    u_xlat5 = u_xlat5 * u_xlat6;
    u_xlat5 = clamp(u_xlat5, 0.0, 1.0);
    u_xlat10 = u_xlat5 * -2.0 + 3.0;
    u_xlat5 = u_xlat5 * u_xlat5;
    u_xlat5 = u_xlat5 * u_xlat10;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = max(u_xlat0.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_4.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_DISSOLVE) && defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

float _DissolveLineWidth;
float _DissolveThreshold;
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
float4 _DissolveColor;
float4 _FordingParam;
half _FordingLevel;
half _HitEffectBlend;
half _LightAtten;
half3 _DeepColor;
half3 _ShallowColor;
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
half4 _EdgeColor;
half4 _HitEffectColor;
half4 _MainTex_ST;
sampler2D _DissolveTex;
sampler2D _FordingTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.uv3.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float4 u_xlat1;
half u_xlat16_1;
half3 u_xlat16_2;
bool u_xlatb3;
half3 u_xlat16_4;
float3 u_xlat5;
bool u_xlatb5;
float u_xlat6;
half3 u_xlat16_7;
float u_xlat10;
float u_xlat15;
half u_xlat16_15;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlat16_2.x = input.color.w * u_xlat16_0.w + -0.00999999978;
    u_xlatb15 = u_xlat16_2.x<0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat16_15 = tex2Dbias(_DissolveTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat15 = u_xlat16_15 + (-_DissolveThreshold);
    u_xlatb3 = u_xlat15<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlatb3 = u_xlat1.w==0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat16_2.x = _HitEffectColor.w * _HitEffectBlend;
    u_xlat16_7.xyz = (-input.color.xyz) * u_xlat16_0.xyz + _HitEffectColor.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xxx * u_xlat16_7.xyz + u_xlat1.xyz;
    u_xlat0.x = float(1.0) / _DissolveLineWidth;
    u_xlat0.x = u_xlat0.x * u_xlat15;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat5.x = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = (-u_xlat5.x) * u_xlat0.x + 1.0;
    u_xlatb5 = _DissolveThreshold>=0.00100000005;
    u_xlat5.x = u_xlatb5 ? 1.0 : float(0.0);
    u_xlat0.x = u_xlat5.x * u_xlat0.x;
    u_xlat5.xyz = (-u_xlat16_2.xyz) + _DissolveColor.xyz;
    u_xlat0.xyz = u_xlat0.xxx * u_xlat5.xyz + u_xlat16_2.xyz;
    u_xlat15 = input.uv3.x * input.uv3.y + input.uv3.z;
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _FordingParam.y;
    u_xlat1.xy = input.mask.xy * _FordingParam.xx + u_xlat1.xy;
    u_xlat16_1 = tex2Dbias(_FordingTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_1 * 2.0 + -1.0;
    u_xlat1.x = u_xlat16_2.x * _FordingParam.z + _FordingLevel;
    u_xlat6 = (-u_xlat15) + u_xlat1.x;
    u_xlat6 = u_xlat6 / u_xlat1.x;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat16_2.xyz = _DeepColor.xyz + (-_ShallowColor.xyz);
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + _ShallowColor.xyz;
    u_xlat16_2.xyz = (-u_xlat0.xyz) + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat16_2.xyz + u_xlat0.xyz;
    u_xlatb0.x = u_xlat1.x>=u_xlat15;
    u_xlatb0.y = _FordingParam.w>=u_xlat6;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat5.x = float(1.0) / _FordingParam.w;
    u_xlat5.x = u_xlat5.x * u_xlat6;
    u_xlat5.x = clamp(u_xlat5.x, 0.0, 1.0);
    u_xlat10 = u_xlat5.x * -2.0 + 3.0;
    u_xlat5.x = u_xlat5.x * u_xlat5.x;
    u_xlat5.x = u_xlat5.x * u_xlat10;
    u_xlat0.x = u_xlat5.x * u_xlat0.x;
    u_xlat0.xyz = u_xlat0.xxx * _EdgeColor.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = max(u_xlat0.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_2.xyz);
    result.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat16_4.xyz + u_xlat16_2.xyz;
    result.w = u_xlat1.w;
    return result;

}
#endif
ENDCG
}
}
Fallback "Zjcs/Official/World/Map13/BoundEntity/SpritesDefault_b64456be"
}
