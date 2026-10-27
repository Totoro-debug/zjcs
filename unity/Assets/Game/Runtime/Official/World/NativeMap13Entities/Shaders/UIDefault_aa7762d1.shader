Shader "Zjcs/Official/World/Map13/Entity/UIDefault_aa7762d1" {
Properties {
_MainTex ("Sprite Texture", 2D) = "white" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
_StencilComp ("Stencil Comparison", Float) = 8.0
_Stencil ("Stencil ID", Float) = 0.0
_StencilOp ("Stencil Operation", Float) = 0.0
_StencilWriteMask ("Stencil Write Mask", Float) = 255.0
_StencilReadMask ("Stencil Read Mask", Float) = 255.0
_ColorMask ("Color Mask", Float) = 15.0
[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0.0
}
SubShader {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
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
#pragma multi_compile_local __ UNITY_UI_ALPHACLIP
#pragma multi_compile_local __ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#if !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
float4 _MainTex_ST;
half4 _Color;
half4 _TextureSampleAdd;
int _UIVertexColorAlwaysGammaSpace;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool3 u_xlatb0;
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
    u_xlat16_2.xyz = input.color.xyz * float3(0.265884995, 0.265884995, 0.265884995) + float3(0.736584008, 0.736584008, 0.736584008);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(-0.00980184041, -0.00980184041, -0.00980184041);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(0.00319697009, 0.00319697009, 0.00319697009);
    u_xlat16_3.xyz = input.color.xyz * float3(0.0849710032, 0.0849710032, 0.0849710032) + float3(-0.000163029006, -0.000163029006, -0.000163029006);
    u_xlatb0.xyz = (input.color.xyzx < float4(0.0725490004, 0.0725490004, 0.0725490004, 0.0)).xyz;
    {
        float3 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb0.x) ? u_xlat16_3.x : u_xlat16_2.x;
        hlslcc_movcTemp.y = (u_xlatb0.y) ? u_xlat16_3.y : u_xlat16_2.y;
        hlslcc_movcTemp.z = (u_xlatb0.z) ? u_xlat16_3.z : u_xlat16_2.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlat1.xyz = (int(_UIVertexColorAlwaysGammaSpace) != 0) ? u_xlat16_2.xyz : input.color.xyz;
    u_xlat1.w = input.color.w;
    u_xlat1 = u_xlat1 * _Color;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
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
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;

    u_xlat16_0.x = input.color.w * 255.0;
    u_xlat16_0.x = round(u_xlat16_0.x);
    u_xlat16_0.w = u_xlat16_0.x * 0.00392156886;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat1 = u_xlat16_1 + _TextureSampleAdd;
    u_xlat16_0.xyz = input.color.xyz;
    u_xlat0 = u_xlat16_0 * u_xlat1;
    result.xyz = u_xlat0.www * u_xlat0.xyz;
    result.w = u_xlat0.w;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
float4 _MainTex_ST;
half4 _Color;
half4 _TextureSampleAdd;
int _UIVertexColorAlwaysGammaSpace;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool3 u_xlatb0;
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
    u_xlat16_2.xyz = input.color.xyz * float3(0.265884995, 0.265884995, 0.265884995) + float3(0.736584008, 0.736584008, 0.736584008);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(-0.00980184041, -0.00980184041, -0.00980184041);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(0.00319697009, 0.00319697009, 0.00319697009);
    u_xlat16_3.xyz = input.color.xyz * float3(0.0849710032, 0.0849710032, 0.0849710032) + float3(-0.000163029006, -0.000163029006, -0.000163029006);
    u_xlatb0.xyz = (input.color.xyzx < float4(0.0725490004, 0.0725490004, 0.0725490004, 0.0)).xyz;
    {
        float3 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb0.x) ? u_xlat16_3.x : u_xlat16_2.x;
        hlslcc_movcTemp.y = (u_xlatb0.y) ? u_xlat16_3.y : u_xlat16_2.y;
        hlslcc_movcTemp.z = (u_xlatb0.z) ? u_xlat16_3.z : u_xlat16_2.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlat1.xyz = (int(_UIVertexColorAlwaysGammaSpace) != 0) ? u_xlat16_2.xyz : input.color.xyz;
    u_xlat1.w = input.color.w;
    u_xlat1 = u_xlat1 * _Color;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
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
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half u_xlat16_2;
bool u_xlatb3;

    u_xlat16_0.x = input.color.w * 255.0;
    u_xlat16_0.x = round(u_xlat16_0.x);
    u_xlat16_0.w = u_xlat16_0.x * 0.00392156886;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat1 = u_xlat16_1 + _TextureSampleAdd;
    u_xlat16_2 = u_xlat16_0.w * u_xlat1.w + -0.00100000005;
    u_xlatb3 = u_xlat16_2<0.0;
    if(u_xlatb3){clip(-1);}
    u_xlat16_0.xyz = input.color.xyz;
    u_xlat0 = u_xlat16_0 * u_xlat1;
    result.xyz = u_xlat0.www * u_xlat0.xyz;
    result.w = u_xlat0.w;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
float4 _MainTex_ST;
half4 _Color;
half4 _TextureSampleAdd;
int _UIVertexColorAlwaysGammaSpace;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool3 u_xlatb0;
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
    u_xlat16_2.xyz = input.color.xyz * float3(0.265884995, 0.265884995, 0.265884995) + float3(0.736584008, 0.736584008, 0.736584008);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(-0.00980184041, -0.00980184041, -0.00980184041);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(0.00319697009, 0.00319697009, 0.00319697009);
    u_xlat16_3.xyz = input.color.xyz * float3(0.0849710032, 0.0849710032, 0.0849710032) + float3(-0.000163029006, -0.000163029006, -0.000163029006);
    u_xlatb0.xyz = (input.color.xyzx < float4(0.0725490004, 0.0725490004, 0.0725490004, 0.0)).xyz;
    {
        float3 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb0.x) ? u_xlat16_3.x : u_xlat16_2.x;
        hlslcc_movcTemp.y = (u_xlatb0.y) ? u_xlat16_3.y : u_xlat16_2.y;
        hlslcc_movcTemp.z = (u_xlatb0.z) ? u_xlat16_3.z : u_xlat16_2.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlat1.xyz = (int(_UIVertexColorAlwaysGammaSpace) != 0) ? u_xlat16_2.xyz : input.color.xyz;
    u_xlat1.w = input.color.w;
    u_xlat1 = u_xlat1 * _Color;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
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
float4 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
float4 u_xlat2;
half4 u_xlat16_2;
half u_xlat16_4;

    u_xlat0.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat0.xy = u_xlat0.xy + -abs(input.mask.xy);
    u_xlat0.xy = u_xlat0.xy * input.mask.zw;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat16_1 = u_xlat0.y * u_xlat0.x;
    u_xlat16_4 = input.color.w * 255.0;
    u_xlat16_4 = round(u_xlat16_4);
    u_xlat16_0.w = u_xlat16_4 * 0.00392156886;
    u_xlat16_2 = tex2D(_MainTex, input.uv.xy);
    u_xlat2 = u_xlat16_2 + _TextureSampleAdd;
    u_xlat16_0.xyz = input.color.xyz;
    u_xlat0 = u_xlat16_0 * u_xlat2;
    u_xlat16_1 = u_xlat16_1 * u_xlat0.w;
    result.xyz = u_xlat0.xyz * float3(u_xlat16_1,u_xlat16_1,u_xlat16_1);
    result.w = u_xlat16_1;
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; };

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
float4 _MainTex_ST;
half4 _Color;
half4 _TextureSampleAdd;
int _UIVertexColorAlwaysGammaSpace;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool3 u_xlatb0;
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
    u_xlat16_2.xyz = input.color.xyz * float3(0.265884995, 0.265884995, 0.265884995) + float3(0.736584008, 0.736584008, 0.736584008);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(-0.00980184041, -0.00980184041, -0.00980184041);
    u_xlat16_2.xyz = input.color.xyz * u_xlat16_2.xyz + float3(0.00319697009, 0.00319697009, 0.00319697009);
    u_xlat16_3.xyz = input.color.xyz * float3(0.0849710032, 0.0849710032, 0.0849710032) + float3(-0.000163029006, -0.000163029006, -0.000163029006);
    u_xlatb0.xyz = (input.color.xyzx < float4(0.0725490004, 0.0725490004, 0.0725490004, 0.0)).xyz;
    {
        float3 hlslcc_movcTemp = u_xlat16_2;
        hlslcc_movcTemp.x = (u_xlatb0.x) ? u_xlat16_3.x : u_xlat16_2.x;
        hlslcc_movcTemp.y = (u_xlatb0.y) ? u_xlat16_3.y : u_xlat16_2.y;
        hlslcc_movcTemp.z = (u_xlatb0.z) ? u_xlat16_3.z : u_xlat16_2.z;
        u_xlat16_2 = hlslcc_movcTemp;
    }
    u_xlat1.xyz = (int(_UIVertexColorAlwaysGammaSpace) != 0) ? u_xlat16_2.xyz : input.color.xyz;
    u_xlat1.w = input.color.w;
    u_xlat1 = u_xlat1 * _Color;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
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
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0.x = input.color.w * 255.0;
    u_xlat16_0.x = round(u_xlat16_0.x);
    u_xlat16_0.w = u_xlat16_0.x * 0.00392156886;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat1 = u_xlat16_1 + _TextureSampleAdd;
    u_xlat16_0.xyz = input.color.xyz;
    u_xlat0 = u_xlat16_0 * u_xlat1;
    u_xlat1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat1.xy = u_xlat1.xy + -abs(input.mask.xy);
    u_xlat1.xy = u_xlat1.xy * input.mask.zw;
    u_xlat1.xy = clamp(u_xlat1.xy, 0.0, 1.0);
    u_xlat16_2 = u_xlat1.y * u_xlat1.x;
    u_xlat16_5 = u_xlat0.w * u_xlat16_2 + -0.00100000005;
    u_xlat16_2 = u_xlat0.w * u_xlat16_2;
    result.xyz = u_xlat0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2);
    result.w = u_xlat16_2;
    u_xlatb1 = u_xlat16_5<0.0;
    if(u_xlatb1){clip(-1);}
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
