Shader "Zjcs/Official/World/Map13/Entity/UniversalRenderPipelineLit_cc454bf0" {
Properties {
_WorkflowMode ("WorkflowMode", Float) = 1.0
_BaseMap ("Albedo", 2D) = "white" {}
_BaseColor ("Color", Color) = (1.0,1.0,1.0,1.0)
_Cutoff ("Alpha Cutoff", Range(0.0,1.0)) = 0.5
_Smoothness ("Smoothness", Range(0.0,1.0)) = 0.5
_SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0.0
_Metallic ("Metallic", Range(0.0,1.0)) = 0.0
_MetallicGlossMap ("Metallic", 2D) = "white" {}
_SpecColor ("Specular", Color) = (0.20000000298023224,0.20000000298023224,0.20000000298023224,1.0)
_SpecGlossMap ("Specular", 2D) = "white" {}
[ToggleOff] _SpecularHighlights ("Specular Highlights", Float) = 1.0
[ToggleOff] _EnvironmentReflections ("Environment Reflections", Float) = 1.0
_BumpScale ("Scale", Float) = 1.0
_BumpMap ("Normal Map", 2D) = "bump" {}
_Parallax ("Scale", Range(0.004999999888241291,0.07999999821186066)) = 0.004999999888241291
_ParallaxMap ("Height Map", 2D) = "black" {}
_OcclusionStrength ("Strength", Range(0.0,1.0)) = 1.0
_OcclusionMap ("Occlusion", 2D) = "white" {}
_EmissionColor ("Color", Color) = (0.0,0.0,0.0,1.0)
_EmissionMap ("Emission", 2D) = "white" {}
_DetailMask ("Detail Mask", 2D) = "white" {}
_DetailAlbedoMapScale ("Scale", Range(0.0,2.0)) = 1.0
_DetailAlbedoMap ("Detail Albedo x2", 2D) = "linearGrey" {}
_DetailNormalMapScale ("Scale", Range(0.0,2.0)) = 1.0
_DetailNormalMap ("Normal Map", 2D) = "bump" {}
_ClearCoatMask ("_ClearCoatMask", Float) = 0.0
_ClearCoatSmoothness ("_ClearCoatSmoothness", Float) = 0.0
_Surface ("__surface", Float) = 0.0
_Blend ("__blend", Float) = 0.0
_Cull ("__cull", Float) = 2.0
[ToggleUI] _AlphaClip ("__clip", Float) = 0.0
_SrcBlend ("__src", Float) = 1.0
_DstBlend ("__dst", Float) = 0.0
_SrcBlendAlpha ("__srcA", Float) = 1.0
_DstBlendAlpha ("__dstA", Float) = 0.0
_ZWrite ("__zw", Float) = 1.0
_BlendModePreserveSpecular ("_BlendModePreserveSpecular", Float) = 1.0
_AlphaToMask ("__alphaToMask", Float) = 0.0
[ToggleUI] _ReceiveShadows ("Receive Shadows", Float) = 1.0
_QueueOffset ("Queue offset", Float) = 0.0
_MainTex ("BaseMap", 2D) = "white" {}
_Color ("Base Color", Color) = (1.0,1.0,1.0,1.0)
_GlossMapScale ("Smoothness", Float) = 0.0
_Glossiness ("Smoothness", Float) = 0.0
_GlossyReflections ("EnvironmentReflections", Float) = 0.0
unity_Lightmaps ("unity_Lightmaps", 2D) = "" {}
unity_LightmapsInd ("unity_LightmapsInd", 2D) = "" {}
unity_ShadowMasks ("unity_ShadowMasks", 2D) = "" {}
}
SubShader {
LOD 300
Tags { "IGNOREPROJECTOR"="true" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="UniversalForward" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
Cull [_Cull]
ZWrite [_ZWrite]
ZTest LEqual
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask [_AlphaToMask]
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#define EVALUATE_SH_VERTEX 1
#pragma multi_compile __ LOD_FADE_CROSSFADE
#pragma multi_compile __ _LIGHT_COOKIES
#include "UnityCG.cginc"
#if defined(EVALUATE_SH_VERTEX) && defined(LOD_FADE_CROSSFADE) && defined(_LIGHT_COOKIES)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float uv5:TEXCOORD5; float3 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv8:TEXCOORD8; };

float _DitheringTextureInvSize;
float _MainLightCookieTextureFormat;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_DetailAlbedoMap_ST;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
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
float4 _BaseMap_ST;
float4 _MainLightPosition;
float4x4 _MainLightWorldToLight;
half Xhlslcc_UnusedX_BumpScale;
half Xhlslcc_UnusedX_ClearCoatMask;
half Xhlslcc_UnusedX_ClearCoatSmoothness;
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DetailAlbedoMapScale;
half Xhlslcc_UnusedX_DetailNormalMapScale;
half Xhlslcc_UnusedX_OcclusionStrength;
half Xhlslcc_UnusedX_Parallax;
half _Metallic;
half _Smoothness;
half _Surface;
half4 Xhlslcc_UnusedX_EmissionColor;
half4 Xhlslcc_UnusedX_SpecColor;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _BaseColor;
half4 _MainLightColor;
half4 unity_LightData;
sampler2D _BaseMap;
sampler2D _DitheringTexture;
sampler2D _MainLightCookieTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half4 u_xlat16_3;
half3 u_xlat16_4;
float u_xlat15;

    output.uv.xy = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    output.world.xyz = u_xlat0.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = max(u_xlat15, 1.17549435e-38);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    output.mask.xyz = u_xlat1.xyz;
    output.uv5 = 0.0;
    u_xlat16_2.x = u_xlat1.y * u_xlat1.y;
    u_xlat16_2.x = u_xlat1.x * u_xlat1.x + (-u_xlat16_2.x);
    u_xlat16_3 = u_xlat1.yzzx * u_xlat1.xyzz;
    u_xlat16_4.x = dot(unity_SHBr, u_xlat16_3);
    u_xlat16_4.y = dot(unity_SHBg, u_xlat16_3);
    u_xlat16_4.z = dot(unity_SHBb, u_xlat16_3);
    u_xlat16_2.xyz = unity_SHC.xyz * u_xlat16_2.xxx + u_xlat16_4.xyz;
    u_xlat1.w = 1.0;
    u_xlat16_3.x = dot(unity_SHAr, u_xlat1);
    u_xlat16_3.y = dot(unity_SHAg, u_xlat1);
    u_xlat16_3.z = dot(unity_SHAb, u_xlat1);
    u_xlat16_2.xyz = u_xlat16_2.xyz + u_xlat16_3.xyz;
    output.uv8.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
float4 Map13SampleSpecCube0Fragment(float3 direction, float lod) { return UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, direction, lod); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half4 u_xlat16_1;
float3 u_xlat2;
bool u_xlatb2;
half3 u_xlat16_3;
half u_xlat16_4;
half3 u_xlat16_5;
half u_xlat16_6;
bool2 u_xlatb7;
half3 u_xlat16_8;
half3 u_xlat16_9;
half2 u_xlat16_10;
float u_xlat11;
half3 u_xlat16_15;
half u_xlat16_17;
half3 u_xlat16_21;
half u_xlat16_28;
float u_xlat33;
half u_xlat16_33;
bool u_xlatb33;
float u_xlat35;
half u_xlat16_36;
half u_xlat16_38;
half u_xlat16_39;

float4 hlslcc_FragCoord = float4(input.position.xyz, 1.0/input.position.w);
    u_xlat16_0 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = u_xlat16_0 * _BaseColor;
    u_xlat2.xy = hlslcc_FragCoord.xy * float2(float2(_DitheringTextureInvSize, _DitheringTextureInvSize));
    u_xlat16_33 = tex2Dbias(_DitheringTexture, float4(u_xlat2.xy, 0, _GlobalMipBias.x)).w;
    u_xlatb2 = unity_LODFade.x>=0.0;
    u_xlat33 = (u_xlatb2) ? abs(u_xlat16_33) : -abs(u_xlat16_33);
    u_xlat33 = (-u_xlat33) + unity_LODFade.x;
    u_xlatb33 = u_xlat33<0.0;
    if(u_xlatb33){clip(-1);}
    u_xlatb33 = unity_OrthoParams.w==0.0;
    u_xlat2.xyz = (-input.world.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat35 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat35 = rsqrt(u_xlat35);
    u_xlat2.xyz = float3(u_xlat35,u_xlat35,u_xlat35) * u_xlat2.xyz;
    u_xlat16_3.x = (u_xlatb33) ? u_xlat2.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat16_3.y = (u_xlatb33) ? u_xlat2.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat16_3.z = (u_xlatb33) ? u_xlat2.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat33 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat33 = rsqrt(u_xlat33);
    u_xlat2.xyz = float3(u_xlat33,u_xlat33,u_xlat33) * input.mask.xyz;
    u_xlat16_36 = (-_Metallic) * 0.959999979 + 0.959999979;
    u_xlat16_4 = (-u_xlat16_36) + _Smoothness;
    u_xlat16_15.xyz = u_xlat16_1.xyz * float3(u_xlat16_36,u_xlat16_36,u_xlat16_36);
    u_xlat16_5.xyz = u_xlat16_0.xyz * _BaseColor.xyz + float3(-0.0399999991, -0.0399999991, -0.0399999991);
    u_xlat16_5.xyz = float3(float3(_Metallic, _Metallic, _Metallic)) * u_xlat16_5.xyz + float3(0.0399999991, 0.0399999991, 0.0399999991);
    u_xlat16_36 = (-_Smoothness) + 1.0;
    u_xlat16_38 = u_xlat16_36 * u_xlat16_36;
    u_xlat16_38 = max(u_xlat16_38, 0.0078125);
    u_xlat16_6 = u_xlat16_38 * u_xlat16_38;
    u_xlat16_4 = u_xlat16_4 + 1.0;
    u_xlat16_4 = clamp(u_xlat16_4, 0.0, 1.0);
    u_xlat16_17 = u_xlat16_38 * 4.0 + 2.0;
    u_xlatb0 = _MainLightCookieTextureFormat!=-1.0;
    if(u_xlatb0){
        u_xlat0.xy = input.world.yy * float4(_MainLightWorldToLight[0][1],_MainLightWorldToLight[1][1],_MainLightWorldToLight[2][1],_MainLightWorldToLight[3][1]).xy;
        u_xlat0.xy = float4(_MainLightWorldToLight[0][0],_MainLightWorldToLight[1][0],_MainLightWorldToLight[2][0],_MainLightWorldToLight[3][0]).xy * input.world.xx + u_xlat0.xy;
        u_xlat0.xy = float4(_MainLightWorldToLight[0][2],_MainLightWorldToLight[1][2],_MainLightWorldToLight[2][2],_MainLightWorldToLight[3][2]).xy * input.world.zz + u_xlat0.xy;
        u_xlat0.xy = u_xlat0.xy + float4(_MainLightWorldToLight[0][3],_MainLightWorldToLight[1][3],_MainLightWorldToLight[2][3],_MainLightWorldToLight[3][3]).xy;
        u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
        u_xlat16_0 = tex2Dbias(_MainLightCookieTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x));
        u_xlatb7.xy = ((float4(float4(_MainLightCookieTextureFormat, _MainLightCookieTextureFormat, _MainLightCookieTextureFormat, _MainLightCookieTextureFormat))) == ( float4(0.0, 1.0, 0.0, 0.0))).xy;
        u_xlat16_28 = (u_xlatb7.y) ? u_xlat16_0.w : u_xlat16_0.x;
        u_xlat16_8.xyz = (u_xlatb7.x) ? u_xlat16_0.xyz : float3(u_xlat16_28,u_xlat16_28,u_xlat16_28);
    } else {
        u_xlat16_8.x = float(1.0);
        u_xlat16_8.y = float(1.0);
        u_xlat16_8.z = float(1.0);
    }
    u_xlat16_8.xyz = u_xlat16_8.xyz * _MainLightColor.xyz;
    u_xlat16_28 = dot((-u_xlat16_3.xyz), u_xlat2.xyz);
    u_xlat16_28 = u_xlat16_28 + u_xlat16_28;
    u_xlat16_9.xyz = u_xlat2.xyz * (-float3(u_xlat16_28,u_xlat16_28,u_xlat16_28)) + (-u_xlat16_3.xyz);
    u_xlat16_28 = dot(u_xlat2.xyz, u_xlat16_3.xyz);
    u_xlat16_28 = clamp(u_xlat16_28, 0.0, 1.0);
    u_xlat16_28 = (-u_xlat16_28) + 1.0;
    u_xlat16_28 = u_xlat16_28 * u_xlat16_28;
    u_xlat16_28 = u_xlat16_28 * u_xlat16_28;
    u_xlat16_39 = (-u_xlat16_36) * 0.699999988 + 1.70000005;
    u_xlat16_36 = u_xlat16_36 * u_xlat16_39;
    u_xlat16_36 = u_xlat16_36 * 6.0;
    u_xlat16_0 = Map13SampleSpecCube0Fragment( u_xlat16_9.xyz, u_xlat16_36);
    u_xlat16_36 = u_xlat16_0.w + -1.0;
    u_xlat16_36 = unity_SpecCube0_HDR.w * u_xlat16_36 + 1.0;
    u_xlat16_36 = max(u_xlat16_36, 0.0);
    u_xlat16_36 = log2(u_xlat16_36);
    u_xlat16_36 = u_xlat16_36 * unity_SpecCube0_HDR.y;
    u_xlat16_36 = exp2(u_xlat16_36);
    u_xlat16_36 = u_xlat16_36 * unity_SpecCube0_HDR.x;
    u_xlat16_9.xyz = u_xlat16_0.xyz * float3(u_xlat16_36,u_xlat16_36,u_xlat16_36);
    u_xlat16_10.xy = float2(u_xlat16_38,u_xlat16_38) * float2(u_xlat16_38,u_xlat16_38) + float2(-1.0, 1.0);
    u_xlat16_36 = float(1.0) / u_xlat16_10.y;
    u_xlat16_21.xyz = (-u_xlat16_5.xyz) + float3(u_xlat16_4,u_xlat16_4,u_xlat16_4);
    u_xlat16_21.xyz = float3(u_xlat16_28,u_xlat16_28,u_xlat16_28) * u_xlat16_21.xyz + u_xlat16_5.xyz;
    u_xlat0.xyz = float3(u_xlat16_36,u_xlat16_36,u_xlat16_36) * u_xlat16_21.xyz;
    u_xlat16_9.xyz = u_xlat0.xyz * u_xlat16_9.xyz;
    u_xlat16_9.xyz = input.uv8.xyz * u_xlat16_15.xyz + u_xlat16_9.xyz;
    u_xlat16_36 = dot(u_xlat2.xyz, _MainLightPosition.xyz);
    u_xlat16_36 = clamp(u_xlat16_36, 0.0, 1.0);
    u_xlat16_36 = u_xlat16_36 * unity_LightData.z;
    u_xlat16_8.xyz = float3(u_xlat16_36,u_xlat16_36,u_xlat16_36) * u_xlat16_8.xyz;
    u_xlat0.xyz = u_xlat16_3.xyz + _MainLightPosition.xyz;
    u_xlat33 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat33 = max(u_xlat33, 1.17549435e-38);
    u_xlat33 = rsqrt(u_xlat33);
    u_xlat0.xyz = float3(u_xlat33,u_xlat33,u_xlat33) * u_xlat0.xyz;
    u_xlat33 = dot(u_xlat2.xyz, u_xlat0.xyz);
    u_xlat33 = clamp(u_xlat33, 0.0, 1.0);
    u_xlat0.x = dot(_MainLightPosition.xyz, u_xlat0.xyz);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat11 = u_xlat33 * u_xlat33;
    u_xlat11 = u_xlat11 * u_xlat16_10.x + 1.00001001;
    u_xlat16_3.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = u_xlat11 * u_xlat11;
    u_xlat11 = max(u_xlat16_3.x, 0.100000001);
    u_xlat0.x = u_xlat11 * u_xlat0.x;
    u_xlat0.x = u_xlat16_17 * u_xlat0.x;
    u_xlat0.x = u_xlat16_6 / u_xlat0.x;
    u_xlat16_3.x = u_xlat0.x + -6.10351562e-05;
    u_xlat16_3.x = max(u_xlat16_3.x, 0.0);
    u_xlat16_3.x = min(u_xlat16_3.x, 1000.0);
    u_xlat16_3.xyz = u_xlat16_5.xyz * u_xlat16_3.xxx + u_xlat16_15.xyz;
    u_xlat16_1.xyz = u_xlat16_3.xyz * u_xlat16_8.xyz + u_xlat16_9.xyz;
    u_xlat16_0 = min(u_xlat16_1, float4(65504.0, 65504.0, 65504.0, 65504.0));
    u_xlatb2 = _Surface==1.0;
    result.w = (u_xlatb2) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#elif defined(EVALUATE_SH_VERTEX) && !defined(LOD_FADE_CROSSFADE) && defined(_LIGHT_COOKIES)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float uv5:TEXCOORD5; float3 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv8:TEXCOORD8; };

float _MainLightCookieTextureFormat;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_DetailAlbedoMap_ST;
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
float4 _BaseMap_ST;
float4 _MainLightPosition;
float4x4 _MainLightWorldToLight;
half Xhlslcc_UnusedX_BumpScale;
half Xhlslcc_UnusedX_ClearCoatMask;
half Xhlslcc_UnusedX_ClearCoatSmoothness;
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DetailAlbedoMapScale;
half Xhlslcc_UnusedX_DetailNormalMapScale;
half Xhlslcc_UnusedX_OcclusionStrength;
half Xhlslcc_UnusedX_Parallax;
half _Metallic;
half _Smoothness;
half _Surface;
half4 Xhlslcc_UnusedX_EmissionColor;
half4 Xhlslcc_UnusedX_SpecColor;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _BaseColor;
half4 _MainLightColor;
half4 unity_LightData;
sampler2D _BaseMap;
sampler2D _MainLightCookieTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half4 u_xlat16_3;
half3 u_xlat16_4;
float u_xlat15;

    output.uv.xy = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    output.world.xyz = u_xlat0.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = max(u_xlat15, 1.17549435e-38);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    output.mask.xyz = u_xlat1.xyz;
    output.uv5 = 0.0;
    u_xlat16_2.x = u_xlat1.y * u_xlat1.y;
    u_xlat16_2.x = u_xlat1.x * u_xlat1.x + (-u_xlat16_2.x);
    u_xlat16_3 = u_xlat1.yzzx * u_xlat1.xyzz;
    u_xlat16_4.x = dot(unity_SHBr, u_xlat16_3);
    u_xlat16_4.y = dot(unity_SHBg, u_xlat16_3);
    u_xlat16_4.z = dot(unity_SHBb, u_xlat16_3);
    u_xlat16_2.xyz = unity_SHC.xyz * u_xlat16_2.xxx + u_xlat16_4.xyz;
    u_xlat1.w = 1.0;
    u_xlat16_3.x = dot(unity_SHAr, u_xlat1);
    u_xlat16_3.y = dot(unity_SHAg, u_xlat1);
    u_xlat16_3.z = dot(unity_SHAb, u_xlat1);
    u_xlat16_2.xyz = u_xlat16_2.xyz + u_xlat16_3.xyz;
    output.uv8.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
float4 Map13SampleSpecCube0Fragment(float3 direction, float lod) { return UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, direction, lod); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half4 u_xlat16_1;
float3 u_xlat2;
bool u_xlatb2;
half3 u_xlat16_3;
half u_xlat16_4;
half3 u_xlat16_5;
half u_xlat16_6;
bool2 u_xlatb7;
half3 u_xlat16_8;
half3 u_xlat16_9;
half2 u_xlat16_10;
float u_xlat11;
half3 u_xlat16_15;
half u_xlat16_17;
half3 u_xlat16_21;
half u_xlat16_28;
float u_xlat33;
bool u_xlatb33;
float u_xlat35;
half u_xlat16_36;
half u_xlat16_38;
half u_xlat16_39;

    u_xlat16_0 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = u_xlat16_0 * _BaseColor;
    u_xlatb33 = unity_OrthoParams.w==0.0;
    u_xlat2.xyz = (-input.world.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat35 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat35 = rsqrt(u_xlat35);
    u_xlat2.xyz = float3(u_xlat35,u_xlat35,u_xlat35) * u_xlat2.xyz;
    u_xlat16_3.x = (u_xlatb33) ? u_xlat2.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat16_3.y = (u_xlatb33) ? u_xlat2.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat16_3.z = (u_xlatb33) ? u_xlat2.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat33 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat33 = rsqrt(u_xlat33);
    u_xlat2.xyz = float3(u_xlat33,u_xlat33,u_xlat33) * input.mask.xyz;
    u_xlat16_36 = (-_Metallic) * 0.959999979 + 0.959999979;
    u_xlat16_4 = (-u_xlat16_36) + _Smoothness;
    u_xlat16_15.xyz = u_xlat16_1.xyz * float3(u_xlat16_36,u_xlat16_36,u_xlat16_36);
    u_xlat16_5.xyz = u_xlat16_0.xyz * _BaseColor.xyz + float3(-0.0399999991, -0.0399999991, -0.0399999991);
    u_xlat16_5.xyz = float3(float3(_Metallic, _Metallic, _Metallic)) * u_xlat16_5.xyz + float3(0.0399999991, 0.0399999991, 0.0399999991);
    u_xlat16_36 = (-_Smoothness) + 1.0;
    u_xlat16_38 = u_xlat16_36 * u_xlat16_36;
    u_xlat16_38 = max(u_xlat16_38, 0.0078125);
    u_xlat16_6 = u_xlat16_38 * u_xlat16_38;
    u_xlat16_4 = u_xlat16_4 + 1.0;
    u_xlat16_4 = clamp(u_xlat16_4, 0.0, 1.0);
    u_xlat16_17 = u_xlat16_38 * 4.0 + 2.0;
    u_xlatb0 = _MainLightCookieTextureFormat!=-1.0;
    if(u_xlatb0){
        u_xlat0.xy = input.world.yy * float4(_MainLightWorldToLight[0][1],_MainLightWorldToLight[1][1],_MainLightWorldToLight[2][1],_MainLightWorldToLight[3][1]).xy;
        u_xlat0.xy = float4(_MainLightWorldToLight[0][0],_MainLightWorldToLight[1][0],_MainLightWorldToLight[2][0],_MainLightWorldToLight[3][0]).xy * input.world.xx + u_xlat0.xy;
        u_xlat0.xy = float4(_MainLightWorldToLight[0][2],_MainLightWorldToLight[1][2],_MainLightWorldToLight[2][2],_MainLightWorldToLight[3][2]).xy * input.world.zz + u_xlat0.xy;
        u_xlat0.xy = u_xlat0.xy + float4(_MainLightWorldToLight[0][3],_MainLightWorldToLight[1][3],_MainLightWorldToLight[2][3],_MainLightWorldToLight[3][3]).xy;
        u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
        u_xlat16_0 = tex2Dbias(_MainLightCookieTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x));
        u_xlatb7.xy = ((float4(float4(_MainLightCookieTextureFormat, _MainLightCookieTextureFormat, _MainLightCookieTextureFormat, _MainLightCookieTextureFormat))) == ( float4(0.0, 1.0, 0.0, 0.0))).xy;
        u_xlat16_28 = (u_xlatb7.y) ? u_xlat16_0.w : u_xlat16_0.x;
        u_xlat16_8.xyz = (u_xlatb7.x) ? u_xlat16_0.xyz : float3(u_xlat16_28,u_xlat16_28,u_xlat16_28);
    } else {
        u_xlat16_8.x = float(1.0);
        u_xlat16_8.y = float(1.0);
        u_xlat16_8.z = float(1.0);
    }
    u_xlat16_8.xyz = u_xlat16_8.xyz * _MainLightColor.xyz;
    u_xlat16_28 = dot((-u_xlat16_3.xyz), u_xlat2.xyz);
    u_xlat16_28 = u_xlat16_28 + u_xlat16_28;
    u_xlat16_9.xyz = u_xlat2.xyz * (-float3(u_xlat16_28,u_xlat16_28,u_xlat16_28)) + (-u_xlat16_3.xyz);
    u_xlat16_28 = dot(u_xlat2.xyz, u_xlat16_3.xyz);
    u_xlat16_28 = clamp(u_xlat16_28, 0.0, 1.0);
    u_xlat16_28 = (-u_xlat16_28) + 1.0;
    u_xlat16_28 = u_xlat16_28 * u_xlat16_28;
    u_xlat16_28 = u_xlat16_28 * u_xlat16_28;
    u_xlat16_39 = (-u_xlat16_36) * 0.699999988 + 1.70000005;
    u_xlat16_36 = u_xlat16_36 * u_xlat16_39;
    u_xlat16_36 = u_xlat16_36 * 6.0;
    u_xlat16_0 = Map13SampleSpecCube0Fragment( u_xlat16_9.xyz, u_xlat16_36);
    u_xlat16_36 = u_xlat16_0.w + -1.0;
    u_xlat16_36 = unity_SpecCube0_HDR.w * u_xlat16_36 + 1.0;
    u_xlat16_36 = max(u_xlat16_36, 0.0);
    u_xlat16_36 = log2(u_xlat16_36);
    u_xlat16_36 = u_xlat16_36 * unity_SpecCube0_HDR.y;
    u_xlat16_36 = exp2(u_xlat16_36);
    u_xlat16_36 = u_xlat16_36 * unity_SpecCube0_HDR.x;
    u_xlat16_9.xyz = u_xlat16_0.xyz * float3(u_xlat16_36,u_xlat16_36,u_xlat16_36);
    u_xlat16_10.xy = float2(u_xlat16_38,u_xlat16_38) * float2(u_xlat16_38,u_xlat16_38) + float2(-1.0, 1.0);
    u_xlat16_36 = float(1.0) / u_xlat16_10.y;
    u_xlat16_21.xyz = (-u_xlat16_5.xyz) + float3(u_xlat16_4,u_xlat16_4,u_xlat16_4);
    u_xlat16_21.xyz = float3(u_xlat16_28,u_xlat16_28,u_xlat16_28) * u_xlat16_21.xyz + u_xlat16_5.xyz;
    u_xlat0.xyz = float3(u_xlat16_36,u_xlat16_36,u_xlat16_36) * u_xlat16_21.xyz;
    u_xlat16_9.xyz = u_xlat0.xyz * u_xlat16_9.xyz;
    u_xlat16_9.xyz = input.uv8.xyz * u_xlat16_15.xyz + u_xlat16_9.xyz;
    u_xlat16_36 = dot(u_xlat2.xyz, _MainLightPosition.xyz);
    u_xlat16_36 = clamp(u_xlat16_36, 0.0, 1.0);
    u_xlat16_36 = u_xlat16_36 * unity_LightData.z;
    u_xlat16_8.xyz = float3(u_xlat16_36,u_xlat16_36,u_xlat16_36) * u_xlat16_8.xyz;
    u_xlat0.xyz = u_xlat16_3.xyz + _MainLightPosition.xyz;
    u_xlat33 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat33 = max(u_xlat33, 1.17549435e-38);
    u_xlat33 = rsqrt(u_xlat33);
    u_xlat0.xyz = float3(u_xlat33,u_xlat33,u_xlat33) * u_xlat0.xyz;
    u_xlat33 = dot(u_xlat2.xyz, u_xlat0.xyz);
    u_xlat33 = clamp(u_xlat33, 0.0, 1.0);
    u_xlat0.x = dot(_MainLightPosition.xyz, u_xlat0.xyz);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat11 = u_xlat33 * u_xlat33;
    u_xlat11 = u_xlat11 * u_xlat16_10.x + 1.00001001;
    u_xlat16_3.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = u_xlat11 * u_xlat11;
    u_xlat11 = max(u_xlat16_3.x, 0.100000001);
    u_xlat0.x = u_xlat11 * u_xlat0.x;
    u_xlat0.x = u_xlat16_17 * u_xlat0.x;
    u_xlat0.x = u_xlat16_6 / u_xlat0.x;
    u_xlat16_3.x = u_xlat0.x + -6.10351562e-05;
    u_xlat16_3.x = max(u_xlat16_3.x, 0.0);
    u_xlat16_3.x = min(u_xlat16_3.x, 1000.0);
    u_xlat16_3.xyz = u_xlat16_5.xyz * u_xlat16_3.xxx + u_xlat16_15.xyz;
    u_xlat16_1.xyz = u_xlat16_3.xyz * u_xlat16_8.xyz + u_xlat16_9.xyz;
    u_xlat16_0 = min(u_xlat16_1, float4(65504.0, 65504.0, 65504.0, 65504.0));
    u_xlatb2 = _Surface==1.0;
    result.w = (u_xlatb2) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#elif defined(EVALUATE_SH_VERTEX) && defined(LOD_FADE_CROSSFADE) && !defined(_LIGHT_COOKIES)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float uv5:TEXCOORD5; float3 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv8:TEXCOORD8; };

float _DitheringTextureInvSize;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_DetailAlbedoMap_ST;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
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
float4 _BaseMap_ST;
float4 _MainLightPosition;
half Xhlslcc_UnusedX_BumpScale;
half Xhlslcc_UnusedX_ClearCoatMask;
half Xhlslcc_UnusedX_ClearCoatSmoothness;
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DetailAlbedoMapScale;
half Xhlslcc_UnusedX_DetailNormalMapScale;
half Xhlslcc_UnusedX_OcclusionStrength;
half Xhlslcc_UnusedX_Parallax;
half _Metallic;
half _Smoothness;
half _Surface;
half4 Xhlslcc_UnusedX_EmissionColor;
half4 Xhlslcc_UnusedX_SpecColor;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _BaseColor;
half4 _MainLightColor;
half4 unity_LightData;
sampler2D _BaseMap;
sampler2D _DitheringTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half4 u_xlat16_3;
half3 u_xlat16_4;
float u_xlat15;

    output.uv.xy = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    output.world.xyz = u_xlat0.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = max(u_xlat15, 1.17549435e-38);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    output.mask.xyz = u_xlat1.xyz;
    output.uv5 = 0.0;
    u_xlat16_2.x = u_xlat1.y * u_xlat1.y;
    u_xlat16_2.x = u_xlat1.x * u_xlat1.x + (-u_xlat16_2.x);
    u_xlat16_3 = u_xlat1.yzzx * u_xlat1.xyzz;
    u_xlat16_4.x = dot(unity_SHBr, u_xlat16_3);
    u_xlat16_4.y = dot(unity_SHBg, u_xlat16_3);
    u_xlat16_4.z = dot(unity_SHBb, u_xlat16_3);
    u_xlat16_2.xyz = unity_SHC.xyz * u_xlat16_2.xxx + u_xlat16_4.xyz;
    u_xlat1.w = 1.0;
    u_xlat16_3.x = dot(unity_SHAr, u_xlat1);
    u_xlat16_3.y = dot(unity_SHAg, u_xlat1);
    u_xlat16_3.z = dot(unity_SHAb, u_xlat1);
    u_xlat16_2.xyz = u_xlat16_2.xyz + u_xlat16_3.xyz;
    output.uv8.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
float4 Map13SampleSpecCube0Fragment(float3 direction, float lod) { return UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, direction, lod); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half2 u_xlat16_1;
half4 u_xlat16_2;
half4 u_xlat16_3;
float3 u_xlat4;
bool u_xlatb4;
half3 u_xlat16_5;
half3 u_xlat16_6;
float3 u_xlat7;
float u_xlat8;
bool u_xlatb8;
half3 u_xlat16_9;
half u_xlat16_10;
half3 u_xlat16_14;
half u_xlat16_17;
half u_xlat16_18;
float u_xlat24;
bool u_xlatb24;
half u_xlat16_26;
half u_xlat16_29;

float4 hlslcc_FragCoord = float4(input.position.xyz, 1.0/input.position.w);
    u_xlat0.xy = hlslcc_FragCoord.xy * float2(float2(_DitheringTextureInvSize, _DitheringTextureInvSize));
    u_xlat16_0.x = tex2Dbias(_DitheringTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).w;
    u_xlatb8 = unity_LODFade.x>=0.0;
    u_xlat0.x = (u_xlatb8) ? abs(u_xlat16_0.x) : -abs(u_xlat16_0.x);
    u_xlat0.x = (-u_xlat0.x) + unity_LODFade.x;
    u_xlatb0 = u_xlat0.x<0.0;
    if(u_xlatb0){clip(-1);}
    u_xlat16_1.x = (-_Smoothness) + 1.0;
    u_xlat16_9.x = (-u_xlat16_1.x) * 0.699999988 + 1.70000005;
    u_xlat16_9.x = u_xlat16_9.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_9.x = u_xlat16_9.x * 6.0;
    u_xlat0.xyz = (-input.world.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat24 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat24 = rsqrt(u_xlat24);
    u_xlat0.xyz = float3(u_xlat24,u_xlat24,u_xlat24) * u_xlat0.xyz;
    u_xlatb24 = unity_OrthoParams.w==0.0;
    u_xlat16_2.x = (u_xlatb24) ? u_xlat0.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat16_2.y = (u_xlatb24) ? u_xlat0.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat16_2.z = (u_xlatb24) ? u_xlat0.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat0.x = dot(input.mask.xyz, input.mask.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * input.mask.xyz;
    u_xlat16_17 = dot((-u_xlat16_2.xyz), u_xlat0.xyz);
    u_xlat16_17 = u_xlat16_17 + u_xlat16_17;
    u_xlat16_3.xyz = u_xlat0.xyz * (-float3(u_xlat16_17,u_xlat16_17,u_xlat16_17)) + (-u_xlat16_2.xyz);
    u_xlat16_3 = Map13SampleSpecCube0Fragment( u_xlat16_3.xyz, u_xlat16_9.x);
    u_xlat16_9.x = u_xlat16_3.w + -1.0;
    u_xlat16_1.y = unity_SpecCube0_HDR.w * u_xlat16_9.x + 1.0;
    u_xlat16_1.xy = max(u_xlat16_1.xy, float2(0.0078125, 0.0));
    u_xlat16_9.x = log2(u_xlat16_1.y);
    u_xlat16_9.x = u_xlat16_9.x * unity_SpecCube0_HDR.y;
    u_xlat16_9.x = exp2(u_xlat16_9.x);
    u_xlat16_9.x = u_xlat16_9.x * unity_SpecCube0_HDR.x;
    u_xlat16_9.xyz = u_xlat16_3.xyz * u_xlat16_9.xxx;
    u_xlat16_26 = dot(u_xlat0.xyz, u_xlat16_2.xyz);
    u_xlat16_26 = clamp(u_xlat16_26, 0.0, 1.0);
    u_xlat4.xyz = u_xlat16_2.xyz + _MainLightPosition.xyz;
    u_xlat16_2.x = (-u_xlat16_26) + 1.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_10 = (-_Metallic) * 0.959999979 + 0.959999979;
    u_xlat16_18 = (-u_xlat16_10) + _Smoothness;
    u_xlat16_18 = u_xlat16_18 + 1.0;
    u_xlat16_18 = clamp(u_xlat16_18, 0.0, 1.0);
    u_xlat16_3 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_5.xyz = u_xlat16_3.xyz * _BaseColor.xyz + float3(-0.0399999991, -0.0399999991, -0.0399999991);
    u_xlat16_3 = u_xlat16_3 * _BaseColor;
    u_xlat16_5.xyz = float3(float3(_Metallic, _Metallic, _Metallic)) * u_xlat16_5.xyz + float3(0.0399999991, 0.0399999991, 0.0399999991);
    u_xlat16_6.xyz = float3(u_xlat16_18,u_xlat16_18,u_xlat16_18) + (-u_xlat16_5.xyz);
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_6.xyz + u_xlat16_5.xyz;
    u_xlat16_6.xy = u_xlat16_1.xx * u_xlat16_1.xx + float2(-1.0, 1.0);
    u_xlat16_29 = float(1.0) / u_xlat16_6.y;
    u_xlat7.xyz = u_xlat16_2.xzw * float3(u_xlat16_29,u_xlat16_29,u_xlat16_29);
    u_xlat16_9.xyz = u_xlat16_9.xyz * u_xlat7.xyz;
    u_xlat16_2.xyz = float3(u_xlat16_10,u_xlat16_10,u_xlat16_10) * u_xlat16_3.xyz;
    u_xlat16_9.xyz = input.uv8.xyz * u_xlat16_2.xyz + u_xlat16_9.xyz;
    u_xlat24 = dot(u_xlat4.xyz, u_xlat4.xyz);
    u_xlat24 = max(u_xlat24, 1.17549435e-38);
    u_xlat24 = rsqrt(u_xlat24);
    u_xlat4.xyz = float3(u_xlat24,u_xlat24,u_xlat24) * u_xlat4.xyz;
    u_xlat24 = dot(_MainLightPosition.xyz, u_xlat4.xyz);
    u_xlat24 = clamp(u_xlat24, 0.0, 1.0);
    u_xlat4.x = dot(u_xlat0.xyz, u_xlat4.xyz);
    u_xlat4.x = clamp(u_xlat4.x, 0.0, 1.0);
    u_xlat16_26 = dot(u_xlat0.xyz, _MainLightPosition.xyz);
    u_xlat16_26 = clamp(u_xlat16_26, 0.0, 1.0);
    u_xlat16_26 = u_xlat16_26 * unity_LightData.z;
    u_xlat16_14.xyz = float3(u_xlat16_26,u_xlat16_26,u_xlat16_26) * _MainLightColor.xyz;
    u_xlat0.x = u_xlat4.x * u_xlat4.x;
    u_xlat0.x = u_xlat0.x * u_xlat16_6.x + 1.00001001;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat16_26 = u_xlat24 * u_xlat24;
    u_xlat8 = max(u_xlat16_26, 0.100000001);
    u_xlat0.x = u_xlat8 * u_xlat0.x;
    u_xlat16_26 = u_xlat16_1.x * 4.0 + 2.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat0.x = u_xlat0.x * u_xlat16_26;
    u_xlat0.x = u_xlat16_1.x / u_xlat0.x;
    u_xlat16_1.x = u_xlat0.x + -6.10351562e-05;
    u_xlat16_1.x = max(u_xlat16_1.x, 0.0);
    u_xlat16_1.x = min(u_xlat16_1.x, 1000.0);
    u_xlat16_2.xyz = u_xlat16_5.xyz * u_xlat16_1.xxx + u_xlat16_2.xyz;
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_14.xyz + u_xlat16_9.xyz;
    u_xlat16_0 = min(u_xlat16_3, float4(65504.0, 65504.0, 65504.0, 65504.0));
    u_xlatb4 = _Surface==1.0;
    result.w = (u_xlatb4) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#elif defined(EVALUATE_SH_VERTEX) && !defined(LOD_FADE_CROSSFADE) && !defined(_LIGHT_COOKIES)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float uv5:TEXCOORD5; float3 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv8:TEXCOORD8; };

float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_DetailAlbedoMap_ST;
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
float4 _BaseMap_ST;
float4 _MainLightPosition;
half Xhlslcc_UnusedX_BumpScale;
half Xhlslcc_UnusedX_ClearCoatMask;
half Xhlslcc_UnusedX_ClearCoatSmoothness;
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DetailAlbedoMapScale;
half Xhlslcc_UnusedX_DetailNormalMapScale;
half Xhlslcc_UnusedX_OcclusionStrength;
half Xhlslcc_UnusedX_Parallax;
half _Metallic;
half _Smoothness;
half _Surface;
half4 Xhlslcc_UnusedX_EmissionColor;
half4 Xhlslcc_UnusedX_SpecColor;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _BaseColor;
half4 _MainLightColor;
half4 unity_LightData;
sampler2D _BaseMap;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half4 u_xlat16_3;
half3 u_xlat16_4;
float u_xlat15;

    output.uv.xy = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    output.world.xyz = u_xlat0.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = max(u_xlat15, 1.17549435e-38);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    output.mask.xyz = u_xlat1.xyz;
    output.uv5 = 0.0;
    u_xlat16_2.x = u_xlat1.y * u_xlat1.y;
    u_xlat16_2.x = u_xlat1.x * u_xlat1.x + (-u_xlat16_2.x);
    u_xlat16_3 = u_xlat1.yzzx * u_xlat1.xyzz;
    u_xlat16_4.x = dot(unity_SHBr, u_xlat16_3);
    u_xlat16_4.y = dot(unity_SHBg, u_xlat16_3);
    u_xlat16_4.z = dot(unity_SHBb, u_xlat16_3);
    u_xlat16_2.xyz = unity_SHC.xyz * u_xlat16_2.xxx + u_xlat16_4.xyz;
    u_xlat1.w = 1.0;
    u_xlat16_3.x = dot(unity_SHAr, u_xlat1);
    u_xlat16_3.y = dot(unity_SHAg, u_xlat1);
    u_xlat16_3.z = dot(unity_SHAb, u_xlat1);
    u_xlat16_2.xyz = u_xlat16_2.xyz + u_xlat16_3.xyz;
    output.uv8.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
float4 Map13SampleSpecCube0Fragment(float3 direction, float lod) { return UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, direction, lod); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float3 u_xlat1;
bool u_xlatb1;
half4 u_xlat16_2;
half4 u_xlat16_3;
float3 u_xlat4;
half3 u_xlat16_5;
half3 u_xlat16_6;
float3 u_xlat7;
half3 u_xlat16_8;
float u_xlat9;
half u_xlat16_10;
half3 u_xlat16_14;
half u_xlat16_16;
half u_xlat16_18;
float u_xlat25;
bool u_xlatb25;
half u_xlat16_26;
half u_xlat16_29;

    u_xlat16_0.x = (-_Smoothness) + 1.0;
    u_xlat16_8.x = (-u_xlat16_0.x) * 0.699999988 + 1.70000005;
    u_xlat16_8.x = u_xlat16_8.x * u_xlat16_0.x;
    u_xlat16_0.x = u_xlat16_0.x * u_xlat16_0.x;
    u_xlat16_8.x = u_xlat16_8.x * 6.0;
    u_xlat1.xyz = (-input.world.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat25 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat25 = rsqrt(u_xlat25);
    u_xlat1.xyz = float3(u_xlat25,u_xlat25,u_xlat25) * u_xlat1.xyz;
    u_xlatb25 = unity_OrthoParams.w==0.0;
    u_xlat16_2.x = (u_xlatb25) ? u_xlat1.x : float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat16_2.y = (u_xlatb25) ? u_xlat1.y : float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat16_2.z = (u_xlatb25) ? u_xlat1.z : float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat1.x = dot(input.mask.xyz, input.mask.xyz);
    u_xlat1.x = rsqrt(u_xlat1.x);
    u_xlat1.xyz = u_xlat1.xxx * input.mask.xyz;
    u_xlat16_16 = dot((-u_xlat16_2.xyz), u_xlat1.xyz);
    u_xlat16_16 = u_xlat16_16 + u_xlat16_16;
    u_xlat16_3.xyz = u_xlat1.xyz * (-float3(u_xlat16_16,u_xlat16_16,u_xlat16_16)) + (-u_xlat16_2.xyz);
    u_xlat16_3 = Map13SampleSpecCube0Fragment( u_xlat16_3.xyz, u_xlat16_8.x);
    u_xlat16_8.x = u_xlat16_3.w + -1.0;
    u_xlat16_0.y = unity_SpecCube0_HDR.w * u_xlat16_8.x + 1.0;
    u_xlat16_0.xy = max(u_xlat16_0.xy, float2(0.0078125, 0.0));
    u_xlat16_8.x = log2(u_xlat16_0.y);
    u_xlat16_8.x = u_xlat16_8.x * unity_SpecCube0_HDR.y;
    u_xlat16_8.x = exp2(u_xlat16_8.x);
    u_xlat16_8.x = u_xlat16_8.x * unity_SpecCube0_HDR.x;
    u_xlat16_8.xyz = u_xlat16_3.xyz * u_xlat16_8.xxx;
    u_xlat16_26 = dot(u_xlat1.xyz, u_xlat16_2.xyz);
    u_xlat16_26 = clamp(u_xlat16_26, 0.0, 1.0);
    u_xlat4.xyz = u_xlat16_2.xyz + _MainLightPosition.xyz;
    u_xlat16_2.x = (-u_xlat16_26) + 1.0;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_2.x = u_xlat16_2.x * u_xlat16_2.x;
    u_xlat16_10 = (-_Metallic) * 0.959999979 + 0.959999979;
    u_xlat16_18 = (-u_xlat16_10) + _Smoothness;
    u_xlat16_18 = u_xlat16_18 + 1.0;
    u_xlat16_18 = clamp(u_xlat16_18, 0.0, 1.0);
    u_xlat16_3 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_5.xyz = u_xlat16_3.xyz * _BaseColor.xyz + float3(-0.0399999991, -0.0399999991, -0.0399999991);
    u_xlat16_3 = u_xlat16_3 * _BaseColor;
    u_xlat16_5.xyz = float3(float3(_Metallic, _Metallic, _Metallic)) * u_xlat16_5.xyz + float3(0.0399999991, 0.0399999991, 0.0399999991);
    u_xlat16_6.xyz = float3(u_xlat16_18,u_xlat16_18,u_xlat16_18) + (-u_xlat16_5.xyz);
    u_xlat16_2.xzw = u_xlat16_2.xxx * u_xlat16_6.xyz + u_xlat16_5.xyz;
    u_xlat16_6.xy = u_xlat16_0.xx * u_xlat16_0.xx + float2(-1.0, 1.0);
    u_xlat16_29 = float(1.0) / u_xlat16_6.y;
    u_xlat7.xyz = u_xlat16_2.xzw * float3(u_xlat16_29,u_xlat16_29,u_xlat16_29);
    u_xlat16_8.xyz = u_xlat16_8.xyz * u_xlat7.xyz;
    u_xlat16_2.xyz = float3(u_xlat16_10,u_xlat16_10,u_xlat16_10) * u_xlat16_3.xyz;
    u_xlat16_8.xyz = input.uv8.xyz * u_xlat16_2.xyz + u_xlat16_8.xyz;
    u_xlat25 = dot(u_xlat4.xyz, u_xlat4.xyz);
    u_xlat25 = max(u_xlat25, 1.17549435e-38);
    u_xlat25 = rsqrt(u_xlat25);
    u_xlat4.xyz = float3(u_xlat25,u_xlat25,u_xlat25) * u_xlat4.xyz;
    u_xlat25 = dot(_MainLightPosition.xyz, u_xlat4.xyz);
    u_xlat25 = clamp(u_xlat25, 0.0, 1.0);
    u_xlat4.x = dot(u_xlat1.xyz, u_xlat4.xyz);
    u_xlat4.x = clamp(u_xlat4.x, 0.0, 1.0);
    u_xlat16_26 = dot(u_xlat1.xyz, _MainLightPosition.xyz);
    u_xlat16_26 = clamp(u_xlat16_26, 0.0, 1.0);
    u_xlat16_26 = u_xlat16_26 * unity_LightData.z;
    u_xlat16_14.xyz = float3(u_xlat16_26,u_xlat16_26,u_xlat16_26) * _MainLightColor.xyz;
    u_xlat1.x = u_xlat4.x * u_xlat4.x;
    u_xlat1.x = u_xlat1.x * u_xlat16_6.x + 1.00001001;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat16_26 = u_xlat25 * u_xlat25;
    u_xlat9 = max(u_xlat16_26, 0.100000001);
    u_xlat1.x = u_xlat9 * u_xlat1.x;
    u_xlat16_26 = u_xlat16_0.x * 4.0 + 2.0;
    u_xlat16_0.x = u_xlat16_0.x * u_xlat16_0.x;
    u_xlat1.x = u_xlat1.x * u_xlat16_26;
    u_xlat1.x = u_xlat16_0.x / u_xlat1.x;
    u_xlat16_0.x = u_xlat1.x + -6.10351562e-05;
    u_xlat16_0.x = max(u_xlat16_0.x, 0.0);
    u_xlat16_0.x = min(u_xlat16_0.x, 1000.0);
    u_xlat16_2.xyz = u_xlat16_5.xyz * u_xlat16_0.xxx + u_xlat16_2.xyz;
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_14.xyz + u_xlat16_8.xyz;
    u_xlat16_0 = min(u_xlat16_3, float4(65504.0, 65504.0, 65504.0, 65504.0));
    u_xlatb1 = _Surface==1.0;
    result.w = (u_xlatb1) ? u_xlat16_0.w : 1.0;
    result.xyz = u_xlat16_0.xyz;
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="UniversalGBuffer" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
Cull [_Cull]
ZWrite [_ZWrite]
ZTest LEqual
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
}
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="DepthOnly" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
Cull [_Cull]
ZWrite On
ZTest LEqual
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask R
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ LOD_FADE_CROSSFADE
#include "UnityCG.cginc"
#if !defined(LOD_FADE_CROSSFADE)
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

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


float4 hlslcc_FragCoord = float4(input.position.xyz, 1.0/input.position.w);
    result = hlslcc_FragCoord.z;
    return result;

}
#elif defined(LOD_FADE_CROSSFADE)
struct Input { float4 vertex:POSITION; };
struct Varying { float4 position:SV_POSITION;  };

float _DitheringTextureInvSize;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
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
sampler2D _DitheringTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
bool u_xlatb1;

float4 hlslcc_FragCoord = float4(input.position.xyz, 1.0/input.position.w);
    u_xlat0.xy = hlslcc_FragCoord.xy * float2(_DitheringTextureInvSize,_DitheringTextureInvSize);
    u_xlat16_0 = tex2Dbias(_DitheringTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).w;
    u_xlatb1 = unity_LODFade.x>=0.0;
    u_xlat0.x = (u_xlatb1) ? abs(u_xlat16_0) : -abs(u_xlat16_0);
    u_xlat0.x = (-u_xlat0.x) + unity_LODFade.x;
    u_xlatb0 = u_xlat0.x<0.0;
    if(u_xlatb0){clip(-1);}
    result = hlslcc_FragCoord.z;
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="DepthNormals" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
Cull [_Cull]
ZWrite On
ZTest LEqual
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ LOD_FADE_CROSSFADE
#include "UnityCG.cginc"
#if !defined(LOD_FADE_CROSSFADE)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; };
struct Varying { float4 position:SV_POSITION; float3 mask:TEXCOORD2; float3 uv5:TEXCOORD5; };

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
float u_xlat6;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    u_xlat0.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat0.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat0.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat6 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat6 = max(u_xlat6, 1.17549435e-38);
    u_xlat6 = rsqrt(u_xlat6);
    u_xlat0.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat0.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.uv5.xyz = float3(0.0, 0.0, 0.0);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;

    u_xlat0.x = dot(input.mask.xyz, input.mask.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * input.mask.xyz;
    result.xyz = u_xlat0.xyz;
    result.w = 0.0;
    return result;

}
#elif defined(LOD_FADE_CROSSFADE)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; };
struct Varying { float4 position:SV_POSITION; float3 mask:TEXCOORD2; float3 uv5:TEXCOORD5; };

float _DitheringTextureInvSize;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
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
sampler2D _DitheringTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float u_xlat6;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    u_xlat0.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat0.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat0.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat6 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat6 = max(u_xlat6, 1.17549435e-38);
    u_xlat6 = rsqrt(u_xlat6);
    u_xlat0.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat0.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.uv5.xyz = float3(0.0, 0.0, 0.0);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
bool u_xlatb1;

float4 hlslcc_FragCoord = float4(input.position.xyz, 1.0/input.position.w);
    u_xlat0.xy = hlslcc_FragCoord.xy * float2(float2(_DitheringTextureInvSize, _DitheringTextureInvSize));
    u_xlat16_0 = tex2Dbias(_DitheringTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).w;
    u_xlatb1 = unity_LODFade.x>=0.0;
    u_xlat0.x = (u_xlatb1) ? abs(u_xlat16_0) : -abs(u_xlat16_0);
    u_xlat0.x = (-u_xlat0.x) + unity_LODFade.x;
    u_xlatb0 = u_xlat0.x<0.0;
    if(u_xlatb0){clip(-1);}
    u_xlat0.x = dot(input.mask.xyz, input.mask.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * input.mask.xyz;
    result.xyz = u_xlat0.xyz;
    result.w = 0.0;
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="Universal2D" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
Cull [_Cull]
ZWrite [_ZWrite]
ZTest LEqual
Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float2 _GlobalMipBias;
float4 Xhlslcc_UnusedX_DetailAlbedoMap_ST;
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
float4 _BaseMap_ST;
half Xhlslcc_UnusedX_BumpScale;
half Xhlslcc_UnusedX_ClearCoatMask;
half Xhlslcc_UnusedX_ClearCoatSmoothness;
half Xhlslcc_UnusedX_Cutoff;
half Xhlslcc_UnusedX_DetailAlbedoMapScale;
half Xhlslcc_UnusedX_DetailNormalMapScale;
half Xhlslcc_UnusedX_Metallic;
half Xhlslcc_UnusedX_OcclusionStrength;
half Xhlslcc_UnusedX_Parallax;
half Xhlslcc_UnusedX_Smoothness;
half Xhlslcc_UnusedX_Surface;
half4 Xhlslcc_UnusedX_EmissionColor;
half4 Xhlslcc_UnusedX_SpecColor;
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
float4 u_xlat0;
float4 u_xlat1;

    output.uv.xy = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
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
half4 u_xlat16_0;

    u_xlat16_0 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    result = u_xlat16_0 * _BaseColor;
    return result;

}
ENDCG
}
}
Fallback Off
}
