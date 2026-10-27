Shader "Zjcs/Official/World/Map13/BoundEntity/SpritesDefaultZWrite_a1002d66" {
Properties {
_MainTex ("Sprite Texture", 2D) = "white" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
[MaterialToggle] PixelSnap ("Pixel snap", Float) = 0.0
_RendererColor ("RendererColor", Color) = (1.0,1.0,1.0,1.0)
_Flip ("Flip", Vector) = (1.0,1.0,1.0,1.0)
_AlphaTex ("External Alpha", 2D) = "white" {}
_EnableExternalAlpha ("Enable External Alpha", Float) = 0.0
}
SubShader {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite On
ZTest LEqual
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ ETC1_EXTERNAL_ALPHA
#pragma multi_compile_local __ PIXELSNAP_ON
#include "UnityCG.cginc"
#if !defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

half2 _Flip;
half4 _Color;
half4 _RendererColor;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
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
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half u_xlat16_1;
bool u_xlatb2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1 = u_xlat16_0.w * input.color.w + -0.00999999978;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb2 = u_xlat16_1<0.0;
    if(u_xlatb2){clip(-1);}
    result.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif !defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

half2 _Flip;
half4 _Color;
half4 _RendererColor;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = input.vertex.xy * _Flip.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    u_xlat1.xy = _ScreenParams.xy * float2(0.5, 0.5);
    u_xlat0.xy = u_xlat0.xy * u_xlat1.xy;
    u_xlat0.xy = round(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy / u_xlat1.xy;
    output.position.xy = u_xlat0.ww * u_xlat0.xy;
    output.position.zw = u_xlat0.zw;
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half u_xlat16_1;
bool u_xlatb2;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat16_1 = u_xlat16_0.w * input.color.w + -0.00999999978;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb2 = u_xlat16_1<0.0;
    if(u_xlatb2){clip(-1);}
    result.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif defined(ETC1_EXTERNAL_ALPHA) && !defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

float _EnableExternalAlpha;
half2 _Flip;
half4 _Color;
half4 _RendererColor;
sampler2D _AlphaTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
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
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;

    u_xlat16_0.x = tex2D(_AlphaTex, input.uv.xy).x;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat0 = u_xlat16_0.x + (-u_xlat16_1.w);
    u_xlat16_1.w = _EnableExternalAlpha * u_xlat0 + u_xlat16_1.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat16_2 = u_xlat16_1.w * input.color.w + -0.00999999978;
    u_xlatb1 = u_xlat16_2<0.0;
    if(u_xlatb1){clip(-1);}
    result.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif defined(ETC1_EXTERNAL_ALPHA) && defined(PIXELSNAP_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

float _EnableExternalAlpha;
half2 _Flip;
half4 _Color;
half4 _RendererColor;
sampler2D _AlphaTex;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = input.vertex.xy * _Flip.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    u_xlat1.xy = _ScreenParams.xy * float2(0.5, 0.5);
    u_xlat0.xy = u_xlat0.xy * u_xlat1.xy;
    u_xlat0.xy = round(u_xlat0.xy);
    u_xlat0.xy = u_xlat0.xy / u_xlat1.xy;
    output.position.xy = u_xlat0.ww * u_xlat0.xy;
    output.position.zw = u_xlat0.zw;
    u_xlat0 = input.color * _Color;
    u_xlat0 = u_xlat0 * _RendererColor;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;

    u_xlat16_0.x = tex2D(_AlphaTex, input.uv.xy).x;
    u_xlat16_1 = tex2D(_MainTex, input.uv.xy);
    u_xlat0 = u_xlat16_0.x + (-u_xlat16_1.w);
    u_xlat16_1.w = _EnableExternalAlpha * u_xlat0 + u_xlat16_1.w;
    u_xlat16_0 = u_xlat16_1 * input.color;
    u_xlat16_2 = u_xlat16_1.w * input.color.w + -0.00999999978;
    u_xlatb1 = u_xlat16_2<0.0;
    if(u_xlatb1){clip(-1);}
    result.xyz = u_xlat16_0.www * u_xlat16_0.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
