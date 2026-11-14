Shader "Universal Render Pipeline/2D/CR-Lit-ColorShift" {
Properties {

_MainTex ("Diffuse", 2D) = "white" { }

[MaterialToggle] _ZWrite ("ZWrite", Float) = 0.0

}
SubShader {
LOD 0
Tags { "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Pass {
Tags { "LIGHTMODE"="Universal2D" "QUEUE"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
Cull Off
ZWrite [_ZWrite]
ZTest LEqual
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ USE_SHAPE_LIGHT_TYPE_0
#include "UnityCG.cginc"
#if defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; };

float Epsilon;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
float4 Xhlslcc_UnusedXunity_LODFade;
float4 Xhlslcc_UnusedXunity_LightmapST;
float4 Xhlslcc_UnusedXunity_MotionVectorsParams;
float4 Xhlslcc_UnusedXunity_ProbesOcclusion;
float4 Xhlslcc_UnusedXunity_RendererBounds_Max;
float4 Xhlslcc_UnusedXunity_RendererBounds_Min;
float4 Xhlslcc_UnusedXunity_RenderingLayer;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube0_ProbePosition;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube1_ProbePosition;
half _HDREmulationScale;
half _UseSceneLighting;
half2 _ShapeLightBlendFactors0;
half4 Xhlslcc_UnusedXunity_LightData;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SHAb;
half4 Xhlslcc_UnusedXunity_SHAg;
half4 Xhlslcc_UnusedXunity_SHAr;
half4 Xhlslcc_UnusedXunity_SHBb;
half4 Xhlslcc_UnusedXunity_SHBg;
half4 Xhlslcc_UnusedXunity_SHBr;
half4 Xhlslcc_UnusedXunity_SHC;
half4 Xhlslcc_UnusedXunity_SpecCube0_HDR;
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
half4 _MainTex_ST;
half4 _ShapeLightInvertedFilter0;
half4 _ShapeLightMaskFilter0;
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
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.mask.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
half4 u_xlat16_1;
float4 u_xlat2;
half3 u_xlat16_2;
float4 u_xlat3;
half4 u_xlat16_3;
bool3 u_xlatb3;
float4 u_xlat4;
half3 u_xlat16_5;
half3 u_xlat16_6;
float u_xlat7;
bool u_xlatb7;
half2 u_xlat16_12;
float u_xlat14;
float2 u_xlat16;
float u_xlat21;
bool u_xlatb21;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = u_xlat16_0 * input.color;
    u_xlatb21 = u_xlat16_1.w==0.0;
    if(u_xlatb21){clip(-1);}
    u_xlatb21 = u_xlat16_1.y>=u_xlat16_1.z;
    u_xlat21 = u_xlatb21 ? 1.0 : float(0.0);
    u_xlat2.xy = u_xlat16_0.yz * input.color.yz + (-u_xlat16_1.zy);
    u_xlat16.x = float(1.0);
    u_xlat16.y = float(-1.0);
    u_xlat2.xy = (float2)(u_xlat21) * u_xlat2.xy;
    u_xlat3.xy = u_xlat16_0.zy * input.color.zy + u_xlat2.xy;
    u_xlat3.zw = (float2)(u_xlat21) * u_xlat16.xy + float2(-1.0,0.666666687);
    u_xlatb7 = u_xlat16_1.x>=u_xlat3.x;
    u_xlat7 = u_xlatb7 ? 1.0 : float(0.0);
    u_xlat2.xyz = (-u_xlat3.xyw);
    u_xlat2.w = (-u_xlat16_1.x);
    u_xlat4.x = u_xlat16_0.x * input.color.x + u_xlat2.x;
    u_xlat4.yzw = u_xlat2.yzw + u_xlat3.yzx;
    u_xlat0.xzw = (float3)(u_xlat7) * u_xlat4.xyz + u_xlat3.xyw;
    u_xlat7 = u_xlat7 * u_xlat4.w + u_xlat16_1.x;
    u_xlat2.x = min(u_xlat0.z, u_xlat7);
    u_xlat2.x = u_xlat0.x + (-u_xlat2.x);
    u_xlat7 = (-u_xlat0.z) + u_xlat7;
    u_xlat14 = u_xlat2.x * 6.0 + Epsilon;
    u_xlat7 = u_xlat7 / u_xlat14;
    u_xlat7 = u_xlat0.w + u_xlat7;
    u_xlat3.y = (-u_xlat2.x) * 0.5 + u_xlat0.x;
    u_xlat0.x = u_xlat3.y * 2.0 + -1.0;
    u_xlat0.x = -abs(u_xlat0.x) + Epsilon;
    u_xlat0.x = u_xlat0.x + 1.0;
    u_xlat3.x = u_xlat2.x / u_xlat0.x;
    u_xlat16_5.x = abs(u_xlat7) + input.mask.x;
    u_xlatb0.xy = (float4(0.0,0.0,0.0,0.0) < input.mask.yzyy).xy;
    u_xlat16_12.xy = (-u_xlat3.xy) + float2(1.0,1.0);
    u_xlat16_12.xy = input.mask.yz * u_xlat16_12.xy + u_xlat3.xy;
    u_xlat16_6.xy = input.mask.yz * u_xlat3.xy + u_xlat3.xy;
    u_xlat0.x = (u_xlatb0.x) ? u_xlat16_12.x : u_xlat16_6.x;
    u_xlat0.y = (u_xlatb0.y) ? u_xlat16_12.y : u_xlat16_6.y;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat14 = frac(u_xlat16_5.x);
    u_xlat2.xyz = (float3)(u_xlat14) * float3(6.0,6.0,6.0) + float3(0.0,4.0,2.0);
    u_xlat2.xyz = u_xlat2.xyz * float3(0.166666672,0.166666672,0.166666672);
    u_xlatb3.xyz = (u_xlat2.xyzx >= (-u_xlat2.xyzx)).xyz;
    u_xlat2.xyz = frac(u_xlat2.xyz);
    {
        float4 hlslcc_movcTemp = u_xlat2;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat2.x : (-u_xlat2.x);
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat2.y : (-u_xlat2.y);
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat2.z : (-u_xlat2.z);
        u_xlat2 = hlslcc_movcTemp;
    }
    u_xlat2.xyz = u_xlat2.xyz * float3(6.0,6.0,6.0) + float3(-3.0,-3.0,-3.0);
    u_xlat2.xyz = abs(u_xlat2.xyz) + float3(-1.0,-1.0,-1.0);
    u_xlat2.xyz = clamp(u_xlat2.xyz, 0.0, 1.0);
    u_xlat2.xyz = u_xlat2.xyz + float3(-0.5,-0.5,-0.5);
    u_xlat0.xzw = u_xlat0.xxx * u_xlat2.xyz;
    u_xlat2.x = u_xlat0.y * 2.0 + -1.0;
    u_xlat2.x = -abs(u_xlat2.x) + 1.0;
    u_xlat0.xyz = u_xlat0.xzw * u_xlat2.xxx + u_xlat0.yyy;
    u_xlat16_2.xyz = tex2Dbias(_ShapeLightTexture0, float4(input.world.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_5.x = dot(_ShapeLightMaskFilter0, _ShapeLightMaskFilter0);
    u_xlatb21 = u_xlat16_5.x!=0.0;
    u_xlat16_3 = (-_ShapeLightInvertedFilter0) + float4(1.0,1.0,1.0,1.0);
    u_xlat16_5.x = dot(u_xlat16_3, _ShapeLightMaskFilter0);
    u_xlat16_5.xyz = u_xlat16_2.xyz * u_xlat16_5.xxx;
    u_xlat16_5.xyz = (bool(u_xlatb21)) ? u_xlat16_5.xyz : u_xlat16_2.xyz;
    u_xlat16_6.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors0.xxx;
    u_xlat16_5.xyz = u_xlat16_5.xyz * _ShapeLightBlendFactors0.yyy;
    u_xlat16_5.xyz = u_xlat0.xyz * u_xlat16_6.xyz + u_xlat16_5.xyz;
    u_xlat16_5.xyz = (float3)(_HDREmulationScale) * u_xlat16_5.xyz + (-u_xlat0.xyz);
    u_xlat16_1.xyz = (float3)(float3(_UseSceneLighting, _UseSceneLighting, _UseSceneLighting)) * u_xlat16_5.xyz + u_xlat0.xyz;
    result = max(u_xlat16_1, float4(0.0,0.0,0.0,0.0));
    return result;

}
#elif !defined(USE_SHAPE_LIGHT_TYPE_0)
struct Input { float3 vertex:POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float3 uv1:TEXCOORD1; };
struct Varying { float4 position:SV_POSITION; float4 color:COLOR0; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float3 mask:TEXCOORD2; };

float Epsilon;
float2 _GlobalMipBias;
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousMI[4];
float4 Xhlslcc_UnusedXhlslcc_mtx4x4unity_MatrixPreviousM[4];
float4 Xhlslcc_UnusedXunity_DynamicLightmapST;
float4 Xhlslcc_UnusedXunity_LODFade;
float4 Xhlslcc_UnusedXunity_LightmapST;
float4 Xhlslcc_UnusedXunity_MotionVectorsParams;
float4 Xhlslcc_UnusedXunity_ProbesOcclusion;
float4 Xhlslcc_UnusedXunity_RendererBounds_Max;
float4 Xhlslcc_UnusedXunity_RendererBounds_Min;
float4 Xhlslcc_UnusedXunity_RenderingLayer;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube0_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube0_ProbePosition;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMax;
float4 Xhlslcc_UnusedXunity_SpecCube1_BoxMin;
float4 Xhlslcc_UnusedXunity_SpecCube1_ProbePosition;
half4 Xhlslcc_UnusedXunity_LightData;
half4 Xhlslcc_UnusedXunity_LightIndices[2];
half4 Xhlslcc_UnusedXunity_SHAb;
half4 Xhlslcc_UnusedXunity_SHAg;
half4 Xhlslcc_UnusedXunity_SHAr;
half4 Xhlslcc_UnusedXunity_SHBb;
half4 Xhlslcc_UnusedXunity_SHBg;
half4 Xhlslcc_UnusedXunity_SHBr;
half4 Xhlslcc_UnusedXunity_SHC;
half4 Xhlslcc_UnusedXunity_SpecCube0_HDR;
half4 Xhlslcc_UnusedXunity_SpecCube1_HDR;
half4 Xhlslcc_UnusedXunity_WorldTransformParams;
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
    u_xlat0 = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.position = u_xlat0;
    u_xlat0.xyz = u_xlat0.xyw / u_xlat0.www;
    output.color = input.color;
    output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    u_xlat0.y = u_xlat0.y * _ProjectionParams.x;
    u_xlat0.xzw = u_xlat0.xzy * float3(0.5,0.5,0.5);
    u_xlat0.xy = u_xlat0.zz + u_xlat0.xw;
    output.world.xy = u_xlat0.xy;
    output.mask.xyz = input.uv1.xyz;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float4 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
half4 u_xlat16_1;
float4 u_xlat2;
float4 u_xlat3;
bool3 u_xlatb3;
float4 u_xlat4;
half2 u_xlat16_5;
float u_xlat6;
bool u_xlatb6;
half2 u_xlat16_7;
float u_xlat12;
float2 u_xlat14;
float u_xlat18;
bool u_xlatb18;

    u_xlat16_0 = tex2Dbias(_MainTex, float4(input.uv.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = u_xlat16_0 * input.color;
    u_xlatb18 = u_xlat16_1.w==0.0;
    if(u_xlatb18){clip(-1);}
    u_xlatb18 = u_xlat16_1.y>=u_xlat16_1.z;
    u_xlat18 = u_xlatb18 ? 1.0 : float(0.0);
    u_xlat2.xy = u_xlat16_0.yz * input.color.yz + (-u_xlat16_1.zy);
    u_xlat14.x = float(1.0);
    u_xlat14.y = float(-1.0);
    u_xlat2.xy = (float2)(u_xlat18) * u_xlat2.xy;
    u_xlat3.xy = u_xlat16_0.zy * input.color.zy + u_xlat2.xy;
    u_xlat3.zw = (float2)(u_xlat18) * u_xlat14.xy + float2(-1.0,0.666666687);
    u_xlatb6 = u_xlat16_1.x>=u_xlat3.x;
    u_xlat6 = u_xlatb6 ? 1.0 : float(0.0);
    u_xlat2.xyz = (-u_xlat3.xyw);
    u_xlat2.w = (-u_xlat16_1.x);
    u_xlat4.x = u_xlat16_0.x * input.color.x + u_xlat2.x;
    u_xlat4.yzw = u_xlat2.yzw + u_xlat3.yzx;
    u_xlat0.xzw = (float3)(u_xlat6) * u_xlat4.xyz + u_xlat3.xyw;
    u_xlat6 = u_xlat6 * u_xlat4.w + u_xlat16_1.x;
    u_xlat2.x = min(u_xlat0.z, u_xlat6);
    u_xlat2.x = u_xlat0.x + (-u_xlat2.x);
    u_xlat6 = (-u_xlat0.z) + u_xlat6;
    u_xlat12 = u_xlat2.x * 6.0 + Epsilon;
    u_xlat6 = u_xlat6 / u_xlat12;
    u_xlat6 = u_xlat0.w + u_xlat6;
    u_xlat3.y = (-u_xlat2.x) * 0.5 + u_xlat0.x;
    u_xlat0.x = u_xlat3.y * 2.0 + -1.0;
    u_xlat0.x = -abs(u_xlat0.x) + Epsilon;
    u_xlat0.x = u_xlat0.x + 1.0;
    u_xlat3.x = u_xlat2.x / u_xlat0.x;
    u_xlat16_1.x = abs(u_xlat6) + input.mask.x;
    u_xlatb0.xy = (float4(0.0,0.0,0.0,0.0) < input.mask.yzyy).xy;
    u_xlat16_7.xy = (-u_xlat3.xy) + float2(1.0,1.0);
    u_xlat16_7.xy = input.mask.yz * u_xlat16_7.xy + u_xlat3.xy;
    u_xlat16_5.xy = input.mask.yz * u_xlat3.xy + u_xlat3.xy;
    u_xlat0.x = (u_xlatb0.x) ? u_xlat16_7.x : u_xlat16_5.x;
    u_xlat0.y = (u_xlatb0.y) ? u_xlat16_7.y : u_xlat16_5.y;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat12 = frac(u_xlat16_1.x);
    u_xlat2.xyz = (float3)(u_xlat12) * float3(6.0,6.0,6.0) + float3(0.0,4.0,2.0);
    u_xlat2.xyz = u_xlat2.xyz * float3(0.166666672,0.166666672,0.166666672);
    u_xlatb3.xyz = (u_xlat2.xyzx >= (-u_xlat2.xyzx)).xyz;
    u_xlat2.xyz = frac(u_xlat2.xyz);
    {
        float4 hlslcc_movcTemp = u_xlat2;
        hlslcc_movcTemp.x = (u_xlatb3.x) ? u_xlat2.x : (-u_xlat2.x);
        hlslcc_movcTemp.y = (u_xlatb3.y) ? u_xlat2.y : (-u_xlat2.y);
        hlslcc_movcTemp.z = (u_xlatb3.z) ? u_xlat2.z : (-u_xlat2.z);
        u_xlat2 = hlslcc_movcTemp;
    }
    u_xlat2.xyz = u_xlat2.xyz * float3(6.0,6.0,6.0) + float3(-3.0,-3.0,-3.0);
    u_xlat2.xyz = abs(u_xlat2.xyz) + float3(-1.0,-1.0,-1.0);
    u_xlat2.xyz = clamp(u_xlat2.xyz, 0.0, 1.0);
    u_xlat2.xyz = u_xlat2.xyz + float3(-0.5,-0.5,-0.5);
    u_xlat0.xzw = u_xlat0.xxx * u_xlat2.xyz;
    u_xlat2.x = u_xlat0.y * 2.0 + -1.0;
    u_xlat2.x = -abs(u_xlat2.x) + 1.0;
    u_xlat0.xyz = u_xlat0.xzw * u_xlat2.xxx + u_xlat0.yyy;
    result.xyz = max(u_xlat0.xyz, float3(0.0,0.0,0.0));
    result.w = max(u_xlat16_1.w, 0.0);
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
