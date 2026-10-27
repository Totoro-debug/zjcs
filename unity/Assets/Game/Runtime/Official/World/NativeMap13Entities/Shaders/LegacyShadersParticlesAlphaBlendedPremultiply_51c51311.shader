Shader "Zjcs/Official/World/Map13/Entity/LegacyShadersParticlesAlphaBlendedPremultiply_51c51311" {
Properties {
_MainTex ("Particle Texture", 2D) = "white" {}
_InvFade ("Soft Particles Factor", Range(0.009999999776482582,3.0)) = 1.0
}
SubShader {
Tags { "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGB
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ SOFTPARTICLES_ON
#include "UnityCG.cginc"
#if !defined(SOFTPARTICLES_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

float4 _MainTex_ST;
sampler2D _MainTex;
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
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;

    u_xlat16_0 = tex2D(_MainTex, input.uv.xy);
    u_xlat0 = u_xlat16_0 * input.color;
    u_xlat0 = u_xlat0 * input.color.wwww;
    result = u_xlat0;
    return result;

}
#elif defined(SOFTPARTICLES_ON)
struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; };

float _InvFade;
float4 _MainTex_ST;
sampler2D _CameraDepthTexture;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float u_xlat2;

    u_xlat0 = input.vertex.yyyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]);
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]);
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat1;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat2 = u_xlat0.y * float4(UNITY_MATRIX_V[0][1],UNITY_MATRIX_V[1][1],UNITY_MATRIX_V[2][1],UNITY_MATRIX_V[3][1]).z;
    u_xlat0.x = float4(UNITY_MATRIX_V[0][0],UNITY_MATRIX_V[1][0],UNITY_MATRIX_V[2][0],UNITY_MATRIX_V[3][0]).z * u_xlat0.x + u_xlat2;
    u_xlat0.x = float4(UNITY_MATRIX_V[0][2],UNITY_MATRIX_V[1][2],UNITY_MATRIX_V[2][2],UNITY_MATRIX_V[3][2]).z * u_xlat0.z + u_xlat0.x;
    u_xlat0.x = float4(UNITY_MATRIX_V[0][3],UNITY_MATRIX_V[1][3],UNITY_MATRIX_V[2][3],UNITY_MATRIX_V[3][3]).z * u_xlat0.w + u_xlat0.x;
    output.world.z = (-u_xlat0.x);
    u_xlat0.x = u_xlat1.y * _ProjectionParams.x;
    u_xlat0.w = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat1.xw * float2(0.5, 0.5);
    output.world.w = u_xlat1.w;
    output.world.xy = u_xlat0.zz + u_xlat0.xw;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
float4 u_xlat1;
half4 u_xlat16_2;

    u_xlat0.xy = input.world.xy / input.world.ww;
    u_xlat0.x = tex2D(_CameraDepthTexture, u_xlat0.xy).x;
    u_xlat0.x = _ZBufferParams.z * u_xlat0.x + _ZBufferParams.w;
    u_xlat0.x = float(1.0) / u_xlat0.x;
    u_xlat0.x = u_xlat0.x + (-input.world.z);
    u_xlat0.x = u_xlat0.x * _InvFade;
    u_xlat0.x = clamp(u_xlat0.x, 0.0, 1.0);
    u_xlat0.x = u_xlat0.x * input.color.w;
    u_xlat1.w = u_xlat0.x * u_xlat0.x;
    u_xlat16_2 = tex2D(_MainTex, input.uv.xy);
    u_xlat0.w = u_xlat16_2.w;
    u_xlat1.xyz = u_xlat16_2.xyz * input.color.xyz;
    u_xlat0 = u_xlat0.xxxw * u_xlat1;
    result = u_xlat0;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
