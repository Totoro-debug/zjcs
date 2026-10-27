Shader "Zjcs/Official/UI/Gray" {
Properties {
_MainTex ("_MainTex", 2D) = "white" {}
_Color ("_Color", Color) = (1.0,1.0,1.0,1.0)
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
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
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
struct Varying { float4 position : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; float4 mask : TEXCOORD2; };
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
float4 _ClipRect;
float4 _MainTex_ST;
half4 _Color;
half4 _TextureSampleAdd;
sampler2D _MainTex;
#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat1 = input.color * _Color;
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
half u_xlat16_8;

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
    u_xlat16_8 = dot(u_xlat0.xyz, float3(0.298999995, 0.587000012, 0.114));
    result.xyz = float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) * float3(u_xlat16_8,u_xlat16_8,u_xlat16_8);
    result.w = u_xlat16_2;
    u_xlatb1 = u_xlat16_5<0.0;
    if(u_xlatb1){clip(-1);}
    return result;

}
#elif defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat1 = input.color * _Color;
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
    u_xlat16_2 = dot(u_xlat0.xyz, float3(0.298999995, 0.587000012, 0.114));
    result.xyz = u_xlat0.www * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2);
    result.w = u_xlat0.w;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat1 = input.color * _Color;
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
half u_xlat16_5;

    u_xlat16_0.x = input.color.w * 255.0;
    u_xlat16_0.x = round(u_xlat16_0.x);
    u_xlat16_0.w = u_xlat16_0.x * 0.00392156886;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat1 = u_xlat16_1 + _TextureSampleAdd;
    u_xlat16_0.xyz = input.color.xyz;
    u_xlat0 = u_xlat16_0 * u_xlat1;
    u_xlat16_2 = dot(u_xlat0.xyz, float3(0.298999995, 0.587000012, 0.114));
    u_xlat1.xy = (-_ClipRect.xy) + _ClipRect.zw;
    u_xlat1.xy = u_xlat1.xy + -abs(input.mask.xy);
    u_xlat1.xy = u_xlat1.xy * input.mask.zw;
    u_xlat1.xy = clamp(u_xlat1.xy, 0.0, 1.0);
    u_xlat16_5 = u_xlat1.y * u_xlat1.x;
    u_xlat16_5 = u_xlat0.w * u_xlat16_5;
    result.xyz = float3(u_xlat16_5,u_xlat16_5,u_xlat16_5) * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2);
    result.w = u_xlat16_5;
    return result;

}
#elif !defined(UNITY_UI_ALPHACLIP) && !defined(UNITY_UI_CLIP_RECT)
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat1 = input.color * _Color;
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

    u_xlat16_0.x = input.color.w * 255.0;
    u_xlat16_0.x = round(u_xlat16_0.x);
    u_xlat16_0.w = u_xlat16_0.x * 0.00392156886;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat1 = u_xlat16_1 + _TextureSampleAdd;
    u_xlat16_0.xyz = input.color.xyz;
    u_xlat0 = u_xlat16_0 * u_xlat1;
    u_xlat16_2 = dot(u_xlat0.xyz, float3(0.298999995, 0.587000012, 0.114));
    result.xyz = u_xlat0.www * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2);
    result.w = u_xlat0.w;
    return result;

}
#endif
ENDCG
}
}
}
