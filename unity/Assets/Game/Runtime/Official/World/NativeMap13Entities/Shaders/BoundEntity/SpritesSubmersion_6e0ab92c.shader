Shader "Zjcs/Official/World/Map13/BoundEntity/SpritesSubmersion_6e0ab92c" {
Properties {
_MainTex ("Diffuse", 2D) = "white" {}
_Color ("Tint", Color) = (1.0,1.0,1.0,1.0)
_RendererColor ("RendererColor", Color) = (1.0,1.0,1.0,1.0)
_Flip ("Flip", Vector) = (1.0,1.0,1.0,1.0)
_AlphaTex ("External Alpha", 2D) = "white" {}
_HeightMap ("HeightMap", 2D) = "white" {}
[KeywordEnum(Under, Above)] _Water ("Water", Float) = 0.0
_ZWrite ("Z Write", Float) = 0.0
}
SubShader {
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite [_ZWrite]
ZTest LEqual
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _WATER_ABOVE _WATER_UNDER
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_WATER_ABOVE) && defined(_WATER_UNDER)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _WaterLevel;
float2 _GlobalMipBias;
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _HeightMap;
sampler2D _MainTex;
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
    output.uv.xy = input.uv.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half3 u_xlat16_4;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = tex2Dbias(_HeightMap, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_1.x + input.color.x;
    u_xlat16_2.x = u_xlat16_2.x + (-input.color.y);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlatb1 = u_xlat16_2.x>=_WaterLevel;
    u_xlat16_1.w = (u_xlatb1) ? 0.0 : u_xlat16_0.w;
    u_xlatb15 = u_xlat16_1.w==0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb15 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_2.xxx * u_xlat16_3.xyz;
    u_xlat16_2.xyz = (bool(u_xlatb15)) ? u_xlat16_2.xyz : u_xlat16_3.xyz;
    u_xlat16_4.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_2.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_2.xyz = u_xlat16_0.xyz * u_xlat16_4.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat16_0.xyz);
    u_xlat16_1.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2.xyz + u_xlat16_0.xyz;
    result = max(u_xlat16_1, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#elif defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_WATER_ABOVE) && !defined(_WATER_UNDER)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _WaterLevel;
float2 _GlobalMipBias;
float4 _Color;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 _RendererColor;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
sampler2D _HeightMap;
sampler2D _MainTex;
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
    output.uv.xy = input.uv.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half4 u_xlat16_1;
bool u_xlatb1;
half4 u_xlat16_2;
half3 u_xlat16_3;
half3 u_xlat16_4;
bool u_xlatb15;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1.x = tex2Dbias(_HeightMap, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2.x = u_xlat16_1.x + input.color.x;
    u_xlat16_2.x = u_xlat16_2.x + (-input.color.y);
    u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0);
    u_xlatb1 = u_xlat16_2.x<_WaterLevel;
    u_xlat16_1.w = (u_xlatb1) ? 0.0 : u_xlat16_0.w;
    u_xlatb15 = u_xlat16_1.w==0.0;
    if(u_xlatb15){clip(-1);}
    u_xlat16_3.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_2.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb15 = u_xlat16_2.x!=0.0;
    u_xlat16_2 = (-_ShapeLightInvertedFilter0) + float4(1.0, 1.0, 1.0, 1.0);
    u_xlat16_2.x = dot(u_xlat16_2, _ShapeLightMaskFilter0);
    u_xlat16_2.xyz = u_xlat16_2.xxx * u_xlat16_3.xyz;
    u_xlat16_2.xyz = (bool(u_xlatb15)) ? u_xlat16_2.xyz : u_xlat16_3.xyz;
    u_xlat16_4.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_2.xyz = u_xlat16_2.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_2.xyz = u_xlat16_0.xyz * u_xlat16_4.xyz + u_xlat16_2.xyz;
    u_xlat16_2.xyz = float3(_HDREmulationScale,_HDREmulationScale,_HDREmulationScale) * u_xlat16_2.xyz + (-u_xlat16_0.xyz);
    u_xlat16_1.xyz = float3(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_2.xyz + u_xlat16_0.xyz;
    result = max(u_xlat16_1, float4(0.0, 0.0, 0.0, 0.0));
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && !defined(_WATER_ABOVE) && defined(_WATER_UNDER)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _WaterLevel;
float2 _GlobalMipBias;
float4 _Color;
half4 _RendererColor;
sampler2D _HeightMap;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
bool u_xlatb9;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = tex2Dbias(_HeightMap, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2 = u_xlat16_1 + input.color.x;
    u_xlat16_2 = u_xlat16_2 + (-input.color.y);
    u_xlat16_2 = clamp(u_xlat16_2, 0.0, 1.0);
    u_xlatb1 = u_xlat16_2>=_WaterLevel;
    u_xlat16_2 = (u_xlatb1) ? 0.0 : u_xlat16_0.w;
    u_xlatb9 = u_xlat16_2==0.0;
    if(u_xlatb9){clip(-1);}
    result.xyz = max(u_xlat16_0.xyz, float3(0.0, 0.0, 0.0));
    result.w = max(u_xlat16_2, 0.0);
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0) && defined(_WATER_ABOVE) && !defined(_WATER_UNDER)
struct Input { float3 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };

float _WaterLevel;
float2 _GlobalMipBias;
float4 _Color;
half4 _RendererColor;
sampler2D _HeightMap;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    u_xlat1 = input.color * _Color;
    u_xlat1 = u_xlat1 * _RendererColor;
    output.color = u_xlat1;
    output.uv.xy = input.uv.xy;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5, 0.5, 0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half4 u_xlat16_0;
half u_xlat16_1;
bool u_xlatb1;
half u_xlat16_2;
bool u_xlatb9;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = tex2Dbias(_HeightMap, float4(input.uv.xy, 0, _GlobalMipBias.x)).x;
    u_xlat16_2 = u_xlat16_1 + input.color.x;
    u_xlat16_2 = u_xlat16_2 + (-input.color.y);
    u_xlat16_2 = clamp(u_xlat16_2, 0.0, 1.0);
    u_xlatb1 = u_xlat16_2<_WaterLevel;
    u_xlat16_2 = (u_xlatb1) ? 0.0 : u_xlat16_0.w;
    u_xlatb9 = u_xlat16_2==0.0;
    if(u_xlatb9){clip(-1);}
    result.xyz = max(u_xlat16_0.xyz, float3(0.0, 0.0, 0.0));
    result.w = max(u_xlat16_2, 0.0);
    return result;

}
#endif
ENDCG
}
}
Fallback "Zjcs/Official/World/Map13/BoundEntity/SpritesDefault_b64456be"
}
