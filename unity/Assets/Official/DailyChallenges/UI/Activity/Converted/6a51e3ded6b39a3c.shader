Shader "TextMeshPro/Distance Field" {
Properties {

_FaceTex ("Face Texture", 2D) = "white" { }

_FaceUVSpeedX ("Face UV Speed X", Range(-5, 5)) = 0.0

_FaceUVSpeedY ("Face UV Speed Y", Range(-5, 5)) = 0.0

_FaceColor ("Face Color", Color) = (1,1,1,1)

_FaceDilate ("Face Dilate", Range(-1, 1)) = 0.0

_OutlineColor ("Outline Color", Color) = (0,0,0,1)

_OutlineTex ("Outline Texture", 2D) = "white" { }

_OutlineUVSpeedX ("Outline UV Speed X", Range(-5, 5)) = 0.0

_OutlineUVSpeedY ("Outline UV Speed Y", Range(-5, 5)) = 0.0

_OutlineWidth ("Outline Thickness", Range(0, 1)) = 0.0

_OutlineSoftness ("Outline Softness", Range(0, 1)) = 0.0

_Bevel ("Bevel", Range(0, 1)) = 0.5

_BevelOffset ("Bevel Offset", Range(-0.5, 0.5)) = 0.0

_BevelWidth ("Bevel Width", Range(-0.5, 0.5)) = 0.0

_BevelClamp ("Bevel Clamp", Range(0, 1)) = 0.0

_BevelRoundness ("Bevel Roundness", Range(0, 1)) = 0.0

_LightAngle ("Light Angle", Range(0, 6.28319)) = 3.1415998935699463

_SpecularColor ("Specular", Color) = (1,1,1,1)

_SpecularPower ("Specular", Range(0, 4)) = 2.0

_Reflectivity ("Reflectivity", Range(5, 15)) = 10.0

_Diffuse ("Diffuse", Range(0, 1)) = 0.5

_Ambient ("Ambient", Range(1, 0)) = 0.5

_BumpMap ("Normal map", 2D) = "bump" { }

_BumpOutline ("Bump Outline", Range(0, 1)) = 0.0

_BumpFace ("Bump Face", Range(0, 1)) = 0.0

_ReflectFaceColor ("Reflection Color", Color) = (0,0,0,1)

_ReflectOutlineColor ("Reflection Color", Color) = (0,0,0,1)

_Cube ("Reflection Cubemap", Cube) = "black" { }

_EnvMatrixRotation ("Texture Rotation", Vector) = (0,0,0,0)

_UnderlayColor ("Border Color", Color) = (0,0,0,0.5)

_UnderlayOffsetX ("Border OffsetX", Range(-1, 1)) = 0.0

_UnderlayOffsetY ("Border OffsetY", Range(-1, 1)) = 0.0

_UnderlayDilate ("Border Dilate", Range(-1, 1)) = 0.0

_UnderlaySoftness ("Border Softness", Range(0, 1)) = 0.0

_GlowColor ("Color", Color) = (0,1,0,0.5)

_GlowOffset ("Offset", Range(-1, 1)) = 0.0

_GlowInner ("Inner", Range(0, 1)) = 0.05000000074505806

_GlowOuter ("Outer", Range(0, 1)) = 0.05000000074505806

_GlowPower ("Falloff", Range(1, 0)) = 0.75

_WeightNormal ("Weight Normal", Float) = 0.0

_WeightBold ("Weight Bold", Float) = 0.5

_ShaderFlags ("Flags", Float) = 0.0

_ScaleRatioA ("Scale RatioA", Float) = 1.0

_ScaleRatioB ("Scale RatioB", Float) = 1.0

_ScaleRatioC ("Scale RatioC", Float) = 1.0

_MainTex ("Font Atlas", 2D) = "white" { }

_TextureWidth ("Texture Width", Float) = 512.0

_TextureHeight ("Texture Height", Float) = 512.0

_GradientScale ("Gradient Scale", Float) = 5.0

_ScaleX ("Scale X", Float) = 1.0

_ScaleY ("Scale Y", Float) = 1.0

_PerspectiveFilter ("Perspective Correction", Range(0, 1)) = 0.875

_Sharpness ("Sharpness", Range(-1, 1)) = 0.0

_VertexOffsetX ("Vertex OffsetX", Float) = 0.0

_VertexOffsetY ("Vertex OffsetY", Float) = 0.0

_MaskCoord ("Mask Coordinates", Vector) = (0,0,32767,32767)

_ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)

_MaskSoftnessX ("Mask SoftnessX", Float) = 0.0

_MaskSoftnessY ("Mask SoftnessY", Float) = 0.0

_StencilComp ("Stencil Comparison", Float) = 8.0

_Stencil ("Stencil ID", Float) = 0.0

_StencilOp ("Stencil Operation", Float) = 0.0

_StencilWriteMask ("Stencil Write Mask", Float) = 255.0

_StencilReadMask ("Stencil Read Mask", Float) = 255.0

_CullMode ("Cull Mode", Float) = 0.0

_ColorMask ("Color Mask", Float) = 15.0

}
SubShader {
LOD 0
Tags { "IGNOREPROJECTOR"="true" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull [_CullMode]
ZWrite Off
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
#pragma multi_compile __ GLOW_ON
#pragma multi_compile __ UNDERLAY_ON
#pragma multi_compile __ UNITY_UI_ALPHACLIP
#pragma multi_compile __ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#if !defined(GLOW_ON) && !defined(UNDERLAY_ON) && !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat2 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat2 = min(u_xlat2, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat2.xy);
    output.mask.xy = (-u_xlat2.zw) + u_xlat0.xy;
    u_xlat0.xyz = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyz = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyz;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyz;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat4;
half4 u_xlat16_4;
float u_xlat5;
bool u_xlatb5;
half u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat5 = u_xlat16_0.x + (-input.world.x);
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlatb5 = u_xlat5<0.0;
    if(u_xlatb5){clip(-1);}
    u_xlat5 = _OutlineWidth * _ScaleRatioA;
    u_xlat5 = u_xlat5 * input.world.y;
    u_xlat16_1 = min(u_xlat5, 1.0);
    u_xlat16_6 = u_xlat5 * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4 = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * (float3)(u_xlat4);
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_0.xyz = u_xlat0.xyz * (float3)(u_xlat4) + u_xlat16_2.xyz;
    u_xlat16_0.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat4 = _OutlineSoftness * _ScaleRatioA;
    u_xlat9 = u_xlat4 * input.world.y;
    u_xlat16_1 = u_xlat4 * input.world.y + 1.0;
    u_xlat16_6 = u_xlat9 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_0 = u_xlat16_0 * (float4)(u_xlat16_1);
    result = u_xlat16_0 * input.color.wwww;
    return result;

}
#elif !defined(GLOW_ON) && defined(UNDERLAY_ON) && !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float u_xlat4;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat6.x + 1.0;
    u_xlat0.x = u_xlat6.x / u_xlat0.x;
    u_xlat4 = u_xlat8.x * u_xlat0.x + -0.5;
    u_xlat8.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat8.x) * 0.5 + u_xlat4;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat4;
half4 u_xlat16_4;
half u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_6 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4 = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * (float3)(u_xlat4);
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_0.xyz = u_xlat0.xyz * (float3)(u_xlat4) + u_xlat16_2.xyz;
    u_xlat16_0.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat4 = _OutlineSoftness * _ScaleRatioA;
    u_xlat9 = u_xlat4 * input.world.y;
    u_xlat16_1 = u_xlat4 * input.world.y + 1.0;
    u_xlat16_6 = u_xlat9 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_2 = u_xlat16_0 * (float4)(u_xlat16_1);
    u_xlat16_1 = (-u_xlat16_0.w) * u_xlat16_1 + 1.0;
    u_xlat16_4.x = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat4 = u_xlat16_4.x * input.uv4.z + (-input.uv4.w);
    u_xlat4 = clamp(u_xlat4, 0.0, 1.0);
    u_xlat0 = (float4)(u_xlat4) * input.color1;
    u_xlat0 = u_xlat0 * (float4)(u_xlat16_1) + u_xlat16_2;
    result = u_xlat0 * input.color.wwww;
    return result;

}
#elif defined(GLOW_ON) && defined(UNDERLAY_ON) && !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GlowInner;
float _GlowOffset;
float _GlowOuter;
float _GlowPower;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioB;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _GlowColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat4;
float u_xlat5;
float3 u_xlat7;
float2 u_xlat10;
float u_xlat15;
float u_xlat16;
bool u_xlatb16;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat10.x = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat10.x = (-_OutlineSoftness) * _ScaleRatioA + u_xlat10.x;
    u_xlat15 = (-_GlowOffset) * _ScaleRatioB + 1.0;
    u_xlat15 = (-_GlowOuter) * _ScaleRatioB + u_xlat15;
    u_xlat10.x = min(u_xlat15, u_xlat10.x);
    u_xlat2.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat2.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat2.xy;
    u_xlat2.xy = abs(u_xlat2.xy) * float2(_ScaleX,_ScaleY);
    u_xlat2.xy = u_xlat2.ww / u_xlat2.xy;
    u_xlat15 = dot(u_xlat2.xy, u_xlat2.xy);
    u_xlat2.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat2.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat2.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat16 = abs(input.uv1.y) * _GradientScale;
    u_xlat2.x = _Sharpness + 1.0;
    u_xlat16 = u_xlat16 * u_xlat2.x;
    u_xlat2.x = u_xlat15 * u_xlat16;
    u_xlat7.x = (-_PerspectiveFilter) + 1.0;
    u_xlat7.x = u_xlat7.x * abs(u_xlat2.x);
    u_xlat15 = u_xlat15 * u_xlat16 + (-u_xlat7.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat16 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat3.xyz = (float3)(u_xlat16) * u_xlat3.xyz;
    u_xlat16 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat4.xyz = (float3)(u_xlat16) * u_xlat1.xyz;
    u_xlat16 = dot(u_xlat3.xyz, u_xlat4.xyz);
    u_xlat15 = abs(u_xlat16) * u_xlat15 + u_xlat7.x;
    u_xlatb16 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat7.x = (u_xlatb16) ? u_xlat15 : u_xlat2.x;
    u_xlat15 = 0.5 / u_xlat7.x;
    u_xlat10.x = u_xlat10.x * 0.5 + (-u_xlat15);
    u_xlatb16 = 0.0>=input.uv1.y;
    u_xlat16 = u_xlatb16 ? 1.0 : float(0.0);
    u_xlat2.x = (-_WeightNormal) + _WeightBold;
    u_xlat16 = u_xlat16 * u_xlat2.x + _WeightNormal;
    u_xlat16 = u_xlat16 * 0.25 + _FaceDilate;
    u_xlat16 = u_xlat16 * _ScaleRatioA;
    output.world.x = (-u_xlat16) * 0.5 + u_xlat10.x;
    u_xlat7.z = u_xlat16 * 0.5;
    u_xlat10.x = (-u_xlat16) * 0.5 + 0.5;
    output.world.yw = u_xlat7.xz;
    output.world.z = u_xlat15 + u_xlat10.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat7.x + 1.0;
    u_xlat0.x = u_xlat7.x / u_xlat0.x;
    u_xlat5 = u_xlat10.x * u_xlat0.x + -0.5;
    u_xlat10.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat10.x) * 0.5 + u_xlat5;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat10.x = floor(u_xlat0.x);
    u_xlat10.y = (-u_xlat10.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat10.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
float4 u_xlat3;
half4 u_xlat16_3;
half3 u_xlat16_4;
half4 u_xlat16_5;
float3 u_xlat6;
half u_xlat16_6;
bool u_xlatb6;
half u_xlat16_7;
float u_xlat12;
half u_xlat16_13;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_7 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0) + input.world.z;
    u_xlat16_13 = u_xlat0.x * input.world.y + u_xlat16_7;
    u_xlat16_13 = clamp(u_xlat16_13, 0.0, 1.0);
    u_xlat16_7 = u_xlat0.x * input.world.y + (-u_xlat16_7);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_13;
    u_xlat6.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_2 = tex2D(_OutlineTex, u_xlat6.xy);
    u_xlat3 = u_xlat16_2 * _OutlineColor;
    u_xlat16_4.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat6.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_5 = tex2D(_FaceTex, u_xlat6.xy);
    u_xlat6.xyz = u_xlat16_4.xyz * u_xlat16_5.xyz;
    u_xlat2.x = u_xlat16_5.w * _FaceColor.w;
    u_xlat16_4.xyz = u_xlat6.xyz * u_xlat2.xxx;
    u_xlat16_3.xyz = u_xlat3.xyz * u_xlat3.www + (-u_xlat16_4.xyz);
    u_xlat16_3.w = _OutlineColor.w * u_xlat16_2.w + (-u_xlat2.x);
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_3;
    u_xlat16_2.xyz = u_xlat6.xyz * u_xlat2.xxx + u_xlat16_3.xyz;
    u_xlat16_2.w = _FaceColor.w * u_xlat16_5.w + u_xlat16_3.w;
    u_xlat0.y = _OutlineSoftness * _ScaleRatioA;
    u_xlat0.xz = u_xlat0.xy * input.world.yy;
    u_xlat16_1 = u_xlat0.y * input.world.y + 1.0;
    u_xlat16_7 = u_xlat0.z * 0.5 + u_xlat16_7;
    u_xlat16_1 = u_xlat16_7 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_1 = (-u_xlat16_2.w) * u_xlat16_1 + 1.0;
    u_xlat16_6 = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat6.x = u_xlat16_6 * input.uv4.z + (-input.uv4.w);
    u_xlat6.x = clamp(u_xlat6.x, 0.0, 1.0);
    u_xlat2 = u_xlat6.xxxx * input.color1;
    u_xlat1 = u_xlat2 * (float4)(u_xlat16_1) + u_xlat16_3;
    u_xlat6.x = _GlowOffset * _ScaleRatioB;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat0.x = (-u_xlat6.x) * 0.5 + u_xlat0.x;
    u_xlatb6 = u_xlat0.x>=0.0;
    u_xlat6.x = u_xlatb6 ? 1.0 : float(0.0);
    u_xlat12 = _GlowOuter * _ScaleRatioB + (-_GlowInner);
    u_xlat6.x = u_xlat6.x * u_xlat12 + _GlowInner;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat12 = u_xlat6.x * 0.5 + 1.0;
    u_xlat6.x = u_xlat6.x * 0.5;
    u_xlat6.x = min(u_xlat6.x, 1.0);
    u_xlat6.x = sqrt(u_xlat6.x);
    u_xlat0.x = u_xlat0.x / u_xlat12;
    u_xlat0.x = min(abs(u_xlat0.x), 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _GlowPower;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat0.x = (-u_xlat0.x) + 1.0;
    u_xlat0.x = u_xlat6.x * u_xlat0.x;
    u_xlat0.x = dot(_GlowColor.ww, u_xlat0.xx);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat1.xyz = _GlowColor.xyz * u_xlat0.xxx + u_xlat1.xyz;
    result = u_xlat1 * input.color.wwww;
    return result;

}
#elif !defined(GLOW_ON) && !defined(UNDERLAY_ON) && defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat2 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat2 = min(u_xlat2, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat2.xy);
    output.mask.xy = (-u_xlat2.zw) + u_xlat0.xy;
    u_xlat0.xyz = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyz = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyz;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyz;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half4 u_xlat16_3;
float u_xlat4;
half4 u_xlat16_4;
bool u_xlatb4;
float u_xlat5;
bool u_xlatb5;
half u_xlat16_6;
half u_xlat16_11;

    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat5 = u_xlat16_0.x + (-input.world.x);
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlatb5 = u_xlat5<0.0;
    if(u_xlatb5){clip(-1);}
    u_xlat5 = _OutlineWidth * _ScaleRatioA;
    u_xlat5 = u_xlat5 * input.world.y;
    u_xlat16_1 = min(u_xlat5, 1.0);
    u_xlat16_6 = u_xlat5 * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4 = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * (float3)(u_xlat4);
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_3.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat16_3.xyz = u_xlat0.xyz * (float3)(u_xlat4) + u_xlat16_2.xyz;
    u_xlat0.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat5 = u_xlat0.x * input.world.y;
    u_xlat16_1 = u_xlat0.x * input.world.y + 1.0;
    u_xlat16_6 = u_xlat5 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_6 = u_xlat16_3.w * u_xlat16_1 + -0.00100000005;
    u_xlat16_0 = (float4)(u_xlat16_1) * u_xlat16_3;
    result = u_xlat16_0 * input.color.wwww;
    u_xlatb4 = u_xlat16_6<0.0;
    if(u_xlatb4){clip(-1);}
    return result;

}
#elif !defined(GLOW_ON) && defined(UNDERLAY_ON) && defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float u_xlat4;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat6.x + 1.0;
    u_xlat0.x = u_xlat6.x / u_xlat0.x;
    u_xlat4 = u_xlat8.x * u_xlat0.x + -0.5;
    u_xlat8.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat8.x) * 0.5 + u_xlat4;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat4;
half4 u_xlat16_4;
bool u_xlatb4;
half u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_6 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4 = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * (float3)(u_xlat4);
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_0.xyz = u_xlat0.xyz * (float3)(u_xlat4) + u_xlat16_2.xyz;
    u_xlat16_0.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat4 = _OutlineSoftness * _ScaleRatioA;
    u_xlat9 = u_xlat4 * input.world.y;
    u_xlat16_1 = u_xlat4 * input.world.y + 1.0;
    u_xlat16_6 = u_xlat9 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_2 = u_xlat16_0 * (float4)(u_xlat16_1);
    u_xlat16_1 = (-u_xlat16_0.w) * u_xlat16_1 + 1.0;
    u_xlat16_4.x = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat4 = u_xlat16_4.x * input.uv4.z + (-input.uv4.w);
    u_xlat4 = clamp(u_xlat4, 0.0, 1.0);
    u_xlat0 = (float4)(u_xlat4) * input.color1;
    u_xlat0 = u_xlat0 * (float4)(u_xlat16_1) + u_xlat16_2;
    u_xlat16_1 = u_xlat0.w + -0.00100000005;
    result = u_xlat0 * input.color.wwww;
    u_xlatb4 = u_xlat16_1<0.0;
    if(u_xlatb4){clip(-1);}
    return result;

}
#elif defined(GLOW_ON) && defined(UNDERLAY_ON) && defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GlowInner;
float _GlowOffset;
float _GlowOuter;
float _GlowPower;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioB;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _GlowColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat4;
float u_xlat5;
float3 u_xlat7;
float2 u_xlat10;
float u_xlat15;
float u_xlat16;
bool u_xlatb16;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat10.x = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat10.x = (-_OutlineSoftness) * _ScaleRatioA + u_xlat10.x;
    u_xlat15 = (-_GlowOffset) * _ScaleRatioB + 1.0;
    u_xlat15 = (-_GlowOuter) * _ScaleRatioB + u_xlat15;
    u_xlat10.x = min(u_xlat15, u_xlat10.x);
    u_xlat2.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat2.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat2.xy;
    u_xlat2.xy = abs(u_xlat2.xy) * float2(_ScaleX,_ScaleY);
    u_xlat2.xy = u_xlat2.ww / u_xlat2.xy;
    u_xlat15 = dot(u_xlat2.xy, u_xlat2.xy);
    u_xlat2.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat2.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat2.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat16 = abs(input.uv1.y) * _GradientScale;
    u_xlat2.x = _Sharpness + 1.0;
    u_xlat16 = u_xlat16 * u_xlat2.x;
    u_xlat2.x = u_xlat15 * u_xlat16;
    u_xlat7.x = (-_PerspectiveFilter) + 1.0;
    u_xlat7.x = u_xlat7.x * abs(u_xlat2.x);
    u_xlat15 = u_xlat15 * u_xlat16 + (-u_xlat7.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat16 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat3.xyz = (float3)(u_xlat16) * u_xlat3.xyz;
    u_xlat16 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat4.xyz = (float3)(u_xlat16) * u_xlat1.xyz;
    u_xlat16 = dot(u_xlat3.xyz, u_xlat4.xyz);
    u_xlat15 = abs(u_xlat16) * u_xlat15 + u_xlat7.x;
    u_xlatb16 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat7.x = (u_xlatb16) ? u_xlat15 : u_xlat2.x;
    u_xlat15 = 0.5 / u_xlat7.x;
    u_xlat10.x = u_xlat10.x * 0.5 + (-u_xlat15);
    u_xlatb16 = 0.0>=input.uv1.y;
    u_xlat16 = u_xlatb16 ? 1.0 : float(0.0);
    u_xlat2.x = (-_WeightNormal) + _WeightBold;
    u_xlat16 = u_xlat16 * u_xlat2.x + _WeightNormal;
    u_xlat16 = u_xlat16 * 0.25 + _FaceDilate;
    u_xlat16 = u_xlat16 * _ScaleRatioA;
    output.world.x = (-u_xlat16) * 0.5 + u_xlat10.x;
    u_xlat7.z = u_xlat16 * 0.5;
    u_xlat10.x = (-u_xlat16) * 0.5 + 0.5;
    output.world.yw = u_xlat7.xz;
    output.world.z = u_xlat15 + u_xlat10.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat7.x + 1.0;
    u_xlat0.x = u_xlat7.x / u_xlat0.x;
    u_xlat5 = u_xlat10.x * u_xlat0.x + -0.5;
    u_xlat10.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat10.x) * 0.5 + u_xlat5;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat10.x = floor(u_xlat0.x);
    u_xlat10.y = (-u_xlat10.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat10.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
float4 u_xlat3;
half4 u_xlat16_3;
half3 u_xlat16_4;
half4 u_xlat16_5;
float3 u_xlat6;
half u_xlat16_6;
bool u_xlatb6;
half u_xlat16_7;
float u_xlat12;
half u_xlat16_13;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_7 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0 = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0) + input.world.z;
    u_xlat16_13 = u_xlat0.x * input.world.y + u_xlat16_7;
    u_xlat16_13 = clamp(u_xlat16_13, 0.0, 1.0);
    u_xlat16_7 = u_xlat0.x * input.world.y + (-u_xlat16_7);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_13;
    u_xlat6.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_2 = tex2D(_OutlineTex, u_xlat6.xy);
    u_xlat3 = u_xlat16_2 * _OutlineColor;
    u_xlat16_4.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat6.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_5 = tex2D(_FaceTex, u_xlat6.xy);
    u_xlat6.xyz = u_xlat16_4.xyz * u_xlat16_5.xyz;
    u_xlat2.x = u_xlat16_5.w * _FaceColor.w;
    u_xlat16_4.xyz = u_xlat6.xyz * u_xlat2.xxx;
    u_xlat16_3.xyz = u_xlat3.xyz * u_xlat3.www + (-u_xlat16_4.xyz);
    u_xlat16_3.w = _OutlineColor.w * u_xlat16_2.w + (-u_xlat2.x);
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_3;
    u_xlat16_2.xyz = u_xlat6.xyz * u_xlat2.xxx + u_xlat16_3.xyz;
    u_xlat16_2.w = _FaceColor.w * u_xlat16_5.w + u_xlat16_3.w;
    u_xlat0.y = _OutlineSoftness * _ScaleRatioA;
    u_xlat0.xz = u_xlat0.xy * input.world.yy;
    u_xlat16_1 = u_xlat0.y * input.world.y + 1.0;
    u_xlat16_7 = u_xlat0.z * 0.5 + u_xlat16_7;
    u_xlat16_1 = u_xlat16_7 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_1 = (-u_xlat16_2.w) * u_xlat16_1 + 1.0;
    u_xlat16_6 = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat6.x = u_xlat16_6 * input.uv4.z + (-input.uv4.w);
    u_xlat6.x = clamp(u_xlat6.x, 0.0, 1.0);
    u_xlat2 = u_xlat6.xxxx * input.color1;
    u_xlat1 = u_xlat2 * (float4)(u_xlat16_1) + u_xlat16_3;
    u_xlat16_4.x = u_xlat1.w + -0.00100000005;
    u_xlatb6 = u_xlat16_4.x<0.0;
    if(u_xlatb6){clip(-1);}
    u_xlat6.x = _GlowOffset * _ScaleRatioB;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat0.x = (-u_xlat6.x) * 0.5 + u_xlat0.x;
    u_xlatb6 = u_xlat0.x>=0.0;
    u_xlat6.x = u_xlatb6 ? 1.0 : float(0.0);
    u_xlat12 = _GlowOuter * _ScaleRatioB + (-_GlowInner);
    u_xlat6.x = u_xlat6.x * u_xlat12 + _GlowInner;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat12 = u_xlat6.x * 0.5 + 1.0;
    u_xlat6.x = u_xlat6.x * 0.5;
    u_xlat6.x = min(u_xlat6.x, 1.0);
    u_xlat6.x = sqrt(u_xlat6.x);
    u_xlat0.x = u_xlat0.x / u_xlat12;
    u_xlat0.x = min(abs(u_xlat0.x), 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _GlowPower;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat0.x = (-u_xlat0.x) + 1.0;
    u_xlat0.x = u_xlat6.x * u_xlat0.x;
    u_xlat0.x = dot(_GlowColor.ww, u_xlat0.xx);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat1.xyz = _GlowColor.xyz * u_xlat0.xxx + u_xlat1.xyz;
    result = u_xlat1 * input.color.wwww;
    return result;

}
#elif !defined(GLOW_ON) && !defined(UNDERLAY_ON) && !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat2 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat2 = min(u_xlat2, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat2.xy);
    output.mask.xy = (-u_xlat2.zw) + u_xlat0.xy;
    u_xlat0.xyz = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyz = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyz;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyz;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat4;
half4 u_xlat16_4;
float u_xlat5;
bool u_xlatb5;
half u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat5 = u_xlat16_0.x + (-input.world.x);
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlatb5 = u_xlat5<0.0;
    if(u_xlatb5){clip(-1);}
    u_xlat5 = _OutlineWidth * _ScaleRatioA;
    u_xlat5 = u_xlat5 * input.world.y;
    u_xlat16_1 = min(u_xlat5, 1.0);
    u_xlat16_6 = u_xlat5 * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4.x = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * u_xlat4.xxx;
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4.x);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_0.xyz = u_xlat0.xyz * u_xlat4.xxx + u_xlat16_2.xyz;
    u_xlat16_0.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat4.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat9 = u_xlat4.x * input.world.y;
    u_xlat16_1 = u_xlat4.x * input.world.y + 1.0;
    u_xlat16_6 = u_xlat9 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_0 = u_xlat16_0 * (float4)(u_xlat16_1);
    u_xlat4.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat4.xy = u_xlat4.xy + -abs(input.mask.xy);
    u_xlat4.xy = u_xlat4.xy * input.mask.zw;
    u_xlat4.xy = clamp(u_xlat4.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat4.y * u_xlat4.x;
    u_xlat16_0 = u_xlat16_0 * (float4)(u_xlat16_1);
    result = u_xlat16_0 * input.color.wwww;
    return result;

}
#elif !defined(GLOW_ON) && defined(UNDERLAY_ON) && !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float u_xlat4;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat6.x + 1.0;
    u_xlat0.x = u_xlat6.x / u_xlat0.x;
    u_xlat4 = u_xlat8.x * u_xlat0.x + -0.5;
    u_xlat8.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat8.x) * 0.5 + u_xlat4;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat4;
half4 u_xlat16_4;
half u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_6 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4.x = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * u_xlat4.xxx;
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4.x);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_0.xyz = u_xlat0.xyz * u_xlat4.xxx + u_xlat16_2.xyz;
    u_xlat16_0.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat4.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat9 = u_xlat4.x * input.world.y;
    u_xlat16_1 = u_xlat4.x * input.world.y + 1.0;
    u_xlat16_6 = u_xlat9 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_2 = u_xlat16_0 * (float4)(u_xlat16_1);
    u_xlat16_1 = (-u_xlat16_0.w) * u_xlat16_1 + 1.0;
    u_xlat16_4.x = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat4.x = u_xlat16_4.x * input.uv4.z + (-input.uv4.w);
    u_xlat4.x = clamp(u_xlat4.x, 0.0, 1.0);
    u_xlat0 = u_xlat4.xxxx * input.color1;
    u_xlat0 = u_xlat0 * (float4)(u_xlat16_1) + u_xlat16_2;
    u_xlat4.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat4.xy = u_xlat4.xy + -abs(input.mask.xy);
    u_xlat4.xy = u_xlat4.xy * input.mask.zw;
    u_xlat4.xy = clamp(u_xlat4.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat4.y * u_xlat4.x;
    u_xlat16_0 = u_xlat0 * (float4)(u_xlat16_1);
    result = u_xlat16_0 * input.color.wwww;
    return result;

}
#elif defined(GLOW_ON) && defined(UNDERLAY_ON) && !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GlowInner;
float _GlowOffset;
float _GlowOuter;
float _GlowPower;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioB;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _GlowColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat4;
float u_xlat5;
float3 u_xlat7;
float2 u_xlat10;
float u_xlat15;
float u_xlat16;
bool u_xlatb16;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat10.x = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat10.x = (-_OutlineSoftness) * _ScaleRatioA + u_xlat10.x;
    u_xlat15 = (-_GlowOffset) * _ScaleRatioB + 1.0;
    u_xlat15 = (-_GlowOuter) * _ScaleRatioB + u_xlat15;
    u_xlat10.x = min(u_xlat15, u_xlat10.x);
    u_xlat2.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat2.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat2.xy;
    u_xlat2.xy = abs(u_xlat2.xy) * float2(_ScaleX,_ScaleY);
    u_xlat2.xy = u_xlat2.ww / u_xlat2.xy;
    u_xlat15 = dot(u_xlat2.xy, u_xlat2.xy);
    u_xlat2.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat2.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat2.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat16 = abs(input.uv1.y) * _GradientScale;
    u_xlat2.x = _Sharpness + 1.0;
    u_xlat16 = u_xlat16 * u_xlat2.x;
    u_xlat2.x = u_xlat15 * u_xlat16;
    u_xlat7.x = (-_PerspectiveFilter) + 1.0;
    u_xlat7.x = u_xlat7.x * abs(u_xlat2.x);
    u_xlat15 = u_xlat15 * u_xlat16 + (-u_xlat7.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat16 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat3.xyz = (float3)(u_xlat16) * u_xlat3.xyz;
    u_xlat16 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat4.xyz = (float3)(u_xlat16) * u_xlat1.xyz;
    u_xlat16 = dot(u_xlat3.xyz, u_xlat4.xyz);
    u_xlat15 = abs(u_xlat16) * u_xlat15 + u_xlat7.x;
    u_xlatb16 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat7.x = (u_xlatb16) ? u_xlat15 : u_xlat2.x;
    u_xlat15 = 0.5 / u_xlat7.x;
    u_xlat10.x = u_xlat10.x * 0.5 + (-u_xlat15);
    u_xlatb16 = 0.0>=input.uv1.y;
    u_xlat16 = u_xlatb16 ? 1.0 : float(0.0);
    u_xlat2.x = (-_WeightNormal) + _WeightBold;
    u_xlat16 = u_xlat16 * u_xlat2.x + _WeightNormal;
    u_xlat16 = u_xlat16 * 0.25 + _FaceDilate;
    u_xlat16 = u_xlat16 * _ScaleRatioA;
    output.world.x = (-u_xlat16) * 0.5 + u_xlat10.x;
    u_xlat7.z = u_xlat16 * 0.5;
    u_xlat10.x = (-u_xlat16) * 0.5 + 0.5;
    output.world.yw = u_xlat7.xz;
    output.world.z = u_xlat15 + u_xlat10.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat7.x + 1.0;
    u_xlat0.x = u_xlat7.x / u_xlat0.x;
    u_xlat5 = u_xlat10.x * u_xlat0.x + -0.5;
    u_xlat10.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat10.x) * 0.5 + u_xlat5;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat10.x = floor(u_xlat0.x);
    u_xlat10.y = (-u_xlat10.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat10.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
float4 u_xlat3;
half4 u_xlat16_3;
half3 u_xlat16_4;
half4 u_xlat16_5;
float3 u_xlat6;
half u_xlat16_6;
bool u_xlatb6;
half u_xlat16_7;
float u_xlat12;
half u_xlat16_13;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_7 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlat16_13 = u_xlat0.x * input.world.y + u_xlat16_7;
    u_xlat16_13 = clamp(u_xlat16_13, 0.0, 1.0);
    u_xlat16_7 = u_xlat0.x * input.world.y + (-u_xlat16_7);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_13;
    u_xlat6.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_2 = tex2D(_OutlineTex, u_xlat6.xy);
    u_xlat3 = u_xlat16_2 * _OutlineColor;
    u_xlat16_4.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat6.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_5 = tex2D(_FaceTex, u_xlat6.xy);
    u_xlat6.xyz = u_xlat16_4.xyz * u_xlat16_5.xyz;
    u_xlat2.x = u_xlat16_5.w * _FaceColor.w;
    u_xlat16_4.xyz = u_xlat6.xyz * u_xlat2.xxx;
    u_xlat16_3.xyz = u_xlat3.xyz * u_xlat3.www + (-u_xlat16_4.xyz);
    u_xlat16_3.w = _OutlineColor.w * u_xlat16_2.w + (-u_xlat2.x);
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_3;
    u_xlat16_2.xyz = u_xlat6.xyz * u_xlat2.xxx + u_xlat16_3.xyz;
    u_xlat16_2.w = _FaceColor.w * u_xlat16_5.w + u_xlat16_3.w;
    u_xlat0.y = _OutlineSoftness * _ScaleRatioA;
    u_xlat0.xz = u_xlat0.xy * input.world.yy;
    u_xlat16_1 = u_xlat0.y * input.world.y + 1.0;
    u_xlat16_7 = u_xlat0.z * 0.5 + u_xlat16_7;
    u_xlat16_1 = u_xlat16_7 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_1 = (-u_xlat16_2.w) * u_xlat16_1 + 1.0;
    u_xlat16_6 = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat6.x = u_xlat16_6 * input.uv4.z + (-input.uv4.w);
    u_xlat6.x = clamp(u_xlat6.x, 0.0, 1.0);
    u_xlat2 = u_xlat6.xxxx * input.color1;
    u_xlat1 = u_xlat2 * (float4)(u_xlat16_1) + u_xlat16_3;
    u_xlat6.x = _GlowOffset * _ScaleRatioB;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat0.x = (-u_xlat6.x) * 0.5 + u_xlat0.x;
    u_xlatb6 = u_xlat0.x>=0.0;
    u_xlat6.x = u_xlatb6 ? 1.0 : float(0.0);
    u_xlat12 = _GlowOuter * _ScaleRatioB + (-_GlowInner);
    u_xlat6.x = u_xlat6.x * u_xlat12 + _GlowInner;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat12 = u_xlat6.x * 0.5 + 1.0;
    u_xlat6.x = u_xlat6.x * 0.5;
    u_xlat6.x = min(u_xlat6.x, 1.0);
    u_xlat6.x = sqrt(u_xlat6.x);
    u_xlat0.x = u_xlat0.x / u_xlat12;
    u_xlat0.x = min(abs(u_xlat0.x), 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _GlowPower;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat0.x = (-u_xlat0.x) + 1.0;
    u_xlat0.x = u_xlat6.x * u_xlat0.x;
    u_xlat0.x = dot(_GlowColor.ww, u_xlat0.xx);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat1.xyz = _GlowColor.xyz * u_xlat0.xxx + u_xlat1.xyz;
    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_4.x = u_xlat0.y * u_xlat0.x;
    u_xlat16_0 = u_xlat1 * u_xlat16_4.xxxx;
    result = u_xlat16_0 * input.color.wwww;
    return result;

}
#elif !defined(GLOW_ON) && !defined(UNDERLAY_ON) && defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat2 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat2 = min(u_xlat2, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat2.xy);
    output.mask.xy = (-u_xlat2.zw) + u_xlat0.xy;
    u_xlat0.xyz = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyz = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyz;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyz;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat4;
half4 u_xlat16_4;
bool u_xlatb4;
float u_xlat5;
bool u_xlatb5;
half u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat5 = u_xlat16_0.x + (-input.world.x);
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlatb5 = u_xlat5<0.0;
    if(u_xlatb5){clip(-1);}
    u_xlat5 = _OutlineWidth * _ScaleRatioA;
    u_xlat5 = u_xlat5 * input.world.y;
    u_xlat16_1 = min(u_xlat5, 1.0);
    u_xlat16_6 = u_xlat5 * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4.x = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * u_xlat4.xxx;
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4.x);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_0.xyz = u_xlat0.xyz * u_xlat4.xxx + u_xlat16_2.xyz;
    u_xlat16_0.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat4.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat9 = u_xlat4.x * input.world.y;
    u_xlat16_1 = u_xlat4.x * input.world.y + 1.0;
    u_xlat16_6 = u_xlat9 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_0 = u_xlat16_0 * (float4)(u_xlat16_1);
    u_xlat4.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat4.xy = u_xlat4.xy + -abs(input.mask.xy);
    u_xlat4.xy = u_xlat4.xy * input.mask.zw;
    u_xlat4.xy = clamp(u_xlat4.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat4.y * u_xlat4.x;
    u_xlat16_6 = u_xlat16_0.w * u_xlat16_1 + -0.00100000005;
    u_xlat16_0 = u_xlat16_0 * (float4)(u_xlat16_1);
    result = u_xlat16_0 * input.color.wwww;
    u_xlatb4 = u_xlat16_6<0.0;
    if(u_xlatb4){clip(-1);}
    return result;

}
#elif !defined(GLOW_ON) && defined(UNDERLAY_ON) && defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float u_xlat4;
float3 u_xlat6;
float2 u_xlat8;
bool u_xlatb8;
float u_xlat12;
bool u_xlatb12;
float u_xlat13;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat8.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat8.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat8.xy;
    u_xlat8.xy = abs(u_xlat8.xy) * float2(_ScaleX,_ScaleY);
    u_xlat8.xy = u_xlat2.ww / u_xlat8.xy;
    u_xlat13 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat8.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat8.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat8.xy;
    u_xlat8.x = rsqrt(u_xlat13);
    u_xlat12 = abs(input.uv1.y) * _GradientScale;
    u_xlat13 = _Sharpness + 1.0;
    u_xlat12 = u_xlat12 * u_xlat13;
    u_xlat13 = u_xlat12 * u_xlat8.x;
    u_xlat2.x = (-_PerspectiveFilter) + 1.0;
    u_xlat2.x = abs(u_xlat13) * u_xlat2.x;
    u_xlat8.x = u_xlat8.x * u_xlat12 + (-u_xlat2.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat12 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat6.xyz = (float3)(u_xlat12) * u_xlat3.xyz;
    u_xlat12 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat12 = rsqrt(u_xlat12);
    u_xlat3.xyz = (float3)(u_xlat12) * u_xlat1.xyz;
    u_xlat12 = dot(u_xlat6.xyz, u_xlat3.xyz);
    u_xlat8.x = abs(u_xlat12) * u_xlat8.x + u_xlat2.x;
    u_xlatb12 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat6.x = (u_xlatb12) ? u_xlat8.x : u_xlat13;
    u_xlatb8 = 0.0>=input.uv1.y;
    u_xlat8.x = u_xlatb8 ? 1.0 : float(0.0);
    u_xlat12 = (-_WeightNormal) + _WeightBold;
    u_xlat8.x = u_xlat8.x * u_xlat12 + _WeightNormal;
    u_xlat8.x = u_xlat8.x * 0.25 + _FaceDilate;
    u_xlat8.x = u_xlat8.x * _ScaleRatioA;
    u_xlat6.z = u_xlat8.x * 0.5;
    output.world.yw = u_xlat6.xz;
    u_xlat12 = 0.5 / u_xlat6.x;
    u_xlat13 = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat13 = (-_OutlineSoftness) * _ScaleRatioA + u_xlat13;
    u_xlat13 = u_xlat13 * 0.5 + (-u_xlat12);
    output.world.x = (-u_xlat8.x) * 0.5 + u_xlat13;
    u_xlat8.x = (-u_xlat8.x) * 0.5 + 0.5;
    output.world.z = u_xlat12 + u_xlat8.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat6.x + 1.0;
    u_xlat0.x = u_xlat6.x / u_xlat0.x;
    u_xlat4 = u_xlat8.x * u_xlat0.x + -0.5;
    u_xlat8.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat8.x) * 0.5 + u_xlat4;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat8.x = floor(u_xlat0.x);
    u_xlat8.y = (-u_xlat8.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat8.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat4;
half4 u_xlat16_4;
bool u_xlatb4;
half u_xlat16_6;
float u_xlat9;
half u_xlat16_11;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_6 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlat16_11 = u_xlat0.x * input.world.y + u_xlat16_6;
    u_xlat16_11 = clamp(u_xlat16_11, 0.0, 1.0);
    u_xlat16_6 = u_xlat0.x * input.world.y + (-u_xlat16_6);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_11;
    u_xlat0.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_0 = tex2D(_OutlineTex, u_xlat0.xy);
    u_xlat2 = u_xlat16_0 * _OutlineColor;
    u_xlat16_3.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat0.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_4 = tex2D(_FaceTex, u_xlat0.xy);
    u_xlat0.xyz = u_xlat16_3.xyz * u_xlat16_4.xyz;
    u_xlat4.x = u_xlat16_4.w * _FaceColor.w;
    u_xlat16_3.xyz = u_xlat0.xyz * u_xlat4.xxx;
    u_xlat16_2.xyz = u_xlat2.xyz * u_xlat2.www + (-u_xlat16_3.xyz);
    u_xlat16_2.w = _OutlineColor.w * u_xlat16_0.w + (-u_xlat4.x);
    u_xlat16_2 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_0.xyz = u_xlat0.xyz * u_xlat4.xxx + u_xlat16_2.xyz;
    u_xlat16_0.w = _FaceColor.w * u_xlat16_4.w + u_xlat16_2.w;
    u_xlat4.x = _OutlineSoftness * _ScaleRatioA;
    u_xlat9 = u_xlat4.x * input.world.y;
    u_xlat16_1 = u_xlat4.x * input.world.y + 1.0;
    u_xlat16_6 = u_xlat9 * 0.5 + u_xlat16_6;
    u_xlat16_1 = u_xlat16_6 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_2 = u_xlat16_0 * (float4)(u_xlat16_1);
    u_xlat16_1 = (-u_xlat16_0.w) * u_xlat16_1 + 1.0;
    u_xlat16_4.x = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat4.x = u_xlat16_4.x * input.uv4.z + (-input.uv4.w);
    u_xlat4.x = clamp(u_xlat4.x, 0.0, 1.0);
    u_xlat0 = u_xlat4.xxxx * input.color1;
    u_xlat0 = u_xlat0 * (float4)(u_xlat16_1) + u_xlat16_2;
    u_xlat4.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat4.xy = u_xlat4.xy + -abs(input.mask.xy);
    u_xlat4.xy = u_xlat4.xy * input.mask.zw;
    u_xlat4.xy = clamp(u_xlat4.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat4.y * u_xlat4.x;
    u_xlat16_6 = u_xlat0.w * u_xlat16_1 + -0.00100000005;
    u_xlat16_0 = u_xlat0 * (float4)(u_xlat16_1);
    result = u_xlat16_0 * input.color.wwww;
    u_xlatb4 = u_xlat16_6<0.0;
    if(u_xlatb4){clip(-1);}
    return result;

}
#elif defined(GLOW_ON) && defined(UNDERLAY_ON) && defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float3 uv3:TEXCOORD3; float4 uv4:TEXCOORD4; float4 color1:COLOR1; float4 uv5:TEXCOORD5; };

float _FaceDilate;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
float _GlowInner;
float _GlowOffset;
float _GlowOuter;
float _GlowPower;
float _GradientScale;
float _MaskSoftnessX;
float _MaskSoftnessY;
float _OutlineSoftness;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
float _OutlineWidth;
float _PerspectiveFilter;
float _ScaleRatioA;
float _ScaleRatioB;
float _ScaleRatioC;
float _ScaleX;
float _ScaleY;
float _Sharpness;
float _TextureHeight;
float _TextureWidth;
float _UnderlayDilate;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlaySoftness;
float _VertexOffsetX;
float _VertexOffsetY;
float _WeightBold;
float _WeightNormal;
float4 _ClipRect;
float4 _FaceTex_ST;
float4 _OutlineTex_ST;
float4x4 _EnvMatrix;
half4 _FaceColor;
half4 _GlowColor;
half4 _OutlineColor;
half4 _UnderlayColor;
sampler2D _FaceTex;
sampler2D _MainTex;
sampler2D _OutlineTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float4 u_xlat2;
float4 u_xlat3;
float3 u_xlat4;
float u_xlat5;
float3 u_xlat7;
float2 u_xlat10;
float u_xlat15;
float u_xlat16;
bool u_xlatb16;

    u_xlat0.xy = input.vertex.xy + float2(_VertexOffsetX,_VertexOffsetY);
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
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    u_xlat10.x = (-_OutlineWidth) * _ScaleRatioA + 1.0;
    u_xlat10.x = (-_OutlineSoftness) * _ScaleRatioA + u_xlat10.x;
    u_xlat15 = (-_GlowOffset) * _ScaleRatioB + 1.0;
    u_xlat15 = (-_GlowOuter) * _ScaleRatioB + u_xlat15;
    u_xlat10.x = min(u_xlat15, u_xlat10.x);
    u_xlat2.xy = _ScreenParams.yy * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).xy;
    u_xlat2.xy = float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).xy * _ScreenParams.xx + u_xlat2.xy;
    u_xlat2.xy = abs(u_xlat2.xy) * float2(_ScaleX,_ScaleY);
    u_xlat2.xy = u_xlat2.ww / u_xlat2.xy;
    u_xlat15 = dot(u_xlat2.xy, u_xlat2.xy);
    u_xlat2.xy = float2(_MaskSoftnessX,_MaskSoftnessY) * float2(0.25,0.25) + u_xlat2.xy;
    output.mask.zw = float2(0.25,0.25) / u_xlat2.xy;
    u_xlat15 = rsqrt(u_xlat15);
    u_xlat16 = abs(input.uv1.y) * _GradientScale;
    u_xlat2.x = _Sharpness + 1.0;
    u_xlat16 = u_xlat16 * u_xlat2.x;
    u_xlat2.x = u_xlat15 * u_xlat16;
    u_xlat7.x = (-_PerspectiveFilter) + 1.0;
    u_xlat7.x = u_xlat7.x * abs(u_xlat2.x);
    u_xlat15 = u_xlat15 * u_xlat16 + (-u_xlat7.x);
    u_xlat3.x = dot(input.normal.xyz, float4(unity_WorldToObject[0][0],unity_WorldToObject[1][0],unity_WorldToObject[2][0],unity_WorldToObject[3][0]).xyz);
    u_xlat3.y = dot(input.normal.xyz, float4(unity_WorldToObject[0][1],unity_WorldToObject[1][1],unity_WorldToObject[2][1],unity_WorldToObject[3][1]).xyz);
    u_xlat3.z = dot(input.normal.xyz, float4(unity_WorldToObject[0][2],unity_WorldToObject[1][2],unity_WorldToObject[2][2],unity_WorldToObject[3][2]).xyz);
    u_xlat16 = dot(u_xlat3.xyz, u_xlat3.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat3.xyz = (float3)(u_xlat16) * u_xlat3.xyz;
    u_xlat16 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat16 = rsqrt(u_xlat16);
    u_xlat4.xyz = (float3)(u_xlat16) * u_xlat1.xyz;
    u_xlat16 = dot(u_xlat3.xyz, u_xlat4.xyz);
    u_xlat15 = abs(u_xlat16) * u_xlat15 + u_xlat7.x;
    u_xlatb16 = all(float4(UNITY_MATRIX_P[0][3],UNITY_MATRIX_P[1][3],UNITY_MATRIX_P[2][3],UNITY_MATRIX_P[3][3]).w==0.0);
    u_xlat7.x = (u_xlatb16) ? u_xlat15 : u_xlat2.x;
    u_xlat15 = 0.5 / u_xlat7.x;
    u_xlat10.x = u_xlat10.x * 0.5 + (-u_xlat15);
    u_xlatb16 = 0.0>=input.uv1.y;
    u_xlat16 = u_xlatb16 ? 1.0 : float(0.0);
    u_xlat2.x = (-_WeightNormal) + _WeightBold;
    u_xlat16 = u_xlat16 * u_xlat2.x + _WeightNormal;
    u_xlat16 = u_xlat16 * 0.25 + _FaceDilate;
    u_xlat16 = u_xlat16 * _ScaleRatioA;
    output.world.x = (-u_xlat16) * 0.5 + u_xlat10.x;
    u_xlat7.z = u_xlat16 * 0.5;
    u_xlat10.x = (-u_xlat16) * 0.5 + 0.5;
    output.world.yw = u_xlat7.xz;
    output.world.z = u_xlat15 + u_xlat10.x;
    u_xlat3 = max(_ClipRect, float4(-2e+10,-2e+10,-2e+10,-2e+10));
    u_xlat3 = min(u_xlat3, float4(2e+10,2e+10,2e+10,2e+10));
    u_xlat0.xy = u_xlat0.xy * float2(2.0,2.0) + (-u_xlat3.xy);
    output.mask.xy = (-u_xlat3.zw) + u_xlat0.xy;
    u_xlat0.xyw = u_xlat1.yyy * float4(_EnvMatrix[0][1],_EnvMatrix[1][1],_EnvMatrix[2][1],_EnvMatrix[3][1]).xyz;
    u_xlat0.xyw = float4(_EnvMatrix[0][0],_EnvMatrix[1][0],_EnvMatrix[2][0],_EnvMatrix[3][0]).xyz * u_xlat1.xxx + u_xlat0.xyw;
    output.uv3.xyz = float4(_EnvMatrix[0][2],_EnvMatrix[1][2],_EnvMatrix[2][2],_EnvMatrix[3][2]).xyz * u_xlat1.zzz + u_xlat0.xyw;
    u_xlat1 = float4(_UnderlaySoftness,_UnderlayDilate,_UnderlayOffsetX,_UnderlayOffsetY) * (float4)(float4(_ScaleRatioC, _ScaleRatioC, _ScaleRatioC, _ScaleRatioC));
    u_xlat0.x = u_xlat1.x * u_xlat7.x + 1.0;
    u_xlat0.x = u_xlat7.x / u_xlat0.x;
    u_xlat5 = u_xlat10.x * u_xlat0.x + -0.5;
    u_xlat10.x = u_xlat0.x * u_xlat1.y;
    u_xlat1.xy = (-u_xlat1.zw) * (float2)(_GradientScale);
    u_xlat1.xy = u_xlat1.xy / float2(_TextureWidth,_TextureHeight);
    output.uv4.xy = u_xlat1.xy + input.uv.xy;
    output.uv4.z = u_xlat0.x;
    output.uv4.w = (-u_xlat10.x) * 0.5 + u_xlat5;
    u_xlat0.xyz = _UnderlayColor.www * _UnderlayColor.xyz;
    output.color1.xyz = u_xlat0.xyz;
    output.color1.w = _UnderlayColor.w;
    u_xlat0.x = input.uv1.x * 0.000244140625;
    u_xlat10.x = floor(u_xlat0.x);
    u_xlat10.y = (-u_xlat10.x) * 4096.0 + input.uv1.x;
    u_xlat0.xy = u_xlat10.xy * float2(0.001953125,0.001953125);
    output.uv5.xy = u_xlat0.xy * _FaceTex_ST.xy + _FaceTex_ST.zw;
    output.uv5.zw = u_xlat0.xy * _OutlineTex_ST.xy + _OutlineTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
float4 u_xlat3;
half4 u_xlat16_3;
half3 u_xlat16_4;
half4 u_xlat16_5;
float3 u_xlat6;
half u_xlat16_6;
bool u_xlatb6;
half u_xlat16_7;
half u_xlat16_10;
float u_xlat12;
half u_xlat16_13;

    u_xlat0.x = _OutlineWidth * _ScaleRatioA;
    u_xlat0.x = u_xlat0.x * input.world.y;
    u_xlat16_1 = min(u_xlat0.x, 1.0);
    u_xlat16_7 = u_xlat0.x * 0.5;
    u_xlat16_1 = sqrt(u_xlat16_1);
    u_xlat16_0.x = tex2D(_MainTex, input.uv.xy).w;
    u_xlat0.x = (-u_xlat16_0.x) + input.world.z;
    u_xlat16_13 = u_xlat0.x * input.world.y + u_xlat16_7;
    u_xlat16_13 = clamp(u_xlat16_13, 0.0, 1.0);
    u_xlat16_7 = u_xlat0.x * input.world.y + (-u_xlat16_7);
    u_xlat16_1 = u_xlat16_1 * u_xlat16_13;
    u_xlat6.xy = float2(_OutlineUVSpeedX,_OutlineUVSpeedY) * _Time.yy + input.uv5.zw;
    u_xlat16_2 = tex2D(_OutlineTex, u_xlat6.xy);
    u_xlat3 = u_xlat16_2 * _OutlineColor;
    u_xlat16_4.xyz = input.color.xyz * _FaceColor.xyz;
    u_xlat6.xy = float2(_FaceUVSpeedX,_FaceUVSpeedY) * _Time.yy + input.uv5.xy;
    u_xlat16_5 = tex2D(_FaceTex, u_xlat6.xy);
    u_xlat6.xyz = u_xlat16_4.xyz * u_xlat16_5.xyz;
    u_xlat2.x = u_xlat16_5.w * _FaceColor.w;
    u_xlat16_4.xyz = u_xlat6.xyz * u_xlat2.xxx;
    u_xlat16_3.xyz = u_xlat3.xyz * u_xlat3.www + (-u_xlat16_4.xyz);
    u_xlat16_3.w = _OutlineColor.w * u_xlat16_2.w + (-u_xlat2.x);
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_3;
    u_xlat16_2.xyz = u_xlat6.xyz * u_xlat2.xxx + u_xlat16_3.xyz;
    u_xlat16_2.w = _FaceColor.w * u_xlat16_5.w + u_xlat16_3.w;
    u_xlat0.y = _OutlineSoftness * _ScaleRatioA;
    u_xlat0.xz = u_xlat0.xy * input.world.yy;
    u_xlat16_1 = u_xlat0.y * input.world.y + 1.0;
    u_xlat16_7 = u_xlat0.z * 0.5 + u_xlat16_7;
    u_xlat16_1 = u_xlat16_7 / u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_3 = (float4)(u_xlat16_1) * u_xlat16_2;
    u_xlat16_1 = (-u_xlat16_2.w) * u_xlat16_1 + 1.0;
    u_xlat16_6 = tex2D(_MainTex, input.uv4.xy).w;
    u_xlat6.x = u_xlat16_6 * input.uv4.z + (-input.uv4.w);
    u_xlat6.x = clamp(u_xlat6.x, 0.0, 1.0);
    u_xlat2 = u_xlat6.xxxx * input.color1;
    u_xlat1 = u_xlat2 * (float4)(u_xlat16_1) + u_xlat16_3;
    u_xlat6.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat6.xy = u_xlat6.xy + -abs(input.mask.xy);
    u_xlat6.xy = u_xlat6.xy * input.mask.zw;
    u_xlat6.xy = clamp(u_xlat6.xy, 0.0, 1.0);
    u_xlat16_4.x = u_xlat6.y * u_xlat6.x;
    u_xlat16_10 = u_xlat1.w * u_xlat16_4.x + -0.00100000005;
    u_xlatb6 = u_xlat16_10<0.0;
    if(u_xlatb6){clip(-1);}
    u_xlat6.x = _GlowOffset * _ScaleRatioB;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat0.x = (-u_xlat6.x) * 0.5 + u_xlat0.x;
    u_xlatb6 = u_xlat0.x>=0.0;
    u_xlat6.x = u_xlatb6 ? 1.0 : float(0.0);
    u_xlat12 = _GlowOuter * _ScaleRatioB + (-_GlowInner);
    u_xlat6.x = u_xlat6.x * u_xlat12 + _GlowInner;
    u_xlat6.x = u_xlat6.x * input.world.y;
    u_xlat12 = u_xlat6.x * 0.5 + 1.0;
    u_xlat6.x = u_xlat6.x * 0.5;
    u_xlat6.x = min(u_xlat6.x, 1.0);
    u_xlat6.x = sqrt(u_xlat6.x);
    u_xlat0.x = u_xlat0.x / u_xlat12;
    u_xlat0.x = min(abs(u_xlat0.x), 1.0);
    u_xlat0.x = log2(u_xlat0.x);
    u_xlat0.x = u_xlat0.x * _GlowPower;
    u_xlat0.x = exp2(u_xlat0.x);
    u_xlat0.x = (-u_xlat0.x) + 1.0;
    u_xlat0.x = u_xlat6.x * u_xlat0.x;
    u_xlat0.x = dot(_GlowColor.ww, u_xlat0.xx);
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat1.xyz = _GlowColor.xyz * u_xlat0.xxx + u_xlat1.xyz;
    u_xlat16_0 = u_xlat1 * u_xlat16_4.xxxx;
    result = u_xlat16_0 * input.color.wwww;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
