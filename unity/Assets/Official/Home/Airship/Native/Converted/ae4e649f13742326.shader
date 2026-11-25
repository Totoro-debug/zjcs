Shader "Universal Render Pipeline/2D/HomeSpineOnlySnow" {
Properties {

_MainTex ("Main Texture", 2D) = "black" { }

_SnowMaskTex ("Snow Mask", 2D) = "black" { }

_SnowDetailTex ("Snow Detail Texture", 2D) = "white" { }

_SnowTileScale ("Snow Tiling Scale", Float) = 4.0

_SnowColorTint ("Snow Color Tint", Color) = (1,1,1,1)

_SnowColorTint2 ("Snow Color Tint2", Color) = (1,1,1,1)

_EdgeSoftness ("Edge Smoothness", Range(0.01, 0.2)) = 0.05000000074505806

_Param ("Param", Vector) = (0,0,1,1)

[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0.0

[MaterialToggle(_LIGHT_AFFECTS_ADDITIVE)] _LightAffectsAdditive ("Light Affects Additive", Float) = 0.0

_StencilRef ("Stencil Reference", Float) = 1.0

[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare", Float) = 8.0

_LightAtten ("Light Atten", Range(0, 1)) = 0.0

}
SubShader {
LOD 0
Tags { "IGNOREPROJECTOR"="true" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
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
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#pragma multi_compile __ _LIGHT_AFFECTS_ADDITIVE
#pragma multi_compile __ _STRAIGHT_ALPHA_INPUT
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_LIGHT_AFFECTS_ADDITIVE) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _HDREmulationScale;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _ShapeLightTexture0;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half3 u_xlat16_5;
float u_xlat6;
float2 u_xlat12;
bool u_xlatb12;
bool u_xlatb19;
half u_xlat16_20;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat12.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat12.x = u_xlat12.x / u_xlat1.x;
    u_xlat12.x = clamp(u_xlat12.x, 0.0, 1.0);
    u_xlat12.x = u_xlat12.x * u_xlat1.y + _Param.y;
    u_xlat12.x = (-u_xlat12.x) + 1.0;
    u_xlat6 = (-u_xlat12.x) + u_xlat16_0.y;
    u_xlat12.x = float(1.0) / _EdgeSoftness;
    u_xlat6 = u_xlat12.x * u_xlat6;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat12.x = u_xlat6 * -2.0 + 3.0;
    u_xlat6 = u_xlat6 * u_xlat6;
    u_xlat6 = u_xlat6 * u_xlat12.x;
    u_xlatb12 = u_xlat6==0.0;
    if(u_xlatb12){clip(-1);}
    u_xlat12.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat12.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat12.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat12.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_20 = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb19 = u_xlat16_20!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_20 = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = (float3)(u_xlat16_20) * u_xlat16_3.xyz;
    u_xlat16_4.xyz = (bool(u_xlatb19)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_4.xyz = u_xlat0.xzw * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = (float3)(_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat0.xzw);
    u_xlat16_4.xyz = (float3)(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat0.xzw;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_4.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_4.xyz;
    result.xyz = (float3)(u_xlat6) * u_xlat16_2.xyz;
    result.w = u_xlat6;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_LIGHT_AFFECTS_ADDITIVE) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _HDREmulationScale;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _ShapeLightTexture0;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half3 u_xlat16_5;
float u_xlat6;
float2 u_xlat12;
bool u_xlatb12;
bool u_xlatb19;
half u_xlat16_20;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat12.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat12.x = u_xlat12.x / u_xlat1.x;
    u_xlat12.x = clamp(u_xlat12.x, 0.0, 1.0);
    u_xlat12.x = u_xlat12.x * u_xlat1.y + _Param.y;
    u_xlat12.x = (-u_xlat12.x) + 1.0;
    u_xlat6 = (-u_xlat12.x) + u_xlat16_0.y;
    u_xlat12.x = float(1.0) / _EdgeSoftness;
    u_xlat6 = u_xlat12.x * u_xlat6;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat12.x = u_xlat6 * -2.0 + 3.0;
    u_xlat6 = u_xlat6 * u_xlat6;
    u_xlat6 = u_xlat6 * u_xlat12.x;
    u_xlatb12 = u_xlat6==0.0;
    if(u_xlatb12){clip(-1);}
    u_xlat12.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat12.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat12.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat12.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_20 = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb19 = u_xlat16_20!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_20 = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = (float3)(u_xlat16_20) * u_xlat16_3.xyz;
    u_xlat16_4.xyz = (bool(u_xlatb19)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_4.xyz = u_xlat0.xzw * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = (float3)(_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat0.xzw);
    u_xlat16_4.xyz = (float3)(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat0.xzw;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_4.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_4.xyz;
    result.xyz = (float3)(u_xlat6) * u_xlat16_2.xyz;
    result.w = u_xlat6;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_LIGHT_AFFECTS_ADDITIVE) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _HDREmulationScale;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _ShapeLightTexture0;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half3 u_xlat16_5;
float u_xlat6;
float2 u_xlat12;
bool u_xlatb12;
bool u_xlatb19;
half u_xlat16_20;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat12.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat12.x = u_xlat12.x / u_xlat1.x;
    u_xlat12.x = clamp(u_xlat12.x, 0.0, 1.0);
    u_xlat12.x = u_xlat12.x * u_xlat1.y + _Param.y;
    u_xlat12.x = (-u_xlat12.x) + 1.0;
    u_xlat6 = (-u_xlat12.x) + u_xlat16_0.y;
    u_xlat12.x = float(1.0) / _EdgeSoftness;
    u_xlat6 = u_xlat12.x * u_xlat6;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat12.x = u_xlat6 * -2.0 + 3.0;
    u_xlat6 = u_xlat6 * u_xlat6;
    u_xlat6 = u_xlat6 * u_xlat12.x;
    u_xlatb12 = u_xlat6==0.0;
    if(u_xlatb12){clip(-1);}
    u_xlat12.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat12.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat12.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat12.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_20 = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb19 = u_xlat16_20!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_20 = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = (float3)(u_xlat16_20) * u_xlat16_3.xyz;
    u_xlat16_4.xyz = (bool(u_xlatb19)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_4.xyz = u_xlat0.xzw * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = (float3)(_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat0.xzw);
    u_xlat16_4.xyz = (float3)(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat0.xzw;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_4.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_4.xyz;
    result.xyz = (float3)(u_xlat6) * u_xlat16_2.xyz;
    result.w = u_xlat6;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_LIGHT_AFFECTS_ADDITIVE) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _HDREmulationScale;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _ShapeLightTexture0;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half3 u_xlat16_5;
float u_xlat6;
float2 u_xlat12;
bool u_xlatb12;
bool u_xlatb19;
half u_xlat16_20;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat12.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat12.x = u_xlat12.x / u_xlat1.x;
    u_xlat12.x = clamp(u_xlat12.x, 0.0, 1.0);
    u_xlat12.x = u_xlat12.x * u_xlat1.y + _Param.y;
    u_xlat12.x = (-u_xlat12.x) + 1.0;
    u_xlat6 = (-u_xlat12.x) + u_xlat16_0.y;
    u_xlat12.x = float(1.0) / _EdgeSoftness;
    u_xlat6 = u_xlat12.x * u_xlat6;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat12.x = u_xlat6 * -2.0 + 3.0;
    u_xlat6 = u_xlat6 * u_xlat6;
    u_xlat6 = u_xlat6 * u_xlat12.x;
    u_xlatb12 = u_xlat6==0.0;
    if(u_xlatb12){clip(-1);}
    u_xlat12.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat12.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat12.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat12.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_20 = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb19 = u_xlat16_20!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_20 = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = (float3)(u_xlat16_20) * u_xlat16_3.xyz;
    u_xlat16_4.xyz = (bool(u_xlatb19)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_4.xyz = u_xlat0.xzw * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = (float3)(_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat0.xzw);
    u_xlat16_4.xyz = (float3)(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat0.xzw;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_4.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_4.xyz;
    result.xyz = (float3)(u_xlat6) * u_xlat16_2.xyz;
    result.w = u_xlat6;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_LIGHT_AFFECTS_ADDITIVE) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat4;
float2 u_xlat8;
bool u_xlatb8;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat8.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat8.x = u_xlat8.x / u_xlat1.x;
    u_xlat8.x = clamp(u_xlat8.x, 0.0, 1.0);
    u_xlat8.x = u_xlat8.x * u_xlat1.y + _Param.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat4 = (-u_xlat8.x) + u_xlat16_0.y;
    u_xlat8.x = float(1.0) / _EdgeSoftness;
    u_xlat4 = u_xlat8.x * u_xlat4;
    u_xlat4 = clamp(u_xlat4, 0.0, 1.0);
    u_xlat8.x = u_xlat4 * -2.0 + 3.0;
    u_xlat4 = u_xlat4 * u_xlat4;
    u_xlat4 = u_xlat4 * u_xlat8.x;
    u_xlatb8 = u_xlat4==0.0;
    if(u_xlatb8){clip(-1);}
    u_xlat8.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat8.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat8.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat8.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = max(u_xlat0.xzw, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_3.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.xyz = (float3)(u_xlat4) * u_xlat16_2.xyz;
    result.w = u_xlat4;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_LIGHT_AFFECTS_ADDITIVE) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat4;
float2 u_xlat8;
bool u_xlatb8;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat8.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat8.x = u_xlat8.x / u_xlat1.x;
    u_xlat8.x = clamp(u_xlat8.x, 0.0, 1.0);
    u_xlat8.x = u_xlat8.x * u_xlat1.y + _Param.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat4 = (-u_xlat8.x) + u_xlat16_0.y;
    u_xlat8.x = float(1.0) / _EdgeSoftness;
    u_xlat4 = u_xlat8.x * u_xlat4;
    u_xlat4 = clamp(u_xlat4, 0.0, 1.0);
    u_xlat8.x = u_xlat4 * -2.0 + 3.0;
    u_xlat4 = u_xlat4 * u_xlat4;
    u_xlat4 = u_xlat4 * u_xlat8.x;
    u_xlatb8 = u_xlat4==0.0;
    if(u_xlatb8){clip(-1);}
    u_xlat8.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat8.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat8.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat8.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = max(u_xlat0.xzw, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_3.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.xyz = (float3)(u_xlat4) * u_xlat16_2.xyz;
    result.w = u_xlat4;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_LIGHT_AFFECTS_ADDITIVE) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat4;
float2 u_xlat8;
bool u_xlatb8;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat8.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat8.x = u_xlat8.x / u_xlat1.x;
    u_xlat8.x = clamp(u_xlat8.x, 0.0, 1.0);
    u_xlat8.x = u_xlat8.x * u_xlat1.y + _Param.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat4 = (-u_xlat8.x) + u_xlat16_0.y;
    u_xlat8.x = float(1.0) / _EdgeSoftness;
    u_xlat4 = u_xlat8.x * u_xlat4;
    u_xlat4 = clamp(u_xlat4, 0.0, 1.0);
    u_xlat8.x = u_xlat4 * -2.0 + 3.0;
    u_xlat4 = u_xlat4 * u_xlat4;
    u_xlat4 = u_xlat4 * u_xlat8.x;
    u_xlatb8 = u_xlat4==0.0;
    if(u_xlatb8){clip(-1);}
    u_xlat8.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat8.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat8.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat8.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = max(u_xlat0.xzw, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_3.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.xyz = (float3)(u_xlat4) * u_xlat16_2.xyz;
    result.w = u_xlat4;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_LIGHT_AFFECTS_ADDITIVE) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float Xhlslcc_UnusedX_NoiseIntensity;
float Xhlslcc_UnusedX_SnowLevel;
float _EdgeSoftness;
float _SnowTileScale;
float _SnowTime;
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
float4 _MainTex_TexelSize;
float4 _Param;
half _LightAtten;
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
half4 _SnowColorTint2;
half4 _SnowColorTint;
sampler2D _SnowDetailTex;
sampler2D _SnowMaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
bool3 u_xlatb4;
bool3 u_xlatb5;
bool u_xlatb18;

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
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat16_3.xyz = u_xlat16_2.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat1.xyz = log2(abs(u_xlat16_3.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb4.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= u_xlat16_2.xyzx).xyz;
    u_xlat16_2.x = (u_xlatb4.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_2.y = (u_xlatb4.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_2.z = (u_xlatb4.z) ? u_xlat16_3.z : u_xlat1.z;
    u_xlat16_1.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlat16_2.xyz = input.color.xyz + float3(0.0549999997,0.0549999997,0.0549999997);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(0.947867334,0.947867334,0.947867334);
    u_xlat4.xyz = log2(abs(u_xlat16_2.xyz));
    u_xlat4.xyz = u_xlat4.xyz * float3(2.4000001,2.4000001,2.4000001);
    u_xlat4.xyz = exp2(u_xlat4.xyz);
    u_xlat16_2.xyz = input.color.xyz * float3(0.0773993805,0.0773993805,0.0773993805);
    u_xlatb5.xyz = (float4(0.0404499993,0.0404499993,0.0404499993,0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb5.x) ? u_xlat16_2.x : u_xlat4.x;
        hlslcc_movcTemp.y = (u_xlatb5.y) ? u_xlat16_2.y : u_xlat4.y;
        hlslcc_movcTemp.z = (u_xlatb5.z) ? u_xlat16_2.z : u_xlat4.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlatb18 = input.color.w==0.0;
    u_xlat16_2.w = 0.0;
    u_xlat16_1.w = input.color.w;
    u_xlat16_1 = (bool(u_xlatb18)) ? u_xlat16_2 : u_xlat16_1;
    u_xlatb18 = u_xlat16_1.w==0.0;
    u_xlat16_2.xyz = u_xlat16_1.xyz / u_xlat16_1.www;
    output.color.xyz = (bool(u_xlatb18)) ? u_xlat16_1.xyz : u_xlat16_2.xyz;
    output.color.w = u_xlat16_1.w;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half2 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat4;
float2 u_xlat8;
bool u_xlatb8;

    u_xlat16_0.xy = tex2Dbias(_SnowMaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat8.x = _SnowTime + (-_Param.x);
    u_xlat1.xy = (-_Param.xy) + _Param.zw;
    u_xlat8.x = u_xlat8.x / u_xlat1.x;
    u_xlat8.x = clamp(u_xlat8.x, 0.0, 1.0);
    u_xlat8.x = u_xlat8.x * u_xlat1.y + _Param.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat4 = (-u_xlat8.x) + u_xlat16_0.y;
    u_xlat8.x = float(1.0) / _EdgeSoftness;
    u_xlat4 = u_xlat8.x * u_xlat4;
    u_xlat4 = clamp(u_xlat4, 0.0, 1.0);
    u_xlat8.x = u_xlat4 * -2.0 + 3.0;
    u_xlat4 = u_xlat4 * u_xlat4;
    u_xlat4 = u_xlat4 * u_xlat8.x;
    u_xlatb8 = u_xlat4==0.0;
    if(u_xlatb8){clip(-1);}
    u_xlat8.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat1.x = u_xlat8.x * input.uv.x;
    u_xlat1.y = input.uv.y;
    u_xlat8.xy = u_xlat1.xy * (float2)(float2(_SnowTileScale, _SnowTileScale));
    u_xlat16_1.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat8.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_2.xyz = u_xlat16_0.xxx * u_xlat16_2.xyz + _SnowColorTint.xyz;
    u_xlat0.xzw = u_xlat16_1.xyz * u_xlat16_2.xyz;
    u_xlat16_3.xyz = max(u_xlat0.xzw, float3(0.0,0.0,0.0));
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xyz + (-u_xlat16_3.xyz);
    u_xlat16_2.xyz = (float3)(_LightAtten) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.xyz = (float3)(u_xlat4) * u_xlat16_2.xyz;
    result.w = u_xlat4;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
