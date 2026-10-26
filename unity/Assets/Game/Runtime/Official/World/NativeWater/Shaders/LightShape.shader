Shader "Zjcs/Official/World/Map13/LightShape" {
Properties {
_SrcBlend ("__src", Float) = 1.0
_DstBlend ("__dst", Float) = 0.0
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
BlendOp Add, Add
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ LIGHT_QUALITY_FAST
#pragma multi_compile __ SPRITE_LIGHT
#pragma multi_compile __ USE_ADDITIVE_BLENDING
#pragma multi_compile __ USE_NORMAL_MAP
#pragma multi_compile __ USE_RECESSION
#include "UnityCG.cginc"
#if defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat5;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_14 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * input.mask.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat5 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat5) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat5 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat5;
    u_xlat1.xyz = u_xlat16_0.xyz * u_xlat1.xxx;
    result.xyz = u_xlat1.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_14 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * input.mask.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat5;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_14 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * input.mask.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat5 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat5) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat5 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat5;
    u_xlat1.x = u_xlat16_0.w * u_xlat1.x;
    result.xyz = u_xlat16_0.xyz;
    result.w = u_xlat1.x;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_14 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * input.mask.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.xyz = u_xlat0.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat5;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat1.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat1.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat1.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat1.xyz);
    u_xlat16_14 = u_xlat16_14 + (-_LightZDistance);
    u_xlat16_3.xyz = (-float3(u_xlat16_14,u_xlat16_14,u_xlat16_14)) * u_xlat1.xyz + _LightPosition.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz + (-input.mask.xyz);
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat16_3.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * u_xlat16_3.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat5 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat5) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat5 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat5;
    u_xlat1.xyz = u_xlat16_0.xyz * u_xlat1.xxx;
    result.xyz = u_xlat1.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.world.xy = u_xlat0.xy;
    output.uv3.xy = u_xlat0.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat1.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat1.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat1.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat1.xyz);
    u_xlat16_14 = u_xlat16_14 + (-_LightZDistance);
    u_xlat16_3.xyz = (-float3(u_xlat16_14,u_xlat16_14,u_xlat16_14)) * u_xlat1.xyz + _LightPosition.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz + (-input.mask.xyz);
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat16_3.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * u_xlat16_3.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.xyz = u_xlat0.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat5;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat1.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat1.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat1.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat1.xyz);
    u_xlat16_14 = u_xlat16_14 + (-_LightZDistance);
    u_xlat16_3.xyz = (-float3(u_xlat16_14,u_xlat16_14,u_xlat16_14)) * u_xlat1.xyz + _LightPosition.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz + (-input.mask.xyz);
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat16_3.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * u_xlat16_3.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat5 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat5) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat5 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat5;
    u_xlat1.x = u_xlat16_0.w * u_xlat1.x;
    result.xyz = u_xlat16_0.xyz;
    result.w = u_xlat1.x;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.world.xy = u_xlat0.xy;
    output.uv3.xy = u_xlat0.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat1.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat1.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat1.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat1.xyz);
    u_xlat16_14 = u_xlat16_14 + (-_LightZDistance);
    u_xlat16_3.xyz = (-float3(u_xlat16_14,u_xlat16_14,u_xlat16_14)) * u_xlat1.xyz + _LightPosition.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz + (-input.mask.xyz);
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat16_3.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * u_xlat16_3.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat16_0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
float u_xlat4;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(float3(_ShadowIntensity, _ShadowIntensity, _ShadowIntensity));
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat4 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat4) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat4 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat4;
    u_xlat1.xyz = u_xlat16_0.xyz * u_xlat1.xxx;
    result.xyz = u_xlat1.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
float u_xlat4;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(float3(_ShadowIntensity, _ShadowIntensity, _ShadowIntensity));
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat4 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat4) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat4 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat4;
    u_xlat1.x = u_xlat16_0.w * u_xlat1.x;
    result.xyz = u_xlat16_0.xyz;
    result.w = u_xlat1.x;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
float u_xlat4;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(float3(_ShadowIntensity, _ShadowIntensity, _ShadowIntensity));
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat4 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat4) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat4 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat4;
    u_xlat1.xyz = u_xlat16_0.xyz * u_xlat1.xxx;
    result.xyz = u_xlat1.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0.wwww * u_xlat16_0;
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
float u_xlat4;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(float3(_ShadowIntensity, _ShadowIntensity, _ShadowIntensity));
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat4 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat4) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat4 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat4;
    u_xlat1.x = u_xlat16_0.w * u_xlat1.x;
    result.xyz = u_xlat16_0.xyz;
    result.w = u_xlat1.x;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _CookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.xy = input.uv.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat5;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = u_xlat16_0.xxxx * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_14 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * input.mask.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat5 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat5) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat5 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat5;
    u_xlat1.xyz = u_xlat16_0.xyz * u_xlat1.xxx;
    result.xyz = u_xlat1.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = u_xlat16_0.xxxx * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_14 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * input.mask.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half3 u_xlat16_1;
half3 u_xlat16_2;
float u_xlat3;
half3 u_xlat16_3;
float3 u_xlat4;
half3 u_xlat16_4;
half u_xlat16_5;
half u_xlat16_10;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_3.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_1.xyz = u_xlat16_3.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_10 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_10 = rsqrt(u_xlat16_10);
    u_xlat16_2.xyz = float3(u_xlat16_10,u_xlat16_10,u_xlat16_10) * input.mask.xyz;
    u_xlat16_1.x = dot(u_xlat16_2.xyz, u_xlat16_1.xyz);
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_4.xyz = u_xlat16_1.xxx * input.color.xyz;
    u_xlat16_1.x = u_xlat16_0.x * u_xlat16_1.x;
    u_xlatb0 = _ShadowIntensity<1.0;
    if(u_xlatb0){
        u_xlat16_0 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat0 = u_xlat16_0;
        u_xlat0 = clamp(u_xlat0, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat0);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat0.xyz = u_xlat16_4.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2.x) + 1.0;
        u_xlat0.xyz = u_xlat0.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat4.xyz = u_xlat16_4.xyz * u_xlat16_2.xxx + u_xlat0.xyz;
        u_xlat16_4.xyz = u_xlat4.xyz;
    }
    u_xlat0.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat0.x = dot(u_xlat0.xy, u_xlat0.xy);
    u_xlat0.x = sqrt(u_xlat0.x);
    u_xlat3 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat0.x = (-u_xlat3) + u_xlat0.x;
    u_xlat0.x = u_xlat0.x / _RecessionBlurEdge;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat3 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = u_xlat0.x * u_xlat3;
    u_xlat0.x = u_xlat0.x * u_xlat16_1.x;
    result.xyz = u_xlat16_4.xyz;
    result.w = u_xlat0.x;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float u_xlat9;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    u_xlat1.xyz = (-u_xlat0.xyz) + _LightPosition.xyz;
    u_xlat2.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat2.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat2.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat2.xyz);
    u_xlat9 = u_xlat9 + (-_LightZDistance);
    u_xlat1.xyz = (-float3(u_xlat9,u_xlat9,u_xlat9)) * u_xlat2.xyz + _LightPosition.xyz;
    u_xlat0.xyz = (-u_xlat0.xyz) + u_xlat1.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.mask.w = 0.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half3 u_xlat16_1;
float3 u_xlat2;
half4 u_xlat16_2;
half3 u_xlat16_3;
half u_xlat16_4;
half u_xlat16_10;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_3.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_1.xyz = u_xlat16_3.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_10 = dot(input.mask.xyz, input.mask.xyz);
    u_xlat16_10 = rsqrt(u_xlat16_10);
    u_xlat16_2.xyz = float3(u_xlat16_10,u_xlat16_10,u_xlat16_10) * input.mask.xyz;
    u_xlat16_1.x = dot(u_xlat16_2.xyz, u_xlat16_1.xyz);
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_2.xyz = u_xlat16_1.xxx * input.color.xyz;
    u_xlat16_2.w = u_xlat16_0.x * u_xlat16_1.x;
    u_xlatb0 = _ShadowIntensity<1.0;
    if(u_xlatb0){
        u_xlat16_0 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat0 = u_xlat16_0;
        u_xlat0 = clamp(u_xlat0, 0.0, 1.0);
        u_xlat16_1.x = dot(_ShadowColorMask, u_xlat0);
        u_xlat16_1.x = (-u_xlat16_1.x) + 1.0;
        u_xlat0.xyz = u_xlat16_2.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_4 = (-u_xlat16_1.x) + 1.0;
        u_xlat0.xyz = u_xlat0.xyz * float3(u_xlat16_4,u_xlat16_4,u_xlat16_4);
        u_xlat2.xyz = u_xlat16_2.xyz * u_xlat16_1.xxx + u_xlat0.xyz;
        u_xlat16_2.xyz = u_xlat2.xyz;
    }
    result = u_xlat16_2;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.xyz = u_xlat0.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat5;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = u_xlat16_0.xxxx * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat1.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat1.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat1.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat1.xyz);
    u_xlat16_14 = u_xlat16_14 + (-_LightZDistance);
    u_xlat16_3.xyz = (-float3(u_xlat16_14,u_xlat16_14,u_xlat16_14)) * u_xlat1.xyz + _LightPosition.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz + (-input.mask.xyz);
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat16_3.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * u_xlat16_3.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat5 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat5) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat5 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat5;
    u_xlat1.xyz = u_xlat16_0.xyz * u_xlat1.xxx;
    result.xyz = u_xlat1.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.world.xy = u_xlat0.xy;
    output.uv3.xy = u_xlat0.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;
half u_xlat16_6;
half u_xlat16_14;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = u_xlat16_0.xxxx * input.color;
    u_xlat16_1.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.xyz = u_xlat16_1.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_3.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat1.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat1.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat1.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat1.xyz);
    u_xlat16_14 = u_xlat16_14 + (-_LightZDistance);
    u_xlat16_3.xyz = (-float3(u_xlat16_14,u_xlat16_14,u_xlat16_14)) * u_xlat1.xyz + _LightPosition.xyz;
    u_xlat16_3.xyz = u_xlat16_3.xyz + (-input.mask.xyz);
    u_xlat16_14 = dot(u_xlat16_3.xyz, u_xlat16_3.xyz);
    u_xlat16_14 = rsqrt(u_xlat16_14);
    u_xlat16_3.xyz = float3(u_xlat16_14,u_xlat16_14,u_xlat16_14) * u_xlat16_3.xyz;
    u_xlat16_2.x = dot(u_xlat16_3.xyz, u_xlat16_2.xyz);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlat16_0 = u_xlat0 * u_xlat16_2.xxxx;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat16_0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat0.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx + u_xlat1.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    result = u_xlat16_0;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat1.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.xyz = u_xlat0.xyz;
    output.mask.xyz = u_xlat0.xyz;
    output.uv4.w = 1.0;
    output.world.xy = u_xlat1.xy;
    output.uv3.xy = u_xlat1.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half3 u_xlat16_1;
half3 u_xlat16_2;
float3 u_xlat3;
float u_xlat4;
half3 u_xlat16_4;
float3 u_xlat5;
half3 u_xlat16_5;
half u_xlat16_6;
half u_xlat16_13;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_4.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_1.xyz = u_xlat16_4.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat3.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat3.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat3.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_13 = dot(u_xlat16_2.xyz, u_xlat3.xyz);
    u_xlat16_13 = u_xlat16_13 + (-_LightZDistance);
    u_xlat16_2.xyz = (-float3(u_xlat16_13,u_xlat16_13,u_xlat16_13)) * u_xlat3.xyz + _LightPosition.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz + (-input.mask.xyz);
    u_xlat16_13 = dot(u_xlat16_2.xyz, u_xlat16_2.xyz);
    u_xlat16_13 = rsqrt(u_xlat16_13);
    u_xlat16_2.xyz = float3(u_xlat16_13,u_xlat16_13,u_xlat16_13) * u_xlat16_2.xyz;
    u_xlat16_1.x = dot(u_xlat16_2.xyz, u_xlat16_1.xyz);
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5.xyz = u_xlat16_1.xxx * input.color.xyz;
    u_xlat16_1.x = u_xlat16_0.x * u_xlat16_1.x;
    u_xlatb0 = _ShadowIntensity<1.0;
    if(u_xlatb0){
        u_xlat16_0 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat0 = u_xlat16_0;
        u_xlat0 = clamp(u_xlat0, 0.0, 1.0);
        u_xlat16_2.x = dot(_ShadowColorMask, u_xlat0);
        u_xlat16_2.x = (-u_xlat16_2.x) + 1.0;
        u_xlat0.xyz = u_xlat16_5.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2.x) + 1.0;
        u_xlat0.xyz = u_xlat0.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat5.xyz = u_xlat16_5.xyz * u_xlat16_2.xxx + u_xlat0.xyz;
        u_xlat16_5.xyz = u_xlat5.xyz;
    }
    u_xlat0.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat0.x = dot(u_xlat0.xy, u_xlat0.xy);
    u_xlat0.x = sqrt(u_xlat0.x);
    u_xlat4 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat0.x = (-u_xlat4) + u_xlat0.x;
    u_xlat0.x = u_xlat0.x / _RecessionBlurEdge;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat4 = u_xlat0.x * -2.0 + 3.0;
    u_xlat0.x = u_xlat0.x * u_xlat0.x;
    u_xlat0.x = u_xlat0.x * u_xlat4;
    u_xlat0.x = u_xlat0.x * u_xlat16_1.x;
    result.xyz = u_xlat16_5.xyz;
    result.w = u_xlat0.x;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 mask:TEXCOORD2; float2 uv3:TEXCOORD3; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _LightZDistance;
half4 _LightColor;
half4 _LightPosition;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _NormalMap;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.mask.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    u_xlat0.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.world.xy = u_xlat0.xy;
    output.uv3.xy = u_xlat0.xy;
    output.mask.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool u_xlatb0;
half3 u_xlat16_1;
float3 u_xlat2;
half4 u_xlat16_2;
float3 u_xlat3;
half3 u_xlat16_4;
half u_xlat16_5;
half u_xlat16_13;

    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_4.xyz = tex2Dbias(_NormalMap, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_1.xyz = u_xlat16_4.xyz * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, -1.0);
    u_xlat16_2.xyz = (-input.mask.xyz) + _LightPosition.xyz;
    u_xlat3.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z;
    u_xlat3.y = float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat3.z = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z;
    u_xlat16_13 = dot(u_xlat16_2.xyz, u_xlat3.xyz);
    u_xlat16_13 = u_xlat16_13 + (-_LightZDistance);
    u_xlat16_2.xyz = (-float3(u_xlat16_13,u_xlat16_13,u_xlat16_13)) * u_xlat3.xyz + _LightPosition.xyz;
    u_xlat16_2.xyz = u_xlat16_2.xyz + (-input.mask.xyz);
    u_xlat16_13 = dot(u_xlat16_2.xyz, u_xlat16_2.xyz);
    u_xlat16_13 = rsqrt(u_xlat16_13);
    u_xlat16_2.xyz = float3(u_xlat16_13,u_xlat16_13,u_xlat16_13) * u_xlat16_2.xyz;
    u_xlat16_1.x = dot(u_xlat16_2.xyz, u_xlat16_1.xyz);
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_2.xyz = u_xlat16_1.xxx * input.color.xyz;
    u_xlat16_2.w = u_xlat16_0.x * u_xlat16_1.x;
    u_xlatb0 = _ShadowIntensity<1.0;
    if(u_xlatb0){
        u_xlat16_0 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat0 = u_xlat16_0;
        u_xlat0 = clamp(u_xlat0, 0.0, 1.0);
        u_xlat16_1.x = dot(_ShadowColorMask, u_xlat0);
        u_xlat16_1.x = (-u_xlat16_1.x) + 1.0;
        u_xlat0.xyz = u_xlat16_2.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_1.x) + 1.0;
        u_xlat0.xyz = u_xlat0.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat2.xyz = u_xlat16_2.xyz * u_xlat16_1.xxx + u_xlat0.xyz;
        u_xlat16_2.xyz = u_xlat2.xyz;
    }
    result = u_xlat16_2;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
float2 u_xlat3;
half u_xlat16_6;
float u_xlat7;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0) * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat1.xyz = u_xlat0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_1.xyz = u_xlat1.xyz;
    } else {
        u_xlat16_1.xyz = u_xlat0.xyz;
    }
    u_xlat3.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat3.x = dot(u_xlat3.xy, u_xlat3.xy);
    u_xlat3.x = sqrt(u_xlat3.x);
    u_xlat7 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat3.x = (-u_xlat7) + u_xlat3.x;
    u_xlat3.x = u_xlat3.x / _RecessionBlurEdge;
    u_xlat3.x = clamp(u_xlat3.x, 0.0, 1.0);
    u_xlat7 = u_xlat3.x * -2.0 + 3.0;
    u_xlat3.x = u_xlat3.x * u_xlat3.x;
    u_xlat3.x = u_xlat3.x * u_xlat7;
    u_xlat0.xyz = u_xlat16_1.xyz * u_xlat3.xxx;
    result = u_xlat0;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0) * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat1.xyz = u_xlat0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_1.xyz = u_xlat1.xyz;
    } else {
        u_xlat16_1.xyz = u_xlat0.xyz;
    }
    result.xyz = u_xlat16_1.xyz;
    result.w = u_xlat0.w;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
half3 u_xlat16_3;
bool u_xlatb3;
float u_xlat4;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlatb3 = _ShadowIntensity<1.0;
    if(u_xlatb3){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = input.color.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = u_xlat3.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat3.xyz = input.color.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat3.xyz;
        u_xlat16_3.xyz = u_xlat3.xyz;
    } else {
        u_xlat16_3.xyz = input.color.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat4 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat4) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat4 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat4;
    u_xlat1.x = u_xlat16_0 * u_xlat1.x;
    result.xyz = u_xlat16_3.xyz;
    result.w = u_xlat1.x;
    return result;

}
#elif defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
half3 u_xlat16_3;
bool u_xlatb3;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlatb3 = _ShadowIntensity<1.0;
    if(u_xlatb3){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = input.color.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = u_xlat3.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat3.xyz = input.color.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat3.xyz;
        u_xlat16_3.xyz = u_xlat3.xyz;
    } else {
        u_xlat16_3.xyz = input.color.xyz;
    }
    result.xyz = u_xlat16_3.xyz;
    result.w = u_xlat16_0;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
float2 u_xlat3;
half u_xlat16_6;
float u_xlat7;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0) * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_6 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_6,u_xlat16_6,u_xlat16_6);
        u_xlat1.xyz = u_xlat0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_1.xyz = u_xlat1.xyz;
    } else {
        u_xlat16_1.xyz = u_xlat0.xyz;
    }
    u_xlat3.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat3.x = dot(u_xlat3.xy, u_xlat3.xy);
    u_xlat3.x = sqrt(u_xlat3.x);
    u_xlat7 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat3.x = (-u_xlat7) + u_xlat3.x;
    u_xlat3.x = u_xlat3.x / _RecessionBlurEdge;
    u_xlat3.x = clamp(u_xlat3.x, 0.0, 1.0);
    u_xlat7 = u_xlat3.x * -2.0 + 3.0;
    u_xlat3.x = u_xlat3.x * u_xlat3.x;
    u_xlat3.x = u_xlat3.x * u_xlat7;
    u_xlat0.xyz = u_xlat16_1.xyz * u_xlat3.xxx;
    result = u_xlat0;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0) * input.color;
    u_xlatb1 = _ShadowIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat0.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat1.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat1.xyz = u_xlat0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat1.xyz;
        u_xlat16_1.xyz = u_xlat1.xyz;
    } else {
        u_xlat16_1.xyz = u_xlat0.xyz;
    }
    result.xyz = u_xlat16_1.xyz;
    result.w = u_xlat0.w;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 uv4:TEXCOORD4; };

float _RecessionBlurEdge;
float _RecessionRadius;
float _ShadowIntensity;
float2 _GlobalMipBias;
float4 _RecessionCenter;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv4.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.uv4.w = 1.0;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
half3 u_xlat16_3;
bool u_xlatb3;
float u_xlat4;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlatb3 = _ShadowIntensity<1.0;
    if(u_xlatb3){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = input.color.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = u_xlat3.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat3.xyz = input.color.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat3.xyz;
        u_xlat16_3.xyz = u_xlat3.xyz;
    } else {
        u_xlat16_3.xyz = input.color.xyz;
    }
    u_xlat1.xy = (-input.uv4.xy) + _RecessionCenter.xy;
    u_xlat1.x = dot(u_xlat1.xy, u_xlat1.xy);
    u_xlat1.x = sqrt(u_xlat1.x);
    u_xlat4 = (-_RecessionBlurEdge) + _RecessionRadius;
    u_xlat1.x = (-u_xlat4) + u_xlat1.x;
    u_xlat1.x = u_xlat1.x / _RecessionBlurEdge;
    u_xlat1.x = clamp(u_xlat1.x, 0.0, 1.0);
    u_xlat4 = u_xlat1.x * -2.0 + 3.0;
    u_xlat1.x = u_xlat1.x * u_xlat1.x;
    u_xlat1.x = u_xlat1.x * u_xlat4;
    u_xlat1.x = u_xlat16_0 * u_xlat1.x;
    result.xyz = u_xlat16_3.xyz;
    result.w = u_xlat1.x;
    return result;

}
#elif !defined(LIGHT_QUALITY_FAST) && !defined(SPRITE_LIGHT) && !defined(USE_ADDITIVE_BLENDING) && !defined(USE_NORMAL_MAP) && !defined(USE_RECESSION)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half4 _LightColor;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;

    u_xlat0.xy = float2(_FalloffDistance,_FalloffDistance) * input.color.xy + input.vertex.xy;
    u_xlat1 = u_xlat0.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xy = u_xlat0.xy / u_xlat0.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
half3 u_xlat16_3;
bool u_xlatb3;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlatb3 = _ShadowIntensity<1.0;
    if(u_xlatb3){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = input.color.xyz * float3(_ShadowIntensity,_ShadowIntensity,_ShadowIntensity);
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = u_xlat3.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat3.xyz = input.color.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat3.xyz;
        u_xlat16_3.xyz = u_xlat3.xyz;
    } else {
        u_xlat16_3.xyz = input.color.xyz;
    }
    result.xyz = u_xlat16_3.xyz;
    result.w = u_xlat16_0;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
