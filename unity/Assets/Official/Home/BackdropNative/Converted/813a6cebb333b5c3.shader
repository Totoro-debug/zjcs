Shader "Effect/Nebula" {
Properties {

[Header(Textures)] _NoiseTex ("Noise Texture (RGBA)", 2D) = "white" { }

_MaskTex ("Mask Texture (R)", 2D) = "white" { }

[Header(Colors)] _CoreColor ("Core Color", Color) = (1,0.9,0.5,1)

_EdgeColor ("Edge Color", Color) = (0.2,0.1,0.6,1)

_BgColor ("Background Color", Color) = (0.05,0.05,0.1,0)

[Header(Settings)] _Scale ("Texture Scale", Float) = 1.0

_WarpStrength ("Warp Strength", Range(0, 0.1)) = 0.10000000149011612

_Sparsity ("Sparsity (Island Effect)", Range(1, 10)) = 4.0

_Speed ("Flow Speed", Float) = 0.20000000298023224

_Density ("Density", Range(0.01, 3)) = 1.5

}
SubShader {
LOD 0
Tags { "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One One, One One
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _Density;
float _Scale;
float _Sparsity;
float _Speed;
float _WarpStrength;
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
half3 _BgColor;
half3 _CoreColor;
half3 _EdgeColor;
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
half4 _NoiseTex_ST;
sampler2D _MaskTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = input.uv.xy * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
    output.world.xy = u_xlat0.xy * (float2)(_Scale);
    output.uv.xy = input.uv.xy;
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
half2 u_xlat16_0;
half2 u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
float u_xlat6;

    u_xlat16_0.xy = tex2Dbias(_NoiseTex, float4(input.world.xy, 0, _GlobalMipBias.x)).xy;
    u_xlat0.xy = u_xlat16_0.xy + input.world.xy;
    u_xlat6 = _Time.y * _Speed;
    u_xlat0.xy = (float2)(u_xlat6) * float2(0.177000001,0.230000004) + u_xlat0.xy;
    u_xlat16_0.xy = tex2Dbias(_NoiseTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).yz;
    u_xlat16_1.xy = u_xlat16_0.xy + float2(-0.5,-0.5);
    u_xlat16_1.xy = u_xlat16_1.xy + u_xlat16_1.xy;
    u_xlat0.xy = u_xlat16_1.xy * (float2)(float2(_WarpStrength, _WarpStrength)) + input.world.xy;
    u_xlat16_0.x = tex2Dbias(_NoiseTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).w;
    u_xlat0.x = log2(abs(u_xlat16_0.x));
    u_xlat0.x = u_xlat0.x * _Sparsity;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat3.xyz = _CoreColor.xyz + (-_EdgeColor.xyz);
    u_xlat3.xyz = u_xlat0.xxx * u_xlat3.xyz + _EdgeColor.xyz;
    u_xlat0.x = u_xlat0.x * _Density;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat3.xyz = u_xlat3.xyz + (-_BgColor.xyz);
    u_xlat3.xyz = u_xlat0.xxx * u_xlat3.xyz + _BgColor.xyz;
    u_xlat16_2 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0.x = u_xlat0.x * u_xlat16_2;
    result.xyz = u_xlat0.xxx * u_xlat3.xyz;
    result.w = u_xlat0.x;
    return result;

}
ENDCG
}
}
Fallback Off
}
