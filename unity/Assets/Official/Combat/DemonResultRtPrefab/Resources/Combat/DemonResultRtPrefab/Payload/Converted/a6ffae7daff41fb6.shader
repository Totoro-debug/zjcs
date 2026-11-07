Shader "Spine/OutlineBlur" {
Properties {

_MainTex ("Main Texture", 2D) = "black" { }

_StencilRef ("Stencil Reference", Float) = 1.0

[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8.0

_ColorIntensity ("ColorIntensity", Range(0, 5)) = 1.0

_OutlineWidth ("Outline Width", Range(0, 10)) = 3.0

_OutlineColor ("Outline Color", Color) = (1,1,0,1)

_OutlineReferenceTexWidth ("Reference Texture Width", Float) = 1024.0

_ThresholdEnd ("Outline Threshold", Range(0, 1)) = 0.25

_OutlineSmoothness ("Outline Smoothness", Range(0, 1)) = 1.0

[MaterialToggle(_USE8NEIGHBOURHOOD_ON)] _Use8Neighbourhood ("Sample 8 Neighbours", Float) = 1.0

_OutlineOpaqueAlpha ("Opaque Alpha", Range(0, 1)) = 1.0

_OutlineMipLevel ("Outline Mip Level", Range(0, 3)) = 0.0

[Header(NoiseTex)] [MaterialToggle(_USE_NOISE)] _UseNoise ("UseNoise", Float) = 1.0

_NoiseTex ("NoiseTex", 2D) = "white" { }

_NoiseTex_MoveCenter ("NoiseTex_Move&Center", Vector) = (0,0,0.5,0.5)

[Enum(Rotate_Noise,0,Move_Noise,1)] _Noise_Rotator ("Noise_Rotator", Float) = 0.0

_Noise_Rota ("Noise_Rota", Float) = 0.0

[Header(DistortionTex)] [MaterialToggle(_USE_DISTORT)] _UseDistort ("UseDistort", Float) = 1.0

_DistortionTex ("DistortionTex", 2D) = "white" { }

_Distortion ("Distortion", Range(0, 1)) = 0.0

_DistortionTex_MoveCenter ("DistortionTex_Move&Center", Vector) = (0,0,0.5,0.5)

[Enum(Rotator_DistortionTex,0,Move_DistortionTex,1)] _DistortionTex_Rotator ("DistortionTex_Rotator", Float) = 0.0

_DistortionTex_Rota ("DistortionTex_Rota", Float) = 0.0

}
SubShader {
LOD 100
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Name "Outline"
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite On
ZTest LEqual
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref [_StencilRef] ReadMask 255 WriteMask 255 Comp [_StencilComp] Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ _USE8NEIGHBOURHOOD_ON
#pragma multi_compile __ _USE_NOISE
#include "UnityCG.cginc"
#if !defined(_USE8NEIGHBOURHOOD_ON) && !defined(_USE_NOISE)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float color:COLOR0; };

float _ColorIntensity;
float _OutlineMipLevel;
float _OutlineOpaqueAlpha;
float _OutlineSmoothness;
float _OutlineWidth;
float _ThresholdEnd;
float4 Xhlslcc_UnusedX_MainTex_ST;
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
float4 _MainTex_TexelSize;
float4 _OutlineColor;
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
int _OutlineReferenceTexWidth;
sampler2D _MainTex;
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
    output.uv.xy = input.uv.xy;
    output.color = input.color.w;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
bool u_xlatb1;
float3 u_xlat2;
float u_xlat3;
half u_xlat16_3;
bool u_xlatb3;
float u_xlat6;
half u_xlat16_6;
half u_xlat16_9;

    u_xlat0.x = float(_OutlineReferenceTexWidth);
    u_xlat0.x = u_xlat0.x * _MainTex_TexelSize.x;
    u_xlat0.x = _OutlineWidth / u_xlat0.x;
    u_xlat0.xy = u_xlat0.xx * _MainTex_TexelSize.xy;
    u_xlat0.w = (-u_xlat0.y);
    u_xlat1.xy = input.uv.xy;
    u_xlat1.z = _OutlineMipLevel;
    u_xlat0.z = 0.0;
    u_xlat2.xyz = u_xlat0.zwz + u_xlat1.xyz;
    u_xlat16_9 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat2.xyz = u_xlat0.zyz + u_xlat1.xyz;
    u_xlat16_3 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat3 = u_xlat16_9 + u_xlat16_3;
    u_xlat2.xyz = u_xlat0.xzz * float3(-1.0,1.0,1.0) + u_xlat1.xyz;
    u_xlat0.xzw = u_xlat0.xzz + u_xlat1.xyz;
    u_xlat16_0 = tex2Dlod(_MainTex, float4(u_xlat0.xz, 0, u_xlat0.w)).w;
    u_xlat16_6 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat3 = u_xlat16_6 + u_xlat3;
    u_xlat0.x = u_xlat16_0 + u_xlat3;
    u_xlat0.x = u_xlat0.x * input.color;
    u_xlat3 = (-_OutlineSmoothness) + 1.0;
    u_xlat6 = u_xlat3 * _ThresholdEnd;
    u_xlat3 = (-_ThresholdEnd) * u_xlat3 + _ThresholdEnd;
    u_xlat0.x = u_xlat0.x * 0.25 + (-u_xlat6);
    u_xlat0.x = u_xlat0.x / u_xlat3;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat3 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat3) + u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlatb3 = _OutlineOpaqueAlpha<u_xlat3;
    u_xlat1 = u_xlat0.xxxx * _OutlineColor;
    u_xlat0 = (bool(u_xlatb3)) ? float4(0.0,0.0,0.0,0.0) : u_xlat1;
    u_xlat1.x = u_xlat0.w + -0.00999999978;
    u_xlatb1 = u_xlat1.x<0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat0.xyz = u_xlat0.www * u_xlat0.xyz;
    result.w = u_xlat0.w;
    result.xyz = u_xlat0.xyz * (float3)(_ColorIntensity);
    return result;

}
#elif defined(_USE8NEIGHBOURHOOD_ON) && !defined(_USE_NOISE)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float color:COLOR0; };

float _ColorIntensity;
float _OutlineMipLevel;
float _OutlineOpaqueAlpha;
float _OutlineSmoothness;
float _OutlineWidth;
float _ThresholdEnd;
float4 Xhlslcc_UnusedX_MainTex_ST;
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
float4 _MainTex_TexelSize;
float4 _OutlineColor;
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
int _OutlineReferenceTexWidth;
sampler2D _MainTex;
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
    output.uv.xy = input.uv.xy;
    output.color = input.color.w;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
bool u_xlatb1;
float3 u_xlat2;
float u_xlat3;
half u_xlat16_3;
bool u_xlatb3;
float u_xlat6;
float u_xlat9;
half u_xlat16_9;
half u_xlat16_10;

    u_xlat0.x = float(_OutlineReferenceTexWidth);
    u_xlat0.x = u_xlat0.x * _MainTex_TexelSize.x;
    u_xlat0.x = _OutlineWidth / u_xlat0.x;
    u_xlat0.xy = u_xlat0.xx * _MainTex_TexelSize.xy;
    u_xlat0.w = (-u_xlat0.y);
    u_xlat1.xy = input.uv.xy;
    u_xlat1.z = _OutlineMipLevel;
    u_xlat0.z = 0.0;
    u_xlat2.xyz = u_xlat0.zwz + u_xlat1.xyz;
    u_xlat16_9 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat2.xyz = u_xlat0.zyz + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat16_9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xzz * float3(-1.0,1.0,1.0) + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xzz + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xyz * float3(-0.699999988,0.699999988,1.0) + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xyz * float3(0.699999988,0.699999988,1.0) + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xyz * float3(-0.699999988,-0.699999988,1.0) + u_xlat1.xyz;
    u_xlat0.xyz = u_xlat0.xyz * float3(0.699999988,-0.699999988,1.0) + u_xlat1.xyz;
    u_xlat16_0 = tex2Dlod(_MainTex, float4(u_xlat0.xy, 0, u_xlat0.z)).w;
    u_xlat16_3 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat3 = u_xlat16_3 + u_xlat9;
    u_xlat0.x = u_xlat16_0 + u_xlat3;
    u_xlat0.x = u_xlat0.x * input.color;
    u_xlat3 = (-_OutlineSmoothness) + 1.0;
    u_xlat6 = u_xlat3 * _ThresholdEnd;
    u_xlat3 = (-_ThresholdEnd) * u_xlat3 + _ThresholdEnd;
    u_xlat0.x = u_xlat0.x * 0.125 + (-u_xlat6);
    u_xlat0.x = u_xlat0.x / u_xlat3;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat3 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat3) + u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlatb3 = _OutlineOpaqueAlpha<u_xlat3;
    u_xlat1 = u_xlat0.xxxx * _OutlineColor;
    u_xlat0 = (bool(u_xlatb3)) ? float4(0.0,0.0,0.0,0.0) : u_xlat1;
    u_xlat1.x = u_xlat0.w + -0.00999999978;
    u_xlatb1 = u_xlat1.x<0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat0.xyz = u_xlat0.www * u_xlat0.xyz;
    result.w = u_xlat0.w;
    result.xyz = u_xlat0.xyz * (float3)(_ColorIntensity);
    return result;

}
#elif defined(_USE8NEIGHBOURHOOD_ON) && defined(_USE_NOISE)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float color:COLOR0; float2 mask:TEXCOORD2; };

float _ColorIntensity;
float _Noise_Rota;
float _Noise_Rotator;
float _OutlineMipLevel;
float _OutlineOpaqueAlpha;
float _OutlineSmoothness;
float _OutlineWidth;
float _ThresholdEnd;
float4 Xhlslcc_UnusedX_MainTex_ST;
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
float4 _MainTex_TexelSize;
float4 _NoiseTex_MoveCenter;
float4 _NoiseTex_ST;
float4 _OutlineColor;
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
int _OutlineReferenceTexWidth;
sampler2D _MainTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv.xy = input.uv.xy;
    output.color = input.color.w;
    u_xlat0.x = _Noise_Rota * _Time.y;
    u_xlat0.x = frac(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * 6.28299999 + (-_Noise_Rota);
    u_xlat0.x = _Noise_Rotator * u_xlat0.x + _Noise_Rota;
    u_xlat0.x = u_xlat0.x * 6.28318548;
    u_xlat1.x = cos(u_xlat0.x);
    u_xlat0.x = sin(u_xlat0.x);
    u_xlat2.z = u_xlat0.x;
    u_xlat2.y = u_xlat1.x;
    u_xlat2.x = (-u_xlat0.x);
    u_xlat0.xy = input.uv.xy * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
    u_xlat0.xy = u_xlat0.xy + (-_NoiseTex_MoveCenter.zw);
    u_xlat1.x = dot(u_xlat0.xy, u_xlat2.yz);
    u_xlat1.y = dot(u_xlat0.xy, u_xlat2.xy);
    u_xlat0.xy = u_xlat1.xy + _NoiseTex_MoveCenter.zw;
    output.mask.xy = _Time.yy * _NoiseTex_MoveCenter.xy + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
float3 u_xlat2;
float u_xlat3;
half u_xlat16_3;
bool u_xlatb3;
float u_xlat6;
float u_xlat9;
half u_xlat16_9;
half u_xlat16_10;

    u_xlat0.x = float(_OutlineReferenceTexWidth);
    u_xlat0.x = u_xlat0.x * _MainTex_TexelSize.x;
    u_xlat0.x = _OutlineWidth / u_xlat0.x;
    u_xlat0.xy = u_xlat0.xx * _MainTex_TexelSize.xy;
    u_xlat0.w = (-u_xlat0.y);
    u_xlat1.xy = input.uv.xy;
    u_xlat1.z = _OutlineMipLevel;
    u_xlat0.z = 0.0;
    u_xlat2.xyz = u_xlat0.zwz + u_xlat1.xyz;
    u_xlat16_9 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat2.xyz = u_xlat0.zyz + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat16_9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xzz * float3(-1.0,1.0,1.0) + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xzz + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xyz * float3(-0.699999988,0.699999988,1.0) + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xyz * float3(0.699999988,0.699999988,1.0) + u_xlat1.xyz;
    u_xlat16_10 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat9 = u_xlat9 + u_xlat16_10;
    u_xlat2.xyz = u_xlat0.xyz * float3(-0.699999988,-0.699999988,1.0) + u_xlat1.xyz;
    u_xlat0.xyz = u_xlat0.xyz * float3(0.699999988,-0.699999988,1.0) + u_xlat1.xyz;
    u_xlat16_0 = tex2Dlod(_MainTex, float4(u_xlat0.xy, 0, u_xlat0.z)).w;
    u_xlat16_3 = tex2Dlod(_MainTex, float4(u_xlat2.xy, 0, u_xlat2.z)).w;
    u_xlat3 = u_xlat16_3 + u_xlat9;
    u_xlat0.x = u_xlat16_0 + u_xlat3;
    u_xlat0.x = u_xlat0.x * input.color;
    u_xlat3 = (-_OutlineSmoothness) + 1.0;
    u_xlat6 = u_xlat3 * _ThresholdEnd;
    u_xlat3 = (-_ThresholdEnd) * u_xlat3 + _ThresholdEnd;
    u_xlat0.x = u_xlat0.x * 0.125 + (-u_xlat6);
    u_xlat0.x = u_xlat0.x / u_xlat3;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat3 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat3) + u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlatb3 = _OutlineOpaqueAlpha<u_xlat3;
    u_xlat1 = u_xlat0.xxxx * _OutlineColor;
    u_xlat0 = (bool(u_xlatb3)) ? float4(0.0,0.0,0.0,0.0) : u_xlat1;
    u_xlat1.xyz = u_xlat0.www * u_xlat0.xyz;
    u_xlat0.xyz = u_xlat1.xyz * (float3)(float3(_ColorIntensity, _ColorIntensity, _ColorIntensity));
    u_xlat16_1 = tex2D(_NoiseTex, input.mask.xy);
    u_xlat2.x = u_xlat0.w * u_xlat16_1.w + -0.00999999978;
    u_xlat0 = u_xlat0 * u_xlat16_1;
    result = u_xlat0;
    u_xlatb0 = u_xlat2.x<0.0;
    if(u_xlatb0){clip(-1);}
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
