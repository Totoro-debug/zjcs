Shader "Zjcs/Official/World/Map13/MiniMapMaskableTilemap" {
Properties {
_MainTex ("Sprite Texture", 2D) = "white" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
[MaterialToggle] PixelSnap ("Pixel snap", Float) = 0.0
_RendererColor ("RendererColor", Color) = (1.0,1.0,1.0,1.0)
_Flip ("Flip", Vector) = (1.0,1.0,1.0,1.0)
_AlphaTex ("External Alpha", 2D) = "white" {}
_EnableExternalAlpha ("Enable External Alpha", Float) = 0.0
_MaskTex ("Mask Texture", 2D) = "white" {}
}
SubShader {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ ETC1_EXTERNAL_ALPHA
#pragma multi_compile __ PIXELSNAP_ON
#include "UnityCG.cginc"
#if !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float world:TEXCOORD1; float3 mask:TEXCOORD2; };

half2 _Flip;
half4 _Color;
half4 _MaskTex_ST;
half4 _RendererColor;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;

    u_xlat0.xy = input.vertex.xy * _Flip.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = input.vertex.z<0.0;
    output.world = (u_xlatb0) ? 1.0 : 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half3 u_xlat16_1;
float2 u_xlat2;
half4 u_xlat16_2;
bool u_xlatb2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    u_xlatb2 = any(float4(0.0, 0.0, 0.0, 0.0)!=float4(input.world,input.world,input.world,input.world));
    if(u_xlatb2){
        u_xlat2.xy = input.mask.xy * _MaskTex_ST.xy;
        u_xlat16_2 = tex2D(_MaskTex, u_xlat2.xy);
        u_xlat16_3.xyz = (-u_xlat16_0.xyz) * u_xlat16_0.www + u_xlat16_2.xyz;
        u_xlat16_0.xyz = u_xlat16_2.www * u_xlat16_3.xyz + u_xlat16_1.xyz;
    } else {
        u_xlat16_0.xyz = u_xlat16_1.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float world:TEXCOORD1; float3 mask:TEXCOORD2; };

half2 _Flip;
half4 _Color;
half4 _MaskTex_ST;
half4 _RendererColor;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
float2 u_xlat4;

    u_xlat0.xy = input.vertex.xy * _Flip.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat4.xy = _ScreenParams.xy * float2(0.5, 0.5);
    u_xlat0.xy = u_xlat4.xy * u_xlat0.xy;
    u_xlat0.xy = round(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy / u_xlat4.xy;
    output.position.xy = u_xlat1.ww * u_xlat0.xy;
    output.position.zw = u_xlat1.zw;
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = input.vertex.z<0.0;
    output.world = (u_xlatb0) ? 1.0 : 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half3 u_xlat16_1;
float2 u_xlat2;
half4 u_xlat16_2;
bool u_xlatb2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    u_xlatb2 = any(float4(0.0, 0.0, 0.0, 0.0)!=float4(input.world,input.world,input.world,input.world));
    if(u_xlatb2){
        u_xlat2.xy = input.mask.xy * _MaskTex_ST.xy;
        u_xlat16_2 = tex2D(_MaskTex, u_xlat2.xy);
        u_xlat16_3.xyz = (-u_xlat16_0.xyz) * u_xlat16_0.www + u_xlat16_2.xyz;
        u_xlat16_0.xyz = u_xlat16_2.www * u_xlat16_3.xyz + u_xlat16_1.xyz;
    } else {
        u_xlat16_0.xyz = u_xlat16_1.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float world:TEXCOORD1; float3 mask:TEXCOORD2; };

float _EnableExternalAlpha;
half2 _Flip;
half4 _Color;
half4 _MaskTex_ST;
half4 _RendererColor;
sampler2D _AlphaTex;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;

    u_xlat0.xy = input.vertex.xy * _Flip.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = input.vertex.z<0.0;
    output.world = (u_xlatb0) ? 1.0 : 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.x = tex2D(_AlphaTex, input.uv.xy).x;
    u_xlat1.x = (-u_xlat16_0.w) + u_xlat16_1.x;
    u_xlat16_0.w = _EnableExternalAlpha * u_xlat1.x + u_xlat16_0.w;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_2.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    u_xlatb1 = any(float4(0.0, 0.0, 0.0, 0.0)!=float4(input.world,input.world,input.world,input.world));
    if(u_xlatb1){
        u_xlat1.xy = input.mask.xy * _MaskTex_ST.xy;
        u_xlat16_1 = tex2D(_MaskTex, u_xlat1.xy);
        u_xlat16_3.xyz = (-u_xlat16_0.xyz) * u_xlat16_0.www + u_xlat16_1.xyz;
        u_xlat16_0.xyz = u_xlat16_1.www * u_xlat16_3.xyz + u_xlat16_2.xyz;
    } else {
        u_xlat16_0.xyz = u_xlat16_2.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float world:TEXCOORD1; float3 mask:TEXCOORD2; };

float _EnableExternalAlpha;
half2 _Flip;
half4 _Color;
half4 _MaskTex_ST;
half4 _RendererColor;
sampler2D _AlphaTex;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
bool u_xlatb0;
float4 u_xlat1;
float2 u_xlat4;

    u_xlat0.xy = input.vertex.xy * _Flip.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat4.xy = _ScreenParams.xy * float2(0.5, 0.5);
    u_xlat0.xy = u_xlat4.xy * u_xlat0.xy;
    u_xlat0.xy = round(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy / u_xlat4.xy;
    output.position.xy = u_xlat1.ww * u_xlat0.xy;
    output.position.zw = u_xlat1.zw;
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    u_xlatb0 = input.vertex.z<0.0;
    output.world = (u_xlatb0) ? 1.0 : 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
float2 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1.x = tex2D(_AlphaTex, input.uv.xy).x;
    u_xlat1.x = (-u_xlat16_0.w) + u_xlat16_1.x;
    u_xlat16_0.w = _EnableExternalAlpha * u_xlat1.x + u_xlat16_0.w;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_2.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    u_xlatb1 = any(float4(0.0, 0.0, 0.0, 0.0)!=float4(input.world,input.world,input.world,input.world));
    if(u_xlatb1){
        u_xlat1.xy = input.mask.xy * _MaskTex_ST.xy;
        u_xlat16_1 = tex2D(_MaskTex, u_xlat1.xy);
        u_xlat16_3.xyz = (-u_xlat16_0.xyz) * u_xlat16_0.www + u_xlat16_1.xyz;
        u_xlat16_0.xyz = u_xlat16_1.www * u_xlat16_3.xyz + u_xlat16_2.xyz;
    } else {
        u_xlat16_0.xyz = u_xlat16_2.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
