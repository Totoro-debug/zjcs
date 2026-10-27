Shader "Zjcs/Official/World/Map13/BoundEntity/UniversalRenderPipeline2DSpriteLitColorShift_8de0c09e" {
Properties {
_MainTex ("Diffuse", 2D) = "white" {}
_MaskTex ("Mask", 2D) = "white" {}
_NormalMap ("Normal Map", 2D) = "bump" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
_RendererColor ("RendererColor", Color) = (1.0,1.0,1.0,1.0)
_Flip ("Flip", Vector) = (1.0,1.0,1.0,1.0)
_AlphaTex ("External Alpha", 2D) = "white" {}
_EnableExternalAlpha ("Enable External Alpha", Float) = 0.0
_HitEffectColor ("Hit Effect Color", Color) = (1.0,1.0,1.0,1.0)
_HitEffectBlend ("Hit Effect Blend", Range(0.0,1.0)) = 0.0
[MaterialToggle] _ZWrite ("ZWrite", Float) = 0.0
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
#pragma multi_compile_local __ _HITEFFECT
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Epsilon;
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
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
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
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1.xyz = log2(abs(input.color.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= input.color.xyzx).xyz;
    u_xlat16_3.xyz = input.color.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    {
        float4 hlslcc_movcTemp = u_xlat1;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
        hlslcc_movcTemp.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
        u_xlat1 = hlslcc_movcTemp;
    }
    u_xlat1.w = input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
half4 u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
bool u_xlatb2;
float4 u_xlat3;
half3 u_xlat16_3;
bool3 u_xlatb3;
float4 u_xlat4;
half4 u_xlat16_4;
half4 u_xlat16_5;
half3 u_xlat16_6;
float u_xlat7;
half2 u_xlat16_12;
float u_xlat14;
float u_xlat21;
bool u_xlatb21;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).yzwx;
    u_xlat16_1.w = u_xlat16_0.z * input.color.w;
    u_xlatb2 = u_xlat16_1.w==0.0;
    if(u_xlatb2){clip(-1);}
    u_xlatb2 = u_xlat16_0.x>=u_xlat16_0.y;
    u_xlat2.x = u_xlatb2 ? 1.0 : float(0.0);
    u_xlat3.xy = u_xlat16_0.yx;
    u_xlat3.z = float(-1.0);
    u_xlat3.w = float(0.666666687);
    u_xlat4.xy = u_xlat16_0.xy + (-u_xlat3.xy);
    u_xlat4.z = float(1.0);
    u_xlat4.w = float(-1.0);
    u_xlat2 = u_xlat2.xxxx * u_xlat4 + u_xlat3;
    u_xlatb3.x = u_xlat16_0.w>=u_xlat2.x;
    u_xlat3.x = u_xlatb3.x ? 1.0 : float(0.0);
    u_xlat16_0.xyz = u_xlat2.xyw;
    u_xlat2.xyw = u_xlat16_0.wyx;
    u_xlat2 = (-u_xlat16_0) + u_xlat2;
    u_xlat0 = u_xlat3.xxxx * u_xlat2 + u_xlat16_0;
    u_xlat2.x = min(u_xlat0.y, u_xlat0.w);
    u_xlat2.x = u_xlat0.x + (-u_xlat2.x);
    u_xlat7 = (-u_xlat0.y) + u_xlat0.w;
    u_xlat21 = u_xlat2.x * 6.0 + Epsilon;
    u_xlat7 = u_xlat7 / u_xlat21;
    u_xlat7 = u_xlat0.z + u_xlat7;
    u_xlat3.y = (-u_xlat2.x) * 0.5 + u_xlat0.x;
    u_xlat0.x = u_xlat3.y * 2.0 + -1.0;
    u_xlat0.x = -abs(u_xlat0.x) + Epsilon;
    u_xlat0.x = u_xlat0.x + 1.0;
    u_xlat3.x = u_xlat2.x / u_xlat0.x;
    u_xlat16_5.x = input.color.x + -0.5;
    u_xlat16_5.x = abs(u_xlat7) + u_xlat16_5.x;
    u_xlat16_12.xy = input.color.yz * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb0.xy = (float4(0.0, 0.0, 0.0, 0.0) < u_xlat16_12.xyxx).xy;
    u_xlat16_6.xy = (-u_xlat3.xy) + float2(1.0, 1.0);
    u_xlat16_6.xy = u_xlat16_12.xy * u_xlat16_6.xy + u_xlat3.xy;
    u_xlat16_12.xy = u_xlat16_12.xy * u_xlat3.xy + u_xlat3.xy;
    u_xlat0.x = (u_xlatb0.x) ? u_xlat16_6.x : u_xlat16_12.x;
    u_xlat0.y = (u_xlatb0.y) ? u_xlat16_6.y : u_xlat16_12.y;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat14 = frac(u_xlat16_5.x);
    u_xlat2.xyz = float3(u_xlat14,u_xlat14,u_xlat14) * float3(6.0, 6.0, 6.0) + float3(0.0, 4.0, 2.0);
    u_xlat2.xyz = u_xlat2.xyz * float3(0.166666672, 0.166666672, 0.166666672);
    u_xlatb3.xyz = (u_xlat2.xyzx >= (-u_xlat2.xyzx)).xyz;
    u_xlat2.xyz = frac(u_xlat2.xyz);
    {
        float4 hlslcc_movcTemp = u_xlat2;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat2.x : (-u_xlat2.x);
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat2.y : (-u_xlat2.y);
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat2.z : (-u_xlat2.z);
        u_xlat2 = hlslcc_movcTemp;
    }
    u_xlat2.xyz = u_xlat2.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat2.xyz = abs(u_xlat2.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat2.xyz = clamp(u_xlat2.xyz, 0.0, 1.0);
    u_xlat2.xyz = u_xlat2.xyz + float3(-0.5, -0.5, -0.5);
    u_xlat0.xzw = u_xlat0.xxx * u_xlat2.xyz;
    u_xlat2.x = u_xlat0.y * 2.0 + -1.0;
    u_xlat2.x = -abs(u_xlat2.x) + 1.0;
    u_xlat0.xyz = u_xlat0.xzw * u_xlat2.xxx + u_xlat0.yyy;
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_5.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb21 = u_xlat16_5.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_2 = u_xlat16_4 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_5.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_5.xyz = u_xlat16_3.xyz * u_xlat16_5.xxx;
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_5.xyz = u_xlat0.xyz * u_xlat16_6.xyz + u_xlat16_5.xyz;
    u_xlat16_5.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_5.xyz + (-u_xlat0.xyz);
    u_xlat16_1.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_5.xyz + u_xlat0.xyz;
    result = max(u_xlat16_1, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Epsilon;
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
half _HDREmulationScale;
half _HitEffectBlend;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
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
half4 _HitEffectColor;
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1.xyz = log2(abs(input.color.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= input.color.xyzx).xyz;
    u_xlat16_3.xyz = input.color.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    {
        float4 hlslcc_movcTemp = u_xlat1;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
        hlslcc_movcTemp.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
        u_xlat1 = hlslcc_movcTemp;
    }
    u_xlat1.w = input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
half4 u_xlat16_1;
float4 u_xlat2;
half3 u_xlat16_2;
bool u_xlatb2;
float4 u_xlat3;
half4 u_xlat16_3;
bool3 u_xlatb3;
float4 u_xlat4;
half4 u_xlat16_4;
half3 u_xlat16_5;
half3 u_xlat16_6;
float u_xlat7;
half3 u_xlat16_12;
float u_xlat14;
float u_xlat21;
bool u_xlatb23;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).yzwx;
    u_xlat16_1.w = u_xlat16_0.z * input.color.w;
    u_xlatb2 = u_xlat16_1.w==0.0;
    if(u_xlatb2){clip(-1);}
    u_xlatb2 = u_xlat16_0.x>=u_xlat16_0.y;
    u_xlat2.x = u_xlatb2 ? 1.0 : float(0.0);
    u_xlat3.xy = u_xlat16_0.yx;
    u_xlat3.z = float(-1.0);
    u_xlat3.w = float(0.666666687);
    u_xlat4.xy = u_xlat16_0.xy + (-u_xlat3.xy);
    u_xlat4.z = float(1.0);
    u_xlat4.w = float(-1.0);
    u_xlat2 = u_xlat2.xxxx * u_xlat4 + u_xlat3;
    u_xlatb3.x = u_xlat16_0.w>=u_xlat2.x;
    u_xlat3.x = u_xlatb3.x ? 1.0 : float(0.0);
    u_xlat16_0.xyz = u_xlat2.xyw;
    u_xlat2.xyw = u_xlat16_0.wyx;
    u_xlat2 = (-u_xlat16_0) + u_xlat2;
    u_xlat0 = u_xlat3.xxxx * u_xlat2 + u_xlat16_0;
    u_xlat2.x = min(u_xlat0.y, u_xlat0.w);
    u_xlat2.x = u_xlat0.x + (-u_xlat2.x);
    u_xlat7 = (-u_xlat0.y) + u_xlat0.w;
    u_xlat21 = u_xlat2.x * 6.0 + Epsilon;
    u_xlat7 = u_xlat7 / u_xlat21;
    u_xlat7 = u_xlat0.z + u_xlat7;
    u_xlat3.y = (-u_xlat2.x) * 0.5 + u_xlat0.x;
    u_xlat0.x = u_xlat3.y * 2.0 + -1.0;
    u_xlat0.x = -abs(u_xlat0.x) + Epsilon;
    u_xlat0.x = u_xlat0.x + 1.0;
    u_xlat3.x = u_xlat2.x / u_xlat0.x;
    u_xlat16_5.x = input.color.x + -0.5;
    u_xlat16_5.x = abs(u_xlat7) + u_xlat16_5.x;
    u_xlat16_12.xy = input.color.yz * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb0.xy = (float4(0.0, 0.0, 0.0, 0.0) < u_xlat16_12.xyxx).xy;
    u_xlat16_6.xy = (-u_xlat3.xy) + float2(1.0, 1.0);
    u_xlat16_6.xy = u_xlat16_12.xy * u_xlat16_6.xy + u_xlat3.xy;
    u_xlat16_12.xy = u_xlat16_12.xy * u_xlat3.xy + u_xlat3.xy;
    u_xlat0.x = (u_xlatb0.x) ? u_xlat16_6.x : u_xlat16_12.x;
    u_xlat0.y = (u_xlatb0.y) ? u_xlat16_6.y : u_xlat16_12.y;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat14 = frac(u_xlat16_5.x);
    u_xlat2.xyz = float3(u_xlat14,u_xlat14,u_xlat14) * float3(6.0, 6.0, 6.0) + float3(0.0, 4.0, 2.0);
    u_xlat2.xyz = u_xlat2.xyz * float3(0.166666672, 0.166666672, 0.166666672);
    u_xlatb3.xyz = (u_xlat2.xyzx >= (-u_xlat2.xyzx)).xyz;
    u_xlat2.xyz = frac(u_xlat2.xyz);
    {
        float4 hlslcc_movcTemp = u_xlat2;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat2.x : (-u_xlat2.x);
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat2.y : (-u_xlat2.y);
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat2.z : (-u_xlat2.z);
        u_xlat2 = hlslcc_movcTemp;
    }
    u_xlat2.xyz = u_xlat2.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat2.xyz = abs(u_xlat2.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat2.xyz = clamp(u_xlat2.xyz, 0.0, 1.0);
    u_xlat2.xyz = u_xlat2.xyz + float3(-0.5, -0.5, -0.5);
    u_xlat0.xzw = u_xlat0.xxx * u_xlat2.xyz;
    u_xlat2.x = u_xlat0.y * 2.0 + -1.0;
    u_xlat2.x = -abs(u_xlat2.x) + 1.0;
    u_xlat0.xyz = u_xlat0.xzw * u_xlat2.xxx + u_xlat0.yyy;
    u_xlat16_5.x = _HitEffectColor.w * _HitEffectBlend;
    u_xlat16_12.xyz = (-u_xlat0.xyz) + _HitEffectColor.xyz;
    u_xlat16_1.xyz = u_xlat16_5.xxx * u_xlat16_12.xyz + u_xlat0.xyz;
    u_xlat16_0 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_2.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_5.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb23 = u_xlat16_5.x!=0.0;
    u_xlat16_3 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_4 = (-u_xlat16_0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_4 = u_xlat16_4 * _ShapeLightInvertedFilter0;
    u_xlat16_0 = u_xlat16_3 * u_xlat16_0 + u_xlat16_4;
    u_xlat16_5.x = dot(u_xlat16_0, _ShapeLightMaskFilter0);
    u_xlat16_5.xyz = u_xlat16_2.xyz * u_xlat16_5.xxx;
    u_xlat16_5.xyz = (bool(u_xlatb23)) ? u_xlat16_5.xyz : u_xlat16_2.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors0.xxyx.yyy;
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(_ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y);
    u_xlat16_5.xyz = u_xlat16_1.xyz * u_xlat16_6.xyz + u_xlat16_5.xyz;
    u_xlat16_0.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_5.xyz + (-u_xlat16_1.xyz);
    u_xlat16_0.w = 0.0;
    u_xlat16_0 = float4(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_0 + u_xlat16_1;
    result = max(u_xlat16_0, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Epsilon;
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
half4 _MainTex_ST;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1.xyz = log2(abs(input.color.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= input.color.xyzx).xyz;
    u_xlat16_3.xyz = input.color.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    {
        float4 hlslcc_movcTemp = u_xlat1;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
        hlslcc_movcTemp.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
        u_xlat1 = hlslcc_movcTemp;
    }
    u_xlat1.w = input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
half u_xlat16_1;
float4 u_xlat2;
bool u_xlatb2;
float4 u_xlat3;
bool3 u_xlatb3;
float4 u_xlat4;
half2 u_xlat16_5;
float u_xlat6;
half u_xlat16_7;
float u_xlat12;
half2 u_xlat16_13;
float u_xlat18;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).yzwx;
    u_xlat16_1 = u_xlat16_0.z * input.color.w;
    u_xlatb2 = u_xlat16_1==0.0;
    if(u_xlatb2){clip(-1);}
    u_xlatb2 = u_xlat16_0.x>=u_xlat16_0.y;
    u_xlat2.x = u_xlatb2 ? 1.0 : float(0.0);
    u_xlat3.xy = u_xlat16_0.yx;
    u_xlat3.z = float(-1.0);
    u_xlat3.w = float(0.666666687);
    u_xlat4.xy = u_xlat16_0.xy + (-u_xlat3.xy);
    u_xlat4.z = float(1.0);
    u_xlat4.w = float(-1.0);
    u_xlat2 = u_xlat2.xxxx * u_xlat4 + u_xlat3;
    u_xlatb3.x = u_xlat16_0.w>=u_xlat2.x;
    u_xlat3.x = u_xlatb3.x ? 1.0 : float(0.0);
    u_xlat16_0.xyz = u_xlat2.xyw;
    u_xlat2.xyw = u_xlat16_0.wyx;
    u_xlat2 = (-u_xlat16_0) + u_xlat2;
    u_xlat0 = u_xlat3.xxxx * u_xlat2 + u_xlat16_0;
    u_xlat2.x = min(u_xlat0.y, u_xlat0.w);
    u_xlat2.x = u_xlat0.x + (-u_xlat2.x);
    u_xlat6 = (-u_xlat0.y) + u_xlat0.w;
    u_xlat18 = u_xlat2.x * 6.0 + Epsilon;
    u_xlat6 = u_xlat6 / u_xlat18;
    u_xlat6 = u_xlat0.z + u_xlat6;
    u_xlat3.y = (-u_xlat2.x) * 0.5 + u_xlat0.x;
    u_xlat0.x = u_xlat3.y * 2.0 + -1.0;
    u_xlat0.x = -abs(u_xlat0.x) + Epsilon;
    u_xlat0.x = u_xlat0.x + 1.0;
    u_xlat3.x = u_xlat2.x / u_xlat0.x;
    u_xlat16_7 = input.color.x + -0.5;
    u_xlat16_7 = abs(u_xlat6) + u_xlat16_7;
    u_xlat16_13.xy = input.color.yz * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb0.xy = (float4(0.0, 0.0, 0.0, 0.0) < u_xlat16_13.xyxx).xy;
    u_xlat16_5.xy = (-u_xlat3.xy) + float2(1.0, 1.0);
    u_xlat16_5.xy = u_xlat16_13.xy * u_xlat16_5.xy + u_xlat3.xy;
    u_xlat16_13.xy = u_xlat16_13.xy * u_xlat3.xy + u_xlat3.xy;
    u_xlat0.x = (u_xlatb0.x) ? u_xlat16_5.x : u_xlat16_13.x;
    u_xlat0.y = (u_xlatb0.y) ? u_xlat16_5.y : u_xlat16_13.y;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat12 = frac(u_xlat16_7);
    u_xlat2.xyz = float3(u_xlat12,u_xlat12,u_xlat12) * float3(6.0, 6.0, 6.0) + float3(0.0, 4.0, 2.0);
    u_xlat2.xyz = u_xlat2.xyz * float3(0.166666672, 0.166666672, 0.166666672);
    u_xlatb3.xyz = (u_xlat2.xyzx >= (-u_xlat2.xyzx)).xyz;
    u_xlat2.xyz = frac(u_xlat2.xyz);
    {
        float4 hlslcc_movcTemp = u_xlat2;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat2.x : (-u_xlat2.x);
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat2.y : (-u_xlat2.y);
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat2.z : (-u_xlat2.z);
        u_xlat2 = hlslcc_movcTemp;
    }
    u_xlat2.xyz = u_xlat2.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat2.xyz = abs(u_xlat2.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat2.xyz = clamp(u_xlat2.xyz, 0.0, 1.0);
    u_xlat2.xyz = u_xlat2.xyz + float3(-0.5, -0.5, -0.5);
    u_xlat0.xzw = u_xlat0.xxx * u_xlat2.xyz;
    u_xlat2.x = u_xlat0.y * 2.0 + -1.0;
    u_xlat2.x = -abs(u_xlat2.x) + 1.0;
    u_xlat0.xyz = u_xlat0.xzw * u_xlat2.xxx + u_xlat0.yyy;
    result.xyz = max(u_xlat0.xyz, float3(0.0, 0.0, 0.0));
    result.w = max(u_xlat16_1, 0.0);
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_HITEFFECT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Epsilon;
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
half _HitEffectBlend;
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
half4 _HitEffectColor;
half4 _MainTex_ST;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1.xyz = log2(abs(input.color.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= input.color.xyzx).xyz;
    u_xlat16_3.xyz = input.color.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    {
        float4 hlslcc_movcTemp = u_xlat1;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
        hlslcc_movcTemp.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
        u_xlat1 = hlslcc_movcTemp;
    }
    u_xlat1.w = input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
half4 u_xlat16_1;
float4 u_xlat2;
bool u_xlatb2;
float4 u_xlat3;
bool3 u_xlatb3;
float4 u_xlat4;
half u_xlat16_5;
half2 u_xlat16_6;
float u_xlat7;
half3 u_xlat16_12;
float u_xlat14;
float u_xlat21;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).yzwx;
    u_xlat16_1.w = u_xlat16_0.z * input.color.w;
    u_xlatb2 = u_xlat16_1.w==0.0;
    if(u_xlatb2){clip(-1);}
    u_xlatb2 = u_xlat16_0.x>=u_xlat16_0.y;
    u_xlat2.x = u_xlatb2 ? 1.0 : float(0.0);
    u_xlat3.xy = u_xlat16_0.yx;
    u_xlat3.z = float(-1.0);
    u_xlat3.w = float(0.666666687);
    u_xlat4.xy = u_xlat16_0.xy + (-u_xlat3.xy);
    u_xlat4.z = float(1.0);
    u_xlat4.w = float(-1.0);
    u_xlat2 = u_xlat2.xxxx * u_xlat4 + u_xlat3;
    u_xlatb3.x = u_xlat16_0.w>=u_xlat2.x;
    u_xlat3.x = u_xlatb3.x ? 1.0 : float(0.0);
    u_xlat16_0.xyz = u_xlat2.xyw;
    u_xlat2.xyw = u_xlat16_0.wyx;
    u_xlat2 = (-u_xlat16_0) + u_xlat2;
    u_xlat0 = u_xlat3.xxxx * u_xlat2 + u_xlat16_0;
    u_xlat2.x = min(u_xlat0.y, u_xlat0.w);
    u_xlat2.x = u_xlat0.x + (-u_xlat2.x);
    u_xlat7 = (-u_xlat0.y) + u_xlat0.w;
    u_xlat21 = u_xlat2.x * 6.0 + Epsilon;
    u_xlat7 = u_xlat7 / u_xlat21;
    u_xlat7 = u_xlat0.z + u_xlat7;
    u_xlat3.y = (-u_xlat2.x) * 0.5 + u_xlat0.x;
    u_xlat0.x = u_xlat3.y * 2.0 + -1.0;
    u_xlat0.x = -abs(u_xlat0.x) + Epsilon;
    u_xlat0.x = u_xlat0.x + 1.0;
    u_xlat3.x = u_xlat2.x / u_xlat0.x;
    u_xlat16_5 = input.color.x + -0.5;
    u_xlat16_5 = abs(u_xlat7) + u_xlat16_5;
    u_xlat16_12.xy = input.color.yz * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb0.xy = (float4(0.0, 0.0, 0.0, 0.0) < u_xlat16_12.xyxx).xy;
    u_xlat16_6.xy = (-u_xlat3.xy) + float2(1.0, 1.0);
    u_xlat16_6.xy = u_xlat16_12.xy * u_xlat16_6.xy + u_xlat3.xy;
    u_xlat16_12.xy = u_xlat16_12.xy * u_xlat3.xy + u_xlat3.xy;
    u_xlat0.x = (u_xlatb0.x) ? u_xlat16_6.x : u_xlat16_12.x;
    u_xlat0.y = (u_xlatb0.y) ? u_xlat16_6.y : u_xlat16_12.y;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat14 = frac(u_xlat16_5);
    u_xlat2.xyz = float3(u_xlat14,u_xlat14,u_xlat14) * float3(6.0, 6.0, 6.0) + float3(0.0, 4.0, 2.0);
    u_xlat2.xyz = u_xlat2.xyz * float3(0.166666672, 0.166666672, 0.166666672);
    u_xlatb3.xyz = (u_xlat2.xyzx >= (-u_xlat2.xyzx)).xyz;
    u_xlat2.xyz = frac(u_xlat2.xyz);
    {
        float4 hlslcc_movcTemp = u_xlat2;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat2.x : (-u_xlat2.x);
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat2.y : (-u_xlat2.y);
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat2.z : (-u_xlat2.z);
        u_xlat2 = hlslcc_movcTemp;
    }
    u_xlat2.xyz = u_xlat2.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat2.xyz = abs(u_xlat2.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat2.xyz = clamp(u_xlat2.xyz, 0.0, 1.0);
    u_xlat2.xyz = u_xlat2.xyz + float3(-0.5, -0.5, -0.5);
    u_xlat0.xzw = u_xlat0.xxx * u_xlat2.xyz;
    u_xlat2.x = u_xlat0.y * 2.0 + -1.0;
    u_xlat2.x = -abs(u_xlat2.x) + 1.0;
    u_xlat0.xyz = u_xlat0.xzw * u_xlat2.xxx + u_xlat0.yyy;
    u_xlat16_5 = _HitEffectColor.w * _HitEffectBlend;
    u_xlat16_12.xyz = (-u_xlat0.xyz) + _HitEffectColor.xyz;
    u_xlat16_1.xyz = float3(u_xlat16_5,u_xlat16_5,u_xlat16_5) * u_xlat16_12.xyz + u_xlat0.xyz;
    result = max(u_xlat16_1, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#endif
ENDCG
}
}
Fallback "Zjcs/Official/World/Map13/BoundEntity/SpritesDefault_b64456be"
}
