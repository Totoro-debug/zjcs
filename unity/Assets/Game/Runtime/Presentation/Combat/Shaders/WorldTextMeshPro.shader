Shader "Zjcs/Combat/WorldTextMeshPro" {
Properties {
_FaceColor ("_FaceColor", Color) = (1.0,1.0,1.0,1.0)
_FaceDilate ("_FaceDilate", Float) = 0.0
_OutlineColor ("_OutlineColor", Color) = (0.0,0.0,0.0,1.0)
_OutlineWidth ("_OutlineWidth", Float) = 0.0
_OutlineSoftness ("_OutlineSoftness", Float) = 0.0
_OutlineColor2 ("_OutlineColor2", Color) = (0.0,0.0,0.0,1.0)
_OutlineWidth2 ("_OutlineWidth2", Float) = 0.0
_UnderlayColor ("_UnderlayColor", Color) = (0.0,0.0,0.0,0.5)
_UnderlayOffsetX ("_UnderlayOffsetX", Float) = 0.0
_UnderlayOffsetY ("_UnderlayOffsetY", Float) = 0.0
_UnderlayDilate ("_UnderlayDilate", Float) = 0.0
_UnderlaySoftness ("_UnderlaySoftness", Float) = 0.0
_WeightNormal ("_WeightNormal", Float) = 0.0
_WeightBold ("_WeightBold", Float) = 0.5
_ShaderFlags ("_ShaderFlags", Float) = 0.0
_ScaleRatioA ("_ScaleRatioA", Float) = 1.0
_ScaleRatioB ("_ScaleRatioB", Float) = 1.0
_ScaleRatioC ("_ScaleRatioC", Float) = 1.0
_MainTex ("_MainTex", 2D) = "white" {}
_TextureWidth ("_TextureWidth", Float) = 512.0
_TextureHeight ("_TextureHeight", Float) = 512.0
_GradientScale ("_GradientScale", Float) = 5.0
_ScaleX ("_ScaleX", Float) = 1.0
_ScaleY ("_ScaleY", Float) = 1.0
_PerspectiveFilter ("_PerspectiveFilter", Float) = 0.875
_Sharpness ("_Sharpness", Float) = 0.0
_VertexOffsetX ("_VertexOffsetX", Float) = 0.0
_VertexOffsetY ("_VertexOffsetY", Float) = 0.0
_ClipRect ("_ClipRect", Vector) = (-32767.0,-32767.0,32767.0,32767.0)
_MaskSoftnessX ("_MaskSoftnessX", Float) = 0.0
_MaskSoftnessY ("_MaskSoftnessY", Float) = 0.0
_StencilComp ("_StencilComp", Float) = 8.0
_Stencil ("_Stencil", Float) = 0.0
_StencilOp ("_StencilOp", Float) = 0.0
_StencilWriteMask ("_StencilWriteMask", Float) = 255.0
_StencilReadMask ("_StencilReadMask", Float) = 255.0
_CullMode ("_CullMode", Float) = 0.0
_ColorMask ("_ColorMask", Float) = 15.0
_GlowColor ("_GlowColor", Color) = (0.0,1.0,0.0,0.5)
_GlowOffset ("_GlowOffset", Float) = 0.0
_GlowInner ("_GlowInner", Float) = 0.05000000074505806
_GlowOuter ("_GlowOuter", Float) = 0.05000000074505806
_GlowPower ("_GlowPower", Float) = 0.75
_SrcAlphaMode ("_SrcAlphaMode", Float) = 1.0
}
SubShader {
Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
Cull [_CullMode]
ZWrite Off
ZTest [unity_GUIZTestMode]
Blend One OneMinusSrcAlpha, [_SrcAlphaMode] OneMinusSrcAlpha
ColorMask [_ColorMask]
Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
Pass {
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _ OUTLINE_ON
#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
struct Input { float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float3 normal : NORMAL0; float4 vertex : POSITION0; half4 color : COLOR0; };
struct Varying { float4 position : SV_POSITION; float4 uv : TEXCOORD0; half4 color : COLOR0; half4 color1 : COLOR1; half4 world : TEXCOORD1; half4 mask : TEXCOORD2; };
float _FaceDilate;
float _GradientScale;
float _OutlineSoftness;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
half4 _FaceColor;
half4 _OutlineColor;
sampler2D _MainTex;
#if defined(OUTLINE_ON) && defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float u_xlat5;
float u_xlat7;
float u_xlat10;
float u_xlat12;
float u_xlat15;
bool u_xlatb15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    u_xlat16_3.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color = u_xlat16_3;
    u_xlat10 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat1.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat1.xyz;
    u_xlat2.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat2.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat2.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat10 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat2.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat2.xyz;
    u_xlat10 = dot(u_xlat2.xyz, u_xlat1.xyz);
    u_xlat1.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat1.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat1.xy;
    u_xlat1.xy = abs(u_xlat1.xy) * float2(_ScaleX, _ScaleY);
    u_xlat1.xy = u_xlat2.ww / u_xlat1.xy;
    u_xlat15 = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat1.xy);
    u_xlat1.zw = float2(0.25, 0.25) / u_xlat1.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat2.x = abs(input.uv1.y) * _GradientScale;
    u_xlat7 = _Sharpness + 1.0;
    u_xlat2.x = u_xlat7 * u_xlat2.x;
    u_xlat7 = u_xlat15 * u_xlat2.x;
    u_xlat12 = (-_PerspectiveFilter) + 1.0;
    u_xlat12 = u_xlat12 * abs(u_xlat7);
    u_xlat15 = u_xlat15 * u_xlat2.x + (-u_xlat12);
    u_xlat10 = abs(u_xlat10) * u_xlat15 + u_xlat12;
    u_xlatb15 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat10 = (u_xlatb15) ? u_xlat10 : u_xlat7;
    u_xlat15 = _OutlineSoftness * _ScaleRatioA;
    u_xlat15 = u_xlat15 * u_xlat10 + 1.0;
    u_xlat2.x = u_xlat10 / u_xlat15;
    u_xlat10 = _OutlineWidth * _ScaleRatioA;
    u_xlat10 = u_xlat2.x * u_xlat10;
    u_xlat15 = min(u_xlat10, 1.0);
    u_xlat15 = sqrt(u_xlat15);
    u_xlat4.x = input.color.w * _OutlineColor.w;
    u_xlat4.xyz = _OutlineColor.xyz * u_xlat4.xxx + (-u_xlat16_3.xyz);
    u_xlat4.w = _OutlineColor.w * input.color.w + (-u_xlat16_3.w);
    u_xlat3 = float4(u_xlat15,u_xlat15,u_xlat15,u_xlat15) * u_xlat4 + u_xlat16_3;
    output.color1 = u_xlat3;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat4.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat1.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat4.xy / u_xlat0.xy;
    output.mask = u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = 0.0>=input.uv1.y;
    u_xlat0.x = u_xlatb0 ? 1.0 : float(0.0);
    u_xlat5 = (-_WeightNormal) + _WeightBold;
    u_xlat0.x = u_xlat0.x * u_xlat5 + _WeightNormal;
    u_xlat0.x = u_xlat0.x * 0.25 + _FaceDilate;
    u_xlat0.x = u_xlat0.x * _ScaleRatioA;
    u_xlat0.x = (-u_xlat0.x) * 0.5 + 0.5;
    u_xlat2.zw = u_xlat0.xx * u_xlat2.xx + float2(-0.5, -0.5);
    u_xlat2.y = (-u_xlat10) * 0.5 + u_xlat2.w;
    output.world = u_xlat2;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half u_xlat16_1;
bool u_xlatb1;
half2 u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0 = input.color + (-input.color1);
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_2.xy = float2(u_xlat16_1,u_xlat16_1) * input.world.xx + (-input.world.zy);
    u_xlat16_2.xy = clamp(u_xlat16_2.xy, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_0 + input.color1;
    u_xlat16_0 = u_xlat16_2.yyyy * u_xlat16_0;
    u_xlat1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat1.xy = u_xlat1.xy + -abs(input.mask.xy);
    u_xlat1.xy = u_xlat1.xy * input.mask.zw;
    u_xlat1.xy = clamp(u_xlat1.xy, 0.0, 1.0);
    u_xlat16_2.x = u_xlat1.y * u_xlat1.x;
    u_xlat16_5 = u_xlat16_0.w * u_xlat16_2.x + -0.00100000005;
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    result = u_xlat16_0;
    u_xlatb1 = u_xlat16_5<0.0;
    if(u_xlatb1){clip(-1);}
    return result;

}
#elif !defined(OUTLINE_ON) && defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool u_xlatb1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float2 u_xlat5;
bool u_xlatb5;
float u_xlat6;
float2 u_xlat10;
float u_xlat15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    output.color.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color.w = u_xlat16_3.w;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat4.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat10.xy / u_xlat0.xy;
    output.uv.xy = input.uv.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat0.xyz);
    u_xlat5.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat5.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat5.xy;
    u_xlat5.xy = abs(u_xlat5.xy) * float2(_ScaleX, _ScaleY);
    u_xlat5.xy = u_xlat2.ww / u_xlat5.xy;
    u_xlat15 = dot(u_xlat5.xy, u_xlat5.xy);
    u_xlat5.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat5.xy);
    u_xlat4.zw = float2(0.25, 0.25) / u_xlat5.xy;
    output.mask = u_xlat4;
    u_xlat5.x = rsqrt(u_xlat15);
    u_xlat10.x = abs(input.uv1.y) * _GradientScale;
    u_xlat15 = _Sharpness + 1.0;
    u_xlat10.x = u_xlat15 * u_xlat10.x;
    u_xlat15 = u_xlat10.x * u_xlat5.x;
    u_xlat1.x = (-_PerspectiveFilter) + 1.0;
    u_xlat1.x = abs(u_xlat15) * u_xlat1.x;
    u_xlat5.x = u_xlat5.x * u_xlat10.x + (-u_xlat1.x);
    u_xlat0.x = abs(u_xlat0.x) * u_xlat5.x + u_xlat1.x;
    u_xlatb5 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat0.x = (u_xlatb5) ? u_xlat0.x : u_xlat15;
    u_xlat5.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat5.x = u_xlat5.x * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat0.x / u_xlat5.x;
    u_xlatb1 = 0.0>=input.uv1.y;
    u_xlat1.x = u_xlatb1 ? 1.0 : float(0.0);
    u_xlat6 = (-_WeightNormal) + _WeightBold;
    u_xlat1.x = u_xlat1.x * u_xlat6 + _WeightNormal;
    u_xlat1.x = u_xlat1.x * 0.25 + _FaceDilate;
    u_xlat1.x = u_xlat1.x * _ScaleRatioA;
    u_xlat1.x = (-u_xlat1.x) * 0.5 + 0.5;
    u_xlat0.yzw = u_xlat1.xxx * u_xlat0.xxx + float3(-0.5, -0.5, -0.5);
    output.world = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
bool u_xlatb2;
half u_xlat16_4;

    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat0.y * u_xlat0.x;
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_4 = u_xlat16_0.x * input.world.x + (-input.world.z);
    u_xlat16_4 = clamp(u_xlat16_4, 0.0, 1.0);
    u_xlat16_0 = float4(u_xlat16_4,u_xlat16_4,u_xlat16_4,u_xlat16_4) * input.color;
    u_xlat16_4 = u_xlat16_0.w * u_xlat16_1 + -0.00100000005;
    u_xlat16_0 = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * u_xlat16_0;
    result = u_xlat16_0;
    u_xlatb2 = u_xlat16_4<0.0;
    if(u_xlatb2){clip(-1);}
    return result;

}
#elif defined(OUTLINE_ON) && defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float u_xlat5;
float u_xlat7;
float u_xlat10;
float u_xlat12;
float u_xlat15;
bool u_xlatb15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    u_xlat16_3.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color = u_xlat16_3;
    u_xlat10 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat1.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat1.xyz;
    u_xlat2.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat2.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat2.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat10 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat2.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat2.xyz;
    u_xlat10 = dot(u_xlat2.xyz, u_xlat1.xyz);
    u_xlat1.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat1.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat1.xy;
    u_xlat1.xy = abs(u_xlat1.xy) * float2(_ScaleX, _ScaleY);
    u_xlat1.xy = u_xlat2.ww / u_xlat1.xy;
    u_xlat15 = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat1.xy);
    u_xlat1.zw = float2(0.25, 0.25) / u_xlat1.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat2.x = abs(input.uv1.y) * _GradientScale;
    u_xlat7 = _Sharpness + 1.0;
    u_xlat2.x = u_xlat7 * u_xlat2.x;
    u_xlat7 = u_xlat15 * u_xlat2.x;
    u_xlat12 = (-_PerspectiveFilter) + 1.0;
    u_xlat12 = u_xlat12 * abs(u_xlat7);
    u_xlat15 = u_xlat15 * u_xlat2.x + (-u_xlat12);
    u_xlat10 = abs(u_xlat10) * u_xlat15 + u_xlat12;
    u_xlatb15 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat10 = (u_xlatb15) ? u_xlat10 : u_xlat7;
    u_xlat15 = _OutlineSoftness * _ScaleRatioA;
    u_xlat15 = u_xlat15 * u_xlat10 + 1.0;
    u_xlat2.x = u_xlat10 / u_xlat15;
    u_xlat10 = _OutlineWidth * _ScaleRatioA;
    u_xlat10 = u_xlat2.x * u_xlat10;
    u_xlat15 = min(u_xlat10, 1.0);
    u_xlat15 = sqrt(u_xlat15);
    u_xlat4.x = input.color.w * _OutlineColor.w;
    u_xlat4.xyz = _OutlineColor.xyz * u_xlat4.xxx + (-u_xlat16_3.xyz);
    u_xlat4.w = _OutlineColor.w * input.color.w + (-u_xlat16_3.w);
    u_xlat3 = float4(u_xlat15,u_xlat15,u_xlat15,u_xlat15) * u_xlat4 + u_xlat16_3;
    output.color1 = u_xlat3;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat4.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat1.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat4.xy / u_xlat0.xy;
    output.mask = u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = 0.0>=input.uv1.y;
    u_xlat0.x = u_xlatb0 ? 1.0 : float(0.0);
    u_xlat5 = (-_WeightNormal) + _WeightBold;
    u_xlat0.x = u_xlat0.x * u_xlat5 + _WeightNormal;
    u_xlat0.x = u_xlat0.x * 0.25 + _FaceDilate;
    u_xlat0.x = u_xlat0.x * _ScaleRatioA;
    u_xlat0.x = (-u_xlat0.x) * 0.5 + 0.5;
    u_xlat2.zw = u_xlat0.xx * u_xlat2.xx + float2(-0.5, -0.5);
    u_xlat2.y = (-u_xlat10) * 0.5 + u_xlat2.w;
    output.world = u_xlat2;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half u_xlat16_1;
bool u_xlatb1;
half2 u_xlat16_2;

    u_xlat16_0 = input.color + (-input.color1);
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_2.xy = float2(u_xlat16_1,u_xlat16_1) * input.world.xx + (-input.world.zy);
    u_xlat16_2.xy = clamp(u_xlat16_2.xy, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_0 + input.color1;
    u_xlat16_2.x = u_xlat16_0.w * u_xlat16_2.y + -0.00100000005;
    u_xlat16_0 = u_xlat16_2.yyyy * u_xlat16_0;
    result = u_xlat16_0;
    u_xlatb1 = u_xlat16_2.x<0.0;
    if(u_xlatb1){clip(-1);}
    return result;

}
#elif defined(OUTLINE_ON) && !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float u_xlat5;
float u_xlat7;
float u_xlat10;
float u_xlat12;
float u_xlat15;
bool u_xlatb15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    u_xlat16_3.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color = u_xlat16_3;
    u_xlat10 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat1.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat1.xyz;
    u_xlat2.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat2.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat2.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat10 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat2.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat2.xyz;
    u_xlat10 = dot(u_xlat2.xyz, u_xlat1.xyz);
    u_xlat1.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat1.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat1.xy;
    u_xlat1.xy = abs(u_xlat1.xy) * float2(_ScaleX, _ScaleY);
    u_xlat1.xy = u_xlat2.ww / u_xlat1.xy;
    u_xlat15 = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat1.xy);
    u_xlat1.zw = float2(0.25, 0.25) / u_xlat1.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat2.x = abs(input.uv1.y) * _GradientScale;
    u_xlat7 = _Sharpness + 1.0;
    u_xlat2.x = u_xlat7 * u_xlat2.x;
    u_xlat7 = u_xlat15 * u_xlat2.x;
    u_xlat12 = (-_PerspectiveFilter) + 1.0;
    u_xlat12 = u_xlat12 * abs(u_xlat7);
    u_xlat15 = u_xlat15 * u_xlat2.x + (-u_xlat12);
    u_xlat10 = abs(u_xlat10) * u_xlat15 + u_xlat12;
    u_xlatb15 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat10 = (u_xlatb15) ? u_xlat10 : u_xlat7;
    u_xlat15 = _OutlineSoftness * _ScaleRatioA;
    u_xlat15 = u_xlat15 * u_xlat10 + 1.0;
    u_xlat2.x = u_xlat10 / u_xlat15;
    u_xlat10 = _OutlineWidth * _ScaleRatioA;
    u_xlat10 = u_xlat2.x * u_xlat10;
    u_xlat15 = min(u_xlat10, 1.0);
    u_xlat15 = sqrt(u_xlat15);
    u_xlat4.x = input.color.w * _OutlineColor.w;
    u_xlat4.xyz = _OutlineColor.xyz * u_xlat4.xxx + (-u_xlat16_3.xyz);
    u_xlat4.w = _OutlineColor.w * input.color.w + (-u_xlat16_3.w);
    u_xlat3 = float4(u_xlat15,u_xlat15,u_xlat15,u_xlat15) * u_xlat4 + u_xlat16_3;
    output.color1 = u_xlat3;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat4.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat1.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat4.xy / u_xlat0.xy;
    output.mask = u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = 0.0>=input.uv1.y;
    u_xlat0.x = u_xlatb0 ? 1.0 : float(0.0);
    u_xlat5 = (-_WeightNormal) + _WeightBold;
    u_xlat0.x = u_xlat0.x * u_xlat5 + _WeightNormal;
    u_xlat0.x = u_xlat0.x * 0.25 + _FaceDilate;
    u_xlat0.x = u_xlat0.x * _ScaleRatioA;
    u_xlat0.x = (-u_xlat0.x) * 0.5 + 0.5;
    u_xlat2.zw = u_xlat0.xx * u_xlat2.xx + float2(-0.5, -0.5);
    u_xlat2.y = (-u_xlat10) * 0.5 + u_xlat2.w;
    output.world = u_xlat2;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half u_xlat16_1;
half2 u_xlat16_2;

    u_xlat16_0 = input.color + (-input.color1);
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_2.xy = float2(u_xlat16_1,u_xlat16_1) * input.world.xx + (-input.world.zy);
    u_xlat16_2.xy = clamp(u_xlat16_2.xy, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_0 + input.color1;
    u_xlat16_0 = u_xlat16_2.yyyy * u_xlat16_0;
    u_xlat1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat1.xy = u_xlat1.xy + -abs(input.mask.xy);
    u_xlat1.xy = u_xlat1.xy * input.mask.zw;
    u_xlat1.xy = clamp(u_xlat1.xy, 0.0, 1.0);
    u_xlat16_2.x = u_xlat1.y * u_xlat1.x;
    result = u_xlat16_0 * u_xlat16_2.xxxx;
    return result;

}
#elif !defined(OUTLINE_ON) && defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool u_xlatb1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float2 u_xlat5;
bool u_xlatb5;
float u_xlat6;
float2 u_xlat10;
float u_xlat15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    output.color.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color.w = u_xlat16_3.w;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat4.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat10.xy / u_xlat0.xy;
    output.uv.xy = input.uv.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat0.xyz);
    u_xlat5.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat5.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat5.xy;
    u_xlat5.xy = abs(u_xlat5.xy) * float2(_ScaleX, _ScaleY);
    u_xlat5.xy = u_xlat2.ww / u_xlat5.xy;
    u_xlat15 = dot(u_xlat5.xy, u_xlat5.xy);
    u_xlat5.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat5.xy);
    u_xlat4.zw = float2(0.25, 0.25) / u_xlat5.xy;
    output.mask = u_xlat4;
    u_xlat5.x = rsqrt(u_xlat15);
    u_xlat10.x = abs(input.uv1.y) * _GradientScale;
    u_xlat15 = _Sharpness + 1.0;
    u_xlat10.x = u_xlat15 * u_xlat10.x;
    u_xlat15 = u_xlat10.x * u_xlat5.x;
    u_xlat1.x = (-_PerspectiveFilter) + 1.0;
    u_xlat1.x = abs(u_xlat15) * u_xlat1.x;
    u_xlat5.x = u_xlat5.x * u_xlat10.x + (-u_xlat1.x);
    u_xlat0.x = abs(u_xlat0.x) * u_xlat5.x + u_xlat1.x;
    u_xlatb5 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat0.x = (u_xlatb5) ? u_xlat0.x : u_xlat15;
    u_xlat5.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat5.x = u_xlat5.x * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat0.x / u_xlat5.x;
    u_xlatb1 = 0.0>=input.uv1.y;
    u_xlat1.x = u_xlatb1 ? 1.0 : float(0.0);
    u_xlat6 = (-_WeightNormal) + _WeightBold;
    u_xlat1.x = u_xlat1.x * u_xlat6 + _WeightNormal;
    u_xlat1.x = u_xlat1.x * 0.25 + _FaceDilate;
    u_xlat1.x = u_xlat1.x * _ScaleRatioA;
    u_xlat1.x = (-u_xlat1.x) * 0.5 + 0.5;
    u_xlat0.yzw = u_xlat1.xxx * u_xlat0.xxx + float3(-0.5, -0.5, -0.5);
    output.world = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half u_xlat16_1;
bool u_xlatb2;
half u_xlat16_4;

    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_1 = u_xlat16_0.x * input.world.x + (-input.world.z);
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_4 = input.color.w * u_xlat16_1 + -0.00100000005;
    u_xlat16_0 = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * input.color;
    result = u_xlat16_0;
    u_xlatb2 = u_xlat16_4<0.0;
    if(u_xlatb2){clip(-1);}
    return result;

}
#elif !defined(OUTLINE_ON) && !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool u_xlatb1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float2 u_xlat5;
bool u_xlatb5;
float u_xlat6;
float2 u_xlat10;
float u_xlat15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    output.color.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color.w = u_xlat16_3.w;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat4.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat10.xy / u_xlat0.xy;
    output.uv.xy = input.uv.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat0.xyz);
    u_xlat5.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat5.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat5.xy;
    u_xlat5.xy = abs(u_xlat5.xy) * float2(_ScaleX, _ScaleY);
    u_xlat5.xy = u_xlat2.ww / u_xlat5.xy;
    u_xlat15 = dot(u_xlat5.xy, u_xlat5.xy);
    u_xlat5.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat5.xy);
    u_xlat4.zw = float2(0.25, 0.25) / u_xlat5.xy;
    output.mask = u_xlat4;
    u_xlat5.x = rsqrt(u_xlat15);
    u_xlat10.x = abs(input.uv1.y) * _GradientScale;
    u_xlat15 = _Sharpness + 1.0;
    u_xlat10.x = u_xlat15 * u_xlat10.x;
    u_xlat15 = u_xlat10.x * u_xlat5.x;
    u_xlat1.x = (-_PerspectiveFilter) + 1.0;
    u_xlat1.x = abs(u_xlat15) * u_xlat1.x;
    u_xlat5.x = u_xlat5.x * u_xlat10.x + (-u_xlat1.x);
    u_xlat0.x = abs(u_xlat0.x) * u_xlat5.x + u_xlat1.x;
    u_xlatb5 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat0.x = (u_xlatb5) ? u_xlat0.x : u_xlat15;
    u_xlat5.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat5.x = u_xlat5.x * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat0.x / u_xlat5.x;
    u_xlatb1 = 0.0>=input.uv1.y;
    u_xlat1.x = u_xlatb1 ? 1.0 : float(0.0);
    u_xlat6 = (-_WeightNormal) + _WeightBold;
    u_xlat1.x = u_xlat1.x * u_xlat6 + _WeightNormal;
    u_xlat1.x = u_xlat1.x * 0.25 + _FaceDilate;
    u_xlat1.x = u_xlat1.x * _ScaleRatioA;
    u_xlat1.x = (-u_xlat1.x) * 0.5 + 0.5;
    u_xlat0.yzw = u_xlat1.xxx * u_xlat0.xxx + float3(-0.5, -0.5, -0.5);
    output.world = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
half u_xlat16_3;

    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat0.y * u_xlat0.x;
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_3 = u_xlat16_0.x * input.world.x + (-input.world.z);
    u_xlat16_3 = clamp(u_xlat16_3, 0.0, 1.0);
    u_xlat16_0 = float4(u_xlat16_3,u_xlat16_3,u_xlat16_3,u_xlat16_3) * input.color;
    result = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * u_xlat16_0;
    return result;

}
#elif defined(OUTLINE_ON) && !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float u_xlat5;
float u_xlat7;
float u_xlat10;
float u_xlat12;
float u_xlat15;
bool u_xlatb15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    u_xlat16_3.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color = u_xlat16_3;
    u_xlat10 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat1.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat1.xyz;
    u_xlat2.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat2.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat2.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat10 = dot(u_xlat2.xyz, u_xlat2.xyz);
    u_xlat10 = rsqrt(u_xlat10);
    u_xlat2.xyz = float3(u_xlat10,u_xlat10,u_xlat10) * u_xlat2.xyz;
    u_xlat10 = dot(u_xlat2.xyz, u_xlat1.xyz);
    u_xlat1.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat1.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat1.xy;
    u_xlat1.xy = abs(u_xlat1.xy) * float2(_ScaleX, _ScaleY);
    u_xlat1.xy = u_xlat2.ww / u_xlat1.xy;
    u_xlat15 = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat1.xy);
    u_xlat1.zw = float2(0.25, 0.25) / u_xlat1.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat2.x = abs(input.uv1.y) * _GradientScale;
    u_xlat7 = _Sharpness + 1.0;
    u_xlat2.x = u_xlat7 * u_xlat2.x;
    u_xlat7 = u_xlat15 * u_xlat2.x;
    u_xlat12 = (-_PerspectiveFilter) + 1.0;
    u_xlat12 = u_xlat12 * abs(u_xlat7);
    u_xlat15 = u_xlat15 * u_xlat2.x + (-u_xlat12);
    u_xlat10 = abs(u_xlat10) * u_xlat15 + u_xlat12;
    u_xlatb15 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat10 = (u_xlatb15) ? u_xlat10 : u_xlat7;
    u_xlat15 = _OutlineSoftness * _ScaleRatioA;
    u_xlat15 = u_xlat15 * u_xlat10 + 1.0;
    u_xlat2.x = u_xlat10 / u_xlat15;
    u_xlat10 = _OutlineWidth * _ScaleRatioA;
    u_xlat10 = u_xlat2.x * u_xlat10;
    u_xlat15 = min(u_xlat10, 1.0);
    u_xlat15 = sqrt(u_xlat15);
    u_xlat4.x = input.color.w * _OutlineColor.w;
    u_xlat4.xyz = _OutlineColor.xyz * u_xlat4.xxx + (-u_xlat16_3.xyz);
    u_xlat4.w = _OutlineColor.w * input.color.w + (-u_xlat16_3.w);
    u_xlat3 = float4(u_xlat15,u_xlat15,u_xlat15,u_xlat15) * u_xlat4 + u_xlat16_3;
    output.color1 = u_xlat3;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat4.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat1.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat4.xy / u_xlat0.xy;
    output.mask = u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = 0.0>=input.uv1.y;
    u_xlat0.x = u_xlatb0 ? 1.0 : float(0.0);
    u_xlat5 = (-_WeightNormal) + _WeightBold;
    u_xlat0.x = u_xlat0.x * u_xlat5 + _WeightNormal;
    u_xlat0.x = u_xlat0.x * 0.25 + _FaceDilate;
    u_xlat0.x = u_xlat0.x * _ScaleRatioA;
    u_xlat0.x = (-u_xlat0.x) * 0.5 + 0.5;
    u_xlat2.zw = u_xlat0.xx * u_xlat2.xx + float2(-0.5, -0.5);
    u_xlat2.y = (-u_xlat10) * 0.5 + u_xlat2.w;
    output.world = u_xlat2;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half u_xlat16_1;
half2 u_xlat16_2;

    u_xlat16_0 = input.color + (-input.color1);
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_2.xy = float2(u_xlat16_1,u_xlat16_1) * input.world.xx + (-input.world.zy);
    u_xlat16_2.xy = clamp(u_xlat16_2.xy, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_2.xxxx * u_xlat16_0 + input.color1;
    result = u_xlat16_2.yyyy * u_xlat16_0;
    return result;

}
#elif !defined(OUTLINE_ON) && !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool u_xlatb1;
float4 u_xlat2;
float4 u_xlat3;
half4 u_xlat16_3;
float4 u_xlat4;
float2 u_xlat5;
bool u_xlatb5;
float u_xlat6;
float2 u_xlat10;
float u_xlat15;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX, _VertexOffsetY);
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat1 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat1;
    u_xlat2 = u_xlat1 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz * input.vertex.www + u_xlat1.xyz;
    u_xlat1.xyz = (-u_xlat1.xyz) + _WorldSpaceCameraPos.xyz;
    u_xlat3 = u_xlat2.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat3 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat2.xxxx + u_xlat3;
    u_xlat3 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat2.zzzz + u_xlat3;
    u_xlat2 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat2.wwww + u_xlat3;
    output.position = u_xlat2;
    u_xlat16_3 = input.color * _FaceColor;
    output.color.xyz = u_xlat16_3.www * u_xlat16_3.xyz;
    output.color.w = u_xlat16_3.w;
    u_xlat3 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat10.xy = u_xlat0.xy + (-u_xlat3.xy);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + (-u_xlat3.xy);
    u_xlat4.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xy = (-u_xlat3.xy) + u_xlat3.zw;
    output.uv.zw = u_xlat10.xy / u_xlat0.xy;
    output.uv.xy = input.uv.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat1.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat1.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat1.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat15 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat1.xyz = float3(u_xlat15,u_xlat15,u_xlat15) * u_xlat1.xyz;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat0.xyz);
    u_xlat5.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat5.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat5.xy;
    u_xlat5.xy = abs(u_xlat5.xy) * float2(_ScaleX, _ScaleY);
    u_xlat5.xy = u_xlat2.ww / u_xlat5.xy;
    u_xlat15 = dot(u_xlat5.xy, u_xlat5.xy);
    u_xlat5.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat5.xy);
    u_xlat4.zw = float2(0.25, 0.25) / u_xlat5.xy;
    output.mask = u_xlat4;
    u_xlat5.x = rsqrt(u_xlat15);
    u_xlat10.x = abs(input.uv1.y) * _GradientScale;
    u_xlat15 = _Sharpness + 1.0;
    u_xlat10.x = u_xlat15 * u_xlat10.x;
    u_xlat15 = u_xlat10.x * u_xlat5.x;
    u_xlat1.x = (-_PerspectiveFilter) + 1.0;
    u_xlat1.x = abs(u_xlat15) * u_xlat1.x;
    u_xlat5.x = u_xlat5.x * u_xlat10.x + (-u_xlat1.x);
    u_xlat0.x = abs(u_xlat0.x) * u_xlat5.x + u_xlat1.x;
    u_xlatb5 = float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0;
    u_xlat0.x = (u_xlatb5) ? u_xlat0.x : u_xlat15;
    u_xlat5.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat5.x = u_xlat5.x * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat0.x / u_xlat5.x;
    u_xlatb1 = 0.0>=input.uv1.y;
    u_xlat1.x = u_xlatb1 ? 1.0 : float(0.0);
    u_xlat6 = (-_WeightNormal) + _WeightBold;
    u_xlat1.x = u_xlat1.x * u_xlat6 + _WeightNormal;
    u_xlat1.x = u_xlat1.x * 0.25 + _FaceDilate;
    u_xlat1.x = u_xlat1.x * _ScaleRatioA;
    u_xlat1.x = (-u_xlat1.x) * 0.5 + 0.5;
    u_xlat0.yzw = u_xlat1.xxx * u_xlat0.xxx + float3(-0.5, -0.5, -0.5);
    output.world = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;
half u_xlat16_1;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat16_1 = u_xlat16_0 * input.world.x + (-input.world.z);
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    result = float4(u_xlat16_1,u_xlat16_1,u_xlat16_1,u_xlat16_1) * input.color;
    return result;

}
#endif
ENDCG
}
}
}
