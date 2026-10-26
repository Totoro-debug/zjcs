Shader "Zjcs/Combat/WorldSpriteSequence" {
Properties {
_MainTex ("_MainTex", 2D) = "white" {}
_Intensity ("_Intensity", Float) = 1.0
_SequenceRow ("_SequenceRow", Float) = 1.0
_SequenceColumn ("_SequenceColumn", Float) = 1.0
IsSqueueCycle ("IsSqueueCycle", Float) = 1.0
_SquenceRate ("_SquenceRate", Float) = 0.0
_SquenceSpeed ("_SquenceSpeed", Float) = 1.0
_LightAtten ("_LightAtten", Float) = 1.0
}
SubShader {
Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
Cull Off
ZWrite Off
ZTest LEqual
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ColorMask RGBA
Pass {
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _ IsSqueueCycle
#pragma multi_compile_local _ USE_SHAPE_LIGHT_TYPE_0
#include "UnityCG.cginc"
struct Input { float2 uv : TEXCOORD0; float3 vertex : POSITION0; float4 color : COLOR0; };
struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; half2 world : TEXCOORD1; half4 color : COLOR0; };
float _SequenceColumn;
float _SequenceRow;
float2 _GlobalMipBias;
half _HDREmulationScale;
half _Intensity;
half _LightAtten;
half _SquenceRate;
half _SquenceSpeed;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _MainTex;
sampler2D _ShapeLightTexture0;
#if defined(IsSqueueCycle) && defined(USE_SHAPE_LIGHT_TYPE_0)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half u_xlat16_2;
half2 u_xlat16_3;
half u_xlat16_6;
float2 u_xlat9;
float u_xlat12;

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
    u_xlat12 = _Time.y * _SquenceSpeed;
    u_xlat12 = frac(u_xlat12);
    u_xlat1.x = _SequenceColumn * _SequenceRow;
    u_xlat16_2 = trunc(u_xlat1.x);
    u_xlat16_2 = u_xlat12 * u_xlat16_2;
    u_xlat1.xy = float2(1.0, 1.0) / float2(_SequenceColumn, _SequenceRow);
    u_xlat16_6 = u_xlat1.x * u_xlat16_2;
    u_xlat16_6 = floor(u_xlat16_6);
    u_xlat12 = (-u_xlat16_6) * _SequenceColumn + u_xlat16_2;
    u_xlat16_2 = trunc(u_xlat12);
    u_xlat16_3.x = u_xlat1.x * u_xlat16_2;
    u_xlat16_2 = (-u_xlat1.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_6) * u_xlat1.y + u_xlat16_2;
    u_xlat9.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = u_xlat9.xy * u_xlat1.xy + u_xlat16_3.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half3 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half4 u_xlat16_3;
half3 u_xlat16_4;
bool u_xlatb16;
half u_xlat16_17;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = u_xlat16_0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_2.xyz = u_xlat16_0.xyz * float3(float3(_Intensity, _Intensity, _Intensity));
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_17 = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb16 = u_xlat16_17!=0.0;
    u_xlat16_3 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_17 = dot(u_xlat16_3, _ShapeLightMaskFilter0);
    u_xlat16_3.xyz = u_xlat16_1.xyz * float3(u_xlat16_17,u_xlat16_17,u_xlat16_17);
    u_xlat16_3.xyz = (bool(u_xlatb16)) ? u_xlat16_3.xyz : u_xlat16_1.xyz;
    u_xlat16_4.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_3.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat16_3.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_3.xyz + (-u_xlat16_2.xyz);
    u_xlat16_3.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_3.xyz + u_xlat16_2.xyz;
    u_xlat16_3.xyz = max(u_xlat16_3.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_2.xyz = u_xlat16_2.xyz * u_xlat16_0.www + (-u_xlat16_3.xyz);
    result.xyz = float3(float3(_LightAtten, _LightAtten, _LightAtten)) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif !defined(IsSqueueCycle) && defined(USE_SHAPE_LIGHT_TYPE_0)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half u_xlat16_2;
half2 u_xlat16_3;
half u_xlat16_6;
float2 u_xlat9;
float u_xlat12;

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
    u_xlat16_2 = max(_SquenceRate, 0.0);
    u_xlat16_2 = min(u_xlat16_2, 0.999000013);
    u_xlat12 = _SequenceColumn * _SequenceRow;
    u_xlat16_6 = trunc(u_xlat12);
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2;
    u_xlat1.xy = float2(1.0, 1.0) / float2(_SequenceColumn, _SequenceRow);
    u_xlat16_6 = u_xlat1.x * u_xlat16_2;
    u_xlat16_6 = floor(u_xlat16_6);
    u_xlat12 = (-u_xlat16_6) * _SequenceColumn + u_xlat16_2;
    u_xlat16_2 = trunc(u_xlat12);
    u_xlat16_3.x = u_xlat1.x * u_xlat16_2;
    u_xlat16_2 = (-u_xlat1.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_6) * u_xlat1.y + u_xlat16_2;
    u_xlat9.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = u_xlat9.xy * u_xlat1.xy + u_xlat16_3.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half3 u_xlat16_1;
bool u_xlatb1;
half3 u_xlat16_2;
half4 u_xlat16_3;
half3 u_xlat16_4;
bool u_xlatb16;
half u_xlat16_17;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = u_xlat16_0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_2.xyz = u_xlat16_0.xyz * float3(float3(_Intensity, _Intensity, _Intensity));
    u_xlat16_1.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_17 = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb16 = u_xlat16_17!=0.0;
    u_xlat16_3 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_17 = dot(u_xlat16_3, _ShapeLightMaskFilter0);
    u_xlat16_3.xyz = u_xlat16_1.xyz * float3(u_xlat16_17,u_xlat16_17,u_xlat16_17);
    u_xlat16_3.xyz = (bool(u_xlatb16)) ? u_xlat16_3.xyz : u_xlat16_1.xyz;
    u_xlat16_4.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_3.xyz = u_xlat16_3.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_4.xyz + u_xlat16_3.xyz;
    u_xlat16_3.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_3.xyz + (-u_xlat16_2.xyz);
    u_xlat16_3.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_3.xyz + u_xlat16_2.xyz;
    u_xlat16_3.xyz = max(u_xlat16_3.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_2.xyz = u_xlat16_2.xyz * u_xlat16_0.www + (-u_xlat16_3.xyz);
    result.xyz = float3(float3(_LightAtten, _LightAtten, _LightAtten)) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif defined(IsSqueueCycle) && !defined(USE_SHAPE_LIGHT_TYPE_0)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half u_xlat16_2;
half2 u_xlat16_3;
half u_xlat16_6;
float2 u_xlat9;
float u_xlat12;

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
    u_xlat12 = _Time.y * _SquenceSpeed;
    u_xlat12 = frac(u_xlat12);
    u_xlat1.x = _SequenceColumn * _SequenceRow;
    u_xlat16_2 = trunc(u_xlat1.x);
    u_xlat16_2 = u_xlat12 * u_xlat16_2;
    u_xlat1.xy = float2(1.0, 1.0) / float2(_SequenceColumn, _SequenceRow);
    u_xlat16_6 = u_xlat1.x * u_xlat16_2;
    u_xlat16_6 = floor(u_xlat16_6);
    u_xlat12 = (-u_xlat16_6) * _SequenceColumn + u_xlat16_2;
    u_xlat16_2 = trunc(u_xlat12);
    u_xlat16_3.x = u_xlat1.x * u_xlat16_2;
    u_xlat16_2 = (-u_xlat1.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_6) * u_xlat1.y + u_xlat16_2;
    u_xlat9.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = u_xlat9.xy * u_xlat1.xy + u_xlat16_3.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = u_xlat16_0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_2.xyz = u_xlat16_0.xyz * float3(float3(_Intensity, _Intensity, _Intensity));
    u_xlat16_3.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_2.xyz = u_xlat16_2.xyz * u_xlat16_0.www + (-u_xlat16_3.xyz);
    result.xyz = float3(float3(_LightAtten, _LightAtten, _LightAtten)) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#elif !defined(IsSqueueCycle) && !defined(USE_SHAPE_LIGHT_TYPE_0)
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
half u_xlat16_2;
half2 u_xlat16_3;
half u_xlat16_6;
float2 u_xlat9;
float u_xlat12;

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
    u_xlat16_2 = max(_SquenceRate, 0.0);
    u_xlat16_2 = min(u_xlat16_2, 0.999000013);
    u_xlat12 = _SequenceColumn * _SequenceRow;
    u_xlat16_6 = trunc(u_xlat12);
    u_xlat16_2 = u_xlat16_6 * u_xlat16_2;
    u_xlat1.xy = float2(1.0, 1.0) / float2(_SequenceColumn, _SequenceRow);
    u_xlat16_6 = u_xlat1.x * u_xlat16_2;
    u_xlat16_6 = floor(u_xlat16_6);
    u_xlat12 = (-u_xlat16_6) * _SequenceColumn + u_xlat16_2;
    u_xlat16_2 = trunc(u_xlat12);
    u_xlat16_3.x = u_xlat1.x * u_xlat16_2;
    u_xlat16_2 = (-u_xlat1.y) + 1.0;
    u_xlat16_3.y = (-u_xlat16_6) * u_xlat1.y + u_xlat16_2;
    u_xlat9.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    output.uv.xy = u_xlat9.xy * u_xlat1.xy + u_xlat16_3.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
bool u_xlatb1;
half3 u_xlat16_2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = u_xlat16_0 * input.color;
    u_xlatb1 = u_xlat16_0.w==0.0;
    if(u_xlatb1){clip(-1);}
    u_xlat16_2.xyz = u_xlat16_0.xyz * float3(float3(_Intensity, _Intensity, _Intensity));
    u_xlat16_3.xyz = max(u_xlat16_2.xyz, float3(0.0, 0.0, 0.0));
    u_xlat16_2.xyz = u_xlat16_2.xyz * u_xlat16_0.www + (-u_xlat16_3.xyz);
    result.xyz = float3(float3(_LightAtten, _LightAtten, _LightAtten)) * u_xlat16_2.xyz + u_xlat16_3.xyz;
    result.w = u_xlat16_0.w;
    return result;

}
#endif
ENDCG
}
}
}
