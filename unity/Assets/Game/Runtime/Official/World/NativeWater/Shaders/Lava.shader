Shader "Zjcs/Official/World/Map13/Lava" {
Properties {
_MainTex ("Texture", 2D) = "white" {}
_MainTile ("Main Tile", Float) = 1.0
_MoveDir ("Move Direction", Float) = 0.0
_MoveSpeed ("Move Speed", Float) = 1.0
_NoiseTex ("Noise Tex", 2D) = "black" {}
_NoiseTile ("Noise Tile", Float) = 1.0
_NoiseSpeed ("Noise Speed", Float) = 1.0
_DisturbStrength ("Disturb Strength", Range(0.0,2.0)) = 0.10000000149011612
_Fluctuation ("Fluctuation", Range(0.0,0.20000000298023224)) = 0.05000000074505806
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest LEqual
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float3 world:TEXCOORD1; float2 uv3:TEXCOORD3; };

float _DisturbStrength;
float _Fluctuation;
float _NoiseSpeed;
float2 _GlobalMipBias;
half _HDREmulationScale;
half _MainTile;
half _MoveDir;
half _MoveSpeed;
half _NoiseTile;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _NoiseTex;
sampler2D _ShapeLightTexture0;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
half u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
float u_xlat12;

    u_xlat0 = input.vertex.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.uv.xy = input.uv.xy;
    u_xlat12 = _MoveDir * 0.0174532924;
    u_xlat16_1 = sin(u_xlat12);
    u_xlat16_2 = cos(u_xlat12);
    output.mask.x = u_xlat16_2;
    output.mask.y = u_xlat16_1;
    u_xlat3.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat3.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat3.xyz;
    u_xlat3.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat3.xyz;
    output.world.xyz = u_xlat3.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv3.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float2 u_xlat1;
half4 u_xlat16_1;
bool u_xlatb1;
half4 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat4;
bool u_xlatb13;

    u_xlat0 = input.world.xyxy * float4(_NoiseTile, _NoiseTile, _MainTile, _MainTile);
    u_xlat0.xy = _Time.yy * float2(_NoiseSpeed,_NoiseSpeed) + u_xlat0.xy;
    u_xlat16_0.x = tex2Dbias(_NoiseTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).x;
    u_xlat1.xy = input.mask.xy * float2(float2(_MoveSpeed, _MoveSpeed));
    u_xlat4.xy = u_xlat1.xy * _Time.yy + u_xlat0.zw;
    u_xlat0.xy = u_xlat16_0.xx * float2(float2(_DisturbStrength, _DisturbStrength)) + u_xlat4.xy;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x));
    u_xlat1.x = sin(_Time.y);
    u_xlat1.x = u_xlat1.x * _Fluctuation + 1.0;
    u_xlat0 = u_xlat16_0 * u_xlat1.xxxx;
    u_xlatb1 = u_xlat0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.uv3.xy, 0, _GlobalMipBias.x)).xyz;
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
    result = max(u_xlat16_0, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 mask:TEXCOORD2; float3 world:TEXCOORD1; float2 uv3:TEXCOORD3; };

float _DisturbStrength;
float _Fluctuation;
float _NoiseSpeed;
float2 _GlobalMipBias;
half _MainTile;
half _MoveDir;
half _MoveSpeed;
half _NoiseTile;
sampler2D _MainTex;
sampler2D _NoiseTex;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
half u_xlat16_1;
half u_xlat16_2;
float3 u_xlat3;
float u_xlat12;

    u_xlat0 = input.vertex.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat0 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * input.vertex.xxxx + u_xlat0;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * input.vertex.zzzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.uv.xy = input.uv.xy;
    u_xlat12 = _MoveDir * 0.0174532924;
    u_xlat16_1 = sin(u_xlat12);
    u_xlat16_2 = cos(u_xlat12);
    output.mask.x = u_xlat16_2;
    output.mask.y = u_xlat16_1;
    u_xlat3.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat3.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat3.xyz;
    u_xlat3.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat3.xyz;
    output.world.xyz = u_xlat3.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.uv3.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
float2 u_xlat1;
bool u_xlatb1;
float2 u_xlat2;

    u_xlat0 = input.world.xyxy * float4(_NoiseTile, _NoiseTile, _MainTile, _MainTile);
    u_xlat0.xy = _Time.yy * float2(_NoiseSpeed,_NoiseSpeed) + u_xlat0.xy;
    u_xlat16_0.x = tex2Dbias(_NoiseTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).x;
    u_xlat1.xy = input.mask.xy * float2(float2(_MoveSpeed, _MoveSpeed));
    u_xlat2.xy = u_xlat1.xy * _Time.yy + u_xlat0.zw;
    u_xlat0.xy = u_xlat16_0.xx * float2(float2(_DisturbStrength, _DisturbStrength)) + u_xlat2.xy;
    u_xlat16_0 = tex2Dbias(_MainTex, float4(u_xlat0.xy, 0, _GlobalMipBias.x));
    u_xlat1.x = sin(_Time.y);
    u_xlat1.x = u_xlat1.x * _Fluctuation + 1.0;
    u_xlat0 = u_xlat16_0 * u_xlat1.xxxx;
    u_xlatb1 = u_xlat0.w==0.0;
    if(u_xlatb1){clip(-1);}
    result = max(u_xlat0, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
