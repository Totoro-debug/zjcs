Shader "Zjcs/Official/World/Map13/LightShapeVolume" {
Properties {

}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest Off
Blend SrcAlpha One, SrcAlpha One
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ SPRITE_LIGHT
#include "UnityCG.cginc"
#if defined(SPRITE_LIGHT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowVolumeIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _InverseHDREmulationScale;
half _VolumeOpacity;
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
    output.color = _LightColor * float4(_InverseHDREmulationScale, _InverseHDREmulationScale, _InverseHDREmulationScale, _VolumeOpacity);
    output.uv.xy = input.uv.xy;
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

    u_xlat16_0 = tex2Dbias(_CookieTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat0 = u_xlat16_0 * input.color;
    u_xlatb1 = _ShadowVolumeIntensity<1.0;
    if(u_xlatb1){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat1.xyz = u_xlat0.xyz * float3(float3(_ShadowVolumeIntensity, _ShadowVolumeIntensity, _ShadowVolumeIntensity));
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
#elif !defined(SPRITE_LIGHT)
struct Input { float3 vertex:POSITION; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _ShadowVolumeIntensity;
float2 _GlobalMipBias;
half _FalloffDistance;
half _FalloffIntensity;
half _InverseHDREmulationScale;
half _VolumeOpacity;
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
    output.color = _LightColor * float4(_InverseHDREmulationScale, _InverseHDREmulationScale, _InverseHDREmulationScale, _VolumeOpacity);
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
half3 u_xlat16_3;
bool u_xlatb3;
half u_xlat16_5;

    u_xlat16_0 = tex2Dbias(_FalloffLookup, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat0 = u_xlat16_0 * input.color.w;
    u_xlatb3 = _ShadowVolumeIntensity<1.0;
    if(u_xlatb3){
        u_xlat16_1 = tex2Dbias(_ShadowTex, float4(input.world.xy, 0, _GlobalMipBias.x));
        u_xlat1 = u_xlat16_1;
        u_xlat1 = clamp(u_xlat1, 0.0, 1.0);
        u_xlat16_2 = dot(_ShadowColorMask, u_xlat1);
        u_xlat16_2 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = input.color.xyz * float3(float3(_ShadowVolumeIntensity, _ShadowVolumeIntensity, _ShadowVolumeIntensity));
        u_xlat16_5 = (-u_xlat16_2) + 1.0;
        u_xlat3.xyz = u_xlat3.xyz * float3(u_xlat16_5,u_xlat16_5,u_xlat16_5);
        u_xlat3.xyz = input.color.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2) + u_xlat3.xyz;
        u_xlat16_3.xyz = u_xlat3.xyz;
    } else {
        u_xlat16_3.xyz = input.color.xyz;
    }
    result.xyz = u_xlat16_3.xyz;
    result.w = u_xlat0;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
