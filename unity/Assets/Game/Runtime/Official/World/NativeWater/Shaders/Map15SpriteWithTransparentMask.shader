Shader "Zjcs/Official/World/Map15/SpriteWithTransparentMask" {
Properties {
_MainTex ("Diffuse", 2D) = "white" {}
_MaskChannel ("Mask Channel", Vector) = (0.0,0.0,0.0,0.0)
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float2 _GlobalMipBias;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _MainTex_ST;
half4 _MaskChannel;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _ShapeLightTexture0;
sampler2D _TransparentMaskTex;
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
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb1;
half4 u_xlat16_2;
half3 u_xlat16_3;
bool u_xlatb13;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat0 = u_xlat16_0 * input.color;
    u_xlatb1 = u_xlat0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb13 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xxx;
    u_xlat16_2.xyz = (bool(u_xlatb13)) ? u_xlat16_2.xyz : u_xlat16_1.xyz;
    u_xlat16_3.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_2.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_2.xyz = u_xlat0.xyz * u_xlat16_3.xyz + u_xlat16_2.xyz;
    u_xlat16_1.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat0.xyz);
    u_xlat16_1.w = 0.0;
    u_xlat16_0 = float4(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_1 + u_xlat0;
    u_xlat16_0 = max(u_xlat16_0, float4(0.0, 0.0, 0.0, 0.0));
    u_xlat16_1 = tex2Dbias(_TransparentMaskTex, float4(input.world.xy, 0, _GlobalMipBias.x));
    u_xlat16_2.x = dot(u_xlat16_1, _MaskChannel);
    u_xlat16_2.x = u_xlat16_0.w * u_xlat16_2.x;
    result.xyz = u_xlat16_0.xyz * u_xlat16_2.xxx;
    result.w = u_xlat16_2.x;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float2 _GlobalMipBias;
half4 _MainTex_ST;
half4 _MaskChannel;
sampler2D _MainTex;
sampler2D _TransparentMaskTex;
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
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat0 = u_xlat16_0 * input.color;
    u_xlatb1 = u_xlat0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_0 = max(u_xlat0, float4(0.0, 0.0, 0.0, 0.0));
    u_xlat16_1 = tex2Dbias(_TransparentMaskTex, float4(input.world.xy, 0, _GlobalMipBias.x));
    u_xlat16_2 = dot(u_xlat16_1, _MaskChannel);
    u_xlat16_2 = u_xlat16_0.w * u_xlat16_2;
    result.xyz = u_xlat16_0.xyz * float3(u_xlat16_2,u_xlat16_2,u_xlat16_2);
    result.w = u_xlat16_2;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
