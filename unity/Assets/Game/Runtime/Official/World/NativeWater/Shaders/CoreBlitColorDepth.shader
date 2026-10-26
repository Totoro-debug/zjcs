Shader "Zjcs/Official/World/Map13/CoreBlitColorDepth" {
Properties {

}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" }
Pass {
Tags { "RenderPipeline"="UniversalPipeline" }
Cull Off
ZWrite Off
ZTest Always
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ DISABLE_TEXTURE2D_X_ARRAY
#include "UnityCG.cginc"
#if !defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
sampler2D _BlitTexture;
int int_bitfieldInsert(int base, int insert, int offset, int bits) {
    uint mask = uint(~(int(~0) << uint(bits)) << uint(offset));
    return int((uint(base) & ~mask) | ((uint(insert) << uint(offset)) & mask));
}
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
uint3 u_xlatu0;


    u_xlatu0.x =  uint(int(int_bitfieldInsert(0, input.vertexID, 1 & int(0x1F), 1)));
    u_xlatu0.z = uint(uint(input.vertexID) & 2u);
    u_xlat0.xy = float2(u_xlatu0.xz);
    output.position.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.zw = float2(-1.0, 1.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


    result = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
sampler2D _BlitTexture;
int int_bitfieldInsert(int base, int insert, int offset, int bits) {
    uint mask = uint(~(int(~0) << uint(bits)) << uint(offset));
    return int((uint(base) & ~mask) | ((uint(insert) << uint(offset)) & mask));
}
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
uint3 u_xlatu0;


    u_xlatu0.x =  uint(int(int_bitfieldInsert(0, input.vertexID, 1 & int(0x1F), 1)));
    u_xlatu0.z = uint(uint(input.vertexID) & 2u);
    u_xlat0.xy = float2(u_xlatu0.xz);
    output.position.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.zw = float2(-1.0, 1.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


    result = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "RenderPipeline"="UniversalPipeline" }
Cull Off
ZWrite On
ZTest Always
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask RGBA
Stencil { Ref 0 ReadMask 255 WriteMask 255 Comp Always Pass Keep Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile __ DISABLE_TEXTURE2D_X_ARRAY
#include "UnityCG.cginc"
#if !defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
sampler2D _BlitTexture;
sampler2D _InputDepthTexture;
int int_bitfieldInsert(int base, int insert, int offset, int bits) {
    uint mask = uint(~(int(~0) << uint(bits)) << uint(offset));
    return int((uint(base) & ~mask) | ((uint(insert) << uint(offset)) & mask));
}
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
uint3 u_xlatu0;


    u_xlatu0.x =  uint(int(int_bitfieldInsert(0, input.vertexID, 1 & int(0x1F), 1)));
    u_xlatu0.z = uint(uint(input.vertexID) & 2u);
    u_xlat0.xy = float2(u_xlatu0.xz);
    output.position.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.zw = float2(-1.0, 1.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
struct FragmentResult { half4 color:SV_Target; float depth:SV_Depth; };
FragmentResult frag(Varying input) { FragmentResult fragment = (FragmentResult)0; half4 result = 0;
half u_xlat16_0;

    result = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    u_xlat16_0 = tex2Dlod(_InputDepthTexture, float4(input.uv.xy, 0, _BlitMipLevel)).x;
    fragment.depth = u_xlat16_0;
    fragment.color = result; return fragment;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
sampler2D _BlitTexture;
sampler2D _InputDepthTexture;
int int_bitfieldInsert(int base, int insert, int offset, int bits) {
    uint mask = uint(~(int(~0) << uint(bits)) << uint(offset));
    return int((uint(base) & ~mask) | ((uint(insert) << uint(offset)) & mask));
}
Varying vert(Input input) { Varying output = (Varying)0;
float2 u_xlat0;
uint3 u_xlatu0;


    u_xlatu0.x =  uint(int(int_bitfieldInsert(0, input.vertexID, 1 & int(0x1F), 1)));
    u_xlatu0.z = uint(uint(input.vertexID) & 2u);
    u_xlat0.xy = float2(u_xlatu0.xz);
    output.position.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.zw = float2(-1.0, 1.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
struct FragmentResult { half4 color:SV_Target; float depth:SV_Depth; };
FragmentResult frag(Varying input) { FragmentResult fragment = (FragmentResult)0; half4 result = 0;
half u_xlat16_0;

    result = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    u_xlat16_0 = tex2Dlod(_InputDepthTexture, float4(input.uv.xy, 0, _BlitMipLevel)).x;
    fragment.depth = u_xlat16_0;
    fragment.color = result; return fragment;

}
#endif
ENDCG
}
}
Fallback Off
}
