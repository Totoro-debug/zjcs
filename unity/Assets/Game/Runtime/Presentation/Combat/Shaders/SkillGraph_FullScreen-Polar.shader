Shader "Zjcs/Official/Combat/SkillGraphs/FullScreen-Polar" {
Properties {

_MainTex ("Diffuse", 2D) = "white" { }

_Color ("Tint", Color) = (1,1,1,1)

_MainSpeed ("Main Speed", Vector) = (0,0,1,1)

_MaskTex ("Mask Tex", 2D) = "white" { }

_MaskStrength ("Mask Strength", Float) = 1.0

_MaskAngle ("Mask Angle", Float) = 0.0

[Space(20)] [Toggle] _Noise ("Noise", Float) = 0.0

_NoiseTex ("Noise Tex", 2D) = "white" { }

_NoiseSpeed ("Noise Speed", Vector) = (0,0,0,0)

[Toggle] _Distortion ("Distortion", Float) = 0.0

_DistortionTex ("Distortion Tex", 2D) = "black" { }

_DistortionStrength ("Distortion Strength", Float) = 0.0

_DistortionSpeed ("Distortion Speed", Vector) = (0,0,0,0)

[Toggle] _Polar ("Polar", Float) = 0.0

_Angle ("Angle", Float) = 0.0

[Enum(UnityEngine.Rendering.BlendMode)] _SourceBlend ("Source Blend", Float) = 5.0

[Enum(UnityEngine.Rendering.BlendMode)] _DestBlend ("Dest Blend", Float) = 10.0

}
SubShader {
LOD 0
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend [_SourceBlend] [_DestBlend], [_SourceBlend] [_DestBlend]
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local __ _NOISE_ON
#pragma multi_compile_local __ _POLAR_ON
#include "UnityCG.cginc"
#if !defined(_NOISE_ON) && !defined(_POLAR_ON)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };

float _Angle;
float2 _GlobalMipBias;
half _MaskAngle;
half _MaskStrength;
half2 _MainSpeed;
half4 _Color;
half4 _MainTex_ST;
half4 _MaskTex_ST;
sampler2D _MainTex;
sampler2D _MaskTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;

    output.position.xy = input.vertex.xy * float2(2.0,2.0);
    output.position.zw = float2(0.0,1.0);
    u_xlat0 = input.color * _Color;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
float u_xlat2;
float2 u_xlat3;
half2 u_xlat16_4;
float2 u_xlat5;
bool u_xlatb10;

    u_xlatb0 = _Angle!=0.0;
    u_xlat5.x = _Angle * 0.0174532942;
    u_xlat1.x = sin(u_xlat5.x);
    u_xlat2 = cos(u_xlat5.x);
    u_xlat5.xy = input.uv.yx + float2(-0.5,-0.5);
    u_xlat1.xy = u_xlat1.xx * u_xlat5.xy;
    u_xlat3.x = u_xlat5.y * u_xlat2 + (-u_xlat1.x);
    u_xlat3.y = u_xlat5.x * u_xlat2 + u_xlat1.y;
    u_xlat1.xy = u_xlat3.xy + float2(0.5,0.5);
    u_xlat0.xw = (bool(u_xlatb0)) ? u_xlat1.xy : input.uv.xy;
    u_xlat1.xy = _Time.yy * _MainSpeed.xy;
    u_xlat0.xw = u_xlat0.xw * _MainTex_ST.xy + u_xlat1.xy;
    u_xlat16_1 = tex2Dbias(_MainTex, float4(u_xlat0.xw, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_1 * input.color;
    u_xlat16_4.x = _MaskAngle * 0.0174532942;
    u_xlat0.x = sin(u_xlat16_4.x);
    u_xlat2 = cos(u_xlat16_4.x);
    u_xlat0.xw = u_xlat0.xx * u_xlat5.xy;
    u_xlat3.x = u_xlat5.y * u_xlat2 + (-u_xlat0.x);
    u_xlat3.y = u_xlat5.x * u_xlat2 + u_xlat0.w;
    u_xlat0.xy = u_xlat3.xy + float2(0.5,0.5);
    u_xlatb10 = _MaskAngle!=0.0;
    u_xlat16_4.xy = (bool(u_xlatb10)) ? u_xlat0.xy : input.uv.xy;
    u_xlat16_4.xy = u_xlat16_4.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MaskTex, float4(u_xlat16_4.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0.x = u_xlat16_0 * _MaskStrength;
    result = u_xlat0.xxxx * u_xlat1;
    return result;

}
#elif defined(_NOISE_ON) && !defined(_POLAR_ON)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };

float _Angle;
float2 _GlobalMipBias;
half _MaskAngle;
half _MaskStrength;
half2 _MainSpeed;
half2 _NoiseSpeed;
half4 _Color;
half4 _MainTex_ST;
half4 _MaskTex_ST;
half4 _NoiseTex_ST;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;

    output.position.xy = input.vertex.xy * float2(2.0,2.0);
    output.position.zw = float2(0.0,1.0);
    u_xlat0 = input.color * _Color;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
half4 u_xlat16_2;
float2 u_xlat3;
half2 u_xlat16_4;
float2 u_xlat5;
bool u_xlatb10;

    u_xlatb0 = _Angle!=0.0;
    u_xlat5.x = _Angle * 0.0174532942;
    u_xlat1.x = sin(u_xlat5.x);
    u_xlat2.x = cos(u_xlat5.x);
    u_xlat5.xy = input.uv.yx + float2(-0.5,-0.5);
    u_xlat1.xy = u_xlat1.xx * u_xlat5.xy;
    u_xlat3.x = u_xlat5.y * u_xlat2.x + (-u_xlat1.x);
    u_xlat3.y = u_xlat5.x * u_xlat2.x + u_xlat1.y;
    u_xlat1.xy = u_xlat3.xy + float2(0.5,0.5);
    u_xlat0.xw = (bool(u_xlatb0)) ? u_xlat1.xy : input.uv.xy;
    u_xlat1.xy = _Time.yy * _MainSpeed.xy;
    u_xlat1.xy = u_xlat0.xw * _MainTex_ST.xy + u_xlat1.xy;
    u_xlat16_1 = tex2Dbias(_MainTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_1 * input.color;
    u_xlat2.xy = _Time.yy * _NoiseSpeed.xy;
    u_xlat0.xw = u_xlat0.xw * _NoiseTex_ST.xy + u_xlat2.xy;
    u_xlat16_2 = tex2Dbias(_NoiseTex, float4(u_xlat0.xw, 0, _GlobalMipBias.x));
    u_xlat16_1 = u_xlat1 * u_xlat16_2;
    u_xlat16_4.x = _MaskAngle * 0.0174532942;
    u_xlat0.x = sin(u_xlat16_4.x);
    u_xlat2.x = cos(u_xlat16_4.x);
    u_xlat0.xw = u_xlat0.xx * u_xlat5.xy;
    u_xlat3.x = u_xlat5.y * u_xlat2.x + (-u_xlat0.x);
    u_xlat3.y = u_xlat5.x * u_xlat2.x + u_xlat0.w;
    u_xlat0.xy = u_xlat3.xy + float2(0.5,0.5);
    u_xlatb10 = _MaskAngle!=0.0;
    u_xlat16_4.xy = (bool(u_xlatb10)) ? u_xlat0.xy : input.uv.xy;
    u_xlat16_4.xy = u_xlat16_4.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MaskTex, float4(u_xlat16_4.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0.x = u_xlat16_0 * _MaskStrength;
    result = u_xlat0.xxxx * u_xlat16_1;
    return result;

}
#elif defined(_NOISE_ON) && defined(_POLAR_ON)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; };

float _Angle;
float2 _GlobalMipBias;
half _MaskAngle;
half _MaskStrength;
half2 _MainSpeed;
half2 _NoiseSpeed;
half4 _Color;
half4 _MainTex_ST;
half4 _MaskTex_ST;
half4 _NoiseTex_ST;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;

    output.position.xy = input.vertex.xy * float2(2.0,2.0);
    output.position.zw = float2(0.0,1.0);
    u_xlat0 = input.color * _Color;
    output.color = u_xlat0;
    output.uv.xy = input.uv.xy;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
float2 u_xlat2;
half4 u_xlat16_2;
float2 u_xlat3;
half2 u_xlat16_4;
float2 u_xlat5;
float u_xlat6;
bool u_xlatb6;
bool u_xlatb10;
float u_xlat11;
bool u_xlatb16;

    u_xlatb0 = _Angle!=0.0;
    u_xlat5.x = _Angle * 0.0174532942;
    u_xlat1.x = sin(u_xlat5.x);
    u_xlat2.x = cos(u_xlat5.x);
    u_xlat5.xy = input.uv.yx + float2(-0.5,-0.5);
    u_xlat1.xy = u_xlat1.xx * u_xlat5.xy;
    u_xlat3.y = u_xlat5.y * u_xlat2.x + (-u_xlat1.x);
    u_xlat3.x = u_xlat5.x * u_xlat2.x + u_xlat1.y;
    u_xlat1.xy = u_xlat3.xy + float2(0.5,0.5);
    u_xlat0.xw = (bool(u_xlatb0)) ? u_xlat1.xy : input.uv.yx;
    u_xlat0.xw = u_xlat0.xw + float2(-0.5,-0.5);
    u_xlat0.xw = u_xlat0.xw + u_xlat0.xw;
    u_xlat1.x = max(abs(u_xlat0.w), abs(u_xlat0.x));
    u_xlat1.x = float(1.0) / u_xlat1.x;
    u_xlat6 = min(abs(u_xlat0.w), abs(u_xlat0.x));
    u_xlat1.x = u_xlat1.x * u_xlat6;
    u_xlat6 = u_xlat1.x * u_xlat1.x;
    u_xlat11 = u_xlat6 * 0.0208350997 + -0.0851330012;
    u_xlat11 = u_xlat6 * u_xlat11 + 0.180141002;
    u_xlat11 = u_xlat6 * u_xlat11 + -0.330299497;
    u_xlat6 = u_xlat6 * u_xlat11 + 0.999866009;
    u_xlat11 = u_xlat6 * u_xlat1.x;
    u_xlat11 = u_xlat11 * -2.0 + 1.57079637;
    u_xlatb16 = abs(u_xlat0.w)<abs(u_xlat0.x);
    u_xlat11 = u_xlatb16 ? u_xlat11 : float(0.0);
    u_xlat1.x = u_xlat1.x * u_xlat6 + u_xlat11;
    u_xlatb6 = u_xlat0.w<(-u_xlat0.w);
    u_xlat6 = u_xlatb6 ? -3.14159274 : float(0.0);
    u_xlat1.x = u_xlat6 + u_xlat1.x;
    u_xlat6 = min(u_xlat0.w, u_xlat0.x);
    u_xlatb6 = u_xlat6<(-u_xlat6);
    u_xlat11 = max(u_xlat0.w, u_xlat0.x);
    u_xlat0.x = dot(u_xlat0.xw, u_xlat0.xw);
    u_xlat2.x = sqrt(u_xlat0.x);
    u_xlatb0 = u_xlat11>=(-u_xlat11);
    u_xlatb0 = u_xlatb0 && u_xlatb6;
    u_xlat0.x = (u_xlatb0) ? (-u_xlat1.x) : u_xlat1.x;
    u_xlat2.y = u_xlat0.x * 0.318309873;
    u_xlat0.xw = _Time.yy * _MainSpeed.xy;
    u_xlat0.xw = u_xlat2.xy * _MainTex_ST.xy + u_xlat0.xw;
    u_xlat16_1 = tex2Dbias(_MainTex, float4(u_xlat0.xw, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_1 * input.color;
    u_xlat0.xw = _Time.yy * _NoiseSpeed.xy;
    u_xlat0.xw = u_xlat2.xy * _NoiseTex_ST.xy + u_xlat0.xw;
    u_xlat16_2 = tex2Dbias(_NoiseTex, float4(u_xlat0.xw, 0, _GlobalMipBias.x));
    u_xlat16_1 = u_xlat1 * u_xlat16_2;
    u_xlat16_4.x = _MaskAngle * 0.0174532942;
    u_xlat0.x = sin(u_xlat16_4.x);
    u_xlat2.x = cos(u_xlat16_4.x);
    u_xlat0.xw = u_xlat0.xx * u_xlat5.xy;
    u_xlat3.x = u_xlat5.y * u_xlat2.x + (-u_xlat0.x);
    u_xlat3.y = u_xlat5.x * u_xlat2.x + u_xlat0.w;
    u_xlat0.xy = u_xlat3.xy + float2(0.5,0.5);
    u_xlatb10 = _MaskAngle!=0.0;
    u_xlat16_4.xy = (bool(u_xlatb10)) ? u_xlat0.xy : input.uv.xy;
    u_xlat16_4.xy = u_xlat16_4.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    u_xlat16_0 = tex2Dbias(_MaskTex, float4(u_xlat16_4.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0.x = u_xlat16_0 * _MaskStrength;
    result = u_xlat0.xxxx * u_xlat16_1;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
