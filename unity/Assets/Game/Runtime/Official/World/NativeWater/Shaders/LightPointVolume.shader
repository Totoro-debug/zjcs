Shader "Zjcs/Official/World/Map13/LightPointVolume" {
Properties {

}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One One, One One
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_POINT_LIGHT_COOKIES
#include "UnityCG.cginc"
#if defined(USE_POINT_LIGHT_COOKIES)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float2 uv5:TEXCOORD5; };

float _ShadowVolumeIntensity;
float2 _GlobalMipBias;
float4x4 _LightInvMatrix;
half _FalloffIntensity;
half _InnerAngleMult;
half _InnerRadiusMult;
half _InverseHDREmulationScale;
half _IsFullSpotlight;
half _OuterAngle;
half _VolumeOpacity;
half4 _LightColor;
half4 _LightLookup_TexelSize;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _LightLookup;
sampler2D _PointLightCookieTex;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
half4 phase0_Output0_1;
float4 u_xlat0;
float4 u_xlat1;
half u_xlat16_2;
float3 u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    output.uv5.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    u_xlat3.xz = u_xlat0.yy * float4(_LightInvMatrix[0][1],_LightInvMatrix[1][1],_LightInvMatrix[2][1],_LightInvMatrix[3][1]).xy;
    u_xlat0.xy = float4(_LightInvMatrix[0][0],_LightInvMatrix[1][0],_LightInvMatrix[2][0],_LightInvMatrix[3][0]).xy * u_xlat0.xx + u_xlat3.xz;
    u_xlat0.xy = float4(_LightInvMatrix[0][2],_LightInvMatrix[1][2],_LightInvMatrix[2][2],_LightInvMatrix[3][2]).xy * u_xlat0.zz + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.xy + float4(_LightInvMatrix[0][3],_LightInvMatrix[1][3],_LightInvMatrix[2][3],_LightInvMatrix[3][3]).xy;
    u_xlat0.xy = u_xlat0.xy + float2(1.0, 1.0);
    u_xlat16_2 = _LightLookup_TexelSize.x * 0.5;
    u_xlat0.zw = u_xlat0.xy * float2(0.5, 0.5) + float2(u_xlat16_2,u_xlat16_2);
    u_xlat0.xy = input.uv.xy;
    phase0_Output0_1 = u_xlat0;
output.uv = phase0_Output0_1.xy;
output.mask = phase0_Output0_1.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
float3 u_xlat2;
bool u_xlatb2;
half u_xlat16_3;
half u_xlat16_5;
half u_xlat16_7;

    u_xlat16_0.xy = tex2Dbias(_LightLookup, float4(input.mask.xy, 0, _GlobalMipBias.x)).xy;
    u_xlat16_1.x = u_xlat16_0.x * _InnerRadiusMult;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5 = (-u_xlat16_0.y) + _OuterAngle;
    u_xlat16_5 = u_xlat16_5 + _IsFullSpotlight;
    u_xlat16_5 = u_xlat16_5 * _InnerAngleMult;
    u_xlat16_5 = clamp(u_xlat16_5, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_5 * u_xlat16_1.x;
    u_xlat16_1.y = _FalloffIntensity;
    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(u_xlat16_1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_1 = tex2Dbias(_PointLightCookieTex, float4(input.mask.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = u_xlat16_1 * _LightColor;
    u_xlat16_0 = u_xlat16_0.xxxx * u_xlat16_1;
    u_xlatb2 = _ShadowVolumeIntensity<1.0;
    if(u_xlatb2){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.uv5.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_3 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_3 = (-u_xlat16_3) + 1.0;
        u_xlat2.xyz = u_xlat16_0.xyz * float3(float3(_ShadowVolumeIntensity, _ShadowVolumeIntensity, _ShadowVolumeIntensity));
        u_xlat16_7 = (-u_xlat16_3) + 1.0;
        u_xlat2.xyz = u_xlat2.xyz * float3(u_xlat16_7,u_xlat16_7,u_xlat16_7);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_3,u_xlat16_3,u_xlat16_3) + u_xlat2.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat16_0 = u_xlat16_0 * float4(_VolumeOpacity,_VolumeOpacity,_VolumeOpacity,_VolumeOpacity);
    result = u_xlat16_0 * float4(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale);
    return result;

}
#elif !defined(USE_POINT_LIGHT_COOKIES)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float2 uv5:TEXCOORD5; };

float _ShadowVolumeIntensity;
float2 _GlobalMipBias;
float4x4 _LightInvMatrix;
half _FalloffIntensity;
half _InnerAngleMult;
half _InnerRadiusMult;
half _InverseHDREmulationScale;
half _IsFullSpotlight;
half _OuterAngle;
half _VolumeOpacity;
half4 _LightColor;
half4 _LightLookup_TexelSize;
half4 _ShadowColorMask;
sampler2D _FalloffLookup;
sampler2D _LightLookup;
sampler2D _ShadowTex;
Varying vert(Input input) { Varying output = (Varying)0;
half4 phase0_Output0_1;
float4 u_xlat0;
float4 u_xlat1;
half u_xlat16_2;
float3 u_xlat3;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    u_xlat1.xy = u_xlat1.xy / u_xlat1.ww;
    output.uv5.xy = u_xlat1.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    u_xlat3.xz = u_xlat0.yy * float4(_LightInvMatrix[0][1],_LightInvMatrix[1][1],_LightInvMatrix[2][1],_LightInvMatrix[3][1]).xy;
    u_xlat0.xy = float4(_LightInvMatrix[0][0],_LightInvMatrix[1][0],_LightInvMatrix[2][0],_LightInvMatrix[3][0]).xy * u_xlat0.xx + u_xlat3.xz;
    u_xlat0.xy = float4(_LightInvMatrix[0][2],_LightInvMatrix[1][2],_LightInvMatrix[2][2],_LightInvMatrix[3][2]).xy * u_xlat0.zz + u_xlat0.xy;
    u_xlat0.xy = u_xlat0.xy + float4(_LightInvMatrix[0][3],_LightInvMatrix[1][3],_LightInvMatrix[2][3],_LightInvMatrix[3][3]).xy;
    u_xlat0.xy = u_xlat0.xy + float2(1.0, 1.0);
    u_xlat16_2 = _LightLookup_TexelSize.x * 0.5;
    u_xlat0.zw = u_xlat0.xy * float2(0.5, 0.5) + float2(u_xlat16_2,u_xlat16_2);
    u_xlat0.xy = input.uv.xy;
    phase0_Output0_1 = u_xlat0;
output.uv = phase0_Output0_1.xy;
output.mask = phase0_Output0_1.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
float3 u_xlat2;
bool u_xlatb2;
half u_xlat16_3;
half u_xlat16_5;
half u_xlat16_7;

    u_xlat16_0.xy = tex2Dbias(_LightLookup, float4(input.mask.xy, 0, _GlobalMipBias.x)).xy;
    u_xlat16_1.x = u_xlat16_0.x * _InnerRadiusMult;
    u_xlat16_1.x = clamp(u_xlat16_1.x, 0.0, 1.0);
    u_xlat16_5 = (-u_xlat16_0.y) + _OuterAngle;
    u_xlat16_5 = u_xlat16_5 + _IsFullSpotlight;
    u_xlat16_5 = u_xlat16_5 * _InnerAngleMult;
    u_xlat16_5 = clamp(u_xlat16_5, 0.0, 1.0);
    u_xlat16_1.x = u_xlat16_5 * u_xlat16_1.x;
    u_xlat16_1.y = _FalloffIntensity;
    u_xlat16_0.x = tex2Dbias(_FalloffLookup, float4(u_xlat16_1.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_0 = u_xlat16_0.xxxx * _LightColor;
    u_xlatb2 = _ShadowVolumeIntensity<1.0;
    if(u_xlatb2){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.uv5.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_3 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_3 = (-u_xlat16_3) + 1.0;
        u_xlat2.xyz = u_xlat16_0.xyz * float3(float3(_ShadowVolumeIntensity, _ShadowVolumeIntensity, _ShadowVolumeIntensity));
        u_xlat16_7 = (-u_xlat16_3) + 1.0;
        u_xlat2.xyz = u_xlat2.xyz * float3(u_xlat16_7,u_xlat16_7,u_xlat16_7);
        u_xlat0.xyz = u_xlat16_0.xyz * float3(u_xlat16_3,u_xlat16_3,u_xlat16_3) + u_xlat2.xyz;
        u_xlat16_0.xyz = u_xlat0.xyz;
    }
    u_xlat16_0 = u_xlat16_0 * float4(_VolumeOpacity,_VolumeOpacity,_VolumeOpacity,_VolumeOpacity);
    result = u_xlat16_0 * float4(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale);
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
