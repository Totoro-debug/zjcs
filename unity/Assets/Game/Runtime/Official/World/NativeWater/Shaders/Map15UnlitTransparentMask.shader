Shader "Zjcs/Official/World/Map15/UnlitTransparentMask" {
Properties {
_MainTex ("Diffuse", 2D) = "white" {}
[Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 0.0
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="TransparentMask" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask 0
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

float2 _GlobalMipBias;
half4 _MainTex_ST;
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
half u_xlat16_0;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).w;
    u_xlat0 = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0) * input.color.wwww;
    result = u_xlat0;
    return result;

}
ENDCG
}
}
Fallback Off
}
