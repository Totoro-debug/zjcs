Shader "Universal Render Pipeline/2D/Sprite-Lit-ColorBlend" {
Properties {

_MainTex ("Sprite Texture", 2D) = "white" { }

_Color1 ("Color 1", Color) = (1,0,0,1)

_Color2 ("Color 2", Color) = (0,0,1,1)

_BlendMaskTex ("Blend Mask", 2D) = "white" { }

[Toggle(_USE_MASK)] _UseMask ("Use Mask", Float) = 0.0

_Color1Range ("Color1 Range", Range(0, 1)) = 0.25

_Color2Range ("Color2 Range", Range(0, 1)) = 0.75

_Blend ("Blend Offset", Range(0, 1)) = 0.5

_TransitionSoftness ("Transition Softness", Range(0.001, 1)) = 0.20000000298023224

_Lighting ("Lighting", Range(0, 1)) = 0.0

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
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float2 world:TEXCOORD1; };

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
float4 _BlendMaskTex_ST;
float4 _MainTex_ST;
half _Blend;
half _Color1Range;
half _Color2Range;
half _HDREmulationScale;
half _Lighting;
half _TransitionSoftness;
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
half4 _Color1;
half4 _Color2;
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
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.mask.xy = input.uv.xy * _BlendMaskTex_ST.xy + _BlendMaskTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
bool u_xlatb3;
half4 u_xlat16_4;
half3 u_xlat16_5;
half u_xlat16_7;
float u_xlat8;
half u_xlat16_13;
half u_xlat16_19;
bool u_xlatb21;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = min(_Color2Range, _Color1Range);
    u_xlat16_7 = max(_Color2Range, _Color1Range);
    u_xlat16_13 = _Blend + -0.5;
    u_xlat16_13 = u_xlat16_13 + input.uv.y;
    u_xlat16_13 = clamp(u_xlat16_13, 0.0, 1.0);
    u_xlat16_19 = u_xlat16_7 + u_xlat16_1.x;
    u_xlat16_1.x = (-u_xlat16_1.x) + u_xlat16_7;
    u_xlat2 = u_xlat16_1.x * 0.5;
    u_xlat2 = max(u_xlat2, 9.99999975e-05);
    u_xlat8 = max(_TransitionSoftness, 0.00100000005);
    u_xlat16_1.x = u_xlat16_19 * 0.5 + (-u_xlat2);
    u_xlat16_1.x = (-u_xlat8) + u_xlat16_1.x;
    u_xlat16_7 = u_xlat16_19 * 0.5 + u_xlat2;
    u_xlat16_7 = u_xlat8 + u_xlat16_7;
    u_xlat16_7 = (-u_xlat16_1.x) + u_xlat16_7;
    u_xlat16_1.x = (-u_xlat16_1.x) + u_xlat16_13;
    u_xlat16_7 = float(1.0) / u_xlat16_7;
    u_xlat16_1.x = u_xlat16_7 * u_xlat16_1.x;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_7 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_7;
    u_xlat16_2 = _Color1 + (-_Color2);
    u_xlat16_1 = u_xlat16_1.xxxx * u_xlat16_2 + _Color2;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_1;
    u_xlat16_1.w = u_xlat16_0.w * input.color.w;
    u_xlatb3 = u_xlat16_1.w==0.0;
    if(u_xlatb3){clip(-1);}
    if(u_xlatb3){clip(-1);}
    u_xlat16_1.xyz = u_xlat16_0.xyz * input.color.xyz;
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb21 = u_xlat16_4.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_4 = _ShapeLightInvertedFilter0 * float4(0.0,1.0,1.0,1.0);
    u_xlat16_2 = u_xlat16_2 * float4(1.0,0.0,0.0,0.0) + u_xlat16_4;
    u_xlat16_4.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_4.xyz = u_xlat16_3.xyz * u_xlat16_4.xxx;
    u_xlat16_4.xyz = (bool(u_xlatb21)) ? u_xlat16_4.xyz : u_xlat16_3.xyz;
    u_xlat16_5.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_4.xyz = u_xlat16_4.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_4.xyz = u_xlat16_1.xyz * u_xlat16_5.xyz + u_xlat16_4.xyz;
    u_xlat16_2.xyz = (float3)(float3(_HDREmulationScale, _HDREmulationScale, _HDREmulationScale)) * u_xlat16_4.xyz + (-u_xlat16_1.xyz);
    u_xlat16_2.w = 0.0;
    u_xlat16_2 = (float4)(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2 + u_xlat16_1;
    u_xlat16_2 = max(u_xlat16_2, float4(0.0,0.0,0.0,0.0));
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_1.www;
    u_xlat16_0 = (-u_xlat16_1) + u_xlat16_2;
    result = (float4)(_Lighting) * u_xlat16_0 + u_xlat16_1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float2 world:TEXCOORD1; };

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
float4 _BlendMaskTex_ST;
float4 _MainTex_ST;
half _Blend;
half _Color1Range;
half _Color2Range;
half _Lighting;
half _TransitionSoftness;
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
half4 _Color1;
half4 _Color2;
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
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.mask.xy = input.uv.xy * _BlendMaskTex_ST.xy + _BlendMaskTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float u_xlat2;
half4 u_xlat16_2;
bool u_xlatb3;
half u_xlat16_5;
float u_xlat6;
half u_xlat16_9;
half u_xlat16_13;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = min(_Color2Range, _Color1Range);
    u_xlat16_5 = max(_Color2Range, _Color1Range);
    u_xlat16_9 = _Blend + -0.5;
    u_xlat16_9 = u_xlat16_9 + input.uv.y;
    u_xlat16_9 = clamp(u_xlat16_9, 0.0, 1.0);
    u_xlat16_13 = u_xlat16_5 + u_xlat16_1.x;
    u_xlat16_1.x = (-u_xlat16_1.x) + u_xlat16_5;
    u_xlat2 = u_xlat16_1.x * 0.5;
    u_xlat2 = max(u_xlat2, 9.99999975e-05);
    u_xlat6 = max(_TransitionSoftness, 0.00100000005);
    u_xlat16_1.x = u_xlat16_13 * 0.5 + (-u_xlat2);
    u_xlat16_1.x = (-u_xlat6) + u_xlat16_1.x;
    u_xlat16_5 = u_xlat16_13 * 0.5 + u_xlat2;
    u_xlat16_5 = u_xlat6 + u_xlat16_5;
    u_xlat16_5 = (-u_xlat16_1.x) + u_xlat16_5;
    u_xlat16_1.x = (-u_xlat16_1.x) + u_xlat16_9;
    u_xlat16_5 = float(1.0) / u_xlat16_5;
    u_xlat16_1.x = u_xlat16_5 * u_xlat16_1.x;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5 = u_xlat16_1.x * -2.0 + 3.0;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_1.x;
    u_xlat16_1.x = u_xlat16_1.x * u_xlat16_5;
    u_xlat16_2 = _Color1 + (-_Color2);
    u_xlat16_1 = u_xlat16_1.xxxx * u_xlat16_2 + _Color2;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_1;
    u_xlat16_1.w = u_xlat16_0.w * input.color.w;
    u_xlatb3 = u_xlat16_1.w==0.0;
    if(u_xlatb3){clip(-1);}
    if(u_xlatb3){clip(-1);}
    u_xlat16_1.xyz = u_xlat16_0.xyz * input.color.xyz;
    u_xlat16_2 = max(u_xlat16_1, float4(0.0,0.0,0.0,0.0));
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_1.www;
    u_xlat16_0 = (-u_xlat16_1) + u_xlat16_2;
    result = (float4)(_Lighting) * u_xlat16_0 + u_xlat16_1;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
