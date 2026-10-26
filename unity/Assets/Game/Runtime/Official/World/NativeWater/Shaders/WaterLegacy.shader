Shader "Zjcs/Official/World/Map13/WaterLegacy" {
Properties {
_MainTex ("Diffuse", 2D) = "white" {}
_BaseColor ("MainColor", Color) = (0.0,0.4399999976158142,0.6200000047683716,1.0)
_CausticsTex ("Caustics RGB", 2D) = "black" {}
_CausticsBrightness ("Caustics Brightness", Float) = 2.0
_CausticsTiling ("Caustics Tiling", Float) = 0.5
_CausticsSpeed ("Caustics Speed multiplier", Float) = 0.10000000149011612
_CausticsDistortion ("Caustics Distortion", Range(0.0,1.0)) = 0.15000000596046448
[Toggle] _Intersection ("Enable Intersection", Float) = 0.0
_IntersectionNoise ("Intersection Noise", 2D) = "white" {}
_IntersectionTiling ("Intersection Tiling", Float) = 0.10000000149011612
_IntersectionMask ("Intersection Mask", 2D) = "black" {}
_IntersectionColor ("Intersection Color", Color) = (1.0,1.0,1.0,1.0)
[IntRange] _IntersectionSize ("Intersection Size", Range(1.0,5.0)) = 2.0
_IntersectionSpeed ("Intersection Speed", Float) = 0.10000000149011612
_FoamTex ("Foam Mask", 2D) = "black" {}
_FoamColor ("Foam Color", Color) = (1.0,1.0,1.0,1.0)
_FoamTiling ("Foam Tiling", Float) = 0.10000000149011612
_FoamSize ("Foam Cutoff", Range(0.009999999776482582,0.9990000128746033)) = 0.009999999776482582
_FoamSpeed ("Foam Speed", Float) = 0.10000000149011612
_BumpMap ("Normals", 2D) = "bump" {}
_NormalTiling ("Normals Tiling", Float) = 1.0
_NormalSpeed ("Normals Speed multiplier", Float) = 0.20000000298023224
_NormalStrength ("Normals Strength", Range(0.0,1.0)) = 0.5
_Direction ("Animation direction", Vector) = (1.0,1.0,0.0,0.0)
_Speed ("Animation Speed", Float) = 1.0
_LightDir ("Light Direction", Vector) = (0.0,0.0,1.0,0.0)
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Back
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
struct Input { float4 vertex:POSITION; };
struct Varying { float4 position:SV_POSITION; float2 mask:TEXCOORD2; float3 world:TEXCOORD1; };

float _CausticsBrightness;
float _CausticsSpeed;
float _CausticsTiling;
float _FoamSize;
float _FoamSpeed;
float _FoamTiling;
float _NormalSpeed;
float _NormalStrength;
float _NormalTiling;
float _Speed;
float2 _GlobalMipBias;
float3 _LightDir;
float4 _BaseColor;
float4 _Direction;
float4 _FoamColor;
float4 _TimeParameters;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _BumpMap;
sampler2D _CausticsTex;
sampler2D _FoamTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    output.world.xyz = u_xlat0.xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.mask.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half4 u_xlat16_1;
float3 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
float3 u_xlat4;
half3 u_xlat16_5;
float u_xlat6;
float3 u_xlat7;
half u_xlat16_7;
float2 u_xlat12;
half u_xlat16_12;
float2 u_xlat14;
bool u_xlatb19;

    u_xlatb0 = _BaseColor.w==0.0;
    if(u_xlatb0){clip(-1);}
    u_xlat0.xy = _Direction.xy * float2(_Speed,_Speed);
    u_xlat12.xy = u_xlat0.xy * _TimeParameters.xx;
    u_xlat1 = input.world.xyxy * float4(_NormalTiling, _NormalTiling, _CausticsTiling, _CausticsTiling);
    u_xlat1.xy = u_xlat12.xy * float2(_NormalSpeed,_NormalSpeed) + u_xlat1.xy;
    u_xlat16_2.xyz = tex2Dbias(_BumpMap, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.x = u_xlat16_2.z * 2.0 + -1.0;
    u_xlat1.x = dot(float3(_LightDir.x, _LightDir.y, _LightDir.z), float3(_LightDir.x, _LightDir.y, _LightDir.z));
    u_xlat1.x = rsqrt(u_xlat1.x);
    u_xlat4.xyz = u_xlat1.xxx * float3(_LightDir.x, _LightDir.y, _LightDir.z);
    u_xlat1.xy = input.world.xy * float2(_FoamTiling,_FoamTiling);
    u_xlat14.xy = u_xlat12.xy * float2(float2(_FoamSpeed, _FoamSpeed)) + u_xlat1.xy;
    u_xlat0.xy = (-u_xlat0.xy) * _TimeParameters.xx + float2(1.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(float2(_FoamSpeed, _FoamSpeed));
    u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * float2(0.5, 0.5) + u_xlat0.xy;
    u_xlat16_1.x = tex2Dbias(_FoamTex, float4(u_xlat14.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0.x = tex2Dbias(_FoamTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0.x = u_xlat16_0.x + u_xlat16_1.x;
    u_xlat6 = (-_FoamSize) + 1.0;
    u_xlat0.x = u_xlat0.x + (-_FoamSize);
    u_xlat6 = float(1.0) / u_xlat6;
    u_xlat0.x = u_xlat6 * u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat6 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = u_xlat0.x * u_xlat6;
    u_xlat6 = _FoamColor.w;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat1.x = u_xlat6 * u_xlat0.x;
    u_xlat12.xy = u_xlat12.xy * float2(float2(_CausticsSpeed, _CausticsSpeed));
    u_xlat14.xy = input.world.xy * float2(_CausticsTiling,_CausticsTiling) + u_xlat12.xy;
    u_xlat12.xy = u_xlat1.zw * float2(0.800000012, 0.800000012) + (-u_xlat12.xy);
    u_xlat16_7 = tex2Dbias(_CausticsTex, float4(u_xlat14.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_12 = tex2Dbias(_CausticsTex, float4(u_xlat12.xy, 0, _GlobalMipBias.x)).y;
    u_xlat12.x = min(u_xlat16_12, u_xlat16_7);
    u_xlat0.x = (-u_xlat0.x) * u_xlat6 + 1.0;
    u_xlat0.x = u_xlat0.x * _CausticsBrightness;
    u_xlat6 = _TimeParameters.y + 1.0;
    u_xlat6 = u_xlat6 * 0.25 + 0.5;
    u_xlat0.x = u_xlat6 * u_xlat0.x;
    u_xlat6 = min(u_xlat1.x, 1.0);
    u_xlat6 = (-u_xlat6) + _NormalStrength;
    u_xlat6 = clamp(u_xlat6, 0.0, 1.0);
    u_xlat2.xy = u_xlat16_2.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlat2.z = (-u_xlat16_3.x) + 1.0;
    u_xlat7.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * u_xlat2.xyz + float3(0.0, 0.0, -1.0);
    u_xlat6 = dot(u_xlat7.xyz, (-u_xlat4.xyz));
    u_xlat7.xyz = float3(u_xlat6,u_xlat6,u_xlat6) * _BaseColor.xyz;
    u_xlat2.xyz = (-_BaseColor.xyz) * float3(u_xlat6,u_xlat6,u_xlat6) + _FoamColor.xyz;
    u_xlat1.xyz = u_xlat1.xxx * u_xlat2.xyz + u_xlat7.xyz;
    u_xlat0.xyz = u_xlat12.xxx * u_xlat0.xxx + u_xlat1.xyz;
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.mask.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb19 = u_xlat16_3.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_3.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_3.xyz = u_xlat16_1.xyz * u_xlat16_3.xxx;
    u_xlat16_3.xyz = (bool(u_xlatb19)) ? u_xlat16_3.xyz : u_xlat16_1.xyz;
    u_xlat16_5.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_3.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_3.xyz = u_xlat0.xyz * u_xlat16_5.xyz + u_xlat16_3.xyz;
    u_xlat0.w = _BaseColor.w;
    u_xlat16_1.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_3.xyz + (-u_xlat0.xyz);
    u_xlat16_1.w = 0.0;
    u_xlat16_0 = float4(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_1 + u_xlat0;
    result = max(u_xlat16_0, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float4 vertex:POSITION; };
struct Varying { float4 position:SV_POSITION; float2 mask:TEXCOORD2; float3 world:TEXCOORD1; };

float _CausticsBrightness;
float _CausticsSpeed;
float _CausticsTiling;
float _FoamSize;
float _FoamSpeed;
float _FoamTiling;
float _NormalSpeed;
float _NormalStrength;
float _NormalTiling;
float _Speed;
float2 _GlobalMipBias;
float3 _LightDir;
float4 _BaseColor;
float4 _Direction;
float4 _FoamColor;
float4 _TimeParameters;
sampler2D _BumpMap;
sampler2D _CausticsTex;
sampler2D _FoamTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    output.world.xyz = u_xlat0.xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.mask.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
bool u_xlatb0;
float4 u_xlat1;
half u_xlat16_1;
float3 u_xlat2;
half3 u_xlat16_2;
half u_xlat16_3;
float3 u_xlat4;
float u_xlat5;
float3 u_xlat6;
half u_xlat16_6;
float2 u_xlat10;
half u_xlat16_10;
float2 u_xlat12;

    u_xlatb0 = _BaseColor.w==0.0;
    if(u_xlatb0){clip(-1);}
    u_xlat0.xy = _Direction.xy * float2(_Speed,_Speed);
    u_xlat10.xy = u_xlat0.xy * _TimeParameters.xx;
    u_xlat1 = input.world.xyxy * float4(_NormalTiling, _NormalTiling, _CausticsTiling, _CausticsTiling);
    u_xlat1.xy = u_xlat10.xy * float2(_NormalSpeed,_NormalSpeed) + u_xlat1.xy;
    u_xlat16_2.xyz = tex2Dbias(_BumpMap, float4(u_xlat1.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3 = u_xlat16_2.z * 2.0 + -1.0;
    u_xlat1.x = dot(float3(_LightDir.x, _LightDir.y, _LightDir.z), float3(_LightDir.x, _LightDir.y, _LightDir.z));
    u_xlat1.x = rsqrt(u_xlat1.x);
    u_xlat4.xyz = u_xlat1.xxx * float3(_LightDir.x, _LightDir.y, _LightDir.z);
    u_xlat1.xy = input.world.xy * float2(_FoamTiling,_FoamTiling);
    u_xlat12.xy = u_xlat10.xy * float2(float2(_FoamSpeed, _FoamSpeed)) + u_xlat1.xy;
    u_xlat0.xy = (-u_xlat0.xy) * _TimeParameters.xx + float2(1.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(float2(_FoamSpeed, _FoamSpeed));
    u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5);
    u_xlat0.xy = u_xlat1.xy * float2(0.5, 0.5) + u_xlat0.xy;
    u_xlat16_1 = tex2Dbias(_FoamTex, float4(u_xlat12.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0 = tex2Dbias(_FoamTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0.x = u_xlat16_0 + u_xlat16_1;
    u_xlat5 = (-_FoamSize) + 1.0;
    u_xlat0.x = u_xlat0.x + (-_FoamSize);
    u_xlat5 = float(1.0) / u_xlat5;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat5 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = u_xlat0.x * u_xlat5;
    u_xlat5 = _FoamColor.w;
    u_xlat5 = clamp(u_xlat5, 0.0, 1.0);
    u_xlat1.x = u_xlat5 * u_xlat0.x;
    u_xlat10.xy = u_xlat10.xy * float2(float2(_CausticsSpeed, _CausticsSpeed));
    u_xlat12.xy = input.world.xy * float2(_CausticsTiling,_CausticsTiling) + u_xlat10.xy;
    u_xlat10.xy = u_xlat1.zw * float2(0.800000012, 0.800000012) + (-u_xlat10.xy);
    u_xlat16_6 = tex2Dbias(_CausticsTex, float4(u_xlat12.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_10 = tex2Dbias(_CausticsTex, float4(u_xlat10.xy, 0, _GlobalMipBias.x)).y;
    u_xlat10.x = min(u_xlat16_10, u_xlat16_6);
    u_xlat0.x = (-u_xlat0.x) * u_xlat5 + 1.0;
    u_xlat0.x = u_xlat0.x * _CausticsBrightness;
    u_xlat5 = _TimeParameters.y + 1.0;
    u_xlat5 = u_xlat5 * 0.25 + 0.5;
    u_xlat0.x = u_xlat5 * u_xlat0.x;
    u_xlat5 = min(u_xlat1.x, 1.0);
    u_xlat5 = (-u_xlat5) + _NormalStrength;
    u_xlat5 = clamp(u_xlat5, 0.0, 1.0);
    u_xlat2.xy = u_xlat16_2.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlat2.z = (-u_xlat16_3) + 1.0;
    u_xlat6.xyz = float3(u_xlat5,u_xlat5,u_xlat5) * u_xlat2.xyz + float3(0.0, 0.0, -1.0);
    u_xlat5 = dot(u_xlat6.xyz, (-u_xlat4.xyz));
    u_xlat6.xyz = float3(u_xlat5,u_xlat5,u_xlat5) * _BaseColor.xyz;
    u_xlat2.xyz = (-_BaseColor.xyz) * float3(u_xlat5,u_xlat5,u_xlat5) + _FoamColor.xyz;
    u_xlat1.xyz = u_xlat1.xxx * u_xlat2.xyz + u_xlat6.xyz;
    u_xlat0.xyz = u_xlat10.xxx * u_xlat0.xxx + u_xlat1.xyz;
    u_xlat0.w = _BaseColor.w;
    result = max(u_xlat0, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
