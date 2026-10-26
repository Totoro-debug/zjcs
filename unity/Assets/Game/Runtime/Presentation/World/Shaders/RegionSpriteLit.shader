Shader "Zjcs/Official/World/RegionSpriteLit" {
Properties {
_MainTex ("Main",2D)="white" {}
_MaskTex ("Mask",2D)="white" {}
_Color ("Color",Color)=(1,1,1,1)
_RendererColor ("Renderer",Color)=(1,1,1,1)
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
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 recession:TEXCOORD4; };
float2 _GlobalMipBias;
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _MainTex_ST;
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _MaskTex;
sampler2D _ShapeLightTexture0;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
float4 frag(Varying input) : SV_Target { float4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half4 u_xlat16_3;
half4 u_xlat16_4;
bool u_xlatb17;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat0 = u_xlat16_0 * input.color;
    u_xlatb1 = u_xlat0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_1 = tex2Dbias(_MaskTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_2.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb17 = u_xlat16_3.x!=0.0;
    u_xlat16_3 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_4 = (-u_xlat16_1) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_4 = u_xlat16_4 * _ShapeLightInvertedFilter0;
    u_xlat16_1 = u_xlat16_3 * u_xlat16_1 + u_xlat16_4;
    u_xlat16_3.x = dot(u_xlat16_1, _ShapeLightMaskFilter0);
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_3.xxx;
    u_xlat16_3.xyz = (bool(u_xlatb17)) ? u_xlat16_3.xyz : u_xlat16_2.xyz;
    u_xlat16_4.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_3.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_3.xyz = u_xlat0.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat16_1.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_3.xyz + (-u_xlat0.xyz);
    u_xlat16_1.w = 0.0;
    u_xlat16_0 = float4(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_1 + u_xlat0;
    result = max(u_xlat16_0, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
ENDCG
}
}
}
