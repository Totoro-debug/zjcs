Shader "Zjcs/Official/World/Map13/LavaEdge" {
Properties {
_MainTex ("Texture", 2D) = "white" {}
_MainTile ("Main Tile", Float) = 1.0
_MoveSpeed ("Move Speed", Float) = 1.0
_NoiseTex ("Noise Tex", 2D) = "black" {}
_NoiseTile ("Noise Tile", Float) = 1.0
_NoiseSpeed ("Noise Speed", Float) = 1.0
_DisturbStrength ("Disturb Strength", Float) = 0.10000000149011612
_Start ("Start", Range(0.0,1.0)) = 0.5299999713897705
_LightAtten ("Light Atten", Range(0.0,1.0)) = 0.0
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _DisturbStrength;
float _NoiseSpeed;
float2 _GlobalMipBias;
half _HDREmulationScale;
half _LightAtten;
half _MainTile;
half _MoveSpeed;
half _NoiseTile;
half _Start;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _NoiseTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;

    u_xlat0 = input.vertex.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.uv.xy = input.uv.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float2 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float u_xlat8;
bool u_xlatb13;

    u_xlat0.xy = input.uv.xx * float2(_NoiseTile, _MainTile);
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _NoiseSpeed;
    u_xlat0.z = 0.5;
    u_xlat0.xz = u_xlat0.xz + u_xlat1.xy;
    u_xlat16_0.x = tex2Dbias(_NoiseTex, float4(u_xlat0.xz, 0, _GlobalMipBias.x)).x;
    u_xlat8 = (-input.uv.y) + _Start;
    u_xlat8 = clamp(u_xlat8, 0.0, 1.0);
    u_xlat0.x = u_xlat16_0.x * u_xlat8;
    u_xlat1.y = u_xlat0.x * _DisturbStrength + input.uv.y;
    u_xlat1.x = _MoveSpeed * _Time.y + u_xlat0.y;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x));
    u_xlatb1 = u_xlat16_0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb13 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_1.xyz * u_xlat16_2.xxx;
    u_xlat16_2.xyz = (bool(u_xlatb13)) ? u_xlat16_2.xyz : u_xlat16_1.xyz;
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(_ShapeLightBlendFactors0.x, _ShapeLightBlendFactors0.x, _ShapeLightBlendFactors0.x);
    u_xlat16_2.xyz = u_xlat16_2.xyz * float3(_ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y, _ShapeLightBlendFactors0.y);
    u_xlat16_2.xyz = u_xlat16_0.xyz * u_xlat16_3.xyz + u_xlat16_2.xyz;
    u_xlat16_1.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat16_0.xyz);
    u_xlat16_1.w = 0.0;
    u_xlat16_1 = float4(float4(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_1 + u_xlat16_0;
    u_xlat16_1 = max(u_xlat16_1, float4(0.0, 0.0, 0.0, 0.0));
    u_xlat16_0 = u_xlat16_0 + (-u_xlat16_1);
    result = float4(float4(_LightAtten, _LightAtten, _LightAtten, _LightAtten)) * u_xlat16_0 + u_xlat16_1;
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _DisturbStrength;
float _NoiseSpeed;
float2 _GlobalMipBias;
half _LightAtten;
half _MainTile;
half _MoveSpeed;
half _NoiseTile;
half _Start;
sampler2D _MainTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;

    u_xlat0 = input.vertex.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.uv.xy = input.uv.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
float2 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
float u_xlat4;

    u_xlat0.xy = input.uv.xx * float2(_NoiseTile, _MainTile);
    u_xlat1.x = 0.0;
    u_xlat1.y = _Time.y * _NoiseSpeed;
    u_xlat0.z = 0.5;
    u_xlat0.xz = u_xlat0.xz + u_xlat1.xy;
    u_xlat16_0.x = tex2Dbias(_NoiseTex, float4(u_xlat0.xz, 0, _GlobalMipBias.x)).x;
    u_xlat4 = (-input.uv.y) + _Start;
    u_xlat4 = clamp(u_xlat4, 0.0, 1.0);
    u_xlat0.x = u_xlat16_0.x * u_xlat4;
    u_xlat1.y = u_xlat0.x * _DisturbStrength + input.uv.y;
    u_xlat1.x = _MoveSpeed * _Time.y + u_xlat0.y;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat1.xy, 0, _GlobalMipBias.x));
    u_xlatb1 = u_xlat16_0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_1 = max(u_xlat16_0, float4(0.0, 0.0, 0.0, 0.0));
    u_xlat16_0 = u_xlat16_0 + (-u_xlat16_1);
    result = float4(float4(_LightAtten, _LightAtten, _LightAtten, _LightAtten)) * u_xlat16_0 + u_xlat16_1;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
