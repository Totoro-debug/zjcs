Shader "Zjcs/Official/World/Map13/Sampling" {
Properties {

}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Pass {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
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
#include "UnityCG.cginc"
struct Input { uint vertexID:SV_VertexID;  };
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };

float _SampleOffset;
float2 _GlobalMipBias;
float4 _BlitScaleBias;
float4 _BlitTexture_TexelSize;
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
float4 u_xlat0;
half4 u_xlat16_0;
float4 u_xlat1;
half4 u_xlat16_1;
half4 u_xlat16_2;

    u_xlat0 = float4(_SampleOffset,_SampleOffset,_SampleOffset,_SampleOffset) * float4(-1.0, -1.0, 1.0, 1.0);
    u_xlat1 = _BlitTexture_TexelSize.xyxy * u_xlat0.xyzy + input.uv.xyxy;
    u_xlat0 = _BlitTexture_TexelSize.xyxy * u_xlat0.xwzw + input.uv.xyxy;
    u_xlat16_2 = tex2Dbias(_BlitTexture, float4(u_xlat1.xy, 0, _GlobalMipBias.x));
    u_xlat16_1 = tex2Dbias(_BlitTexture, float4(u_xlat1.zw, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat16_1 + u_xlat16_2;
    u_xlat16_2 = tex2Dbias(_BlitTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x));
    u_xlat16_0 = tex2Dbias(_BlitTexture, float4(u_xlat0.zw, 0, _GlobalMipBias.x));
    u_xlat1 = u_xlat1 + u_xlat16_2;
    u_xlat0 = u_xlat16_0 + u_xlat1;
    u_xlat0 = u_xlat0 * float4(0.25, 0.25, 0.25, 0.25);
    result = u_xlat0;
    return result;

}
ENDCG
}
}
Fallback Off
}
