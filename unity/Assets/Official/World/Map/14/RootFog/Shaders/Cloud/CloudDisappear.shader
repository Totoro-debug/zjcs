Shader "Zjcs/Official/World/Map14/RootFog/CloudDisappear" {
Properties {
_MainTex ("Sprite Texture", 2D) = "white" {}
_CellSize ("CellSize", Float) = 2.0
_DiffusionSpeed ("Diffusion Speed", Float) = 3.0
}
SubShader {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Pass {
Tags { "CanUseSpriteAtlas"="true" "IGNOREPROJECTOR"="true" "PreviewType"="Plane" "QUEUE"="Transparent" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One Zero, One Zero
BlendOp Max, Max
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float world:TEXCOORD1; float mask:TEXCOORD2; };

float _CellSize;
float _DiffusionSpeed;
float2 _GlobalMipBias;
sampler2D _MainTex;
Varying vert(Input input) { Varying output = (Varying)0;
float3 u_xlat0;
float4 u_xlat1;

    u_xlat0.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat0.xyz;
    u_xlat0.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat0.xyz;
    u_xlat0.xyz = u_xlat0.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat1 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    u_xlat0.xy = u_xlat0.xy + (-input.uv1.xy);
    u_xlat0.x = dot(u_xlat0.xy, u_xlat0.xy);
    output.world = sqrt(u_xlat0.x);
    output.position = u_xlat1 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.uv.xy = input.uv.xy;
    output.mask = input.uv1.z;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float u_xlat0;
half u_xlat16_1;

    u_xlat0 = (-input.mask) + _Time.y;
    u_xlat0 = u_xlat0 * _DiffusionSpeed;
    u_xlat0 = max(u_xlat0, 0.0);
    u_xlat0 = (-u_xlat0) + input.world;
    u_xlat0 = u_xlat0 / _CellSize;
    u_xlat0 = clamp(u_xlat0, 0.0, 1.0);
    u_xlat16_1 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x)).w;
    result.xw = float2(u_xlat0,u_xlat0) * float2(u_xlat16_1,u_xlat16_1);
    result.yz = float2(0.0, 0.0);
    return result;

}
ENDCG
}
}
Fallback Off
}
