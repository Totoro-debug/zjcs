Shader "Zjcs/Official/World/Map13/Entity/SpineSkeletonGraphicEffect_484d1990" {
Properties {
_MainTex ("Sprite Texture", 2D) = "white" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8.0
_Stencil ("Stencil ID", Float) = 0.0
[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0.0
_StencilWriteMask ("Stencil Write Mask", Float) = 255.0
_StencilReadMask ("Stencil Read Mask", Float) = 255.0
_ColorMask ("Color Mask", Float) = 15.0
[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0.0
[Toggle(_HSVSHIFT)] _UseHsvShift ("Use HsvShift", Float) = 0.0
_Hue ("Hue", Range(0.0,1.0)) = 0.0
_Saturation ("Saturation", Range(0.0,1.0)) = 1.0
_Value ("Value", Range(0.0,1.0)) = 1.0
_SolidColor ("Unlit Solid Color", Color) = (0.6600000262260437,0.6600000262260437,0.6600000262260437,1.0)
}
SubShader {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
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
#pragma multi_compile_local __ _HSVSHIFT
#pragma multi_compile_local __ _SOLID_COLOR
#include "UnityCG.cginc"
#if !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
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
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    result = u_xlat16_1 * input.color;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
half u_xlat16_0;
bool u_xlatb0;
half u_xlat16_1;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_1 = u_xlat16_0 + _TextureSampleAdd.w;
    u_xlat16_1 = u_xlat16_1 * input.color.w + -0.100000001;
    u_xlatb0 = u_xlat16_1<0.0;
    if(u_xlatb0){clip(-1);}
    result = _SolidColor;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
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
half4 u_xlat16_2;
half4 u_xlat16_3;
float u_xlat4;
bool u_xlatb4;
half3 u_xlat16_5;
half u_xlat16_6;
float u_xlat9;

    u_xlat16_0.z = float(-1.0);
    u_xlat16_0.w = float(0.666666687);
    u_xlat16_1.z = float(1.0);
    u_xlat16_1.w = float(-1.0);
    u_xlat16_2 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_3.w = u_xlat16_2.w + _TextureSampleAdd.w;
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_2.www + _TextureSampleAdd.xyz;
    u_xlat16_2 = u_xlat16_3 * input.color;
    u_xlatb4 = u_xlat16_2.y>=u_xlat16_2.z;
    u_xlat16_3.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_0.xy = u_xlat16_2.zy;
    u_xlat16_1.xy = u_xlat16_3.yz * input.color.yz + (-u_xlat16_0.xy);
    u_xlat16_0 = u_xlat16_3.xxxx * u_xlat16_1.xywz + u_xlat16_0.xywz;
    u_xlat16_1.z = u_xlat16_0.w;
    u_xlatb4 = u_xlat16_2.x>=u_xlat16_0.x;
    u_xlat16_3.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_0.w = u_xlat16_2.x;
    result.w = u_xlat16_2.w;
    u_xlat16_1.xyw = u_xlat16_0.wyx;
    u_xlat16_1 = (-u_xlat16_0) + u_xlat16_1;
    u_xlat16_0 = u_xlat16_3.xxxx * u_xlat16_1 + u_xlat16_0;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_6 = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_5.x = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_5.x = u_xlat16_5.x / u_xlat16_6;
    u_xlat16_5.x = u_xlat16_5.x + u_xlat16_0.z;
    u_xlat4 = abs(u_xlat16_5.x) + _Hue;
    u_xlat16_5.xyz = float3(u_xlat4,u_xlat4,u_xlat4) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_5.xyz = frac(u_xlat16_5.xyz);
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_5.xyz = abs(u_xlat16_5.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_5.xyz = clamp(u_xlat16_5.xyz, 0.0, 1.0);
    u_xlat16_5.xyz = u_xlat16_5.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_6 = u_xlat16_0.x + 9.99999975e-05;
    u_xlat4 = u_xlat16_0.x * _Value;
    u_xlat16_0.x = u_xlat16_1.x / u_xlat16_6;
    u_xlat9 = u_xlat16_0.x * _Saturation;
    u_xlat16_0.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat16_5.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_0.xyz * float3(u_xlat4,u_xlat4,u_xlat4);
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
bool u_xlatb0;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;
bool u_xlatb3;
half3 u_xlat16_5;
float u_xlat7;
half u_xlat16_9;

    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_1.x = u_xlat16_0.x + _TextureSampleAdd.w;
    u_xlat16_1.x = u_xlat16_1.x * input.color.w + -0.100000001;
    u_xlatb0 = u_xlat16_1.x<0.0;
    if(u_xlatb0){clip(-1);}
    u_xlatb0 = _SolidColor.y>=_SolidColor.z;
    u_xlat16_1.xy = _SolidColor.yz;
    u_xlat16_1.z = float(0.0);
    u_xlat16_1.w = float(-0.333333343);
    u_xlat16_2.xy = _SolidColor.zy;
    u_xlat16_2.z = float(-1.0);
    u_xlat16_2.w = float(0.666666687);
    u_xlat16_1 = u_xlat16_1 + (-u_xlat16_2);
    u_xlat16_0 = (bool(u_xlatb0)) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, -0.0);
    u_xlat16_0 = u_xlat16_0.xywz + u_xlat16_2.xywz;
    u_xlat16_1.z = u_xlat16_0.w;
    u_xlatb3 = _SolidColor.x>=u_xlat16_0.x;
    u_xlat16_0.w = _SolidColor.x;
    u_xlat16_1.xyw = u_xlat16_0.wyx;
    u_xlat16_1 = (-u_xlat16_0) + u_xlat16_1;
    u_xlat16_1 = (bool(u_xlatb3)) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, 0.0);
    u_xlat16_0 = u_xlat16_0 + u_xlat16_1;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_5.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_9 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_5.x = u_xlat16_9 / u_xlat16_5.x;
    u_xlat16_5.x = u_xlat16_0.z + u_xlat16_5.x;
    u_xlat3 = abs(u_xlat16_5.x) + _Hue;
    u_xlat16_5.xyz = float3(u_xlat3,u_xlat3,u_xlat3) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_5.xyz = frac(u_xlat16_5.xyz);
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_5.xyz = abs(u_xlat16_5.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_5.xyz = clamp(u_xlat16_5.xyz, 0.0, 1.0);
    u_xlat16_5.xyz = u_xlat16_5.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat3 = u_xlat16_0.x * _Value;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_2.x;
    u_xlat7 = u_xlat16_1.x * _Saturation;
    u_xlat16_1.xyz = float3(u_xlat7,u_xlat7,u_xlat7) * u_xlat16_5.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * float3(u_xlat3,u_xlat3,u_xlat3);
    result.w = _SolidColor.w;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
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
bool u_xlatb2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat16_1.x = u_xlat16_1.w * input.color.w + -0.00100000005;
    u_xlatb2 = u_xlat16_1.x<0.0;
    if(u_xlatb2){clip(-1);}
    result = u_xlat16_0;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
half u_xlat16_0;
bool2 u_xlatb0;
half2 u_xlat16_1;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_1.x = u_xlat16_0 + _TextureSampleAdd.w;
    u_xlat16_1.xy = u_xlat16_1.xx * input.color.ww + float2(-0.00100000005, -0.100000001);
    u_xlatb0.xy = (u_xlat16_1.xyxx < float4(0.0, 0.0, 0.0, 0.0)).xy;
    if(u_xlatb0.x){clip(-1);}
    if(u_xlatb0.y){clip(-1);}
    result = _SolidColor;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
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
half4 u_xlat16_2;
half4 u_xlat16_3;
bool u_xlatb3;
float u_xlat4;
bool u_xlatb4;
half3 u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_2.x = u_xlat16_1.w * input.color.w + -0.00100000005;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlatb3 = u_xlat16_2.x<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat16_2.z = float(-1.0);
    u_xlat16_2.w = float(0.666666687);
    u_xlat16_3.z = float(1.0);
    u_xlat16_3.w = float(-1.0);
    u_xlat16_2.xy = u_xlat16_0.zy;
    u_xlat16_3.xy = u_xlat16_1.yz * input.color.yz + (-u_xlat16_2.xy);
    u_xlatb4 = u_xlat16_2.y>=u_xlat16_0.z;
    u_xlat16_1.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_1 = u_xlat16_1.xxxx * u_xlat16_3.xywz + u_xlat16_2.xywz;
    u_xlatb4 = u_xlat16_0.x>=u_xlat16_1.x;
    u_xlat16_2.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_3.z = u_xlat16_1.w;
    u_xlat16_1.w = u_xlat16_0.x;
    result.w = u_xlat16_0.w;
    u_xlat16_3.xyw = u_xlat16_1.wyx;
    u_xlat16_0 = (-u_xlat16_1) + u_xlat16_3;
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_0 + u_xlat16_1;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_6.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_11 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_6.x = u_xlat16_11 / u_xlat16_6.x;
    u_xlat16_6.x = u_xlat16_0.z + u_xlat16_6.x;
    u_xlat4 = abs(u_xlat16_6.x) + _Hue;
    u_xlat16_6.xyz = float3(u_xlat4,u_xlat4,u_xlat4) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_6.xyz = frac(u_xlat16_6.xyz);
    u_xlat16_6.xyz = u_xlat16_6.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_6.xyz = abs(u_xlat16_6.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_6.xyz = clamp(u_xlat16_6.xyz, 0.0, 1.0);
    u_xlat16_6.xyz = u_xlat16_6.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat4 = u_xlat16_0.x * _Value;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_2.x;
    u_xlat9 = u_xlat16_1.x * _Saturation;
    u_xlat16_1.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat16_6.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * float3(u_xlat4,u_xlat4,u_xlat4);
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
bool2 u_xlatb0;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;
bool u_xlatb3;
half3 u_xlat16_5;
float u_xlat7;
half u_xlat16_9;

    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_1.x = u_xlat16_0.x + _TextureSampleAdd.w;
    u_xlat16_1.xy = u_xlat16_1.xx * input.color.ww + float2(-0.00100000005, -0.100000001);
    u_xlatb0.xy = (u_xlat16_1.xyxx < float4(0.0, 0.0, 0.0, 0.0)).xy;
    if(u_xlatb0.x){clip(-1);}
    if(u_xlatb0.y){clip(-1);}
    u_xlatb0.x = _SolidColor.y>=_SolidColor.z;
    u_xlat16_1.xy = _SolidColor.yz;
    u_xlat16_1.z = float(0.0);
    u_xlat16_1.w = float(-0.333333343);
    u_xlat16_2.xy = _SolidColor.zy;
    u_xlat16_2.z = float(-1.0);
    u_xlat16_2.w = float(0.666666687);
    u_xlat16_1 = u_xlat16_1 + (-u_xlat16_2);
    u_xlat16_0 = (u_xlatb0.x) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, -0.0);
    u_xlat16_0 = u_xlat16_0.xywz + u_xlat16_2.xywz;
    u_xlat16_1.z = u_xlat16_0.w;
    u_xlatb3 = _SolidColor.x>=u_xlat16_0.x;
    u_xlat16_0.w = _SolidColor.x;
    u_xlat16_1.xyw = u_xlat16_0.wyx;
    u_xlat16_1 = (-u_xlat16_0) + u_xlat16_1;
    u_xlat16_1 = (bool(u_xlatb3)) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, 0.0);
    u_xlat16_0 = u_xlat16_0 + u_xlat16_1;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_5.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_9 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_5.x = u_xlat16_9 / u_xlat16_5.x;
    u_xlat16_5.x = u_xlat16_0.z + u_xlat16_5.x;
    u_xlat3 = abs(u_xlat16_5.x) + _Hue;
    u_xlat16_5.xyz = float3(u_xlat3,u_xlat3,u_xlat3) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_5.xyz = frac(u_xlat16_5.xyz);
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_5.xyz = abs(u_xlat16_5.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_5.xyz = clamp(u_xlat16_5.xyz, 0.0, 1.0);
    u_xlat16_5.xyz = u_xlat16_5.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat3 = u_xlat16_0.x * _Value;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_2.x;
    u_xlat7 = u_xlat16_1.x * _Saturation;
    u_xlat16_1.xyz = float3(u_xlat7,u_xlat7,u_xlat7) * u_xlat16_5.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * float3(u_xlat3,u_xlat3,u_xlat3);
    result.w = _SolidColor.w;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
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
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
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
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
float2 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
half u_xlat16_1;
half u_xlat16_3;

    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat0.y * u_xlat0.x;
    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_3 = u_xlat16_0 + _TextureSampleAdd.w;
    u_xlat16_3 = u_xlat16_3 * input.color.w;
    u_xlat16_1 = u_xlat16_3 * u_xlat16_1 + -0.100000001;
    u_xlatb0 = u_xlat16_1<0.0;
    if(u_xlatb0){clip(-1);}
    result = _SolidColor;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
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
half4 u_xlat16_2;
float2 u_xlat3;
half4 u_xlat16_3;
float u_xlat4;
bool u_xlatb4;
half3 u_xlat16_5;
half u_xlat16_7;
float u_xlat9;

    u_xlat16_0.z = float(-1.0);
    u_xlat16_0.w = float(0.666666687);
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_1.www + _TextureSampleAdd.xyz;
    u_xlat16_2.w = u_xlat16_1.w + _TextureSampleAdd.w;
    u_xlat16_1 = u_xlat16_2 * input.color;
    u_xlat3.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat3.xy = u_xlat3.xy + -abs(input.mask.xy);
    u_xlat3.xy = u_xlat3.xy * input.mask.zw;
    u_xlat3.xy = clamp(u_xlat3.xy, 0.0, 1.0);
    u_xlat16_2.x = u_xlat3.y * u_xlat3.x;
    u_xlat16_3 = u_xlat16_1 * u_xlat16_2.xxxx;
    u_xlat16_0.xy = u_xlat16_3.zy;
    u_xlat16_1.xy = u_xlat16_1.yz * u_xlat16_2.xx + (-u_xlat16_0.xy);
    u_xlat16_1.z = float(1.0);
    u_xlat16_1.w = float(-1.0);
    u_xlatb4 = u_xlat16_0.y>=u_xlat16_3.z;
    u_xlat16_2.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_1.xywz + u_xlat16_0.xywz;
    u_xlatb4 = u_xlat16_3.x>=u_xlat16_0.x;
    u_xlat16_2.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_1.z = u_xlat16_0.w;
    u_xlat16_0.w = u_xlat16_3.x;
    result.w = u_xlat16_3.w;
    u_xlat16_1.xyw = u_xlat16_0.wyx;
    u_xlat16_1 = (-u_xlat16_0) + u_xlat16_1;
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_1 + u_xlat16_0;
    u_xlat16_2.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_2.x = u_xlat16_0.x + (-u_xlat16_2.x);
    u_xlat16_7 = u_xlat16_2.x * 6.0 + 9.99999975e-05;
    u_xlat16_5.x = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_5.x = u_xlat16_5.x / u_xlat16_7;
    u_xlat16_5.x = u_xlat16_5.x + u_xlat16_0.z;
    u_xlat4 = abs(u_xlat16_5.x) + _Hue;
    u_xlat16_5.xyz = float3(u_xlat4,u_xlat4,u_xlat4) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_5.xyz = frac(u_xlat16_5.xyz);
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_5.xyz = abs(u_xlat16_5.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_5.xyz = clamp(u_xlat16_5.xyz, 0.0, 1.0);
    u_xlat16_5.xyz = u_xlat16_5.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_7 = u_xlat16_0.x + 9.99999975e-05;
    u_xlat4 = u_xlat16_0.x * _Value;
    u_xlat16_0.x = u_xlat16_2.x / u_xlat16_7;
    u_xlat9 = u_xlat16_0.x * _Saturation;
    u_xlat16_0.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat16_5.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_0.xyz * float3(u_xlat4,u_xlat4,u_xlat4);
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
float2 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;
bool u_xlatb3;
half3 u_xlat16_5;
float u_xlat7;
half u_xlat16_9;

    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat0.y * u_xlat0.x;
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_5.x = u_xlat16_0.x + _TextureSampleAdd.w;
    u_xlat16_5.x = u_xlat16_5.x * input.color.w;
    u_xlat16_1.x = u_xlat16_5.x * u_xlat16_1.x + -0.100000001;
    u_xlatb0 = u_xlat16_1.x<0.0;
    if(u_xlatb0){clip(-1);}
    u_xlatb0 = _SolidColor.y>=_SolidColor.z;
    u_xlat16_1.xy = _SolidColor.yz;
    u_xlat16_1.z = float(0.0);
    u_xlat16_1.w = float(-0.333333343);
    u_xlat16_2.xy = _SolidColor.zy;
    u_xlat16_2.z = float(-1.0);
    u_xlat16_2.w = float(0.666666687);
    u_xlat16_1 = u_xlat16_1 + (-u_xlat16_2);
    u_xlat16_0 = (bool(u_xlatb0)) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, -0.0);
    u_xlat16_0 = u_xlat16_0.xywz + u_xlat16_2.xywz;
    u_xlat16_1.z = u_xlat16_0.w;
    u_xlatb3 = _SolidColor.x>=u_xlat16_0.x;
    u_xlat16_0.w = _SolidColor.x;
    u_xlat16_1.xyw = u_xlat16_0.wyx;
    u_xlat16_1 = (-u_xlat16_0) + u_xlat16_1;
    u_xlat16_1 = (bool(u_xlatb3)) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, 0.0);
    u_xlat16_0 = u_xlat16_0 + u_xlat16_1;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_5.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_9 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_5.x = u_xlat16_9 / u_xlat16_5.x;
    u_xlat16_5.x = u_xlat16_0.z + u_xlat16_5.x;
    u_xlat3 = abs(u_xlat16_5.x) + _Hue;
    u_xlat16_5.xyz = float3(u_xlat3,u_xlat3,u_xlat3) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_5.xyz = frac(u_xlat16_5.xyz);
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_5.xyz = abs(u_xlat16_5.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_5.xyz = clamp(u_xlat16_5.xyz, 0.0, 1.0);
    u_xlat16_5.xyz = u_xlat16_5.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat3 = u_xlat16_0.x * _Value;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_2.x;
    u_xlat7 = u_xlat16_1.x * _Saturation;
    u_xlat16_1.xyz = float3(u_xlat7,u_xlat7,u_xlat7) * u_xlat16_5.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * float3(u_xlat3,u_xlat3,u_xlat3);
    result.w = _SolidColor.w;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
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
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    u_xlat16_4 = u_xlat16_0.w * u_xlat16_1.x + -0.00100000005;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_1.xxxx;
    result = u_xlat16_0;
    u_xlatb2 = u_xlat16_4<0.0;
    if(u_xlatb2){clip(-1);}
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && !defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
float2 u_xlat0;
half u_xlat16_0;
bool2 u_xlatb0;
half2 u_xlat16_1;
half u_xlat16_3;

    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat0.y * u_xlat0.x;
    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_3 = u_xlat16_0 + _TextureSampleAdd.w;
    u_xlat16_3 = u_xlat16_3 * input.color.w;
    u_xlat16_1.xy = float2(u_xlat16_3,u_xlat16_3) * u_xlat16_1.xx + float2(-0.00100000005, -0.100000001);
    u_xlatb0.xy = (u_xlat16_1.xyxx < float4(0.0, 0.0, 0.0, 0.0)).xy;
    if(u_xlatb0.x){clip(-1);}
    if(u_xlatb0.y){clip(-1);}
    result = _SolidColor;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
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
half4 u_xlat16_2;
bool u_xlatb2;
half4 u_xlat16_3;
float u_xlat4;
bool u_xlatb4;
half3 u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    u_xlat16_6.x = u_xlat16_0.w * u_xlat16_1.x + -0.00100000005;
    u_xlatb2 = u_xlat16_6.x<0.0;
    if(u_xlatb2){clip(-1);}
    u_xlat16_2 = u_xlat16_0 * u_xlat16_1.xxxx;
    u_xlat16_3.xy = u_xlat16_2.zy;
    u_xlat16_0.xy = u_xlat16_0.yz * u_xlat16_1.xx + (-u_xlat16_3.xy);
    u_xlatb4 = u_xlat16_3.y>=u_xlat16_2.z;
    u_xlat16_1.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_3.z = float(-1.0);
    u_xlat16_3.w = float(0.666666687);
    u_xlat16_0.z = float(1.0);
    u_xlat16_0.w = float(-1.0);
    u_xlat16_0 = u_xlat16_1.xxxx * u_xlat16_0.xywz + u_xlat16_3.xywz;
    u_xlatb4 = u_xlat16_2.x>=u_xlat16_0.x;
    u_xlat16_1.x = (u_xlatb4) ? 1.0 : 0.0;
    u_xlat16_3.z = u_xlat16_0.w;
    u_xlat16_0.w = u_xlat16_2.x;
    result.w = u_xlat16_2.w;
    u_xlat16_3.xyw = u_xlat16_0.wyx;
    u_xlat16_2 = (-u_xlat16_0) + u_xlat16_3;
    u_xlat16_0 = u_xlat16_1.xxxx * u_xlat16_2 + u_xlat16_0;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_6.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_11 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_6.x = u_xlat16_11 / u_xlat16_6.x;
    u_xlat16_6.x = u_xlat16_0.z + u_xlat16_6.x;
    u_xlat4 = abs(u_xlat16_6.x) + _Hue;
    u_xlat16_6.xyz = float3(u_xlat4,u_xlat4,u_xlat4) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_6.xyz = frac(u_xlat16_6.xyz);
    u_xlat16_6.xyz = u_xlat16_6.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_6.xyz = abs(u_xlat16_6.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_6.xyz = clamp(u_xlat16_6.xyz, 0.0, 1.0);
    u_xlat16_6.xyz = u_xlat16_6.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat4 = u_xlat16_0.x * _Value;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_3.x;
    u_xlat9 = u_xlat16_1.x * _Saturation;
    u_xlat16_1.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat16_6.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * float3(u_xlat4,u_xlat4,u_xlat4);
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_HSVSHIFT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _Hue;
float _Saturation;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _Value;
float4 _ClipRect;
half4 _Color;
half4 _SolidColor;
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
float2 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
half4 u_xlat16_1;
half4 u_xlat16_2;
float u_xlat3;
bool u_xlatb3;
half3 u_xlat16_5;
float u_xlat7;
half u_xlat16_9;

    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat0.y * u_xlat0.x;
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_5.x = u_xlat16_0.x + _TextureSampleAdd.w;
    u_xlat16_5.x = u_xlat16_5.x * input.color.w;
    u_xlat16_1.xy = u_xlat16_5.xx * u_xlat16_1.xx + float2(-0.00100000005, -0.100000001);
    u_xlatb0.xy = (u_xlat16_1.xyxx < float4(0.0, 0.0, 0.0, 0.0)).xy;
    if(u_xlatb0.x){clip(-1);}
    if(u_xlatb0.y){clip(-1);}
    u_xlatb0.x = _SolidColor.y>=_SolidColor.z;
    u_xlat16_1.xy = _SolidColor.yz;
    u_xlat16_1.z = float(0.0);
    u_xlat16_1.w = float(-0.333333343);
    u_xlat16_2.xy = _SolidColor.zy;
    u_xlat16_2.z = float(-1.0);
    u_xlat16_2.w = float(0.666666687);
    u_xlat16_1 = u_xlat16_1 + (-u_xlat16_2);
    u_xlat16_0 = (u_xlatb0.x) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, -0.0);
    u_xlat16_0 = u_xlat16_0.xywz + u_xlat16_2.xywz;
    u_xlat16_1.z = u_xlat16_0.w;
    u_xlatb3 = _SolidColor.x>=u_xlat16_0.x;
    u_xlat16_0.w = _SolidColor.x;
    u_xlat16_1.xyw = u_xlat16_0.wyx;
    u_xlat16_1 = (-u_xlat16_0) + u_xlat16_1;
    u_xlat16_1 = (bool(u_xlatb3)) ? u_xlat16_1 : float4(0.0, 0.0, 0.0, 0.0);
    u_xlat16_0 = u_xlat16_0 + u_xlat16_1;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_5.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_9 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_5.x = u_xlat16_9 / u_xlat16_5.x;
    u_xlat16_5.x = u_xlat16_0.z + u_xlat16_5.x;
    u_xlat3 = abs(u_xlat16_5.x) + _Hue;
    u_xlat16_5.xyz = float3(u_xlat3,u_xlat3,u_xlat3) + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_5.xyz = frac(u_xlat16_5.xyz);
    u_xlat16_5.xyz = u_xlat16_5.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_5.xyz = abs(u_xlat16_5.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_5.xyz = clamp(u_xlat16_5.xyz, 0.0, 1.0);
    u_xlat16_5.xyz = u_xlat16_5.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat3 = u_xlat16_0.x * _Value;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_2.x;
    u_xlat7 = u_xlat16_1.x * _Saturation;
    u_xlat16_1.xyz = float3(u_xlat7,u_xlat7,u_xlat7) * u_xlat16_5.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * float3(u_xlat3,u_xlat3,u_xlat3);
    result.w = _SolidColor.w;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
