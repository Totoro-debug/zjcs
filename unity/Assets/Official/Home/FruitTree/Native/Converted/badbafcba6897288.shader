Shader "Universal Render Pipeline/2D/HomeObjectSnow" {
Properties {

_MainTex ("Diffuse", 2D) = "white" { }

_MaskTex ("Snow Mask", 2D) = "black" { }

_SnowDetailTex ("Snow Detail Texture", 2D) = "white" { }

_SnowTileScale ("Snow Tiling Scale", Float) = 4.0

_SnowColorTint ("Snow Color Tint", Color) = (1,1,1,1)

_SnowColorTint2 ("Snow Color Tint2", Color) = (1,1,1,1)

_EdgeSoftness ("Edge Smoothness", Range(0.01, 0.2)) = 0.05000000074505806

_Param ("Param", Vector) = (0,0,1,1)

_LightAtten ("Light Atten", Range(0, 1)) = 0.0

}
SubShader {
LOD 0
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
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
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float2 mask:TEXCOORD2; };

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
half4 _SnowDetailTex_ST;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
sampler2D _SnowDetailTex;
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
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.mask.xy = input.uv.xy * _SnowDetailTex_ST.xy + _SnowDetailTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half3 u_xlat16_1;
float2 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
half3 u_xlat16_4;
float u_xlat6;
float2 u_xlat11;
bool u_xlatb11;
float u_xlat15;
bool u_xlatb16;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat0 = u_xlat16_0 * input.color;
    u_xlat16_1.xy = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat11.x = (-_Param.x) + _SnowTime;
    u_xlat2.xy = (-_Param.xy) + _Param.zw;
    u_xlat11.x = u_xlat11.x / u_xlat2.x;
    u_xlat11.x = clamp(u_xlat11.x, 0.0, 1.0);
    u_xlat11.x = u_xlat11.x * u_xlat2.y + _Param.y;
    u_xlat11.x = (-u_xlat11.x) + 1.0;
    u_xlat6 = (-u_xlat11.x) + u_xlat16_1.y;
    u_xlat11.x = float(1.0) / _EdgeSoftness;
    u_xlat6 = u_xlat11.x * u_xlat6;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat11.x = u_xlat6 * -2.0 + 3.0;
    u_xlat6 = u_xlat6 * u_xlat6;
    u_xlat6 = u_xlat6 * u_xlat11.x;
    u_xlat15 = max(u_xlat0.w, u_xlat6);
    u_xlatb11 = u_xlat15==0.0;
    if(u_xlatb11){clip(-1);}
    u_xlat11.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat2.x = u_xlat11.x * input.mask.x;
    u_xlat2.y = input.mask.y;
    u_xlat11.xy = u_xlat2.xy * (float2)(_SnowTileScale);
    u_xlat16_2.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat11.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_3.xyz = u_xlat16_1.xxx * u_xlat16_3.xyz + _SnowColorTint.xyz;
    u_xlat1.xzw = u_xlat16_2.xyz * u_xlat16_3.xyz + (-u_xlat0.xyz);
    u_xlat0.xyz = (float3)(u_xlat6) * u_xlat1.xzw + u_xlat0.xyz;
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb16 = u_xlat16_3.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_3.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_3.xyz = u_xlat16_1.xyz * u_xlat16_3.xxx;
    u_xlat16_3.xyz = (bool(u_xlatb16)) ? u_xlat16_3.xyz : u_xlat16_1.xyz;
    u_xlat16_4.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.xxyx.yyy;
    u_xlat16_3.xyz = u_xlat16_3.xyz * float3(_ShapeLightBlendFactors0.y,_ShapeLightBlendFactors0.y,_ShapeLightBlendFactors0.y);
    u_xlat16_3.xyz = u_xlat0.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat16_3.xyz = (float3)(_HDREmulationScale) * u_xlat16_3.xyz + (-u_xlat0.xyz);
    u_xlat16_3.xyz = (float3)(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_3.xyz + u_xlat0.xyz;
    u_xlat16_3.xyz = max(u_xlat16_3.xyz, float3(0.0,0.0,0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_3.xyz);
    result.xyz = (float3)(_LightAtten) * u_xlat16_4.xyz + u_xlat16_3.xyz;
    result.w = u_xlat15;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float2 mask:TEXCOORD2; };

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
half4 _SnowDetailTex_ST;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _SnowDetailTex;
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
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.mask.xy = input.uv.xy * _SnowDetailTex_ST.xy + _SnowDetailTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half2 u_xlat16_1;
float2 u_xlat2;
half3 u_xlat16_2;
half3 u_xlat16_3;
half3 u_xlat16_4;
float u_xlat6;
float2 u_xlat11;
bool u_xlatb11;
float u_xlat15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat0 = u_xlat16_0 * input.color;
    u_xlat16_1.xy = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).zw;
    u_xlat11.x = (-_Param.x) + _SnowTime;
    u_xlat2.xy = (-_Param.xy) + _Param.zw;
    u_xlat11.x = u_xlat11.x / u_xlat2.x;
    u_xlat11.x = clamp(u_xlat11.x, 0.0, 1.0);
    u_xlat11.x = u_xlat11.x * u_xlat2.y + _Param.y;
    u_xlat11.x = (-u_xlat11.x) + 1.0;
    u_xlat6 = (-u_xlat11.x) + u_xlat16_1.y;
    u_xlat11.x = float(1.0) / _EdgeSoftness;
    u_xlat6 = u_xlat11.x * u_xlat6;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat11.x = u_xlat6 * -2.0 + 3.0;
    u_xlat6 = u_xlat6 * u_xlat6;
    u_xlat6 = u_xlat6 * u_xlat11.x;
    u_xlat15 = max(u_xlat0.w, u_xlat6);
    u_xlatb11 = u_xlat15==0.0;
    if(u_xlatb11){clip(-1);}
    u_xlat11.x = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
    u_xlat2.x = u_xlat11.x * input.mask.x;
    u_xlat2.y = input.mask.y;
    u_xlat11.xy = u_xlat2.xy * (float2)(_SnowTileScale);
    u_xlat16_2.xyz = tex2Dbias(_SnowDetailTex, float4(u_xlat11.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.xyz = (-_SnowColorTint.xyz) + _SnowColorTint2.xyz;
    u_xlat16_3.xyz = u_xlat16_1.xxx * u_xlat16_3.xyz + _SnowColorTint.xyz;
    u_xlat1.xzw = u_xlat16_2.xyz * u_xlat16_3.xyz + (-u_xlat0.xyz);
    u_xlat0.xyz = (float3)(u_xlat6) * u_xlat1.xzw + u_xlat0.xyz;
    u_xlat16_3.xyz = max(u_xlat0.xyz, float3(0.0,0.0,0.0));
    u_xlat16_4.xyz = u_xlat0.xyz + (-u_xlat16_3.xyz);
    result.xyz = (float3)(_LightAtten) * u_xlat16_4.xyz + u_xlat16_3.xyz;
    result.w = u_xlat15;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
