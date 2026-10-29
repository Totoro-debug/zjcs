Shader "Zjcs/Official/World/Map14/RootFog/Cloud" {
Properties {
_MainTex ("Texture", 2D) = "white" {}
_MainTiling ("Main Tiling", Range(0.0010000000474974513,1.0)) = 1.0
_Tint ("Tint", Color) = (1.0,1.0,1.0,1.0)
_NoiseTex ("Noise", 2D) = "white" {}
_NoiseTiling ("Noise Tiling", Range(0.10000000149011612,20.0)) = 1.0
_MaskTex ("Mask", 2D) = "white" {}
_CloudDir ("Cloud Dir", Vector) = (0.0,0.0,0.0,0.0)
_Strength ("Strength", Range(0.0,20.0)) = 1.0
_DisturbSpeed ("Disturb Speed", Range(0.0,0.5)) = 0.05000000074505806
_CloudRect ("Cloud Rect", Vector) = (-0.5,-0.5,1.0,1.0)
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest Always
Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float2 mask:TEXCOORD2; };

float _DisturbSpeed;
float _MainTiling;
float2 _GlobalMipBias;
half _NoiseTiling;
half _Strength;
half2 _CloudDir;
half3 _Tint;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;
float2 u_xlat4;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat1 = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat1;
    u_xlat1.xyz = u_xlat1.xyw / u_xlat1.www;
    u_xlat4.xy = _Time.yy * _CloudDir.xy;
    output.uv.xy = u_xlat0.xy * float2(_MainTiling,_MainTiling) + u_xlat4.xy;
    output.world.xy = u_xlat0.xy * float2(float2(_NoiseTiling, _NoiseTiling));
    u_xlat0.x = u_xlat1.y * _ProjectionParams.x;
    u_xlat1.xz = u_xlat1.xz * float2(0.5, 0.5);
    u_xlat1.w = u_xlat0.x * 0.5;
    u_xlat0.xy = u_xlat1.zz + u_xlat1.xw;
    output.mask.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half4 u_xlat16_0;
half u_xlat16_1;
half u_xlat16_2;

    u_xlat0.x = _Time.y * _DisturbSpeed + 1.0;
    u_xlat0.xy = u_xlat0.xx + (-input.world.xy);
    u_xlat16_0.x = tex2Dbias(_NoiseTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2 = tex2Dbias(_NoiseTex, float4(input.world.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_1 = u_xlat16_0.x * u_xlat16_2;
    u_xlat16_1 = u_xlat16_1 * _Strength;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_0.x = tex2Dbias(_MaskTex, float4(input.mask.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_1 = u_xlat16_0.x + u_xlat16_1;
    u_xlat16_1 = u_xlat16_0.x * u_xlat16_1;
    u_xlat16_1 = clamp(u_xlat16_1, 0.0, 1.0);
    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    result.w = u_xlat16_1 * u_xlat16_0.w;
    result.xyz = u_xlat16_0.xyz * _Tint.xyz;
    return result;

}
ENDCG
}
}
Fallback Off
}
