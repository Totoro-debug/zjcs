Shader "Zjcs/Official/World/Map13/Entity/SpineSkeletonGraphicScreen_1eceae8b" {
Properties {
_MainTex ("Sprite Texture", 2D) = "white" {}
[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0.0
[Toggle(_CANVAS_GROUP_COMPATIBLE)] _CanvasGroupCompatible ("CanvasGroup Compatible", Float) = 1.0
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8.0
_Stencil ("Stencil ID", Float) = 0.0
[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0.0
_StencilWriteMask ("Stencil Write Mask", Float) = 255.0
_StencilReadMask ("Stencil Read Mask", Float) = 255.0
_ColorMask ("Color Mask", Float) = 15.0
[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0.0
_OutlineWidth ("Outline Width", Range(0.0,8.0)) = 3.0
[MaterialToggle(_USE_SCREENSPACE_OUTLINE_WIDTH)] _UseScreenSpaceOutlineWidth ("Width in Screen Space", Float) = 0.0
_OutlineColor ("Outline Color", Color) = (1.0,1.0,0.0,1.0)
_OutlineReferenceTexWidth ("Reference Texture Width", Float) = 1024.0
_ThresholdEnd ("Outline Threshold", Range(0.0,1.0)) = 0.25
_OutlineSmoothness ("Outline Smoothness", Range(0.0,1.0)) = 1.0
[MaterialToggle(_USE8NEIGHBOURHOOD_ON)] _Use8Neighbourhood ("Sample 8 Neighbours", Float) = 1.0
_OutlineOpaqueAlpha ("Opaque Alpha", Range(0.0,1.0)) = 1.0
_OutlineMipLevel ("Outline Mip Level", Range(0.0,3.0)) = 0.0
}
SubShader {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest [unity_GUIZTestMode]
Blend One OneMinusSrcColor, One OneMinusSrcColor
BlendOp Add, Add
ColorMask [_ColorMask]
Stencil { Ref [_Stencil] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] Comp [_StencilComp] Pass [_StencilOp] Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ UNITY_UI_ALPHACLIP
#include "UnityCG.cginc"
#if !defined(UNITY_UI_ALPHACLIP)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; };

float4 _ClipRect;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half3 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    u_xlat16_2.xyz = max(input.color.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = log2(u_xlat16_2.xyz);
    u_xlat0.xyz = u_xlat0.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat0.xyz = exp2(u_xlat0.xyz);
    u_xlat0.xyz = u_xlat0.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlat0.xyz = max(u_xlat0.xyz, float3(0.0, 0.0, 0.0));
    u_xlat1.xyz = u_xlat0.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat1.xyz = u_xlat0.xyz * u_xlat1.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat1.xyz = u_xlat0.xyz * u_xlat1.xyz;
    u_xlat16_2.xyz = u_xlat0.xyz / input.color.www;
    u_xlat0.xyz = u_xlat16_2.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat0.xyz = u_xlat16_2.xyz * u_xlat0.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat16_2.xyz = u_xlat0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = clamp(u_xlat16_2.xyz, 0.0, 1.0);
    u_xlat16_2.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlatb0 = input.color.w==0.0;
    u_xlat16_1.xyz = (bool(u_xlatb0)) ? u_xlat1.xyz : u_xlat16_2.xyz;
    u_xlat3 = 0.0;
    u_xlat16_1.w = (u_xlatb0) ? u_xlat3 : input.color.w;
    u_xlat16_0.xyz = _Color.www * _Color.xyz;
    u_xlat16_0.w = _Color.w;
    u_xlat0 = u_xlat16_0 * u_xlat16_1;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
bool4 u_xlatb0;
half4 u_xlat16_1;

    u_xlatb0.xy = (input.world.xyxx >= _ClipRect.xyxx).xy;
    u_xlatb0.zw = (_ClipRect.zzzw >= input.world.xxxy).zw;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
    u_xlat0.z = u_xlatb0.z ? float(1.0) : 0.0;
    u_xlat0.w = u_xlatb0.w ? float(1.0) : 0.0;
;
    u_xlat0.xy = u_xlat0.zw * u_xlat0.xy;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1 = u_xlat16_1 + _TextureSampleAdd;
    u_xlat16_1 = u_xlat16_1 * input.color;
    u_xlat0 = u_xlat0.xxxx * u_xlat16_1;
    result = u_xlat0;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; };

float4 _ClipRect;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half3 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    u_xlat16_2.xyz = max(input.color.xyz, float3(0.0, 0.0, 0.0));
    u_xlat0.xyz = log2(u_xlat16_2.xyz);
    u_xlat0.xyz = u_xlat0.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat0.xyz = exp2(u_xlat0.xyz);
    u_xlat0.xyz = u_xlat0.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlat0.xyz = max(u_xlat0.xyz, float3(0.0, 0.0, 0.0));
    u_xlat1.xyz = u_xlat0.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat1.xyz = u_xlat0.xyz * u_xlat1.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat1.xyz = u_xlat0.xyz * u_xlat1.xyz;
    u_xlat16_2.xyz = u_xlat0.xyz / input.color.www;
    u_xlat0.xyz = u_xlat16_2.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat0.xyz = u_xlat16_2.xyz * u_xlat0.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat16_2.xyz = u_xlat0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = clamp(u_xlat16_2.xyz, 0.0, 1.0);
    u_xlat16_2.xyz = u_xlat16_2.xyz * input.color.www;
    u_xlatb0 = input.color.w==0.0;
    u_xlat16_1.xyz = (bool(u_xlatb0)) ? u_xlat1.xyz : u_xlat16_2.xyz;
    u_xlat3 = 0.0;
    u_xlat16_1.w = (u_xlatb0) ? u_xlat3 : input.color.w;
    u_xlat16_0.xyz = _Color.www * _Color.xyz;
    u_xlat16_0.w = _Color.w;
    u_xlat0 = u_xlat16_0 * u_xlat16_1;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
bool4 u_xlatb0;
half4 u_xlat16_1;
half u_xlat16_2;

    u_xlatb0.xy = (input.world.xyxx >= _ClipRect.xyxx).xy;
    u_xlatb0.zw = (_ClipRect.zzzw >= input.world.xxxy).zw;
    u_xlat0.x = u_xlatb0.x ? float(1.0) : 0.0;
    u_xlat0.y = u_xlatb0.y ? float(1.0) : 0.0;
    u_xlat0.z = u_xlatb0.z ? float(1.0) : 0.0;
    u_xlat0.w = u_xlatb0.w ? float(1.0) : 0.0;
;
    u_xlat0.xy = u_xlat0.zw * u_xlat0.xy;
    u_xlat0.x = u_xlat0.y * u_xlat0.x;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1 = u_xlat16_1 + _TextureSampleAdd;
    u_xlat16_1 = u_xlat16_1 * input.color;
    u_xlat16_2 = u_xlat16_1.w * u_xlat0.x + -0.00100000005;
    u_xlat0 = u_xlat0.xxxx * u_xlat16_1;
    result = u_xlat0;
    u_xlatb0.x = u_xlat16_2<0.0;
    if(u_xlatb0.x){clip(-1);}
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
