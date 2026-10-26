Shader "Zjcs/Combat/HudCapsuleBorderFlow" {
Properties {
_MainTex ("_MainTex", 2D) = "white" {}
_FlowColor ("_FlowColor", Color) = (1.0,0.9800000190734863,0.0,1.0)
_Rect ("_Rect", Vector) = (89.0,50.0,0.0,0.0)
_BorderWidth ("_BorderWidth", Float) = 3.0
_CornerRadius ("_CornerRadius", Float) = 1.0
_FillLength ("_FillLength", Float) = 0.2800000011920929
_FlowSpeed ("_FlowSpeed", Float) = 0.6000000238418579
_Offset ("_Offset", Float) = 0.0
_UseManualOffset ("_UseManualOffset", Float) = 0.0
_SoftEdge ("_SoftEdge", Float) = 2.0
_FlowSoftness ("_FlowSoftness", Float) = 0.05999999865889549
_StencilComp ("_StencilComp", Float) = 8.0
_Stencil ("_Stencil", Float) = 0.0
_StencilOp ("_StencilOp", Float) = 0.0
_StencilWriteMask ("_StencilWriteMask", Float) = 255.0
_StencilReadMask ("_StencilReadMask", Float) = 255.0
_ColorMask ("_ColorMask", Float) = 15.0
_UseUIAlphaClip ("_UseUIAlphaClip", Float) = 0.0
}
SubShader {
Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
Cull Off
ZWrite Off
ZTest [unity_GUIZTestMode]
Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
ColorMask [_ColorMask]
Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
Pass {
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
#include "UnityCG.cginc"
struct Input { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
struct Varying { float4 position : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; half4 mask : TEXCOORD2; };
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _Rect;
half _BorderWidth;
half _CornerRadius;
half _FillLength;
half _FlowSoftness;
half _FlowSpeed;
half _Offset;
half _SoftEdge;
half _UseManualOffset;
half4 _ClipRect;
half4 _FlowColor;
#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
float u_xlat3;
half2 u_xlat16_4;
float u_xlat5;
float u_xlat7;
bool u_xlatb7;
bool u_xlatb8;
half u_xlat16_9;
float2 u_xlat10;
float u_xlat12;
float u_xlat17;
bool u_xlatb17;

    u_xlat0.xy = _Rect.yx * float2(0.5, 0.5);
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat16_1 = _BorderWidth + _SoftEdge;
    u_xlat0.xy = u_xlat0.xy + (-float2(u_xlat16_1,u_xlat16_1));
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat10.xy = input.uv.yx + float2(-0.5, -0.5);
    u_xlat10.xy = u_xlat10.xy * _Rect.yx;
    u_xlat2.xy = u_xlat10.xy / u_xlat0.xy;
    u_xlat12 = max(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat12 = float(1.0) / u_xlat12;
    u_xlat17 = min(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat12 = u_xlat12 * u_xlat17;
    u_xlat17 = u_xlat12 * u_xlat12;
    u_xlat3 = u_xlat17 * 0.0208350997 + -0.0851330012;
    u_xlat3 = u_xlat17 * u_xlat3 + 0.180141002;
    u_xlat3 = u_xlat17 * u_xlat3 + -0.330299497;
    u_xlat17 = u_xlat17 * u_xlat3 + 0.999866009;
    u_xlat3 = u_xlat17 * u_xlat12;
    u_xlat3 = u_xlat3 * -2.0 + 1.57079637;
    u_xlatb8 = abs(u_xlat2.y)<abs(u_xlat2.x);
    u_xlat3 = u_xlatb8 ? u_xlat3 : float(0.0);
    u_xlat12 = u_xlat12 * u_xlat17 + u_xlat3;
    u_xlatb17 = u_xlat2.y<(-u_xlat2.y);
    u_xlat17 = u_xlatb17 ? -3.14159274 : float(0.0);
    u_xlat12 = u_xlat17 + u_xlat12;
    u_xlat17 = min(u_xlat2.y, u_xlat2.x);
    u_xlat2.x = max(u_xlat2.y, u_xlat2.x);
    u_xlatb2 = u_xlat2.x>=(-u_xlat2.x);
    u_xlatb7 = u_xlat17<(-u_xlat17);
    u_xlatb2 = u_xlatb2 && u_xlatb7;
    u_xlat2.x = (u_xlatb2) ? (-u_xlat12) : u_xlat12;
    u_xlat2.x = u_xlat2.x * 0.159154937 + 0.5;
    u_xlat7 = _Time.y * _FlowSpeed;
    u_xlat7 = frac(u_xlat7);
    u_xlat12 = (-u_xlat7) + _Offset;
    u_xlat7 = _UseManualOffset * u_xlat12 + u_xlat7;
    u_xlat2.x = u_xlat7 + u_xlat2.x;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlat2.x = u_xlat2.x + (-_FillLength);
    u_xlat7 = float(1.0) / (-_FlowSoftness);
    u_xlat2.x = u_xlat7 * u_xlat2.x;
    u_xlat2.x = clamp(u_xlat2.x, 0.0, 1.0);
    u_xlat7 = u_xlat2.x * -2.0 + 3.0;
    u_xlat2.x = u_xlat2.x * u_xlat2.x;
    u_xlat2.x = u_xlat2.x * u_xlat7;
    u_xlat7 = min(u_xlat0.x, u_xlat0.y);
    u_xlat0.xy = (-float2(u_xlat7,u_xlat7)) * float2(float2(_CornerRadius, _CornerRadius)) + u_xlat0.yx;
    u_xlat0.xy = (-u_xlat0.xy) + abs(u_xlat10.yx);
    u_xlat10.xy = max(u_xlat0.xy, float2(0.0, 0.0));
    u_xlat0.x = max(u_xlat0.y, u_xlat0.x);
    u_xlat0.x = min(u_xlat0.x, 0.0);
    u_xlat5 = dot(u_xlat10.xy, u_xlat10.xy);
    u_xlat5 = sqrt(u_xlat5);
    u_xlat0.x = u_xlat0.x + u_xlat5;
    u_xlat0.x = (-u_xlat7) * _CornerRadius + u_xlat0.x;
    u_xlat16_1 = _BorderWidth + (-_SoftEdge);
    u_xlat0.x = abs(u_xlat0.x) + (-u_xlat16_1);
    u_xlat5 = (-u_xlat16_1) + _BorderWidth;
    u_xlat5 = float(1.0) / u_xlat5;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat5 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = (-u_xlat5) * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat2.x * u_xlat0.x;
    u_xlat1 = input.color * _FlowColor;
    u_xlat0.x = u_xlat0.x * u_xlat1.w;
    result.xyz = u_xlat1.xyz;
    u_xlat16_4.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_4.xy = u_xlat16_4.xy + -abs(input.mask.xy);
    u_xlat16_4.xy = u_xlat16_4.xy * input.mask.zw;
    u_xlat16_4.xy = clamp(u_xlat16_4.xy, 0.0, 1.0);
    u_xlat16_4.x = u_xlat16_4.y * u_xlat16_4.x;
    u_xlat16_9 = u_xlat0.x * u_xlat16_4.x + -0.00100000005;
    u_xlat16_4.x = u_xlat0.x * u_xlat16_4.x;
    result.w = u_xlat16_4.x;
    u_xlatb0 = u_xlat16_9<0.0;
    if(u_xlatb0){clip(-1);}
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
half u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
float u_xlat3;
half u_xlat16_4;
float u_xlat5;
float u_xlat7;
bool u_xlatb7;
bool u_xlatb8;
float2 u_xlat10;
float u_xlat12;
float u_xlat17;
bool u_xlatb17;

    u_xlat0.xy = _Rect.yx * float2(0.5, 0.5);
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat16_1 = _BorderWidth + _SoftEdge;
    u_xlat0.xy = u_xlat0.xy + (-float2(u_xlat16_1,u_xlat16_1));
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat10.xy = input.uv.yx + float2(-0.5, -0.5);
    u_xlat10.xy = u_xlat10.xy * _Rect.yx;
    u_xlat2.xy = u_xlat10.xy / u_xlat0.xy;
    u_xlat12 = max(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat12 = float(1.0) / u_xlat12;
    u_xlat17 = min(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat12 = u_xlat12 * u_xlat17;
    u_xlat17 = u_xlat12 * u_xlat12;
    u_xlat3 = u_xlat17 * 0.0208350997 + -0.0851330012;
    u_xlat3 = u_xlat17 * u_xlat3 + 0.180141002;
    u_xlat3 = u_xlat17 * u_xlat3 + -0.330299497;
    u_xlat17 = u_xlat17 * u_xlat3 + 0.999866009;
    u_xlat3 = u_xlat17 * u_xlat12;
    u_xlat3 = u_xlat3 * -2.0 + 1.57079637;
    u_xlatb8 = abs(u_xlat2.y)<abs(u_xlat2.x);
    u_xlat3 = u_xlatb8 ? u_xlat3 : float(0.0);
    u_xlat12 = u_xlat12 * u_xlat17 + u_xlat3;
    u_xlatb17 = u_xlat2.y<(-u_xlat2.y);
    u_xlat17 = u_xlatb17 ? -3.14159274 : float(0.0);
    u_xlat12 = u_xlat17 + u_xlat12;
    u_xlat17 = min(u_xlat2.y, u_xlat2.x);
    u_xlat2.x = max(u_xlat2.y, u_xlat2.x);
    u_xlatb2 = u_xlat2.x>=(-u_xlat2.x);
    u_xlatb7 = u_xlat17<(-u_xlat17);
    u_xlatb2 = u_xlatb2 && u_xlatb7;
    u_xlat2.x = (u_xlatb2) ? (-u_xlat12) : u_xlat12;
    u_xlat2.x = u_xlat2.x * 0.159154937 + 0.5;
    u_xlat7 = _Time.y * _FlowSpeed;
    u_xlat7 = frac(u_xlat7);
    u_xlat12 = (-u_xlat7) + _Offset;
    u_xlat7 = _UseManualOffset * u_xlat12 + u_xlat7;
    u_xlat2.x = u_xlat7 + u_xlat2.x;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlat2.x = u_xlat2.x + (-_FillLength);
    u_xlat7 = float(1.0) / (-_FlowSoftness);
    u_xlat2.x = u_xlat7 * u_xlat2.x;
    u_xlat2.x = clamp(u_xlat2.x, 0.0, 1.0);
    u_xlat7 = u_xlat2.x * -2.0 + 3.0;
    u_xlat2.x = u_xlat2.x * u_xlat2.x;
    u_xlat2.x = u_xlat2.x * u_xlat7;
    u_xlat7 = min(u_xlat0.x, u_xlat0.y);
    u_xlat0.xy = (-float2(u_xlat7,u_xlat7)) * float2(float2(_CornerRadius, _CornerRadius)) + u_xlat0.yx;
    u_xlat0.xy = (-u_xlat0.xy) + abs(u_xlat10.yx);
    u_xlat10.xy = max(u_xlat0.xy, float2(0.0, 0.0));
    u_xlat0.x = max(u_xlat0.y, u_xlat0.x);
    u_xlat0.x = min(u_xlat0.x, 0.0);
    u_xlat5 = dot(u_xlat10.xy, u_xlat10.xy);
    u_xlat5 = sqrt(u_xlat5);
    u_xlat0.x = u_xlat0.x + u_xlat5;
    u_xlat0.x = (-u_xlat7) * _CornerRadius + u_xlat0.x;
    u_xlat16_1 = _BorderWidth + (-_SoftEdge);
    u_xlat0.x = abs(u_xlat0.x) + (-u_xlat16_1);
    u_xlat5 = (-u_xlat16_1) + _BorderWidth;
    u_xlat5 = float(1.0) / u_xlat5;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat5 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = (-u_xlat5) * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat2.x * u_xlat0.x;
    u_xlat1 = input.color * _FlowColor;
    u_xlat16_4 = u_xlat1.w * u_xlat0.x + -0.00100000005;
    u_xlat1.w = u_xlat0.x * u_xlat1.w;
    result = u_xlat1;
    u_xlatb0 = u_xlat16_4<0.0;
    if(u_xlatb0){clip(-1);}
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
float4 u_xlat1;
half u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
float u_xlat3;
half2 u_xlat16_4;
float u_xlat5;
float u_xlat7;
bool u_xlatb7;
bool u_xlatb8;
float2 u_xlat10;
float u_xlat12;
float u_xlat17;
bool u_xlatb17;

    u_xlat0.xy = _Rect.yx * float2(0.5, 0.5);
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat16_1 = _BorderWidth + _SoftEdge;
    u_xlat0.xy = u_xlat0.xy + (-float2(u_xlat16_1,u_xlat16_1));
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat10.xy = input.uv.yx + float2(-0.5, -0.5);
    u_xlat10.xy = u_xlat10.xy * _Rect.yx;
    u_xlat2.xy = u_xlat10.xy / u_xlat0.xy;
    u_xlat12 = max(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat12 = float(1.0) / u_xlat12;
    u_xlat17 = min(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat12 = u_xlat12 * u_xlat17;
    u_xlat17 = u_xlat12 * u_xlat12;
    u_xlat3 = u_xlat17 * 0.0208350997 + -0.0851330012;
    u_xlat3 = u_xlat17 * u_xlat3 + 0.180141002;
    u_xlat3 = u_xlat17 * u_xlat3 + -0.330299497;
    u_xlat17 = u_xlat17 * u_xlat3 + 0.999866009;
    u_xlat3 = u_xlat17 * u_xlat12;
    u_xlat3 = u_xlat3 * -2.0 + 1.57079637;
    u_xlatb8 = abs(u_xlat2.y)<abs(u_xlat2.x);
    u_xlat3 = u_xlatb8 ? u_xlat3 : float(0.0);
    u_xlat12 = u_xlat12 * u_xlat17 + u_xlat3;
    u_xlatb17 = u_xlat2.y<(-u_xlat2.y);
    u_xlat17 = u_xlatb17 ? -3.14159274 : float(0.0);
    u_xlat12 = u_xlat17 + u_xlat12;
    u_xlat17 = min(u_xlat2.y, u_xlat2.x);
    u_xlat2.x = max(u_xlat2.y, u_xlat2.x);
    u_xlatb2 = u_xlat2.x>=(-u_xlat2.x);
    u_xlatb7 = u_xlat17<(-u_xlat17);
    u_xlatb2 = u_xlatb2 && u_xlatb7;
    u_xlat2.x = (u_xlatb2) ? (-u_xlat12) : u_xlat12;
    u_xlat2.x = u_xlat2.x * 0.159154937 + 0.5;
    u_xlat7 = _Time.y * _FlowSpeed;
    u_xlat7 = frac(u_xlat7);
    u_xlat12 = (-u_xlat7) + _Offset;
    u_xlat7 = _UseManualOffset * u_xlat12 + u_xlat7;
    u_xlat2.x = u_xlat7 + u_xlat2.x;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlat2.x = u_xlat2.x + (-_FillLength);
    u_xlat7 = float(1.0) / (-_FlowSoftness);
    u_xlat2.x = u_xlat7 * u_xlat2.x;
    u_xlat2.x = clamp(u_xlat2.x, 0.0, 1.0);
    u_xlat7 = u_xlat2.x * -2.0 + 3.0;
    u_xlat2.x = u_xlat2.x * u_xlat2.x;
    u_xlat2.x = u_xlat2.x * u_xlat7;
    u_xlat7 = min(u_xlat0.x, u_xlat0.y);
    u_xlat0.xy = (-float2(u_xlat7,u_xlat7)) * float2(float2(_CornerRadius, _CornerRadius)) + u_xlat0.yx;
    u_xlat0.xy = (-u_xlat0.xy) + abs(u_xlat10.yx);
    u_xlat10.xy = max(u_xlat0.xy, float2(0.0, 0.0));
    u_xlat0.x = max(u_xlat0.y, u_xlat0.x);
    u_xlat0.x = min(u_xlat0.x, 0.0);
    u_xlat5 = dot(u_xlat10.xy, u_xlat10.xy);
    u_xlat5 = sqrt(u_xlat5);
    u_xlat0.x = u_xlat0.x + u_xlat5;
    u_xlat0.x = (-u_xlat7) * _CornerRadius + u_xlat0.x;
    u_xlat16_1 = _BorderWidth + (-_SoftEdge);
    u_xlat0.x = abs(u_xlat0.x) + (-u_xlat16_1);
    u_xlat5 = (-u_xlat16_1) + _BorderWidth;
    u_xlat5 = float(1.0) / u_xlat5;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat5 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = (-u_xlat5) * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat2.x * u_xlat0.x;
    u_xlat1 = input.color * _FlowColor;
    u_xlat0.x = u_xlat0.x * u_xlat1.w;
    result.xyz = u_xlat1.xyz;
    u_xlat16_4.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat16_4.xy = u_xlat16_4.xy + -abs(input.mask.xy);
    u_xlat16_4.xy = u_xlat16_4.xy * input.mask.zw;
    u_xlat16_4.xy = clamp(u_xlat16_4.xy, 0.0, 1.0);
    u_xlat16_4.x = u_xlat16_4.y * u_xlat16_4.x;
    result.w = u_xlat0.x * u_xlat16_4.x;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
float2 u_xlat7;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.world.xyz = u_xlat0.xyz;
    u_xlat0 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    output.color = input.color;
    output.uv.xy = input.uv.xy;
    output.world.w = 0.0;
    u_xlat7.x = _ScreenParams.x * float4(UNITY_MATRIX_P[0][0],UNITY_MATRIX_P[1][0],UNITY_MATRIX_P[2][0],UNITY_MATRIX_P[3][0]).x;
    u_xlat7.y = _ScreenParams.y * float4(UNITY_MATRIX_P[0][1],UNITY_MATRIX_P[1][1],UNITY_MATRIX_P[2][1],UNITY_MATRIX_P[3][1]).y;
    u_xlat0.xy = u_xlat0.ww / abs(u_xlat7.xy);
    u_xlat0.xy = float2(_UIMaskSoftnessX, _UIMaskSoftnessY) * float2(0.25, 0.25) + abs(u_xlat0.xy);
    u_xlat0.zw = float2(0.25, 0.25) / u_xlat0.xy;
    u_xlat16_1 = max(_ClipRect, float4(-2e+10, -2e+10, -2e+10, -2e+10));
    u_xlat16_1 = min(u_xlat16_1, float4(2e+10, 2e+10, 2e+10, 2e+10));
    u_xlat2.xy = input.vertex.xy * float2(2.0, 2.0) + (-u_xlat16_1.xy);
    u_xlat0.xy = (-u_xlat16_1.zw) + u_xlat2.xy;
    output.mask = u_xlat0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
float4 u_xlat1;
half u_xlat16_1;
float2 u_xlat2;
bool u_xlatb2;
float u_xlat3;
float u_xlat4;
float u_xlat6;
bool u_xlatb6;
bool u_xlatb7;
float2 u_xlat8;
float u_xlat10;
float u_xlat14;
bool u_xlatb14;

    u_xlat0.xy = _Rect.yx * float2(0.5, 0.5);
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat16_1 = _BorderWidth + _SoftEdge;
    u_xlat0.xy = u_xlat0.xy + (-float2(u_xlat16_1,u_xlat16_1));
    u_xlat0.xy = max(u_xlat0.xy, float2(0.00100000005, 0.00100000005));
    u_xlat8.xy = input.uv.yx + float2(-0.5, -0.5);
    u_xlat8.xy = u_xlat8.xy * _Rect.yx;
    u_xlat2.xy = u_xlat8.xy / u_xlat0.xy;
    u_xlat10 = max(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat10 = float(1.0) / u_xlat10;
    u_xlat14 = min(abs(u_xlat2.y), abs(u_xlat2.x));
    u_xlat10 = u_xlat10 * u_xlat14;
    u_xlat14 = u_xlat10 * u_xlat10;
    u_xlat3 = u_xlat14 * 0.0208350997 + -0.0851330012;
    u_xlat3 = u_xlat14 * u_xlat3 + 0.180141002;
    u_xlat3 = u_xlat14 * u_xlat3 + -0.330299497;
    u_xlat14 = u_xlat14 * u_xlat3 + 0.999866009;
    u_xlat3 = u_xlat14 * u_xlat10;
    u_xlat3 = u_xlat3 * -2.0 + 1.57079637;
    u_xlatb7 = abs(u_xlat2.y)<abs(u_xlat2.x);
    u_xlat3 = u_xlatb7 ? u_xlat3 : float(0.0);
    u_xlat10 = u_xlat10 * u_xlat14 + u_xlat3;
    u_xlatb14 = u_xlat2.y<(-u_xlat2.y);
    u_xlat14 = u_xlatb14 ? -3.14159274 : float(0.0);
    u_xlat10 = u_xlat14 + u_xlat10;
    u_xlat14 = min(u_xlat2.y, u_xlat2.x);
    u_xlat2.x = max(u_xlat2.y, u_xlat2.x);
    u_xlatb2 = u_xlat2.x>=(-u_xlat2.x);
    u_xlatb6 = u_xlat14<(-u_xlat14);
    u_xlatb2 = u_xlatb2 && u_xlatb6;
    u_xlat2.x = (u_xlatb2) ? (-u_xlat10) : u_xlat10;
    u_xlat2.x = u_xlat2.x * 0.159154937 + 0.5;
    u_xlat6 = _Time.y * _FlowSpeed;
    u_xlat6 = frac(u_xlat6);
    u_xlat10 = (-u_xlat6) + _Offset;
    u_xlat6 = _UseManualOffset * u_xlat10 + u_xlat6;
    u_xlat2.x = u_xlat6 + u_xlat2.x;
    u_xlat2.x = frac(u_xlat2.x);
    u_xlat2.x = u_xlat2.x + (-_FillLength);
    u_xlat6 = float(1.0) / (-_FlowSoftness);
    u_xlat2.x = u_xlat6 * u_xlat2.x;
    u_xlat2.x = clamp(u_xlat2.x, 0.0, 1.0);
    u_xlat6 = u_xlat2.x * -2.0 + 3.0;
    u_xlat2.x = u_xlat2.x * u_xlat2.x;
    u_xlat2.x = u_xlat2.x * u_xlat6;
    u_xlat6 = min(u_xlat0.x, u_xlat0.y);
    u_xlat0.xy = (-float2(u_xlat6,u_xlat6)) * float2(float2(_CornerRadius, _CornerRadius)) + u_xlat0.yx;
    u_xlat0.xy = (-u_xlat0.xy) + abs(u_xlat8.yx);
    u_xlat8.xy = max(u_xlat0.xy, float2(0.0, 0.0));
    u_xlat0.x = max(u_xlat0.y, u_xlat0.x);
    u_xlat0.x = min(u_xlat0.x, 0.0);
    u_xlat4 = dot(u_xlat8.xy, u_xlat8.xy);
    u_xlat4 = sqrt(u_xlat4);
    u_xlat0.x = u_xlat0.x + u_xlat4;
    u_xlat0.x = (-u_xlat6) * _CornerRadius + u_xlat0.x;
    u_xlat16_1 = _BorderWidth + (-_SoftEdge);
    u_xlat0.x = abs(u_xlat0.x) + (-u_xlat16_1);
    u_xlat4 = (-u_xlat16_1) + _BorderWidth;
    u_xlat4 = float(1.0) / u_xlat4;
    u_xlat0.x = u_xlat4 * u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat4 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = (-u_xlat4) * u_xlat0.x + 1.0;
    u_xlat0.x = u_xlat2.x * u_xlat0.x;
    u_xlat1 = input.color * _FlowColor;
    u_xlat1.w = u_xlat0.x * u_xlat1.w;
    result = u_xlat1;
    return result;

}
#endif
ENDCG
}
}
}
