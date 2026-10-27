Shader "Zjcs/Official/World/Map13/Entity/SpineSkeletonGraphicCustom_05649282" {
Properties {
_MainTex ("Sprite Texture", 2D) = "white" {}
[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0.0
[Toggle(_CANVAS_GROUP_COMPATIBLE)] _CanvasGroupCompatible ("CanvasGroup Compatible", Float) = 1.0
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
_Intensity ("Intensity", Float) = 1.0
[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8.0
_Stencil ("Stencil ID", Float) = 0.0
[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0.0
_StencilWriteMask ("Stencil Write Mask", Float) = 255.0
_StencilReadMask ("Stencil Read Mask", Float) = 255.0
_ZWrite ("ZWrite On", Float) = 0.0
_ColorMask ("Color Mask", Float) = 15.0
[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0.0
}
SubShader {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "LIGHTMODE"="Universal2D" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite [_ZWrite]
ZTest [unity_GUIZTestMode]
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask [_ColorMask]
Stencil { Ref [_Stencil] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] Comp [_StencilComp] Pass [_StencilOp] Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local __ UNITY_UI_ALPHACLIP
#pragma multi_compile_local __ UNITY_UI_CLIP_RECT
#pragma multi_compile __ _CANVAS_GROUP_COMPATIBLE
#pragma multi_compile_local __ _SOLID_COLOR
#pragma multi_compile __ _STRAIGHT_ALPHA_INPUT
#include "UnityCG.cginc"
#if !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    result = u_xlat16_1 * input.color;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    result = u_xlat16_1 * input.color;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    result = u_xlat16_1 * input.color;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    result = u_xlat16_1 * input.color;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat16_1.x = u_xlat16_1.w * input.color.w + -0.0199999996;
    u_xlatb2 = u_xlat16_1.x<0.0;
    if(u_xlatb2){clip(-1);}
    result = u_xlat16_0;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
half3 u_xlat16_2;
bool u_xlatb3;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_2.x = u_xlat16_1.w * input.color.w + -0.0199999996;
    u_xlatb3 = u_xlat16_2.x<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat16_2.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_2.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_0 = u_xlat16_1 * input.color;
    result = u_xlat16_0;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat16_1.x = u_xlat16_1.w * input.color.w + -0.0199999996;
    u_xlatb2 = u_xlat16_1.x<0.0;
    if(u_xlatb2){clip(-1);}
    result = u_xlat16_0;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
half3 u_xlat16_2;
bool u_xlatb3;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_2.x = u_xlat16_1.w * input.color.w + -0.0199999996;
    u_xlatb3 = u_xlat16_2.x<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat16_2.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_2.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_0 = u_xlat16_1 * input.color;
    result = u_xlat16_0;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    result = u_xlat16_0 * u_xlat16_1.xxxx;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    result = u_xlat16_0 * u_xlat16_1.xxxx;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    result = u_xlat16_0 * u_xlat16_1.xxxx;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    result = u_xlat16_0 * u_xlat16_1.xxxx;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
half u_xlat16_4;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    u_xlat16_4 = u_xlat16_0.w * u_xlat16_1.x + -0.0199999996;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_1.xxxx;
    result = u_xlat16_0;
    u_xlatb2 = u_xlat16_4<0.0;
    if(u_xlatb2){clip(-1);}
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && !defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
half u_xlat16_4;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    u_xlat16_4 = u_xlat16_0.w * u_xlat16_1.x + -0.0199999996;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_1.xxxx;
    result = u_xlat16_0;
    u_xlatb2 = u_xlat16_4<0.0;
    if(u_xlatb2){clip(-1);}
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && !defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
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
    u_xlat16_2.xyz = _Color.www * _Color.xyz;
    u_xlat16_2.w = _Color.w;
    u_xlat1 = u_xlat16_1 * u_xlat16_2;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
half u_xlat16_4;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity) + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    u_xlat16_4 = u_xlat16_0.w * u_xlat16_1.x + -0.0199999996;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_1.xxxx;
    result = u_xlat16_0;
    u_xlatb2 = u_xlat16_4<0.0;
    if(u_xlatb2){clip(-1);}
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_CANVAS_GROUP_COMPATIBLE) && defined(_SOLID_COLOR) && defined(_STRAIGHT_ALPHA_INPUT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half _Intensity;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat16_2.xyz = input.color.www * input.color.xyz;
    u_xlat16_3.xyz = _Color.www * _Color.xyz;
    u_xlat1.xyz = u_xlat16_2.xyz * u_xlat16_3.xyz;
    u_xlat16_2.x = _Color.w;
    u_xlat1.w = u_xlat16_2.x * input.color.w;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat0.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat0.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat0.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat0 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat0 = min(u_xlat0, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat0.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat0.xy);
    output.mask.xy = (-u_xlat0.zw) + u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
half u_xlat16_4;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * float3(_Intensity,_Intensity,_Intensity);
    u_xlat16_1.xyz = u_xlat16_1.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    u_xlat16_4 = u_xlat16_0.w * u_xlat16_1.x + -0.0199999996;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_1.xxxx;
    result = u_xlat16_0;
    u_xlatb2 = u_xlat16_4<0.0;
    if(u_xlatb2){clip(-1);}
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
