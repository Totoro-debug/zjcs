Shader "Zjcs/Official/World/Map13/WaterSurface" {
Properties {
_MaskTex ("Mask", 2D) = "black" {}
_BaseColor ("MainColor", Color) = (0.0,0.4399999976158142,0.6200000047683716,1.0)
_ShallowColor ("ShallowColor", Color) = (1.0,1.0,1.0,1.0)
_BumpMap ("Normals", 2D) = "bump" {}
_NormalTiling ("Normals Tiling", Float) = 1.0
_NormalSpeed ("Normals Speed", Float) = 0.20000000298023224
_NormalStrength ("Normals Strength", Range(0.0,1.0)) = 0.5
_CausticsTex ("Caustics", 2D) = "black" {}
_CausticsBrightness ("Caustics Brightness", Float) = 2.0
_CausticsTiling ("Caustics Tiling", Float) = 0.5
_CausticsSpeed ("Caustics Speed multiplier", Float) = 0.10000000149011612
_CausticsThreshold ("Caustics Threshold", Range(0.0,1.0)) = 0.0
_FoamColor ("Foam Color", Color) = (1.0,1.0,1.0,1.0)
[SingleLineTexture] _FoamTex ("Foam Mask", 2D) = "black" {}
_FoamSpeed ("Foam Speed", Float) = 0.10000000149011612
_FoamTiling ("Foam Tiling", Float) = 0.10000000149011612
_FoamCutoff ("Foam Cutoff", Range(0.0,1.0)) = 0.0
_EdgeColor ("Edge Color", Color) = (1.0,1.0,1.0,1.0)
_EdgeThreshold ("Edge Threshold", Range(0.0010000000474974513,0.5)) = 0.5
_EdgeCutoff ("Edge Cutoff", Range(0.0010000000474974513,1.0)) = 0.0010000000474974513
_IntersectionRippleStrength ("IntersectionRippleStrength", Range(0.0,1.0)) = 0.5
_IntersectionFalloff ("IntersectionFalloff", Range(0.009999999776482582,1.0)) = 0.5
_IntersectionRippleDist ("Ripple distance", Float) = 32.0
_Test ("Wave", Float) = 1.0
_Test2 ("Test2", Range(0.0,1.0)) = 1.0
_NoiseTex ("Edge Noise", 2D) = "black" {}
_NoiseSpeed ("Noise Speed", Float) = 1.0
_NoiseTiling ("Noise Tiling", Float) = 1.0
_IsPond ("Is Pond", Float) = 0.0
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float3 world:TEXCOORD1; };

float _CausticsBrightness;
float _CausticsSpeed;
float _CausticsTiling;
float _FoamCutoff;
float _FoamSpeed;
float _FoamTiling;
float _NormalSpeed;
float _NormalStrength;
float _NormalTiling;
float _Test2;
float _Test;
float2 _GlobalMipBias;
float3 _FoamColor;
float4 _BaseColor;
float4 _ShallowColor;
half _CausticsThreshold;
half _EdgeThreshold;
half _HDREmulationScale;
half _IsPond;
half _NoiseSpeed;
half _NoiseTiling;
half _UseSceneLighting;
half _WaterLevel;
half2 _ShapeLightBlendFactors0;
half3 _EdgeColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _BumpMap;
sampler2D _CausticsTex;
sampler2D _FoamTex;
sampler2D _MaskTex;
sampler2D _NoiseTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool u_xlatb4;

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
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    u_xlatb4 = any(float4(0.0, 0.0, 0.0, 0.0)!=float4(_IsPond,_IsPond,_IsPond,_IsPond));
    output.uv.xy = (bool(u_xlatb4)) ? input.uv.xy : u_xlat0.xy;
    output.mask.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
float4 u_xlat2;
bool u_xlatb2;
float4 u_xlat3;
half4 u_xlat16_3;
float3 u_xlat4;
half4 u_xlat16_4;
half3 u_xlat16_5;
half4 u_xlat16_6;
float3 u_xlat7;
float3 u_xlat8;
float3 u_xlat9;
half3 u_xlat16_9;
bool u_xlatb9;
half u_xlat16_10;
float2 u_xlat18;
half u_xlat16_19;
float u_xlat27;
bool u_xlatb27;
half u_xlat16_28;
float u_xlat34;
bool u_xlatb34;

    u_xlat0 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlatb9 = u_xlat0>=1.0;
    if(u_xlatb9){clip(-1);}
    u_xlat16_1.x = (-u_xlat0) + _WaterLevel;
    u_xlat16_10 = u_xlat16_1.x / _WaterLevel;
    u_xlat16_10 = clamp(u_xlat16_10, 0.0, 1.0);
    u_xlat16_19 = _WaterLevel * _EdgeThreshold;
    u_xlat9.x = _Time.y * _NoiseSpeed;
    u_xlat9.xy = u_xlat9.xx * float2(0.0, 1.0);
    u_xlat9.xy = input.world.xy * float2(float2(_NoiseTiling, _NoiseTiling)) + u_xlat9.xy;
    u_xlat16_9.x = tex2Dbias(_NoiseTex, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_28 = u_xlat16_9.x * 2.0 + -1.0;
    u_xlat16_28 = u_xlat16_19 * u_xlat16_28;
    u_xlat0 = u_xlat16_28 * _Test + u_xlat0;
    u_xlat0 = (-u_xlat0) + _WaterLevel;
    u_xlat0 = u_xlat0 / u_xlat16_19;
    u_xlatb9 = u_xlat0<1.0;
    u_xlat18.x = u_xlatb9 ? 1.0 : float(0.0);
    u_xlatb27 = u_xlat0>=0.0;
    u_xlatb2 = u_xlat0<_Test2;
    u_xlatb27 = u_xlatb27 && u_xlatb2;
    u_xlatb2 = u_xlat0>=_Test2;
    u_xlatb9 = u_xlatb9 && u_xlatb2;
    u_xlat2.x = (-_Test2) + 1.0;
    u_xlat0 = u_xlat0 + (-_Test2);
    u_xlat2.x = float(1.0) / u_xlat2.x;
    u_xlat0 = u_xlat0 * u_xlat2.x;
    u_xlat0 = clamp(u_xlat0, 0.0, 1.0);
    u_xlat2.x = u_xlat0 * -2.0 + 3.0;
    u_xlat0 = u_xlat0 * u_xlat0;
    u_xlat0 = (-u_xlat2.x) * u_xlat0 + 1.0;
    u_xlat0 = (u_xlatb9) ? u_xlat0 : u_xlat18.x;
    u_xlat0 = (u_xlatb27) ? 1.0 : u_xlat0;
    u_xlat9.x = u_xlat0 + u_xlat16_10;
    u_xlat2.w = u_xlat9.x * u_xlat16_10;
    u_xlat2.w = clamp(u_xlat2.w, 0.0, 1.0);
    u_xlatb9 = u_xlat2.w==0.0;
    if(u_xlatb9){clip(-1);}
    u_xlat16_19 = (-_CausticsThreshold) + 1.0;
    u_xlat16_19 = u_xlat16_19 * _WaterLevel;
    u_xlat16_19 = clamp(u_xlat16_19, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_1.x / u_xlat16_19;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_1.x = (-u_xlat16_1.x) + 1.0;
    u_xlat16_1.x = max(u_xlat16_1.x, 0.0299999993);
    u_xlat3 = input.world.xyxy * float4(_CausticsTiling, _CausticsTiling, _NormalTiling, _NormalTiling);
    u_xlat9.x = _Time.y * _CausticsSpeed;
    u_xlat18.xy = input.world.xy * float2(float2(_CausticsTiling, _CausticsTiling)) + u_xlat9.xx;
    u_xlat16_4.xyz = tex2Dbias(_CausticsTex, float4(u_xlat18.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat9.xy = u_xlat3.xy * float2(0.800000012, 0.800000012) + (-u_xlat9.xx);
    u_xlat16_9.xyz = tex2Dbias(_CausticsTex, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_5.xyz = min(u_xlat16_9.xyz, u_xlat16_4.xyz);
    u_xlat9.xy = input.world.xy * float2(float2(_FoamTiling, _FoamTiling));
    u_xlat27 = _Time.y * _FoamSpeed;
    u_xlat3.xy = input.world.xy * float2(float2(_FoamTiling, _FoamTiling)) + float2(u_xlat27,u_xlat27);
    u_xlat16_4.xyz = tex2Dbias(_FoamTex, float4(u_xlat3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat9.xy = u_xlat9.xy * float2(0.800000012, 0.800000012) + float2(1.0, 1.0);
    u_xlat9.xy = (-float2(_FoamSpeed,_FoamSpeed)) * _Time.yy + u_xlat9.xy;
    u_xlat16_9.xyz = tex2Dbias(_FoamTex, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_6.xyz = u_xlat16_9.xyz + u_xlat16_4.xyz;
    u_xlat16_6.xyz = clamp(u_xlat16_6.xyz, 0.0, 1.0);
    u_xlat9.x = (-_FoamCutoff) + 1.0;
    u_xlat4.xyz = u_xlat16_6.xyz + (-float3(float3(_FoamCutoff, _FoamCutoff, _FoamCutoff)));
    u_xlat9.x = float(1.0) / u_xlat9.x;
    u_xlat9.xyz = u_xlat9.xxx * u_xlat4.xyz;
    u_xlat9.xyz = clamp(u_xlat9.xyz, 0.0, 1.0);
    u_xlat4.xyz = u_xlat9.xyz * float3(-2.0, -2.0, -2.0) + float3(3.0, 3.0, 3.0);
    u_xlat9.xyz = u_xlat9.xyz * u_xlat9.xyz;
    u_xlat9.xyz = u_xlat9.xyz * u_xlat4.xyz;
    u_xlat3.x = _Time.y * _NormalSpeed;
    u_xlat4.xy = input.world.xy * float2(_NormalTiling,_NormalTiling) + u_xlat3.xx;
    u_xlat16_4.xyz = tex2Dbias(_BumpMap, float4(u_xlat4.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4 = u_xlat16_4.xyzz * float4(2.0, 2.0, 2.0, 2.0) + float4(-1.0, -1.0, -1.0, -1.0);
    u_xlat3.xy = u_xlat3.zw * float2(0.800000012, 0.800000012) + (-u_xlat3.xx);
    u_xlat16_3.xyz = tex2Dbias(_BumpMap, float4(u_xlat3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3 = u_xlat16_3.zzxy * float4(2.0, 2.0, 2.0, 2.0) + float4(-1.0, -1.0, -1.0, -1.0);
    u_xlat16_6 = u_xlat16_3 * u_xlat16_4;
    u_xlat16_6.xy = u_xlat16_6.zw + u_xlat16_6.xy;
    u_xlat16_6.z = u_xlat16_3.y * u_xlat16_4.w;
    u_xlat16_19 = dot(u_xlat16_6.xyz, u_xlat16_6.xyz);
    u_xlat16_19 = rsqrt(u_xlat16_19);
    u_xlat16_19 = u_xlat16_19 * u_xlat16_6.z;
    u_xlat7.xyz = _BaseColor.xyz + (-_ShallowColor.xyz);
    u_xlat7.xyz = float3(u_xlat16_10,u_xlat16_10,u_xlat16_10) * u_xlat7.xyz + _ShallowColor.xyz;
    u_xlat34 = (-_NormalStrength) + 1.0;
    u_xlat8.x = (-u_xlat34) + 1.0;
    u_xlat34 = u_xlat16_19 * u_xlat8.x + u_xlat34;
    u_xlat8.xyz = u_xlat16_5.xyz * float3(float3(_CausticsBrightness, _CausticsBrightness, _CausticsBrightness));
    u_xlat8.xyz = u_xlat16_1.xxx * u_xlat8.xyz;
    u_xlat7.xyz = u_xlat7.xyz * float3(u_xlat34,u_xlat34,u_xlat34) + u_xlat8.xyz;
    u_xlatb34 = u_xlat16_10>=0.899999976;
    u_xlat9.xyz = u_xlat9.xyz * _FoamColor.xyz;
    u_xlat9.xyz = bool(u_xlatb34) ? u_xlat9.xyz : float3(0.0, 0.0, 0.0);
    u_xlat9.xyz = u_xlat9.xyz + u_xlat7.xyz;
    u_xlat7.xyz = (-u_xlat9.xyz) + _EdgeColor.xyz;
    u_xlat2.xyz = float3(u_xlat0,u_xlat0,u_xlat0) * u_xlat7.xyz + u_xlat9.xyz;
    u_xlat16_0.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.mask.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_1.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb27 = u_xlat16_1.x!=0.0;
    u_xlat16_1 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_1.x = dot(u_xlat16_1, _ShapeLightMaskFilter0);
    u_xlat16_1.xyz = u_xlat16_0.xyz * u_xlat16_1.xxx;
    u_xlat16_1.xyz = (bool(u_xlatb27)) ? u_xlat16_1.xyz : u_xlat16_0.xyz;
    u_xlat16_5.xyz = u_xlat16_1.xyz * _ShapeLightBlendFactors0.xxyx.yyy;
    u_xlat16_1.xyz = u_xlat16_1.xyz * float3(_ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y);
    u_xlat16_1.xyz = u_xlat2.xyz * u_xlat16_5.xyz + u_xlat16_1.xyz;
    u_xlat16_0.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_1.xyz + (-u_xlat2.xyz);
    u_xlat16_0.w = 0.0;
    u_xlat16_0 = float4(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_0 + u_xlat2;
    result = max(u_xlat16_0, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float3 world:TEXCOORD1; };

float _CausticsBrightness;
float _CausticsSpeed;
float _CausticsTiling;
float _FoamCutoff;
float _FoamSpeed;
float _FoamTiling;
float _NormalSpeed;
float _NormalStrength;
float _NormalTiling;
float _Test2;
float _Test;
float2 _GlobalMipBias;
float3 _FoamColor;
float4 _BaseColor;
float4 _ShallowColor;
half _CausticsThreshold;
half _EdgeThreshold;
half _IsPond;
half _NoiseSpeed;
half _NoiseTiling;
half _WaterLevel;
half3 _EdgeColor;
sampler2D _BumpMap;
sampler2D _CausticsTex;
sampler2D _FoamTex;
sampler2D _MaskTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
bool u_xlatb4;

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
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    u_xlatb4 = any(float4(0.0, 0.0, 0.0, 0.0)!=float4(_IsPond,_IsPond,_IsPond,_IsPond));
    output.uv.xy = (bool(u_xlatb4)) ? input.uv.xy : u_xlat0.xy;
    output.mask.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half u_xlat16_1;
float4 u_xlat2;
bool u_xlatb2;
float4 u_xlat3;
half4 u_xlat16_3;
float3 u_xlat4;
half4 u_xlat16_4;
half3 u_xlat16_5;
half4 u_xlat16_6;
float3 u_xlat7;
float3 u_xlat8;
float3 u_xlat9;
half3 u_xlat16_9;
bool u_xlatb9;
half u_xlat16_10;
float2 u_xlat18;
half u_xlat16_19;
float u_xlat27;
bool u_xlatb27;
half u_xlat16_28;
float u_xlat34;
bool u_xlatb34;

    u_xlat0 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlatb9 = u_xlat0>=1.0;
    if(u_xlatb9){clip(-1);}
    u_xlat16_1 = (-u_xlat0) + _WaterLevel;
    u_xlat16_10 = u_xlat16_1 / _WaterLevel;
    u_xlat16_10 = clamp(u_xlat16_10, 0.0, 1.0);
    u_xlat16_19 = _WaterLevel * _EdgeThreshold;
    u_xlat9.x = _Time.y * _NoiseSpeed;
    u_xlat9.xy = u_xlat9.xx * float2(0.0, 1.0);
    u_xlat9.xy = input.world.xy * float2(float2(_NoiseTiling, _NoiseTiling)) + u_xlat9.xy;
    u_xlat16_9.x = tex2Dbias(_NoiseTex, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_28 = u_xlat16_9.x * 2.0 + -1.0;
    u_xlat16_28 = u_xlat16_19 * u_xlat16_28;
    u_xlat0 = u_xlat16_28 * _Test + u_xlat0;
    u_xlat0 = (-u_xlat0) + _WaterLevel;
    u_xlat0 = u_xlat0 / u_xlat16_19;
    u_xlatb9 = u_xlat0<1.0;
    u_xlat18.x = u_xlatb9 ? 1.0 : float(0.0);
    u_xlatb27 = u_xlat0>=0.0;
    u_xlatb2 = u_xlat0<_Test2;
    u_xlatb27 = u_xlatb27 && u_xlatb2;
    u_xlatb2 = u_xlat0>=_Test2;
    u_xlatb9 = u_xlatb9 && u_xlatb2;
    u_xlat2.x = (-_Test2) + 1.0;
    u_xlat0 = u_xlat0 + (-_Test2);
    u_xlat2.x = float(1.0) / u_xlat2.x;
    u_xlat0 = u_xlat0 * u_xlat2.x;
    u_xlat0 = clamp(u_xlat0, 0.0, 1.0);
    u_xlat2.x = u_xlat0 * -2.0 + 3.0;
    u_xlat0 = u_xlat0 * u_xlat0;
    u_xlat0 = (-u_xlat2.x) * u_xlat0 + 1.0;
    u_xlat0 = (u_xlatb9) ? u_xlat0 : u_xlat18.x;
    u_xlat0 = (u_xlatb27) ? 1.0 : u_xlat0;
    u_xlat9.x = u_xlat0 + u_xlat16_10;
    u_xlat2.w = u_xlat9.x * u_xlat16_10;
    u_xlat2.w = clamp(u_xlat2.w, 0.0, 1.0);
    u_xlatb9 = u_xlat2.w==0.0;
    if(u_xlatb9){clip(-1);}
    u_xlat16_19 = (-_CausticsThreshold) + 1.0;
    u_xlat16_19 = u_xlat16_19 * _WaterLevel;
    u_xlat16_19 = clamp(u_xlat16_19, 0.0, 1.0);
    u_xlat16_1 = u_xlat16_1 / u_xlat16_19;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_1 = (-u_xlat16_1) + 1.0;
    u_xlat16_1 = max(u_xlat16_1, 0.0299999993);
    u_xlat3 = input.world.xyxy * float4(_CausticsTiling, _CausticsTiling, _NormalTiling, _NormalTiling);
    u_xlat9.x = _Time.y * _CausticsSpeed;
    u_xlat18.xy = input.world.xy * float2(float2(_CausticsTiling, _CausticsTiling)) + u_xlat9.xx;
    u_xlat16_4.xyz = tex2Dbias(_CausticsTex, float4(u_xlat18.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat9.xy = u_xlat3.xy * float2(0.800000012, 0.800000012) + (-u_xlat9.xx);
    u_xlat16_9.xyz = tex2Dbias(_CausticsTex, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_5.xyz = min(u_xlat16_9.xyz, u_xlat16_4.xyz);
    u_xlat9.xy = input.world.xy * float2(float2(_FoamTiling, _FoamTiling));
    u_xlat27 = _Time.y * _FoamSpeed;
    u_xlat3.xy = input.world.xy * float2(float2(_FoamTiling, _FoamTiling)) + float2(u_xlat27,u_xlat27);
    u_xlat16_4.xyz = tex2Dbias(_FoamTex, float4(u_xlat3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat9.xy = u_xlat9.xy * float2(0.800000012, 0.800000012) + float2(1.0, 1.0);
    u_xlat9.xy = (-float2(_FoamSpeed,_FoamSpeed)) * _Time.yy + u_xlat9.xy;
    u_xlat16_9.xyz = tex2Dbias(_FoamTex, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_6.xyz = u_xlat16_9.xyz + u_xlat16_4.xyz;
    u_xlat16_6.xyz = clamp(u_xlat16_6.xyz, 0.0, 1.0);
    u_xlat9.x = (-_FoamCutoff) + 1.0;
    u_xlat4.xyz = u_xlat16_6.xyz + (-float3(float3(_FoamCutoff, _FoamCutoff, _FoamCutoff)));
    u_xlat9.x = float(1.0) / u_xlat9.x;
    u_xlat9.xyz = u_xlat9.xxx * u_xlat4.xyz;
    u_xlat9.xyz = clamp(u_xlat9.xyz, 0.0, 1.0);
    u_xlat4.xyz = u_xlat9.xyz * float3(-2.0, -2.0, -2.0) + float3(3.0, 3.0, 3.0);
    u_xlat9.xyz = u_xlat9.xyz * u_xlat9.xyz;
    u_xlat9.xyz = u_xlat9.xyz * u_xlat4.xyz;
    u_xlat3.x = _Time.y * _NormalSpeed;
    u_xlat4.xy = input.world.xy * float2(_NormalTiling,_NormalTiling) + u_xlat3.xx;
    u_xlat16_4.xyz = tex2Dbias(_BumpMap, float4(u_xlat4.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_4 = u_xlat16_4.xyzz * float4(2.0, 2.0, 2.0, 2.0) + float4(-1.0, -1.0, -1.0, -1.0);
    u_xlat3.xy = u_xlat3.zw * float2(0.800000012, 0.800000012) + (-u_xlat3.xx);
    u_xlat16_3.xyz = tex2Dbias(_BumpMap, float4(u_xlat3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3 = u_xlat16_3.zzxy * float4(2.0, 2.0, 2.0, 2.0) + float4(-1.0, -1.0, -1.0, -1.0);
    u_xlat16_6 = u_xlat16_3 * u_xlat16_4;
    u_xlat16_6.xy = u_xlat16_6.zw + u_xlat16_6.xy;
    u_xlat16_6.z = u_xlat16_3.y * u_xlat16_4.w;
    u_xlat16_19 = dot(u_xlat16_6.xyz, u_xlat16_6.xyz);
    u_xlat16_19 = rsqrt(u_xlat16_19);
    u_xlat16_19 = u_xlat16_19 * u_xlat16_6.z;
    u_xlat7.xyz = _BaseColor.xyz + (-_ShallowColor.xyz);
    u_xlat7.xyz = float3(u_xlat16_10,u_xlat16_10,u_xlat16_10) * u_xlat7.xyz + _ShallowColor.xyz;
    u_xlat34 = (-_NormalStrength) + 1.0;
    u_xlat8.x = (-u_xlat34) + 1.0;
    u_xlat34 = u_xlat16_19 * u_xlat8.x + u_xlat34;
    u_xlat8.xyz = u_xlat16_5.xyz * float3(float3(_CausticsBrightness, _CausticsBrightness, _CausticsBrightness));
    u_xlat8.xyz = float3(u_xlat16_1,u_xlat16_1,u_xlat16_1) * u_xlat8.xyz;
    u_xlat7.xyz = u_xlat7.xyz * float3(u_xlat34,u_xlat34,u_xlat34) + u_xlat8.xyz;
    u_xlatb34 = u_xlat16_10>=0.899999976;
    u_xlat9.xyz = u_xlat9.xyz * _FoamColor.xyz;
    u_xlat9.xyz = bool(u_xlatb34) ? u_xlat9.xyz : float3(0.0, 0.0, 0.0);
    u_xlat9.xyz = u_xlat9.xyz + u_xlat7.xyz;
    u_xlat7.xyz = (-u_xlat9.xyz) + _EdgeColor.xyz;
    u_xlat2.xyz = float3(u_xlat0,u_xlat0,u_xlat0) * u_xlat7.xyz + u_xlat9.xyz;
    result = max(u_xlat2, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
