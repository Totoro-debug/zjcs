Shader "Zjcs/Official/World/Map13/Entity/SpineSkeleton_ef302479" {
Properties {
_Cutoff ("Shadow alpha cutoff", Range(0.0,1.0)) = 0.10000000149011612
_MainTex ("Main Texture", 2D) = "black" {}
[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0.0
_StencilRef ("Stencil Reference", Float) = 1.0
[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8.0
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
Tags { "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
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
#pragma multi_compile __ _STRAIGHT_ALPHA_INPUT
#include "UnityCG.cginc"
#if !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half3 u_xlat16_2;
bool u_xlatb9;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat0.xyz = u_xlat16_2.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat0.xyz = u_xlat16_2.xyz * u_xlat0.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat0.xyz = u_xlat0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = u_xlat0.xyz * input.color.www;
    u_xlat0.xyz = input.color.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat0.xyz = input.color.xyz * u_xlat0.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat0.xyz = u_xlat0.xyz * input.color.xyz;
    u_xlatb9 = input.color.w==0.0;
    u_xlat16_1.xyz = (bool(u_xlatb9)) ? u_xlat0.xyz : u_xlat16_2.xyz;
    u_xlat0.x = 0.0;
    u_xlat16_1.w = (u_xlatb9) ? u_xlat0.x : input.color.w;
    output.color = u_xlat16_1;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    result = u_xlat16_0 * input.color;
    return result;

}
#elif defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
half3 u_xlat16_2;
bool u_xlatb9;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlat16_2.xyz = input.color.xyz / input.color.www;
    u_xlat0.xyz = u_xlat16_2.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat0.xyz = u_xlat16_2.xyz * u_xlat0.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat0.xyz = u_xlat0.xyz * u_xlat16_2.xyz;
    u_xlat16_2.xyz = u_xlat0.xyz * input.color.www;
    u_xlat0.xyz = input.color.xyz * float3(0.305306017, 0.305306017, 0.305306017) + float3(0.682171106, 0.682171106, 0.682171106);
    u_xlat0.xyz = input.color.xyz * u_xlat0.xyz + float3(0.0125228781, 0.0125228781, 0.0125228781);
    u_xlat0.xyz = u_xlat0.xyz * input.color.xyz;
    u_xlatb9 = input.color.w==0.0;
    u_xlat16_1.xyz = (bool(u_xlatb9)) ? u_xlat0.xyz : u_xlat16_2.xyz;
    u_xlat0.x = 0.0;
    u_xlat16_1.w = (u_xlatb9) ? u_xlat0.x : input.color.w;
    output.color = u_xlat16_1;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_0.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    result = u_xlat16_0 * input.color;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
