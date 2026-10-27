Shader "Zjcs/Official/World/Map13/Entity/SpineSkeletonGraphicDye_a2f0973c" {
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
[MaterialToggle] _ZWrite ("ZWrite", Float) = 0.0
_DyeTex ("Dye Texture", 2D) = "black" {}
_DyeHsv1 ("Dye Hsv1", Vector) = (0.0,0.0,0.0,1.0)
_DyeHsv2 ("Dye Hsv2", Vector) = (0.0,0.0,0.0,1.0)
_DyeHsv3 ("Dye Hsv3", Vector) = (0.0,0.0,0.0,1.0)
_SolidColor ("Unlit Solid Color", Color) = (0.6600000262260437,0.6600000262260437,0.6600000262260437,1.0)
_ClipLineX ("ClipLineX", Vector) = (0.0,0.0,0.0,1.0)
_ClipLineY ("ClipLineY", Vector) = (0.0,0.0,0.0,1.0)
_ClipLineC ("ClipLineC", Vector) = (0.0,0.0,0.0,1.0)
_ClipDiag ("ClipDiag", Vector) = (0.0,0.0,0.0,0.0)
[Toggle] _EnableHairClip ("EnableHairClip", Float) = 0.0
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
#pragma multi_compile_local __ UNITY_UI_CLIP_RECT
#pragma multi_compile_local __ _SOLID_COLOR
#include "UnityCG.cginc"
#if !defined(UNITY_UI_CLIP_RECT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float3 _DyeHsv1;
float3 _DyeHsv2;
float3 _DyeHsv3;
float4 _ClipDiag;
float4 _ClipLineC;
float4 _ClipLineX;
float4 _ClipLineY;
float4 _ClipRect;
half _EnableHairClip;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _DyeTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
half3 u_xlat16_3;
half3 u_xlat16_4;
float2 u_xlat10;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat1 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat0.xy = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).yx * input.vertex.ww + u_xlat0.yx;
    u_xlat2 = u_xlat1.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat2 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat1.xxxx + u_xlat2;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat1.zzzz + u_xlat2;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat1.wwww + u_xlat2;
    output.position = u_xlat1;
    u_xlat16_3.xyz = input.color.www * input.color.xyz;
    u_xlat16_4.xyz = _Color.www * _Color.xyz;
    u_xlat2.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat16_3.x = _Color.w;
    u_xlat2.w = u_xlat16_3.x * input.color.w;
    output.color = u_xlat2;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat10.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat10.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat10.xy;
    u_xlat10.xy = u_xlat1.ww / abs(u_xlat10.xy);
    u_xlat10.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat10.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat10.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    output.mask.xy = (-u_xlat1.zw) + u_xlat10.xy;
    u_xlat1 = u_xlat0.xxxx * _ClipLineY + _ClipLineC;
    output.uv3 = u_xlat0.yyyy * _ClipLineX + u_xlat1;
    u_xlat0.x = u_xlat0.x * _ClipDiag.y + _ClipDiag.z;
    output.uv4.x = u_xlat0.y * _ClipDiag.x + u_xlat0.x;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half4 u_xlat16_1;
half4 u_xlat16_2;
half4 u_xlat16_3;
bool u_xlatb3;
float4 u_xlat4;
bool3 u_xlatb4;
float3 u_xlat5;
float u_xlat6;
bool u_xlatb6;
half3 u_xlat16_7;
float u_xlat10;
half u_xlat16_13;

    u_xlat0 = max(input.uv3.y, input.uv3.x);
    u_xlat0 = max(u_xlat0, (-input.uv4.x));
    u_xlat6 = max(input.uv3.z, input.uv4.x);
    u_xlat6 = max(u_xlat6, input.uv3.w);
    u_xlat0 = min(u_xlat6, u_xlat0);
    u_xlatb0 = u_xlat0<0.0;
    u_xlatb6 = 0.0<_EnableHairClip;
    u_xlatb0 = u_xlatb6 && u_xlatb0;
    if(u_xlatb0){clip(-1);}
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
    u_xlatb4.x = u_xlat16_2.y>=u_xlat16_0.z;
    u_xlat16_1.x = (u_xlatb4.x) ? 1.0 : 0.0;
    u_xlat16_1 = u_xlat16_1.xxxx * u_xlat16_3.xywz + u_xlat16_2.xywz;
    u_xlatb4.x = u_xlat16_0.x>=u_xlat16_1.x;
    u_xlat16_2.x = (u_xlatb4.x) ? 1.0 : 0.0;
    u_xlat16_3.z = u_xlat16_1.w;
    u_xlat16_1.w = u_xlat16_0.x;
    result.w = u_xlat16_0.w;
    u_xlat16_3.xyw = u_xlat16_1.wyx;
    u_xlat16_0 = (-u_xlat16_1) + u_xlat16_3;
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_0 + u_xlat16_1;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_7.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_13 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_7.x = u_xlat16_13 / u_xlat16_7.x;
    u_xlat16_7.x = u_xlat16_0.z + u_xlat16_7.x;
    u_xlat4.xyz = tex2D(_DyeTex, input.uv.xy).xyz;
    u_xlatb4.xyz = (u_xlat4.xyzx >= float4(0.100000001, 0.100000001, 0.100000001, 0.0)).xyz;
    u_xlat4.x = u_xlatb4.x ? float(1.0) : 0.0;
    u_xlat4.y = u_xlatb4.y ? float(1.0) : 0.0;
    u_xlat4.z = u_xlatb4.z ? float(1.0) : 0.0;
;
    u_xlat5.xyz = u_xlat4.yyy * _DyeHsv2.xyz;
    u_xlat4.xyw = u_xlat4.xxx * _DyeHsv1.xyz + u_xlat5.xyz;
    u_xlat4.xyz = u_xlat4.zzz * _DyeHsv3.xyz + u_xlat4.xyw;
    u_xlat4.x = abs(u_xlat16_7.x) + u_xlat4.x;
    u_xlat16_7.xyz = u_xlat4.xxx + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_7.xyz = frac(u_xlat16_7.xyz);
    u_xlat16_7.xyz = u_xlat16_7.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_7.xyz = abs(u_xlat16_7.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_7.xyz = clamp(u_xlat16_7.xyz, 0.0, 1.0);
    u_xlat16_7.xyz = u_xlat16_7.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat4.x = u_xlat16_0.x * u_xlat4.z + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_2.x;
    u_xlat10 = u_xlat16_1.x * u_xlat4.y + u_xlat16_1.x;
    u_xlat16_1.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat16_7.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * u_xlat4.xxx;
    return result;

}
#elif !defined(UNITY_UI_CLIP_RECT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipDiag;
float4 _ClipLineC;
float4 _ClipLineX;
float4 _ClipLineY;
float4 _ClipRect;
half _EnableHairClip;
half4 _Color;
half4 _SolidColor;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
half3 u_xlat16_3;
half3 u_xlat16_4;
float2 u_xlat10;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat1 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat0.xy = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).yx * input.vertex.ww + u_xlat0.yx;
    u_xlat2 = u_xlat1.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat2 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat1.xxxx + u_xlat2;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat1.zzzz + u_xlat2;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat1.wwww + u_xlat2;
    output.position = u_xlat1;
    u_xlat16_3.xyz = input.color.www * input.color.xyz;
    u_xlat16_4.xyz = _Color.www * _Color.xyz;
    u_xlat2.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat16_3.x = _Color.w;
    u_xlat2.w = u_xlat16_3.x * input.color.w;
    output.color = u_xlat2;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat10.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat10.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat10.xy;
    u_xlat10.xy = u_xlat1.ww / abs(u_xlat10.xy);
    u_xlat10.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat10.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat10.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    output.mask.xy = (-u_xlat1.zw) + u_xlat10.xy;
    u_xlat1 = u_xlat0.xxxx * _ClipLineY + _ClipLineC;
    output.uv3 = u_xlat0.yyyy * _ClipLineX + u_xlat1;
    u_xlat0.x = u_xlat0.x * _ClipDiag.y + _ClipDiag.z;
    output.uv4.x = u_xlat0.y * _ClipDiag.x + u_xlat0.x;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half u_xlat16_0;
bool2 u_xlatb0;
half2 u_xlat16_1;
float u_xlat2;
bool u_xlatb2;

    u_xlat0 = max(input.uv3.y, input.uv3.x);
    u_xlat0 = max(u_xlat0, (-input.uv4.x));
    u_xlat2 = max(input.uv3.z, input.uv4.x);
    u_xlat2 = max(u_xlat2, input.uv3.w);
    u_xlat0 = min(u_xlat2, u_xlat0);
    u_xlatb0.x = u_xlat0<0.0;
    u_xlatb2 = 0.0<_EnableHairClip;
    u_xlatb0.x = u_xlatb2 && u_xlatb0.x;
    if(u_xlatb0.x){clip(-1);}
    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_1.x = u_xlat16_0 + _TextureSampleAdd.w;
    u_xlat16_1.xy = u_xlat16_1.xx * input.color.ww + float2(-0.00100000005, -0.100000001);
    u_xlatb0.xy = (u_xlat16_1.xyxx < float4(0.0, 0.0, 0.0, 0.0)).xy;
    if(u_xlatb0.x){clip(-1);}
    if(u_xlatb0.y){clip(-1);}
    result = _SolidColor;
    return result;

}
#elif defined(UNITY_UI_CLIP_RECT) && !defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float3 _DyeHsv1;
float3 _DyeHsv2;
float3 _DyeHsv3;
float4 _ClipDiag;
float4 _ClipLineC;
float4 _ClipLineX;
float4 _ClipLineY;
float4 _ClipRect;
half _EnableHairClip;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _DyeTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
half3 u_xlat16_3;
half3 u_xlat16_4;
float2 u_xlat10;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat1 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat0.xy = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).yx * input.vertex.ww + u_xlat0.yx;
    u_xlat2 = u_xlat1.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat2 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat1.xxxx + u_xlat2;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat1.zzzz + u_xlat2;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat1.wwww + u_xlat2;
    output.position = u_xlat1;
    u_xlat16_3.xyz = input.color.www * input.color.xyz;
    u_xlat16_4.xyz = _Color.www * _Color.xyz;
    u_xlat2.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat16_3.x = _Color.w;
    u_xlat2.w = u_xlat16_3.x * input.color.w;
    output.color = u_xlat2;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat10.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat10.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat10.xy;
    u_xlat10.xy = u_xlat1.ww / abs(u_xlat10.xy);
    u_xlat10.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat10.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat10.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    output.mask.xy = (-u_xlat1.zw) + u_xlat10.xy;
    u_xlat1 = u_xlat0.xxxx * _ClipLineY + _ClipLineC;
    output.uv3 = u_xlat0.yyyy * _ClipLineX + u_xlat1;
    u_xlat0.x = u_xlat0.x * _ClipDiag.y + _ClipDiag.z;
    output.uv4.x = u_xlat0.y * _ClipDiag.x + u_xlat0.x;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half4 u_xlat16_1;
float2 u_xlat2;
half4 u_xlat16_2;
bool u_xlatb2;
half4 u_xlat16_3;
float4 u_xlat4;
bool3 u_xlatb4;
float3 u_xlat5;
float u_xlat6;
bool u_xlatb6;
half3 u_xlat16_7;
float u_xlat10;
half u_xlat16_13;

    u_xlat0 = max(input.uv3.y, input.uv3.x);
    u_xlat0 = max(u_xlat0, (-input.uv4.x));
    u_xlat6 = max(input.uv3.z, input.uv4.x);
    u_xlat6 = max(u_xlat6, input.uv3.w);
    u_xlat0 = min(u_xlat6, u_xlat0);
    u_xlatb0 = u_xlat0<0.0;
    u_xlatb6 = 0.0<_EnableHairClip;
    u_xlatb0 = u_xlatb6 && u_xlatb0;
    if(u_xlatb0){clip(-1);}
    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_0.www + _TextureSampleAdd.xyz;
    u_xlat16_1.w = u_xlat16_0.w + _TextureSampleAdd.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat2.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat2.xy = u_xlat2.xy + -abs(input.mask.xy);
    u_xlat2.xy = u_xlat2.xy * input.mask.zw;
    u_xlat2.xy = clamp(u_xlat2.xy, 0.0, 1.0);
    u_xlat16_1.x = u_xlat2.y * u_xlat2.x;
    u_xlat16_7.x = u_xlat16_0.w * u_xlat16_1.x + -0.00100000005;
    u_xlatb2 = u_xlat16_7.x<0.0;
    if(u_xlatb2){clip(-1);}
    u_xlat16_2 = u_xlat16_0 * u_xlat16_1.xxxx;
    u_xlat16_3.xy = u_xlat16_2.zy;
    u_xlat16_0.xy = u_xlat16_0.yz * u_xlat16_1.xx + (-u_xlat16_3.xy);
    u_xlatb4.x = u_xlat16_3.y>=u_xlat16_2.z;
    u_xlat16_1.x = (u_xlatb4.x) ? 1.0 : 0.0;
    u_xlat16_3.z = float(-1.0);
    u_xlat16_3.w = float(0.666666687);
    u_xlat16_0.z = float(1.0);
    u_xlat16_0.w = float(-1.0);
    u_xlat16_0 = u_xlat16_1.xxxx * u_xlat16_0.xywz + u_xlat16_3.xywz;
    u_xlatb4.x = u_xlat16_2.x>=u_xlat16_0.x;
    u_xlat16_1.x = (u_xlatb4.x) ? 1.0 : 0.0;
    u_xlat16_3.z = u_xlat16_0.w;
    u_xlat16_0.w = u_xlat16_2.x;
    result.w = u_xlat16_2.w;
    u_xlat16_3.xyw = u_xlat16_0.wyx;
    u_xlat16_2 = (-u_xlat16_0) + u_xlat16_3;
    u_xlat16_0 = u_xlat16_1.xxxx * u_xlat16_2 + u_xlat16_0;
    u_xlat16_1.x = min(u_xlat16_0.y, u_xlat16_0.w);
    u_xlat16_1.x = u_xlat16_0.x + (-u_xlat16_1.x);
    u_xlat16_7.x = u_xlat16_1.x * 6.0 + 9.99999975e-05;
    u_xlat16_13 = (-u_xlat16_0.y) + u_xlat16_0.w;
    u_xlat16_7.x = u_xlat16_13 / u_xlat16_7.x;
    u_xlat16_7.x = u_xlat16_0.z + u_xlat16_7.x;
    u_xlat4.xyz = tex2D(_DyeTex, input.uv.xy).xyz;
    u_xlatb4.xyz = (u_xlat4.xyzx >= float4(0.100000001, 0.100000001, 0.100000001, 0.0)).xyz;
    u_xlat4.x = u_xlatb4.x ? float(1.0) : 0.0;
    u_xlat4.y = u_xlatb4.y ? float(1.0) : 0.0;
    u_xlat4.z = u_xlatb4.z ? float(1.0) : 0.0;
;
    u_xlat5.xyz = u_xlat4.yyy * _DyeHsv2.xyz;
    u_xlat4.xyw = u_xlat4.xxx * _DyeHsv1.xyz + u_xlat5.xyz;
    u_xlat4.xyz = u_xlat4.zzz * _DyeHsv3.xyz + u_xlat4.xyw;
    u_xlat4.x = abs(u_xlat16_7.x) + u_xlat4.x;
    u_xlat16_7.xyz = u_xlat4.xxx + float3(1.0, 0.666666687, 0.333333343);
    u_xlat16_7.xyz = frac(u_xlat16_7.xyz);
    u_xlat16_7.xyz = u_xlat16_7.xyz * float3(6.0, 6.0, 6.0) + float3(-3.0, -3.0, -3.0);
    u_xlat16_7.xyz = abs(u_xlat16_7.xyz) + float3(-1.0, -1.0, -1.0);
    u_xlat16_7.xyz = clamp(u_xlat16_7.xyz, 0.0, 1.0);
    u_xlat16_7.xyz = u_xlat16_7.xyz + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.x = u_xlat16_0.x + 9.99999975e-05;
    u_xlat4.x = u_xlat16_0.x * u_xlat4.z + u_xlat16_0.x;
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_3.x;
    u_xlat10 = u_xlat16_1.x * u_xlat4.y + u_xlat16_1.x;
    u_xlat16_1.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat16_7.xyz + float3(1.0, 1.0, 1.0);
    result.xyz = u_xlat16_1.xyz * u_xlat4.xxx;
    return result;

}
#elif defined(UNITY_UI_CLIP_RECT) && defined(_SOLID_COLOR)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipDiag;
float4 _ClipLineC;
float4 _ClipLineX;
float4 _ClipLineY;
float4 _ClipRect;
half _EnableHairClip;
half4 _Color;
half4 _SolidColor;
half4 _TextureSampleAdd;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
half3 u_xlat16_3;
half3 u_xlat16_4;
float2 u_xlat10;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat1 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat0.xy = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).yx * input.vertex.ww + u_xlat0.yx;
    u_xlat2 = u_xlat1.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat2 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat1.xxxx + u_xlat2;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat1.zzzz + u_xlat2;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat1.wwww + u_xlat2;
    output.position = u_xlat1;
    u_xlat16_3.xyz = input.color.www * input.color.xyz;
    u_xlat16_4.xyz = _Color.www * _Color.xyz;
    u_xlat2.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat16_3.x = _Color.w;
    u_xlat2.w = u_xlat16_3.x * input.color.w;
    output.color = u_xlat2;
    output.uv.xy = input.uv.xy;
    output.world = input.vertex;
    u_xlat10.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat10.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat10.xy;
    u_xlat10.xy = u_xlat1.ww / abs(u_xlat10.xy);
    u_xlat10.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat10.xy);
    output.mask.zw = float2(0.25, 0.25) / u_xlat10.xy;
    u_xlat1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat1 = min(u_xlat1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat1.xy);
    output.mask.xy = (-u_xlat1.zw) + u_xlat10.xy;
    u_xlat1 = u_xlat0.xxxx * _ClipLineY + _ClipLineC;
    output.uv3 = u_xlat0.yyyy * _ClipLineX + u_xlat1;
    u_xlat0.x = u_xlat0.x * _ClipDiag.y + _ClipDiag.z;
    output.uv4.x = u_xlat0.y * _ClipDiag.x + u_xlat0.x;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half u_xlat16_0;
bool2 u_xlatb0;
half2 u_xlat16_1;
float u_xlat2;
bool u_xlatb2;
half u_xlat16_3;

    u_xlat0.x = max(input.uv3.y, input.uv3.x);
    u_xlat0.x = max(u_xlat0.x, (-input.uv4.x));
    u_xlat2 = max(input.uv3.z, input.uv4.x);
    u_xlat2 = max(u_xlat2, input.uv3.w);
    u_xlat0.x = min(u_xlat2, u_xlat0.x);
    u_xlatb0.x = u_xlat0.x<0.0;
    u_xlatb2 = 0.0<_EnableHairClip;
    u_xlatb0.x = u_xlatb2 && u_xlatb0.x;
    if(u_xlatb0.x){clip(-1);}
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
#endif
ENDCG
}
}
Fallback Off
}
