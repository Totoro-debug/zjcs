Shader "Zjcs/Official/World/Map13/BoundEntity/UniversalRenderPipeline2DSpriteLitAtten_15542ddd" {
Properties {
_MainTex ("Diffuse", 2D) = "white" {}
_MaskTex ("Mask", 2D) = "white" {}
_NormalMap ("Normal Map", 2D) = "bump" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
_RendererColor ("RendererColor", Color) = (1.0,1.0,1.0,1.0)
_Flip ("Flip", Vector) = (1.0,1.0,1.0,1.0)
_AlphaTex ("External Alpha", 2D) = "white" {}
_EnableExternalAlpha ("Enable External Alpha", Float) = 0.0
_LightAtten ("Light Atten", Range(0.0,1.0)) = 0.0
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
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_1
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_2
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_3
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half2 _ShapeLightBlendFactors1;
half2 _ShapeLightBlendFactors2;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter0;
half4 _ShapeLightMaskFilter1;
half4 _ShapeLightMaskFilter2;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _ShapeLightTexture1;
sampler2D _ShapeLightTexture2;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
half4 u_xlat16_7;
half4 u_xlat16_8;
half4 u_xlat16_9;
bool u_xlatb30;
half u_xlat16_34;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb30 = u_xlat1.w==0.0;
    if(u_xlatb30){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb30 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb30)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_34 = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb30 = u_xlat16_34!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_7 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_2 + u_xlat16_7;
    u_xlat16_34 = dot(u_xlat16_6, _ShapeLightMaskFilter1);
    u_xlat16_6.xyz = u_xlat16_3.xyz * float3(u_xlat16_34,u_xlat16_34,u_xlat16_34);
    u_xlat16_6.xyz = (bool(u_xlatb30)) ? u_xlat16_6.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors1.xxx;
    u_xlat16_6.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors1.yyy;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_34 = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb30 = u_xlat16_34!=0.0;
    u_xlat16_8 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_9 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_8 = u_xlat16_8 * u_xlat16_2 + u_xlat16_9;
    u_xlat16_34 = dot(u_xlat16_8, _ShapeLightMaskFilter2);
    u_xlat16_8.xyz = u_xlat16_3.xyz * float3(u_xlat16_34,u_xlat16_34,u_xlat16_34);
    u_xlat16_8.xyz = (bool(u_xlatb30)) ? u_xlat16_8.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_34 = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb30 = u_xlat16_34!=0.0;
    u_xlat16_9 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_9 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_34 = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_34,u_xlat16_34,u_xlat16_34);
    u_xlat16_5.xyz = (bool(u_xlatb30)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx + u_xlat16_7.xyz;
    u_xlat16_7.xyz = u_xlat16_8.xyz * _ShapeLightBlendFactors2.xxx + u_xlat16_7.xyz;
    u_xlat16_7.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.xxx + u_xlat16_7.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_8.xyz * _ShapeLightBlendFactors2.yyy + u_xlat16_4.xyz;
    u_xlat16_4.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.yyy + u_xlat16_4.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_7.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half2 _ShapeLightBlendFactors1;
half2 _ShapeLightBlendFactors2;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightMaskFilter0;
half4 _ShapeLightMaskFilter1;
half4 _ShapeLightMaskFilter2;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _ShapeLightTexture1;
sampler2D _ShapeLightTexture2;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
half4 u_xlat16_7;
half4 u_xlat16_8;
bool u_xlatb27;
half u_xlat16_31;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb27 = u_xlat1.w==0.0;
    if(u_xlatb27){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb27 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb27)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_7 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_2 + u_xlat16_7;
    u_xlat16_31 = dot(u_xlat16_6, _ShapeLightMaskFilter1);
    u_xlat16_6.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_6.xyz = (bool(u_xlatb27)) ? u_xlat16_6.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors1.xxx;
    u_xlat16_6.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors1.yyy;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_8 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_2 = u_xlat16_8 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_31 = dot(u_xlat16_2, _ShapeLightMaskFilter2);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_5.xyz = (bool(u_xlatb27)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx + u_xlat16_7.xyz;
    u_xlat16_7.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors2.xxx + u_xlat16_7.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors2.yyy + u_xlat16_4.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_7.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half2 _ShapeLightBlendFactors1;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter0;
half4 _ShapeLightMaskFilter1;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _ShapeLightTexture1;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
half4 u_xlat16_7;
half4 u_xlat16_8;
bool u_xlatb27;
half u_xlat16_31;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb27 = u_xlat1.w==0.0;
    if(u_xlatb27){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb27 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb27)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_7 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_2 + u_xlat16_7;
    u_xlat16_31 = dot(u_xlat16_6, _ShapeLightMaskFilter1);
    u_xlat16_6.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_6.xyz = (bool(u_xlatb27)) ? u_xlat16_6.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors1.xxx;
    u_xlat16_6.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors1.yyy;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_8 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_8 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_31 = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_5.xyz = (bool(u_xlatb27)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx + u_xlat16_7.xyz;
    u_xlat16_7.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.xxx + u_xlat16_7.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.yyy + u_xlat16_4.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_7.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half2 _ShapeLightBlendFactors1;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightMaskFilter0;
half4 _ShapeLightMaskFilter1;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _ShapeLightTexture1;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
bool u_xlatb21;
half u_xlat16_25;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb21 = u_xlat1.w==0.0;
    if(u_xlatb21){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb21 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb21)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_25 = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb21 = u_xlat16_25!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_25 = dot(u_xlat16_2, _ShapeLightMaskFilter1);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_25,u_xlat16_25,u_xlat16_25);
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors1.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors1.yyy;
    u_xlat16_6.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy + u_xlat16_5.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_6.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half2 _ShapeLightBlendFactors2;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter0;
half4 _ShapeLightMaskFilter2;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _ShapeLightTexture2;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
half4 u_xlat16_7;
half4 u_xlat16_8;
bool u_xlatb27;
half u_xlat16_31;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb27 = u_xlat1.w==0.0;
    if(u_xlatb27){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb27 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb27)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_7 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_2 + u_xlat16_7;
    u_xlat16_31 = dot(u_xlat16_6, _ShapeLightMaskFilter2);
    u_xlat16_6.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_6.xyz = (bool(u_xlatb27)) ? u_xlat16_6.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors2.xxx;
    u_xlat16_6.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors2.yyy;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_8 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_8 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_31 = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_5.xyz = (bool(u_xlatb27)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx + u_xlat16_7.xyz;
    u_xlat16_7.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.xxx + u_xlat16_7.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.yyy + u_xlat16_4.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_7.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half2 _ShapeLightBlendFactors2;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightMaskFilter0;
half4 _ShapeLightMaskFilter2;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _ShapeLightTexture2;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
bool u_xlatb21;
half u_xlat16_25;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb21 = u_xlat1.w==0.0;
    if(u_xlatb21){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb21 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb21)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_25 = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb21 = u_xlat16_25!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_25 = dot(u_xlat16_2, _ShapeLightMaskFilter2);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_25,u_xlat16_25,u_xlat16_25);
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors2.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors2.yyy;
    u_xlat16_6.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy + u_xlat16_5.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_6.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter0;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
bool u_xlatb21;
half u_xlat16_25;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb21 = u_xlat1.w==0.0;
    if(u_xlatb21){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb21 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb21)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_25 = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb21 = u_xlat16_25!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_25 = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_25,u_xlat16_25,u_xlat16_25);
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.yyy;
    u_xlat16_6.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy + u_xlat16_5.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_6.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
bool u_xlatb18;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb18 = u_xlat1.w==0.0;
    if(u_xlatb18){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb18 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter0;
    u_xlat16_2 = u_xlat16_4 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_4.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb18)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors1;
half2 _ShapeLightBlendFactors2;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter1;
half4 _ShapeLightMaskFilter2;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture1;
sampler2D _ShapeLightTexture2;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
half4 u_xlat16_7;
half4 u_xlat16_8;
bool u_xlatb27;
half u_xlat16_31;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb27 = u_xlat1.w==0.0;
    if(u_xlatb27){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb27 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter1);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb27)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_7 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_6 = u_xlat16_6 * u_xlat16_2 + u_xlat16_7;
    u_xlat16_31 = dot(u_xlat16_6, _ShapeLightMaskFilter2);
    u_xlat16_6.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_6.xyz = (bool(u_xlatb27)) ? u_xlat16_6.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors2.xxx;
    u_xlat16_6.xyz = u_xlat16_6.xyz * _ShapeLightBlendFactors2.yyy;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_31 = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb27 = u_xlat16_31!=0.0;
    u_xlat16_8 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_8 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_31 = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_31,u_xlat16_31,u_xlat16_31);
    u_xlat16_5.xyz = (bool(u_xlatb27)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_7.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.xxx + u_xlat16_7.xyz;
    u_xlat16_7.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.xxx + u_xlat16_7.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.yyy + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.yyy + u_xlat16_4.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_7.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors1;
half2 _ShapeLightBlendFactors2;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightMaskFilter1;
half4 _ShapeLightMaskFilter2;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture1;
sampler2D _ShapeLightTexture2;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
bool u_xlatb21;
half u_xlat16_25;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb21 = u_xlat1.w==0.0;
    if(u_xlatb21){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb21 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter1);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb21)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_25 = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb21 = u_xlat16_25!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_25 = dot(u_xlat16_2, _ShapeLightMaskFilter2);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_25,u_xlat16_25,u_xlat16_25);
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors2.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors2.yyy;
    u_xlat16_6.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.xxx + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.yyy + u_xlat16_5.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_6.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors1;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter1;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture1;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
bool u_xlatb21;
half u_xlat16_25;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb21 = u_xlat1.w==0.0;
    if(u_xlatb21){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb21 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter1);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb21)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_25 = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb21 = u_xlat16_25!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_25 = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_25,u_xlat16_25,u_xlat16_25);
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.yyy;
    u_xlat16_6.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.xxx + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.yyy + u_xlat16_5.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_6.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors1;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter1;
half4 _ShapeLightMaskFilter1;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture1;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
bool u_xlatb18;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb18 = u_xlat1.w==0.0;
    if(u_xlatb18){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture1, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter1, _ShapeLightMaskFilter1);
    u_xlatb18 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter1;
    u_xlat16_2 = u_xlat16_4 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_4.x = dot(u_xlat16_2, _ShapeLightMaskFilter1);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb18)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors1.yyy;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors2;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter2;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture2;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
half4 u_xlat16_6;
bool u_xlatb21;
half u_xlat16_25;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb21 = u_xlat1.w==0.0;
    if(u_xlatb21){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb21 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_6 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_4 = u_xlat16_4 * u_xlat16_2 + u_xlat16_6;
    u_xlat16_4.x = dot(u_xlat16_4, _ShapeLightMaskFilter2);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb21)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_25 = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb21 = u_xlat16_25!=0.0;
    u_xlat16_6 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_25 = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_5.xyz = u_xlat16_3.xyz * float3(u_xlat16_25,u_xlat16_25,u_xlat16_25);
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_3.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors3.yyy;
    u_xlat16_6.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors2.xxx + u_xlat16_6.xyz;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors2.yyy + u_xlat16_5.xyz;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_6.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors2;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter2;
half4 _ShapeLightMaskFilter2;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture2;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
bool u_xlatb18;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb18 = u_xlat1.w==0.0;
    if(u_xlatb18){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture2, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter2, _ShapeLightMaskFilter2);
    u_xlatb18 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter2;
    u_xlat16_2 = u_xlat16_4 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_4.x = dot(u_xlat16_2, _ShapeLightMaskFilter2);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb18)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors2.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors2.yyy;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors3;
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
half4 _RendererColor;
half4 _ShapeLightInvertedFilter3;
half4 _ShapeLightMaskFilter3;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture3;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half4 u_xlat16_4;
half4 u_xlat16_5;
bool u_xlatb18;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb18 = u_xlat1.w==0.0;
    if(u_xlatb18){clip(-1);}
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture3, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter3, _ShapeLightMaskFilter3);
    u_xlatb18 = u_xlat16_4.x!=0.0;
    u_xlat16_4 = (-_ShapeLightInvertedFilter3) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = (-u_xlat16_2) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_5 = u_xlat16_5 * _ShapeLightInvertedFilter3;
    u_xlat16_2 = u_xlat16_4 * u_xlat16_2 + u_xlat16_5;
    u_xlat16_4.x = dot(u_xlat16_2, _ShapeLightMaskFilter3);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb18)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors3.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors3.yyy;
    u_xlat16_4.xyz = u_xlat1.xyz * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_4.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_4.xyz + (-u_xlat1.xyz);
    u_xlat16_4.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_4.xyz + u_xlat1.xyz;
    u_xlat16_4.xyz = max(u_xlat16_4.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_4.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_4.xyz;
    result = u_xlat1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(USE_SHAPE_LIGHT_TYPE_1) && !defined(USE_SHAPE_LIGHT_TYPE_2) && !defined(USE_SHAPE_LIGHT_TYPE_3)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _LightAtten;
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
float4 _Color;
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
half4 _RendererColor;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half3 u_xlat16_2;
bool u_xlatb9;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb9 = u_xlat1.w==0.0;
    if(u_xlatb9){clip(-1);}
    u_xlat16_2.xyz = max(u_xlat1.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_2.xyz);
    u_xlat1.xyz = float3(_LightAtten,_LightAtten,_LightAtten) * u_xlat0.xyz + u_xlat16_2.xyz;
    result = u_xlat1;
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "LIGHTMODE"="NormalsRendering" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
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
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 tangent:TANGENT; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float3 mask:TEXCOORD2; float3 uv3:TEXCOORD3; };

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
half4 _NormalMap_ST;
sampler2D _MainTex;
sampler2D _NormalMap;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
float u_xlat9;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _NormalMap_ST.xy + _NormalMap_ST.zw;
    u_xlat0.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat0.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat0.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    output.world.xyz = u_xlat0.xyz;
    u_xlat1.xyz = input.tangent.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.tangent.xxx + u_xlat1.xyz;
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.tangent.zzz + u_xlat1.xyz;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat9 = max(u_xlat9, 1.17549435e-38);
    u_xlat9 = rsqrt(u_xlat9);
    u_xlat1.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat1.xyz;
    output.mask.xyz = u_xlat1.xyz;
    u_xlat16_2.xyz = u_xlat0.zxy * u_xlat1.yzx;
    u_xlat16_2.xyz = u_xlat0.yzx * u_xlat1.zxy + (-u_xlat16_2.xyz);
    u_xlat0.xyz = u_xlat16_2.xyz * input.tangent.www;
    output.uv3.xyz = u_xlat0.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half3 u_xlat16_0;
float3 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;

    u_xlat16_0.x = input.mask.x;
    u_xlat16_0.y = input.uv3.x;
    u_xlat16_0.z = input.world.x;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat1.x = dot(u_xlat16_2.xyz, u_xlat16_0.xyz);
    u_xlat16_0.x = input.mask.y;
    u_xlat16_0.y = input.uv3.y;
    u_xlat16_0.z = input.world.y;
    u_xlat1.y = dot(u_xlat16_2.xyz, u_xlat16_0.xyz);
    u_xlat16_0.x = input.mask.z;
    u_xlat16_0.y = input.uv3.z;
    u_xlat16_0.z = input.world.z;
    u_xlat1.z = dot(u_xlat16_2.xyz, u_xlat16_0.xyz);
    u_xlat16_0.xyz = u_xlat1.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_0.xyz * float3(0.5, 0.5, 0.5);
    u_xlat16_1.x = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).w;
    u_xlat1.x = u_xlat16_1.x * input.color.w;
    result.w = u_xlat1.x;
    return result;

}
ENDCG
}
Pass {
Tags { "LIGHTMODE"="UniversalForward" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
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
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

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
float4 _Color;
float4 _MainTex_ST;
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
half4 _RendererColor;
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
    u_xlat0 = input.color * _Color;
    output.color = u_xlat0 * _RendererColor;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    result = u_xlat16_0 * input.color;
    return result;

}
ENDCG
}
}
Fallback "Zjcs/Official/World/Map13/BoundEntity/SpritesDefault_b64456be"
}
