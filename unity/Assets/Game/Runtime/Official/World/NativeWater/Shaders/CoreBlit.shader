Shader "Zjcs/Official/World/Map13/CoreBlit" {
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
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
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
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
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
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
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
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    result = tex2Dlod(_BlitTexture, float4(u_xlat0.xy, 0, _BlitMipLevel));
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    result = tex2Dlod(_BlitTexture, float4(u_xlat0.xy, 0, _BlitMipLevel));
    return result;

}
#endif
ENDCG
}
Pass {
Tags { "RenderPipeline"="UniversalPipeline" }
Cull Off
ZWrite Off
ZTest Always
Blend DstColor Zero, DstColor Zero
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
ZWrite Off
ZTest Always
Blend DstColor Zero, DstColor Zero
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
ZWrite Off
ZTest Always
Blend DstColor Zero, DstColor Zero
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
ZWrite Off
ZTest Always
Blend DstColor Zero, DstColor Zero
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
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
ZWrite Off
ZTest Always
Blend DstColor Zero, DstColor Zero
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
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    result = tex2Dlod(_BlitTexture, float4(u_xlat0.xy, 0, _BlitMipLevel));
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    result = tex2Dlod(_BlitTexture, float4(u_xlat0.xy, 0, _BlitMipLevel));
    return result;

}
#endif
ENDCG
}
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
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result.xyz = u_xlat16_0.xyz;
    result.w = 1.0;
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result.xyz = u_xlat16_0.xyz;
    result.w = 1.0;
    return result;

}
#endif
ENDCG
}
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
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
float4 Map13SplatDotFragment(float value) { return float4(value, value, value, value); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result = Map13SplatDotFragment(dot(u_xlat16_0.xyz, float3(0.212672904, 0.715152204, 0.0721750036)));
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
float4 Map13SplatDotFragment(float value) { return float4(value, value, value, value); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result = Map13SplatDotFragment(dot(u_xlat16_0.xyz, float3(0.212672904, 0.715152204, 0.0721750036)));
    return result;

}
#endif
ENDCG
}
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
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).w;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).w;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#endif
ENDCG
}
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
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).x;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half u_xlat16_0;
float3 u_xlat1;
bool2 u_xlatb2;
float2 u_xlat6;
bool2 u_xlatb6;

    u_xlat0.xy = input.uv.xy;
    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb6.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat6.x = (u_xlatb6.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat6.y = (u_xlatb6.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat6.x = (-u_xlat6.x) + 1.0;
    u_xlat1.z = (-u_xlat6.y) + u_xlat6.x;
    u_xlat6.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat6;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat6.x) : u_xlat6.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat6.x) : u_xlat6.x;
        u_xlat6 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat6.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).x;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#endif
ENDCG
}
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
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
float4 Map13SplatDotFragment(float value) { return float4(value, value, value, value); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
half3 u_xlat16_0;

    u_xlat16_0.xyz = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel)).xyz;
    result = Map13SplatDotFragment(dot(u_xlat16_0.xyz, float3(0.212672904, 0.715152204, 0.0721750036)));
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
float4 Map13SplatDotFragment(float value) { return float4(value, value, value, value); }
half4 frag(Varying input) : SV_Target { half4 result = 0;
half3 u_xlat16_0;

    u_xlat16_0.xyz = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel)).xyz;
    result = Map13SplatDotFragment(dot(u_xlat16_0.xyz, float3(0.212672904, 0.715152204, 0.0721750036)));
    return result;

}
#endif
ENDCG
}
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
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel)).w;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel)).w;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#endif
ENDCG
}
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
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel)).x;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
sampler2D _BlitTexture;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    output.uv.xy = u_xlat1.xw * _BlitScaleBias.xy + _BlitScaleBias.zw;
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
half u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel)).x;
    result = float4(u_xlat16_0,u_xlat16_0,u_xlat16_0,u_xlat16_0);
    return result;

}
#endif
ENDCG
}
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
#pragma multi_compile __ BLIT_DECODE_HDR
#pragma multi_compile __ DISABLE_TEXTURE2D_X_ARRAY
#include "UnityCG.cginc"
#if !defined(BLIT_DECODE_HDR) && !defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
bool2 u_xlatb8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb8.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat8.x = (u_xlatb8.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat8.y = (u_xlatb8.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat1.z = (-u_xlat8.y) + u_xlat8.x;
    u_xlat8.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat8;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat8.x) : u_xlat8.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat8.x) : u_xlat8.x;
        u_xlat8 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat8.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result.xyz = u_xlat16_0.xyz;
    result.w = 1.0;
    return result;

}
#elif defined(BLIT_DECODE_HDR) && !defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitDecodeInstructions;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
half3 u_xlat16_4;
float2 u_xlat5;
float2 u_xlat6;
float2 u_xlat10;
bool2 u_xlatb10;
float2 u_xlat11;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat10.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat5.xy = (u_xlatb0.y) ? u_xlat10.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat5.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat10.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat10.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat10.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat11.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat6.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat11.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat10.xy : u_xlat6.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb10.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat10.x = (u_xlatb10.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat10.y = (u_xlatb10.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat10.x = (-u_xlat10.x) + 1.0;
    u_xlat1.z = (-u_xlat10.y) + u_xlat10.x;
    u_xlat10.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat10;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat10.x) : u_xlat10.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat10.x) : u_xlat10.x;
        u_xlat10 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat10.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel));
    u_xlat16_4.x = u_xlat16_0.w + -1.0;
    u_xlat16_4.x = _BlitDecodeInstructions.w * u_xlat16_4.x + 1.0;
    u_xlat16_4.x = max(u_xlat16_4.x, 0.0);
    u_xlat16_4.x = log2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.y;
    u_xlat16_4.x = exp2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.x;
    u_xlat16_4.xyz = u_xlat16_0.xyz * u_xlat16_4.xxx;
    result.xyz = u_xlat16_4.xyz;
    result.w = 1.0;
    return result;

}
#elif !defined(BLIT_DECODE_HDR) && defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
bool2 u_xlatb8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb8.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat8.x = (u_xlatb8.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat8.y = (u_xlatb8.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat1.z = (-u_xlat8.y) + u_xlat8.x;
    u_xlat8.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat8;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat8.x) : u_xlat8.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat8.x) : u_xlat8.x;
        u_xlat8 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat8.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result.xyz = u_xlat16_0.xyz;
    result.w = 1.0;
    return result;

}
#elif defined(BLIT_DECODE_HDR) && defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitDecodeInstructions;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
half3 u_xlat16_4;
float2 u_xlat5;
float2 u_xlat6;
float2 u_xlat10;
bool2 u_xlatb10;
float2 u_xlat11;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat10.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat5.xy = (u_xlatb0.y) ? u_xlat10.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat5.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat10.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat10.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat10.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat11.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat6.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat11.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat10.xy : u_xlat6.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb10.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat10.x = (u_xlatb10.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat10.y = (u_xlatb10.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat10.x = (-u_xlat10.x) + 1.0;
    u_xlat1.z = (-u_xlat10.y) + u_xlat10.x;
    u_xlat10.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat10;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat10.x) : u_xlat10.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat10.x) : u_xlat10.x;
        u_xlat10 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat10.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel));
    u_xlat16_4.x = u_xlat16_0.w + -1.0;
    u_xlat16_4.x = _BlitDecodeInstructions.w * u_xlat16_4.x + 1.0;
    u_xlat16_4.x = max(u_xlat16_4.x, 0.0);
    u_xlat16_4.x = log2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.y;
    u_xlat16_4.x = exp2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.x;
    u_xlat16_4.xyz = u_xlat16_0.xyz * u_xlat16_4.xxx;
    result.xyz = u_xlat16_4.xyz;
    result.w = 1.0;
    return result;

}
#endif
ENDCG
}
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
#pragma multi_compile __ BLIT_DECODE_HDR
#pragma multi_compile __ DISABLE_TEXTURE2D_X_ARRAY
#include "UnityCG.cginc"
#if !defined(BLIT_DECODE_HDR) && !defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
bool2 u_xlatb8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb8.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat8.x = (u_xlatb8.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat8.y = (u_xlatb8.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat1.z = (-u_xlat8.y) + u_xlat8.x;
    u_xlat8.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat8;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat8.x) : u_xlat8.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat8.x) : u_xlat8.x;
        u_xlat8 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat8.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result.xyz = u_xlat16_0.xyz;
    result.w = 1.0;
    return result;

}
#elif defined(BLIT_DECODE_HDR) && !defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitDecodeInstructions;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
half3 u_xlat16_4;
float2 u_xlat5;
float2 u_xlat6;
float2 u_xlat10;
bool2 u_xlatb10;
float2 u_xlat11;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat10.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat5.xy = (u_xlatb0.y) ? u_xlat10.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat5.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat10.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat10.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat10.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat11.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat6.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat11.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat10.xy : u_xlat6.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb10.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat10.x = (u_xlatb10.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat10.y = (u_xlatb10.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat10.x = (-u_xlat10.x) + 1.0;
    u_xlat1.z = (-u_xlat10.y) + u_xlat10.x;
    u_xlat10.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat10;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat10.x) : u_xlat10.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat10.x) : u_xlat10.x;
        u_xlat10 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat10.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel));
    u_xlat16_4.x = u_xlat16_0.w + -1.0;
    u_xlat16_4.x = _BlitDecodeInstructions.w * u_xlat16_4.x + 1.0;
    u_xlat16_4.x = max(u_xlat16_4.x, 0.0);
    u_xlat16_4.x = log2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.y;
    u_xlat16_4.x = exp2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.x;
    u_xlat16_4.xyz = u_xlat16_0.xyz * u_xlat16_4.xxx;
    result.xyz = u_xlat16_4.xyz;
    result.w = 1.0;
    return result;

}
#elif !defined(BLIT_DECODE_HDR) && defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half3 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
float2 u_xlat4;
float2 u_xlat5;
float2 u_xlat8;
bool2 u_xlatb8;
float2 u_xlat9;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat8.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat4.xy = (u_xlatb0.y) ? u_xlat8.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat4.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat8.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat8.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat8.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat9.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat5.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat9.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat8.xy : u_xlat5.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb8.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat8.x = (u_xlatb8.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat8.y = (u_xlatb8.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat8.x = (-u_xlat8.x) + 1.0;
    u_xlat1.z = (-u_xlat8.y) + u_xlat8.x;
    u_xlat8.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat8;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat8.x) : u_xlat8.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat8.x) : u_xlat8.x;
        u_xlat8 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat8.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0.xyz = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel)).xyz;
    result.xyz = u_xlat16_0.xyz;
    result.w = 1.0;
    return result;

}
#elif defined(BLIT_DECODE_HDR) && defined(DISABLE_TEXTURE2D_X_ARRAY)
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _BlitMipLevel;
float2 _BlitTextureSize;
float4 _BlitDecodeInstructions;
float4 _BlitScaleBias;
float4 _BlitScaleBiasRt;
samplerCUBE _BlitCubeTexture;
uint _BlitPaddingSize;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
int u_xlati0;
uint2 u_xlatu0;
float4 u_xlat1;
float3 u_xlat2;
int u_xlati4;

    u_xlati0 = int(uint(uint(input.vertexID) & 1u));
    u_xlatu0.y = uint(uint(input.vertexID) >> (1u & uint(0x1F)));
    u_xlati4 = (-u_xlati0) + (-int(u_xlatu0.y));
    u_xlati0 = u_xlati0 + int(u_xlatu0.y);
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.xw = float2(u_xlatu0.yx);
    u_xlati0 = u_xlati4 + 1;
    u_xlatu0.x = uint(uint(u_xlati0) & 1u);
    u_xlat1.y = float(u_xlatu0.x);
    u_xlat0.xy = u_xlat1.xy * _BlitScaleBiasRt.xy + _BlitScaleBiasRt.zw;
    u_xlat0.z = float(-1.0);
    u_xlat0.w = float(1.0);
    output.position = u_xlat0 * float4(2.0, -2.0, 1.0, 1.0) + float4(-1.0, 1.0, 0.0, 0.0);
    u_xlat0.x = float(_BlitPaddingSize);
    u_xlat2.x = u_xlat0.x * 0.5;
    u_xlat0.xz = u_xlat0.xx + float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = u_xlat2.xx / u_xlat0.xz;
    u_xlat0.xz = u_xlat0.xz / float2(_BlitTextureSize.x, _BlitTextureSize.y);
    u_xlat2.xz = (-u_xlat2.xz) + u_xlat1.xw;
    u_xlat0.xy = u_xlat0.xz * u_xlat2.xz;
    output.uv.xy = u_xlat0.xy * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.position.z = UNITY_NEAR_CLIP_VALUE * output.position.w;
#if UNITY_UV_STARTS_AT_TOP
output.position.y = -output.position.y;
#endif
return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;
float3 u_xlat0;
half4 u_xlat16_0;
bool2 u_xlatb0;
float3 u_xlat1;
bool3 u_xlatb1;
float4 u_xlat2;
bool2 u_xlatb2;
float3 u_xlat3;
half3 u_xlat16_4;
float2 u_xlat5;
float2 u_xlat6;
float2 u_xlat10;
bool2 u_xlatb10;
float2 u_xlat11;

    u_xlatb0.x = input.uv.x<0.0;
    if(u_xlatb0.x){
        u_xlatb0.xy = (input.uv.yyyy < float4(0.0, 1.0, 0.0, 0.0)).xy;
        u_xlat1.xyz = input.uv.xyy + float3(1.0, 1.0, -1.0);
        u_xlat10.xy = (-input.uv.xy) + float2(0.0, 1.0);
        u_xlat5.xy = (u_xlatb0.y) ? u_xlat10.xy : u_xlat1.xz;
        u_xlat0.xy = (u_xlatb0.x) ? u_xlat1.xy : u_xlat5.xy;
    } else {
        u_xlatb1.xyz = (input.uv.xyyx < float4(1.0, 0.0, 1.0, 0.0)).xyz;
        u_xlat2 = (-input.uv.xyxy) + float4(1.0, 2.0, 2.0, 1.0);
        u_xlat10.xy = (u_xlatb1.z) ? input.uv.xy : u_xlat2.xy;
        u_xlat2.xy = (-input.uv.xy) + float2(1.0, 0.0);
        u_xlat10.xy = (u_xlatb1.y) ? u_xlat2.xy : u_xlat10.xy;
        u_xlat3.xyz = input.uv.xyy + float3(-1.0, 1.0, -1.0);
        u_xlat11.xy = (u_xlatb1.z) ? u_xlat2.zw : u_xlat3.xz;
        u_xlat6.xy = (u_xlatb1.y) ? u_xlat3.xy : u_xlat11.xy;
        u_xlat0.xy = (u_xlatb1.x) ? u_xlat10.xy : u_xlat6.xy;
    }
    u_xlat0.xy = u_xlat0.xy * float2(2.0, 2.0) + float2(-1.0, -1.0);
    u_xlatb10.xy = (u_xlat0.xyxy < float4(0.0, 0.0, 0.0, 0.0)).xy;
    u_xlat10.x = (u_xlatb10.x) ? (-u_xlat0.x) : u_xlat0.x;
    u_xlat10.y = (u_xlatb10.y) ? (-u_xlat0.y) : u_xlat0.y;
    u_xlat10.x = (-u_xlat10.x) + 1.0;
    u_xlat1.z = (-u_xlat10.y) + u_xlat10.x;
    u_xlat10.x = max((-u_xlat1.z), 0.0);
    u_xlatb2.xy = (u_xlat0.xyxx >= float4(0.0, 0.0, 0.0, 0.0)).xy;
    {
        float2 hlslcc_movcTemp = u_xlat10;
        hlslcc_movcTemp.x = (u_xlatb2.x) ? (-u_xlat10.x) : u_xlat10.x;
        hlslcc_movcTemp.y = (u_xlatb2.y) ? (-u_xlat10.x) : u_xlat10.x;
        u_xlat10 = hlslcc_movcTemp;
    }
    u_xlat1.xy = u_xlat10.xy + u_xlat0.xy;
    u_xlat0.x = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xyz = u_xlat0.xxx * u_xlat1.xyz;
    u_xlat16_0 = texCUBElod(_BlitCubeTexture, float4(u_xlat0.xyz, _BlitMipLevel));
    u_xlat16_4.x = u_xlat16_0.w + -1.0;
    u_xlat16_4.x = _BlitDecodeInstructions.w * u_xlat16_4.x + 1.0;
    u_xlat16_4.x = max(u_xlat16_4.x, 0.0);
    u_xlat16_4.x = log2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.y;
    u_xlat16_4.x = exp2(u_xlat16_4.x);
    u_xlat16_4.x = u_xlat16_4.x * _BlitDecodeInstructions.x;
    u_xlat16_4.xyz = u_xlat16_0.xyz * u_xlat16_4.xxx;
    result.xyz = u_xlat16_4.xyz;
    result.w = 1.0;
    return result;

}
#endif
ENDCG
}
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
#pragma multi_compile __ _LINEAR_TO_SRGB_CONVERSION
#include "UnityCG.cginc"
#if !defined(DISABLE_TEXTURE2D_X_ARRAY) && !defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    result = u_xlat16_0;
    return result;

}
#elif !defined(DISABLE_TEXTURE2D_X_ARRAY) && defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;
float3 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    u_xlat1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= u_xlat16_0.xyzx).xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    u_xlat16_0.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_0.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_0.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
    result = u_xlat16_0;
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY) && !defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    result = u_xlat16_0;
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY) && defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;
float3 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    u_xlat1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= u_xlat16_0.xyzx).xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    u_xlat16_0.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_0.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_0.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
    result = u_xlat16_0;
    return result;

}
#endif
ENDCG
}
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
#pragma multi_compile __ _LINEAR_TO_SRGB_CONVERSION
#include "UnityCG.cginc"
#if !defined(DISABLE_TEXTURE2D_X_ARRAY) && !defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    result = u_xlat16_0;
    return result;

}
#elif !defined(DISABLE_TEXTURE2D_X_ARRAY) && defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;
float3 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    u_xlat1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= u_xlat16_0.xyzx).xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    u_xlat16_0.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_0.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_0.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
    result = u_xlat16_0;
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY) && !defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    result = u_xlat16_0;
    return result;

}
#elif defined(DISABLE_TEXTURE2D_X_ARRAY) && defined(_LINEAR_TO_SRGB_CONVERSION)
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
half4 u_xlat16_0;
float3 u_xlat1;
bool3 u_xlatb2;
half3 u_xlat16_3;

    u_xlat16_0 = tex2Dlod(_BlitTexture, float4(input.uv.xy, 0, _BlitMipLevel));
    u_xlat1.xyz = log2(abs(u_xlat16_0.xyz));
    u_xlat1.xyz = u_xlat1.xyz * float3(0.416666657, 0.416666657, 0.416666657);
    u_xlat1.xyz = exp2(u_xlat1.xyz);
    u_xlat1.xyz = u_xlat1.xyz * float3(1.05499995, 1.05499995, 1.05499995) + float3(-0.0549999997, -0.0549999997, -0.0549999997);
    u_xlatb2.xyz = (float4(0.00313080009, 0.00313080009, 0.00313080009, 0.0) >= u_xlat16_0.xyzx).xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(12.9232101, 12.9232101, 12.9232101);
    u_xlat16_0.x = (u_xlatb2.x) ? u_xlat16_3.x : u_xlat1.x;
    u_xlat16_0.y = (u_xlatb2.y) ? u_xlat16_3.y : u_xlat1.y;
    u_xlat16_0.z = (u_xlatb2.z) ? u_xlat16_3.z : u_xlat1.z;
    result = u_xlat16_0;
    return result;

}
#endif
ENDCG
}
}
Fallback Off
}
