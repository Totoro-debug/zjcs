Shader "Universal Render Pipeline/2D/Sprite-Lit-Alpha" {
Properties {

_MainTex ("Diffuse", 2D) = "white" { }

_Color ("Tint", Color) = (1,1,1,1)

_RendererColor ("RendererColor", Color) = (1,1,1,1)

_Flip ("Flip", Vector) = (1,1,1,1)

_AlphaTex ("External Alpha", 2D) = "white" { }

_EnableExternalAlpha ("Enable External Alpha", Float) = 0.0

_ExtraAlpha ("Extra Alpha", Range(0, 1)) = 1.0

_ZWrite ("ZWrite", Float) = 0.0

_LightAtten ("Light Atten", Range(0, 1)) = 0.0

}
SubShader {
LOD 0
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
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

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
half _ExtraAlpha;
half _HDREmulationScale;
half _LightAtten;
half _UseSceneLighting;
half _ZWrite;
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
    output.color.w = u_xlat1.w * _ExtraAlpha;
    output.color.xyz = u_xlat1.xyz;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
bool u_xlatb2;
half4 u_xlat16_3;
half3 u_xlat16_4;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb2 = any(float4(0.0,0.0,0.0,0.0)!=(float4)(_ZWrite));
    u_xlat16_3.x = input.color.w * u_xlat16_0.w + -0.00100000005;
    u_xlatb15 = u_xlat16_3.x<0.0;
    u_xlatb15 = u_xlatb2 && u_xlatb15;
    if(u_xlatb15){clip(-1);}
    u_xlatb15 = u_xlat1.w==0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat16_2.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb15 = u_xlat16_3.x!=0.0;
    u_xlat16_3 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_3.x = dot(u_xlat16_3, _ShapeLightMaskFilter0);
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_3.xxx;
    u_xlat16_3.xyz = (bool(u_xlatb15)) ? u_xlat16_3.xyz : u_xlat16_2.xyz;
    u_xlat16_4.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_3.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_3.xyz = u_xlat1.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat16_2.xyz = (float3)(_HDREmulationScale) * u_xlat16_3.xyz + (-u_xlat1.xyz);
    u_xlat16_2.w = 0.0;
    u_xlat16_1 = (float4)(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2 + u_xlat1;
    u_xlat16_1 = max(u_xlat16_1, float4(0.0,0.0,0.0,0.0));
    u_xlat16_3.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_1.xyz);
    result.xyz = (float3)(float3(_LightAtten, _LightAtten, _LightAtten)) * u_xlat16_3.xyz + u_xlat16_1.xyz;
    result.w = u_xlat16_1.w;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

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
half _ExtraAlpha;
half _LightAtten;
half _ZWrite;
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
    output.color.w = u_xlat1.w * _ExtraAlpha;
    output.color.xyz = u_xlat1.xyz;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb2;
half3 u_xlat16_3;
bool u_xlatb12;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_0 * input.color;
    u_xlatb2 = any(float4(0.0,0.0,0.0,0.0)!=(float4)(_ZWrite));
    u_xlat16_3.x = input.color.w * u_xlat16_0.w + -0.00100000005;
    u_xlatb12 = u_xlat16_3.x<0.0;
    u_xlatb12 = u_xlatb2 && u_xlatb12;
    if(u_xlatb12){clip(-1);}
    u_xlatb12 = u_xlat1.w==0.0;
    if(u_xlatb12){clip(-1);}
    u_xlat16_1 = max(u_xlat1, float4(0.0,0.0,0.0,0.0));
    u_xlat16_3.xyz = input.color.xyz * u_xlat16_0.xyz + (-u_xlat16_1.xyz);
    result.xyz = (float3)(float3(_LightAtten, _LightAtten, _LightAtten)) * u_xlat16_3.xyz + u_xlat16_1.xyz;
    result.w = u_xlat16_1.w;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
