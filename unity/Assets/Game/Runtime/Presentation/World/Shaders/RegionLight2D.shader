Shader "Zjcs/Official/World/RegionLight2D" {
Properties {

}
SubShader {
Tags { "Queue"="Transparent" "RenderType"="Transparent" }
Cull Off ZWrite Off ZTest LEqual
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
Pass {
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_RECESSION
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 recession:TEXCOORD4; };
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
#if defined(USE_RECESSION)
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
    output.recession.xyz = u_xlat0.xyz;
    output.position = u_xlat1;
    u_xlat0.xy = u_xlat1.xy / u_xlat1.ww;
    output.world.xy = u_xlat0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    output.color.xyz = float3(_InverseHDREmulationScale,_InverseHDREmulationScale,_InverseHDREmulationScale) * _LightColor.xyz;
    output.color.w = input.color.w;
    output.uv.x = input.color.w;
    output.uv.y = _FalloffIntensity;
    output.recession.w = 1.0;
    return output;

}
float4 frag(Varying input) : SV_Target { float4 result = 0;
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
    u_xlat1.xy = (-input.recession.xy) + _RecessionCenter.xy;
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
#else
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
float4 frag(Varying input) : SV_Target { float4 result = 0;
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
}
