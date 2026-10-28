Shader "Zjcs/Official/ProfessionUI/WeaponLoaderUniversalRenderPipelineUnlit_7e660c82" {
Properties {
_BaseMap ("Texture", 2D) = "white" {}
_BaseColor ("Color", Color) = (1.0,1.0,1.0,1.0)
_Cutoff ("AlphaCutout", Range(0.0,1.0)) = 0.5
_Surface ("__surface", Float) = 0.0
_Blend ("__mode", Float) = 0.0
_Cull ("__cull", Float) = 2.0
[ToggleUI] _AlphaClip ("__clip", Float) = 0.0
_BlendOp ("__blendop", Float) = 0.0
_SrcBlend ("__src", Float) = 1.0
_DstBlend ("__dst", Float) = 0.0
_SrcBlendAlpha ("__srcA", Float) = 1.0
_DstBlendAlpha ("__dstA", Float) = 0.0
_ZWrite ("__zw", Float) = 1.0
_AlphaToMask ("__alphaToMask", Float) = 0.0
_QueueOffset ("Queue offset", Float) = 0.0
_MainTex ("BaseMap", 2D) = "white" {}
_Color ("Base Color", Color) = (0.5,0.5,0.5,1.0)
_SampleGI ("SampleGI", Float) = 0.0
}
SubShader {
LOD 100
Tags { "IGNOREPROJECTOR"="true" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }
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
#pragma multi_compile __ LOD_FADE_CROSSFADE
#include "UnityCG.cginc"
#if !defined(LOD_FADE_CROSSFADE)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float world:TEXCOORD1; };

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
float4 _BaseMap_ST;
half Xhlslcc_UnusedX_Cutoff;
half _Surface;
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
float u_xlat6;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat6 = u_xlat0.y * float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat6 = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z * u_xlat0.x + u_xlat6;
    u_xlat6 = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z * u_xlat0.z + u_xlat6;
    output.world = u_xlat6 + float4(UNITY_MATRIX_V[0][3],UNITY_MATRIX_V[1][3],UNITY_MATRIX_V[2][3],UNITY_MATRIX_V[3][3]).z;
    output.uv.xy = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
bool u_xlatb0;
half4 u_xlat16_1;
half u_xlat16_2;

    u_xlatb0 = _Surface==1.0;
    u_xlat16_1 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_2 = u_xlat16_1.w * _BaseColor.w;
    result.xyz = u_xlat16_1.xyz * _BaseColor.xyz;
    result.w = (u_xlatb0) ? u_xlat16_2 : 1.0;
    return result;

}
#elif defined(LOD_FADE_CROSSFADE)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float world:TEXCOORD1; };

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
float4 _BaseMap_ST;
half Xhlslcc_UnusedX_Cutoff;
half _Surface;
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
sampler2D _DitheringTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float u_xlat6;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat6 = u_xlat0.y * float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat6 = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z * u_xlat0.x + u_xlat6;
    u_xlat6 = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z * u_xlat0.z + u_xlat6;
    output.world = u_xlat6 + float4(UNITY_MATRIX_V[0][3],UNITY_MATRIX_V[1][3],UNITY_MATRIX_V[2][3],UNITY_MATRIX_V[3][3]).z;
    output.uv.xy = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
half4 u_xlat16_1;
half u_xlat16_2;
bool u_xlatb3;

float4 hlslcc_FragCoord = float4(input.position.xyz, 1.0/input.position.w);
    u_xlat0.xy = hlslcc_FragCoord.xy * float2(float2(_DitheringTextureInvSize, _DitheringTextureInvSize));
    u_xlat16_0 = tex2Dbias(_DitheringTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).w;
    u_xlatb3 = unity_LODFade.x>=0.0;
    u_xlat0.x = (u_xlatb3) ? abs(u_xlat16_0) : -abs(u_xlat16_0);
    u_xlat0.x = (-u_xlat0.x) + unity_LODFade.x;
    u_xlatb0 = u_xlat0.x<0.0;
    if(u_xlatb0){clip(-1);}
    u_xlatb0 = _Surface==1.0;
    u_xlat16_1 = tex2Dbias(_BaseMap, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_2 = u_xlat16_1.w * _BaseColor.w;
    result.xyz = u_xlat16_1.xyz * _BaseColor.xyz;
    result.w = (u_xlatb0) ? u_xlat16_2 : 1.0;
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="UniversalGBuffer" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }
Cull [_Cull]
ZWrite [_ZWrite]
ZTest LEqual
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
}
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="DepthOnly" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }
Cull [_Cull]
ZWrite On
ZTest LEqual
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
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
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="DepthNormalsOnly" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }
Cull [_Cull]
ZWrite On
ZTest LEqual
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
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
struct Input { float3 normal:NORMAL; float4 vertex:POSITION; };
struct Varying { float4 position:SV_POSITION; float3 world:TEXCOORD1; };

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
    u_xlat6 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat6 = rsqrt(u_xlat6);
    output.world.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat0.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;

    u_xlat0.x = dot(input.world.xyz, input.world.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * input.world.xyz;
    result.xyz = u_xlat0.xyz;
    result.w = 0.0;
    return result;

}
#elif defined(LOD_FADE_CROSSFADE)
struct Input { float3 normal:NORMAL; float4 vertex:POSITION; };
struct Varying { float4 position:SV_POSITION; float3 world:TEXCOORD1; };

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
    u_xlat6 = dot(u_xlat0.xyz, u_xlat0.xyz);
    u_xlat6 = rsqrt(u_xlat6);
    output.world.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat0.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
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
    u_xlat0.x = dot(input.world.xyz, input.world.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * input.world.xyz;
    result.xyz = u_xlat0.xyz;
    result.w = 0.0;
    return result;

}
#endif
ENDCG
}
}
Fallback "Zjcs/Official/ProfessionUI/WeaponLoaderHiddenUniversalRenderPipelineFallbackError_fbef946f"
}
