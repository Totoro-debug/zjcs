Shader "Zjcs/Official/Combat/FirstPromotionStatuses/ParticlesUnlit" {
Properties {

_BaseMap ("Base Map", 2D) = "white" { }

_BaseColor ("Base Color", Color) = (1,1,1,1)

_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

_BumpMap ("Normal Map", 2D) = "bump" { }

_EmissionColor ("Color", Color) = (0,0,0,1)

_EmissionMap ("Emission", 2D) = "white" { }

_SoftParticlesNearFadeDistance ("Soft Particles Near Fade", Float) = 0.0

_SoftParticlesFarFadeDistance ("Soft Particles Far Fade", Float) = 1.0

_CameraNearFadeDistance ("Camera Near Fade", Float) = 1.0

_CameraFarFadeDistance ("Camera Far Fade", Float) = 2.0

_DistortionBlend ("Distortion Blend", Range(0, 1)) = 0.5

_DistortionStrength ("Distortion Strength", Float) = 1.0

_Surface ("__surface", Float) = 0.0

_Blend ("__mode", Float) = 0.0

_Cull ("__cull", Float) = 2.0

[ToggleUI] _AlphaClip ("__clip", Float) = 0.0

_BlendOp ("__blendop", Float) = 0.0

_SrcBlend ("__src", Float) = 1.0

_DstBlend ("__dst", Float) = 0.0

_SrcBlendAlpha ("__srcA", Float) = 1.0

_DstBlendAlpha ("__dstA", Float) = 0.0

_ZWrite ("__zw", Float) = 1.0

_AlphaToMask ("__alphaToMask", Float) = 0.0

_ColorMode ("_ColorMode", Float) = 0.0

_BaseColorAddSubDiff ("_ColorMode", Vector) = (0,0,0,0)

[ToggleOff] _FlipbookBlending ("__flipbookblending", Float) = 0.0

[ToggleUI] _SoftParticlesEnabled ("__softparticlesenabled", Float) = 0.0

[ToggleUI] _CameraFadingEnabled ("__camerafadingenabled", Float) = 0.0

[ToggleUI] _DistortionEnabled ("__distortionenabled", Float) = 0.0

_SoftParticleFadeParams ("__softparticlefadeparams", Vector) = (0,0,0,0)

_CameraFadeParams ("__camerafadeparams", Vector) = (0,0,0,0)

_DistortionStrengthScaled ("Distortion Strength Scaled", Float) = 0.10000000149011612

_QueueOffset ("Queue offset", Float) = 0.0

_FlipbookMode ("flipbook", Float) = 0.0

_Mode ("mode", Float) = 0.0

_Color ("color", Color) = (1,1,1,1)

}
SubShader {
LOD 0
Tags { "IGNOREPROJECTOR"="true" "PerformanceChecks"="False" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Pass {
Name "ForwardLit"
Tags { "IGNOREPROJECTOR"="true" "PerformanceChecks"="False" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Cull [_Cull]
ZWrite [_ZWrite]
ZTest LEqual
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
BlendOp [_BlendOp], [_BlendOp]
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask [_AlphaToMask]
HLSLPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ PROCEDURAL_INSTANCING_ON
#pragma multi_compile_local __ _SURFACE_TYPE_TRANSPARENT
#include "HLSLSupport.cginc"
#if defined(PROCEDURAL_INSTANCING_ON)
#define ZJCS_DIRECT_PROCEDURAL_BODY
#undef PROCEDURAL_INSTANCING_ON
#endif
#include "UnityCG.cginc"
#if defined(ZJCS_DIRECT_PROCEDURAL_BODY)
#define PROCEDURAL_INSTANCING_ON 1
CBUFFER_START(UnityDrawCallInfo)
int unity_BaseInstanceID;
int unity_InstanceCount;
CBUFFER_END
#endif
#if !defined(PROCEDURAL_INSTANCING_ON) && !defined(_SURFACE_TYPE_TRANSPARENT)
struct Input { float4 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float3 normal:NORMAL; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; float4 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float3 uv8:TEXCOORD8; };

float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_BaseMap_ST;
float4 Xhlslcc_UnusedX_CameraFadeParams;
float4 Xhlslcc_UnusedX_SoftParticleFadeParams;
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
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DistortionBlend;
half Xhlslcc_UnusedX_DistortionStrengthScaled;
half _Surface;
half4 Xhlslcc_UnusedX_BaseColorAddSubDiff;
half4 Xhlslcc_UnusedX_EmissionColor;
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
sampler2D _BaseMap;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;
float u_xlat6;
bool u_xlatb6;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    output.world.w = 0.0;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat6 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat6 = max(u_xlat6, 1.17549435e-38);
    u_xlat6 = rsqrt(u_xlat6);
    u_xlat1.xyz = (float3)(u_xlat6) * u_xlat1.xyz;
    output.mask.xyz = u_xlat1.xyz;
    u_xlat6 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat6 = rsqrt(u_xlat6);
    u_xlat0.xyz = (float3)(u_xlat6) * u_xlat0.xyz;
    u_xlatb6 = unity_OrthoParams.w==0.0;
    output.uv3.x = (u_xlatb6) ? u_xlat0.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    output.uv3.y = (u_xlatb6) ? u_xlat0.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    output.uv3.z = (u_xlatb6) ? u_xlat0.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    output.uv8.xyz = float3(0.0,0.0,0.0);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
bool u_xlatb1;

    u_xlat16_0 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * _BaseColor;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _Surface==1.0;
    result.w = (u_xlatb1) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#elif !defined(PROCEDURAL_INSTANCING_ON) && defined(_SURFACE_TYPE_TRANSPARENT)
struct Input { float4 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float3 normal:NORMAL; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; float4 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float3 uv8:TEXCOORD8; };

float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_BaseMap_ST;
float4 Xhlslcc_UnusedX_CameraFadeParams;
float4 Xhlslcc_UnusedX_SoftParticleFadeParams;
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
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DistortionBlend;
half Xhlslcc_UnusedX_DistortionStrengthScaled;
half _Surface;
half4 Xhlslcc_UnusedX_BaseColorAddSubDiff;
half4 Xhlslcc_UnusedX_EmissionColor;
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
sampler2D _BaseMap;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;
float u_xlat6;
bool u_xlatb6;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.uv.xy = input.uv.xy;
    output.color = input.color;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + _WorldSpaceCameraPos.xyz;
    output.world.w = 0.0;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat6 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat6 = max(u_xlat6, 1.17549435e-38);
    u_xlat6 = rsqrt(u_xlat6);
    u_xlat1.xyz = (float3)(u_xlat6) * u_xlat1.xyz;
    output.mask.xyz = u_xlat1.xyz;
    u_xlat6 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat6 = rsqrt(u_xlat6);
    u_xlat0.xyz = (float3)(u_xlat6) * u_xlat0.xyz;
    u_xlatb6 = unity_OrthoParams.w==0.0;
    output.uv3.x = (u_xlatb6) ? u_xlat0.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    output.uv3.y = (u_xlatb6) ? u_xlat0.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    output.uv3.z = (u_xlatb6) ? u_xlat0.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    output.uv8.xyz = float3(0.0,0.0,0.0);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
bool u_xlatb1;

    u_xlat16_0 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * _BaseColor;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _Surface==1.0;
    result.w = (u_xlatb1) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#elif defined(PROCEDURAL_INSTANCING_ON) && !defined(_SURFACE_TYPE_TRANSPARENT)
struct Input { float4 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float3 normal:NORMAL; uint instanceID:SV_InstanceID; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; float4 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float3 uv8:TEXCOORD8; nointerpolation uint originalInstance:TEXCOORD7; };

struct unity_ParticleInstanceData_type { uint value[14]; };
StructuredBuffer<unity_ParticleInstanceData_type> unity_ParticleInstanceData;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_BaseMap_ST;
float4 Xhlslcc_UnusedX_CameraFadeParams;
float4 Xhlslcc_UnusedX_SoftParticleFadeParams;
float4 unity_ParticleUVShiftData;
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DistortionBlend;
half Xhlslcc_UnusedX_DistortionStrengthScaled;
half _Surface;
half unity_ParticleUseMeshColors;
half4 Xhlslcc_UnusedX_BaseColorAddSubDiff;
half4 Xhlslcc_UnusedX_EmissionColor;
half4 _BaseColor;
sampler2D _BaseMap;
uint uint_bitfieldExtractVertex(uint value, int offset, int bits) { return (value >> uint(offset)) & uint(~(int(~0) << uint(bits))); }
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
uint u_xlatu0;
float2 u_xlat1;
half4 u_xlat16_1;
int u_xlati1;
uint3 u_xlatu1;
float4 u_xlat2;
float4 u_xlat3;
float4 u_xlat4;
float4 u_xlat5;
float3 u_xlat6;
float4 u_xlat7;
float u_xlat8;
float3 u_xlat9;
bool u_xlatb16;
float u_xlat24;
bool u_xlatb24;


    u_xlat0.xyz = input.vertex.xyz;
    u_xlat0.w = 1.0;
    u_xlati1 = input.instanceID + unity_BaseInstanceID;
    u_xlat2 = float4(asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 2]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 3]));
    u_xlat3.w = u_xlat2.y;
    u_xlat9.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 2]));
    u_xlat3.y = u_xlat9.y;
    u_xlat4.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 2]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 0]));
    u_xlat3.z = u_xlat4.x;
    u_xlat5.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 2]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 1]));
    u_xlat1.x = asfloat(unity_ParticleInstanceData[u_xlati1].value[(52 >> 2) + 0]);
    u_xlat1.x = floor(u_xlat1.x);
    u_xlat3.x = u_xlat5.z;
    u_xlat6.y = dot(u_xlat3, u_xlat0);
    u_xlat7 = u_xlat6.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat4.w = u_xlat2.x;
    u_xlat5.z = u_xlat4.y;
    u_xlat4.x = u_xlat5.y;
    u_xlat4.y = u_xlat9.x;
    u_xlat5.y = u_xlat9.z;
    u_xlat6.x = dot(u_xlat4, u_xlat0);
    u_xlat7 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat6.xxxx + u_xlat7;
    u_xlat5.w = u_xlat2.z;
    u_xlat6.z = dot(u_xlat5, u_xlat0);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat6.zzzz + u_xlat7;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    u_xlat0.x = u_xlat1.x / unity_ParticleUVShiftData.y;
    u_xlat0.x = floor(u_xlat0.x);
    u_xlat8 = (-u_xlat0.x) * unity_ParticleUVShiftData.y + u_xlat1.x;
    u_xlat8 = floor(u_xlat8);
    u_xlat1.x = u_xlat8 * unity_ParticleUVShiftData.z;
    u_xlat8 = (-unity_ParticleUVShiftData.w) + 1.0;
    u_xlat1.y = (-u_xlat0.x) * unity_ParticleUVShiftData.w + u_xlat8;
    u_xlat0.xy = input.uv.xy * unity_ParticleUVShiftData.zw + u_xlat1.xy;
    u_xlatb16 = unity_ParticleUVShiftData.x!=0.0;
    output.uv.xy = (bool(u_xlatb16)) ? u_xlat0.xy : input.uv.xy;
    u_xlatu0 = uint(asuint(u_xlat2.w)) & 255u;
    u_xlat0.x = float(u_xlatu0);
    u_xlatu1.xy = uint2(uint_bitfieldExtractVertex(uint(asuint(u_xlat2.w)), int(8) & int(0x1F), int(8) & int(0x1F)),uint_bitfieldExtractVertex(uint(asuint(u_xlat2.w)), int(16) & int(0x1F), int(8) & int(0x1F)));
    u_xlatu1.z = uint(asuint(u_xlat2.w)) >> (24u & uint(0x1F));
    u_xlat0.yzw = (float3)(u_xlatu1.xyz);
    u_xlat0 = u_xlat0 * float4(0.00392156886,0.00392156886,0.00392156886,0.00392156886);
    u_xlat16_1 = input.color + float4(-1.0,-1.0,-1.0,-1.0);
    u_xlat16_1 = (float4)(unity_ParticleUseMeshColors) * u_xlat16_1 + float4(1.0,1.0,1.0,1.0);
    output.color = u_xlat0 * u_xlat16_1;
    output.world.w = 0.0;
    output.world.xyz = u_xlat6.xyz;
    u_xlat0.xyz = (-u_xlat6.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat2.xyz = u_xlat4.yzx * u_xlat5.zxy;
    u_xlat2.xyz = u_xlat4.zxy * u_xlat5.yzx + (-u_xlat2.xyz);
    u_xlat6.xyz = u_xlat3.zxy * u_xlat5.yzx;
    u_xlat5.xyz = u_xlat3.yzx * u_xlat5.zxy + (-u_xlat6.xyz);
    u_xlat24 = dot(u_xlat4.xyz, u_xlat5.xyz);
    u_xlat24 = float(1.0) / float(u_xlat24);
    u_xlat2.xyz = (float3)(u_xlat24) * u_xlat2.xyz;
    u_xlat2.y = dot(input.normal.xyz, u_xlat2.xyz);
    u_xlat6.xyz = u_xlat3.yzx * u_xlat4.zxy;
    u_xlat3.xyz = u_xlat4.yzx * u_xlat3.zxy + (-u_xlat6.xyz);
    u_xlat3.xyz = (float3)(u_xlat24) * u_xlat3.xyz;
    u_xlat4.xyz = (float3)(u_xlat24) * u_xlat5.xyz;
    u_xlat2.x = dot(input.normal.xyz, u_xlat4.xyz);
    u_xlat2.z = dot(input.normal.xyz, u_xlat3.xyz);
    u_xlat24 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat24 = max(u_xlat24, 1.17549435e-38);
    u_xlat24 = rsqrt(u_xlat24);
    u_xlat2.xyz = (float3)(u_xlat24) * u_xlat2.xyz;
    output.mask.xyz = u_xlat2.xyz;
    u_xlat24 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat24 = rsqrt(u_xlat24);
    u_xlat0.xyz = (float3)(u_xlat24) * u_xlat0.xyz;
    u_xlatb24 = unity_OrthoParams.w==0.0;
    output.uv3.x = (u_xlatb24) ? u_xlat0.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    output.uv3.y = (u_xlatb24) ? u_xlat0.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    output.uv3.z = (u_xlatb24) ? u_xlat0.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    output.uv8.xyz = float3(0.0,0.0,0.0);
    output.originalInstance =  uint(input.instanceID);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
bool u_xlatb1;

    u_xlat16_0 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * _BaseColor;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _Surface==1.0;
    result.w = (u_xlatb1) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#elif defined(PROCEDURAL_INSTANCING_ON) && defined(_SURFACE_TYPE_TRANSPARENT)
struct Input { float4 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float3 normal:NORMAL; uint instanceID:SV_InstanceID; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; float4 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float3 uv8:TEXCOORD8; nointerpolation uint originalInstance:TEXCOORD7; };

struct unity_ParticleInstanceData_type { uint value[14]; };
StructuredBuffer<unity_ParticleInstanceData_type> unity_ParticleInstanceData;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_BaseMap_ST;
float4 Xhlslcc_UnusedX_CameraFadeParams;
float4 Xhlslcc_UnusedX_SoftParticleFadeParams;
float4 unity_ParticleUVShiftData;
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DistortionBlend;
half Xhlslcc_UnusedX_DistortionStrengthScaled;
half _Surface;
half unity_ParticleUseMeshColors;
half4 Xhlslcc_UnusedX_BaseColorAddSubDiff;
half4 Xhlslcc_UnusedX_EmissionColor;
half4 _BaseColor;
sampler2D _BaseMap;
uint uint_bitfieldExtractVertex(uint value, int offset, int bits) { return (value >> uint(offset)) & uint(~(int(~0) << uint(bits))); }
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
uint u_xlatu0;
float2 u_xlat1;
half4 u_xlat16_1;
int u_xlati1;
uint3 u_xlatu1;
float4 u_xlat2;
float4 u_xlat3;
float4 u_xlat4;
float4 u_xlat5;
float3 u_xlat6;
float4 u_xlat7;
float u_xlat8;
float3 u_xlat9;
bool u_xlatb16;
float u_xlat24;
bool u_xlatb24;


    u_xlat0.xyz = input.vertex.xyz;
    u_xlat0.w = 1.0;
    u_xlati1 = input.instanceID + unity_BaseInstanceID;
    u_xlat2 = float4(asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 2]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 3]));
    u_xlat3.w = u_xlat2.y;
    u_xlat9.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 2]));
    u_xlat3.y = u_xlat9.y;
    u_xlat4.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 2]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 0]));
    u_xlat3.z = u_xlat4.x;
    u_xlat5.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 2]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 1]));
    u_xlat1.x = asfloat(unity_ParticleInstanceData[u_xlati1].value[(52 >> 2) + 0]);
    u_xlat1.x = floor(u_xlat1.x);
    u_xlat3.x = u_xlat5.z;
    u_xlat6.y = dot(u_xlat3, u_xlat0);
    u_xlat7 = u_xlat6.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat4.w = u_xlat2.x;
    u_xlat5.z = u_xlat4.y;
    u_xlat4.x = u_xlat5.y;
    u_xlat4.y = u_xlat9.x;
    u_xlat5.y = u_xlat9.z;
    u_xlat6.x = dot(u_xlat4, u_xlat0);
    u_xlat7 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat6.xxxx + u_xlat7;
    u_xlat5.w = u_xlat2.z;
    u_xlat6.z = dot(u_xlat5, u_xlat0);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat6.zzzz + u_xlat7;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    u_xlat0.x = u_xlat1.x / unity_ParticleUVShiftData.y;
    u_xlat0.x = floor(u_xlat0.x);
    u_xlat8 = (-u_xlat0.x) * unity_ParticleUVShiftData.y + u_xlat1.x;
    u_xlat8 = floor(u_xlat8);
    u_xlat1.x = u_xlat8 * unity_ParticleUVShiftData.z;
    u_xlat8 = (-unity_ParticleUVShiftData.w) + 1.0;
    u_xlat1.y = (-u_xlat0.x) * unity_ParticleUVShiftData.w + u_xlat8;
    u_xlat0.xy = input.uv.xy * unity_ParticleUVShiftData.zw + u_xlat1.xy;
    u_xlatb16 = unity_ParticleUVShiftData.x!=0.0;
    output.uv.xy = (bool(u_xlatb16)) ? u_xlat0.xy : input.uv.xy;
    u_xlatu0 = uint(asuint(u_xlat2.w)) & 255u;
    u_xlat0.x = float(u_xlatu0);
    u_xlatu1.xy = uint2(uint_bitfieldExtractVertex(uint(asuint(u_xlat2.w)), int(8) & int(0x1F), int(8) & int(0x1F)),uint_bitfieldExtractVertex(uint(asuint(u_xlat2.w)), int(16) & int(0x1F), int(8) & int(0x1F)));
    u_xlatu1.z = uint(asuint(u_xlat2.w)) >> (24u & uint(0x1F));
    u_xlat0.yzw = (float3)(u_xlatu1.xyz);
    u_xlat0 = u_xlat0 * float4(0.00392156886,0.00392156886,0.00392156886,0.00392156886);
    u_xlat16_1 = input.color + float4(-1.0,-1.0,-1.0,-1.0);
    u_xlat16_1 = (float4)(unity_ParticleUseMeshColors) * u_xlat16_1 + float4(1.0,1.0,1.0,1.0);
    output.color = u_xlat0 * u_xlat16_1;
    output.world.w = 0.0;
    output.world.xyz = u_xlat6.xyz;
    u_xlat0.xyz = (-u_xlat6.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat2.xyz = u_xlat4.yzx * u_xlat5.zxy;
    u_xlat2.xyz = u_xlat4.zxy * u_xlat5.yzx + (-u_xlat2.xyz);
    u_xlat6.xyz = u_xlat3.zxy * u_xlat5.yzx;
    u_xlat5.xyz = u_xlat3.yzx * u_xlat5.zxy + (-u_xlat6.xyz);
    u_xlat24 = dot(u_xlat4.xyz, u_xlat5.xyz);
    u_xlat24 = float(1.0) / float(u_xlat24);
    u_xlat2.xyz = (float3)(u_xlat24) * u_xlat2.xyz;
    u_xlat2.y = dot(input.normal.xyz, u_xlat2.xyz);
    u_xlat6.xyz = u_xlat3.yzx * u_xlat4.zxy;
    u_xlat3.xyz = u_xlat4.yzx * u_xlat3.zxy + (-u_xlat6.xyz);
    u_xlat3.xyz = (float3)(u_xlat24) * u_xlat3.xyz;
    u_xlat4.xyz = (float3)(u_xlat24) * u_xlat5.xyz;
    u_xlat2.x = dot(input.normal.xyz, u_xlat4.xyz);
    u_xlat2.z = dot(input.normal.xyz, u_xlat3.xyz);
    u_xlat24 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat24 = max(u_xlat24, 1.17549435e-38);
    u_xlat24 = rsqrt(u_xlat24);
    u_xlat2.xyz = (float3)(u_xlat24) * u_xlat2.xyz;
    output.mask.xyz = u_xlat2.xyz;
    u_xlat24 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat24 = rsqrt(u_xlat24);
    u_xlat0.xyz = (float3)(u_xlat24) * u_xlat0.xyz;
    u_xlatb24 = unity_OrthoParams.w==0.0;
    output.uv3.x = (u_xlatb24) ? u_xlat0.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    output.uv3.y = (u_xlatb24) ? u_xlat0.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    output.uv3.z = (u_xlatb24) ? u_xlat0.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    output.uv8.xyz = float3(0.0,0.0,0.0);
    output.originalInstance =  uint(input.instanceID);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
bool u_xlatb1;

    u_xlat16_0 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * _BaseColor;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _Surface==1.0;
    result.w = (u_xlatb1) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#endif
ENDHLSL
}
Pass {
Name "DepthOnly"
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="DepthOnly" "PerformanceChecks"="False" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Cull [_Cull]
ZWrite On
ZTest LEqual
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask R
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
HLSLPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ PROCEDURAL_INSTANCING_ON
#include "HLSLSupport.cginc"
#if defined(PROCEDURAL_INSTANCING_ON)
#define ZJCS_DIRECT_PROCEDURAL_BODY
#undef PROCEDURAL_INSTANCING_ON
#endif
#include "UnityCG.cginc"
#if defined(ZJCS_DIRECT_PROCEDURAL_BODY)
#define PROCEDURAL_INSTANCING_ON 1
CBUFFER_START(UnityDrawCallInfo)
int unity_BaseInstanceID;
int unity_InstanceCount;
CBUFFER_END
#endif
#if !defined(PROCEDURAL_INSTANCING_ON)
struct Input { float4 vertex:POSITION; };
struct Varying { float4 position:SV_POSITION;  };

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
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


float4 hlslcc_FragCoord = float4(input.position.xyz,1.0/input.position.w);
    result = hlslcc_FragCoord.z;
    return result;

}
#elif defined(PROCEDURAL_INSTANCING_ON)
struct Input { float4 vertex:POSITION; uint instanceID:SV_InstanceID; };
struct Varying { float4 position:SV_POSITION; nointerpolation uint originalInstance:TEXCOORD7; };

struct unity_ParticleInstanceData_type { uint value[14]; };
StructuredBuffer<unity_ParticleInstanceData_type> unity_ParticleInstanceData;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float u_xlat1;
int u_xlati1;
float4 u_xlat2;
float4 u_xlat3;
float4 u_xlat4;
float3 u_xlat5;
float3 u_xlat7;

    u_xlat0.xyz = input.vertex.xyz;
    u_xlat0.w = 1.0;
    u_xlati1 = input.instanceID + unity_BaseInstanceID;
    u_xlat2.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 2]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(0 >> 2) + 1]));
    u_xlat3.x = u_xlat2.z;
    u_xlat4.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(12 >> 2) + 2]));
    u_xlat3.y = u_xlat4.x;
    u_xlat7.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(24 >> 2) + 2]));
    u_xlat5.xyz = float3(asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 0]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 1]),asfloat(unity_ParticleInstanceData[u_xlati1].value[(36 >> 2) + 2]));
    u_xlat3.z = u_xlat7.y;
    u_xlat3.w = u_xlat5.y;
    u_xlat1 = dot(u_xlat3, u_xlat0);
    u_xlat3 = (float4)(u_xlat1) * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat4.x = u_xlat2.y;
    u_xlat2.y = u_xlat4.z;
    u_xlat4.z = u_xlat7.x;
    u_xlat2.z = u_xlat7.z;
    u_xlat4.w = u_xlat5.x;
    u_xlat2.w = u_xlat5.z;
    u_xlat1 = dot(u_xlat2, u_xlat0);
    u_xlat0.x = dot(u_xlat4, u_xlat0);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat3;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * (float4)(u_xlat1) + u_xlat0;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.originalInstance =  uint(input.instanceID);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


float4 hlslcc_FragCoord = float4(input.position.xyz,1.0/input.position.w);
    result = hlslcc_FragCoord.z;
    return result;

}
#endif
ENDHLSL
}
Pass {
Name "DepthNormalsOnly"
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="DepthNormalsOnly" "PerformanceChecks"="False" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Cull [_Cull]
ZWrite On
ZTest LEqual
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
HLSLPROGRAM
#pragma target 5.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"





static float4 v_gl_Position;
static float4 v__12;
static float3 v__101;
static float3 v_vs_TEXCOORD2;
static float3 v_vs_TEXCOORD3;

struct v_SPIRV_Cross_Input
{
    float4 v__12 : POSITION;
    float3 v__101 : NORMAL;
};

struct v_SPIRV_Cross_Output
{
    float3 v_vs_TEXCOORD2 : TEXCOORD0;
    float3 v_vs_TEXCOORD3 : TEXCOORD1;
    float4 v_gl_Position : SV_Position;
};

static float3 v__9;
static float4 v__58;
static float v__122;
static bool v__151;
static float3 v__158;

void v_vert_main()
{
    v__9 = v__12.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    v__9 = (float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * v__12.xxx) + v__9;
    v__9 = (float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * v__12.zzz) + v__9;
    v__9 += float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    v__58 = v__9.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    v__58 = (float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * v__9.xxxx) + v__58;
    v__58 = (float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * v__9.zzzz) + v__58;
    v__9 = (-v__9) + _WorldSpaceCameraPos;
    v_gl_Position = v__58 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    v__58.x = dot(v__101, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    v__58.y = dot(v__101, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    v__58.z = dot(v__101, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    v__122 = dot(v__58.xyz, v__58.xyz);
    v__122 = max(v__122, 1.1754943508222875079687365372222e-38f);
    v__122 = rsqrt(v__122);
    v_vs_TEXCOORD2 = v__122.xxx * v__58.xyz;
    v__122 = dot(v__9, v__9);
    v__122 = rsqrt(v__122);
    v__9 = v__122.xxx * v__9;
    v__151 = unity_OrthoParams.w == 0.0f;
    float v__161;
    if (v__151)
    {
        v__161 = v__9.x;
    }
    else
    {
        v__161 = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    }
    v__158.x = v__161;
    float v__172;
    if (v__151)
    {
        v__172 = v__9.y;
    }
    else
    {
        v__172 = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    }
    v__158.y = v__172;
    float v__183;
    if (v__151)
    {
        v__183 = v__9.z;
    }
    else
    {
        v__183 = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    }
    v__158.z = v__183;
    v_vs_TEXCOORD3 = v__158;
    
}

v_SPIRV_Cross_Output vert(v_SPIRV_Cross_Input stage_input)
{
    v__12 = stage_input.v__12;
    v__101 = stage_input.v__101;
    v_vert_main();
    v_SPIRV_Cross_Output stage_output;
    stage_output.v_gl_Position = v_gl_Position;
    stage_output.v_vs_TEXCOORD2 = v_vs_TEXCOORD2;
    stage_output.v_vs_TEXCOORD3 = v_vs_TEXCOORD3;
    return stage_output;
}

static float3 f_vs_TEXCOORD2;
static float4 f__29;

struct f_SPIRV_Cross_Input
{
    float3 f_vs_TEXCOORD2 : TEXCOORD0;
};

struct f_SPIRV_Cross_Output
{
    float4 f__29 : SV_Target0;
};

static float3 f__9;

void f_frag_main()
{
    f__9.x = dot(f_vs_TEXCOORD2, f_vs_TEXCOORD2);
    f__9.x = rsqrt(f__9.x);
    f__9 = f__9.xxx * f_vs_TEXCOORD2;
    f__29 = float4(f__9.x, f__9.y, f__9.z, f__29.w);
    f__29.w = 0.0f;
}

f_SPIRV_Cross_Output frag(f_SPIRV_Cross_Input stage_input)
{
    f_vs_TEXCOORD2 = stage_input.f_vs_TEXCOORD2;
    f_frag_main();
    f_SPIRV_Cross_Output stage_output;
    stage_output.f__29 = f__29;
    return stage_output;
}

ENDHLSL
}
}
Fallback Off
}
