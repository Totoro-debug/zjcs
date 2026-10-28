Shader "Zjcs/Official/ProfessionUI/WeaponLoaderUniversalRenderPipelineSpineSkeleton_5e7ae649" {
Properties {
_Cutoff ("Shadow alpha cutoff", Range(0.0,1.0)) = 0.10000000149011612
_MainTex ("Main Texture", 2D) = "black" {}
[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0.0
[Toggle(_ZWRITE)] _ZWrite ("Depth Write", Float) = 0.0
[MaterialToggle(_TINT_BLACK_ON)] _TintBlack ("Tint Black", Float) = 0.0
_Color ("    Light Color", Color) = (1.0,1.0,1.0,1.0)
_Black ("    Dark Color", Color) = (0.0,0.0,0.0,0.0)
_StencilRef ("Stencil Reference", Float) = 1.0
[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8.0
}
SubShader {
LOD 100
Tags { "IGNOREPROJECTOR"="true" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="UniversalForward" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite [_ZWrite]
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
#pragma multi_compile __ _STRAIGHT_ALPHA_INPUT
#include "UnityCG.cginc"
#if !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

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
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float3 u_xlat2;
bool u_xlatb2;
bool3 u_xlatb3;

    u_xlat16_0.xyz = input.color.xyz / input.color.www;
    u_xlat16_1.xyz = u_xlat16_0.xyz + float3(0.0549999997, 0.0549999997, 0.0549999997);
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(0.947867334, 0.947867334, 0.947867334);
    u_xlat2.xyz = log2(abs(u_xlat16_1.xyz));
    u_xlat2.xyz = u_xlat2.xyz * float3(2.4000001, 2.4000001, 2.4000001);
    u_xlat2.xyz = exp2(u_xlat2.xyz);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(0.0773993805, 0.0773993805, 0.0773993805);
    u_xlatb3.xyz = (float4(0.0404499993, 0.0404499993, 0.0404499993, 0.0) >= u_xlat16_0.xyzx).xyz;
    u_xlat16_0.x = (u_xlatb3.x) ? u_xlat16_1.x : u_xlat2.x;
    u_xlat16_0.y = (u_xlatb3.y) ? u_xlat16_1.y : u_xlat2.y;
    u_xlat16_0.z = (u_xlatb3.z) ? u_xlat16_1.z : u_xlat2.z;
    u_xlat16_0.xyz = u_xlat16_0.xyz * input.color.www;
    u_xlat16_1.xyz = input.color.xyz + float3(0.0549999997, 0.0549999997, 0.0549999997);
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(0.947867334, 0.947867334, 0.947867334);
    u_xlat2.xyz = log2(abs(u_xlat16_1.xyz));
    u_xlat2.xyz = u_xlat2.xyz * float3(2.4000001, 2.4000001, 2.4000001);
    u_xlat2.xyz = exp2(u_xlat2.xyz);
    u_xlat16_1.xyz = input.color.xyz * float3(0.0773993805, 0.0773993805, 0.0773993805);
    u_xlatb3.xyz = (float4(0.0404499993, 0.0404499993, 0.0404499993, 0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_1;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat16_1.x : u_xlat2.x;
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat16_1.y : u_xlat2.y;
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat16_1.z : u_xlat2.z;
        u_xlat16_1 = hlslcc_movcTemp;
    }
    u_xlatb2 = input.color.w==0.0;
    u_xlat16_1.w = 0.0;
    u_xlat16_0.w = input.color.w;
    output.color = (bool(u_xlatb2)) ? u_xlat16_1 : u_xlat16_0;
    output.uv.xy = input.uv.xy;
    u_xlat2.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat2.xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat2.xyz;
    u_xlat2.xyz = u_xlat2.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat0 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat0;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat0;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat0 = u_xlat16_0 * input.color;
    result = u_xlat0;
    return result;

}
#elif defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

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
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float3 u_xlat2;
bool u_xlatb2;
bool3 u_xlatb3;

    u_xlat16_0.xyz = input.color.xyz / input.color.www;
    u_xlat16_1.xyz = u_xlat16_0.xyz + float3(0.0549999997, 0.0549999997, 0.0549999997);
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(0.947867334, 0.947867334, 0.947867334);
    u_xlat2.xyz = log2(abs(u_xlat16_1.xyz));
    u_xlat2.xyz = u_xlat2.xyz * float3(2.4000001, 2.4000001, 2.4000001);
    u_xlat2.xyz = exp2(u_xlat2.xyz);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(0.0773993805, 0.0773993805, 0.0773993805);
    u_xlatb3.xyz = (float4(0.0404499993, 0.0404499993, 0.0404499993, 0.0) >= u_xlat16_0.xyzx).xyz;
    u_xlat16_0.x = (u_xlatb3.x) ? u_xlat16_1.x : u_xlat2.x;
    u_xlat16_0.y = (u_xlatb3.y) ? u_xlat16_1.y : u_xlat2.y;
    u_xlat16_0.z = (u_xlatb3.z) ? u_xlat16_1.z : u_xlat2.z;
    u_xlat16_0.xyz = u_xlat16_0.xyz * input.color.www;
    u_xlat16_1.xyz = input.color.xyz + float3(0.0549999997, 0.0549999997, 0.0549999997);
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(0.947867334, 0.947867334, 0.947867334);
    u_xlat2.xyz = log2(abs(u_xlat16_1.xyz));
    u_xlat2.xyz = u_xlat2.xyz * float3(2.4000001, 2.4000001, 2.4000001);
    u_xlat2.xyz = exp2(u_xlat2.xyz);
    u_xlat16_1.xyz = input.color.xyz * float3(0.0773993805, 0.0773993805, 0.0773993805);
    u_xlatb3.xyz = (float4(0.0404499993, 0.0404499993, 0.0404499993, 0.0) >= input.color.xyzx).xyz;
    {
        float4 hlslcc_movcTemp = u_xlat16_1;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat16_1.x : u_xlat2.x;
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat16_1.y : u_xlat2.y;
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat16_1.z : u_xlat2.z;
        u_xlat16_1 = hlslcc_movcTemp;
    }
    u_xlatb2 = input.color.w==0.0;
    u_xlat16_1.w = 0.0;
    u_xlat16_0.w = input.color.w;
    output.color = (bool(u_xlatb2)) ? u_xlat16_1 : u_xlat16_0;
    output.uv.xy = input.uv.xy;
    u_xlat2.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat2.xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat2.xyz;
    u_xlat2.xyz = u_xlat2.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat0 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat0;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat0;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_0.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    u_xlat0 = u_xlat16_0 * input.color;
    result = u_xlat0;
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="DepthOnly" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite On
ZTest LEqual
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask R
Stencil { Ref [_StencilRef] ReadMask 255 WriteMask 255 Comp [_StencilComp] Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 uv:TEXCOORD0; };

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
float4 _MainTex_ST;
half _Cutoff;
half4 Xhlslcc_UnusedX_Black;
half4 Xhlslcc_UnusedX_Color;
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
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.z = 0.0;
    output.uv.w = input.color.w;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half u_xlat16_0;
bool u_xlatb0;

float4 hlslcc_FragCoord = float4(input.position.xyz, 1.0/input.position.w);
    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0 = u_xlat16_0 * input.uv.w + (-_Cutoff);
    u_xlatb0 = u_xlat0<0.0;
    if(u_xlatb0){clip(-1);}
    result = hlslcc_FragCoord.zzzz;
    return result;

}
ENDCG
}
}
Fallback "Zjcs/Official/ProfessionUI/WeaponLoaderUniversalRenderPipelineUnlit_7e660c82"
}
