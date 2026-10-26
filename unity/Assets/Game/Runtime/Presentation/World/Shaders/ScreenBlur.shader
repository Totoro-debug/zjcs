Shader "Zjcs/Official/UI/ScreenBlur" {
SubShader { Cull Off ZWrite Off ZTest Always
Pass { CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
Varying vert(appdata_img input) { Varying output; output.position=UnityObjectToClipPos(input.vertex); output.uv=input.texcoord; return output; }
float2 _GlobalMipBias;
float4 _BlitTexture_TexelSize;
float2 _BlurDir;
sampler2D _BlitTexture;
half4 frag(Varying input) : SV_Target { half4 result = 0;
float2 u_xlat0;
half3 u_xlat16_0;
float2 u_xlat1;
half3 u_xlat16_1;
half3 u_xlat16_2;
half3 u_xlat16_3;
float2 u_xlat8;
float2 u_xlat9;

    u_xlat0.xy = (-_BlurDir.xy) * _BlitTexture_TexelSize.xy + input.uv.xy;
    u_xlat16_0.xyz = tex2Dbias(_BlitTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat1.xy = _BlitTexture_TexelSize.xy * _BlurDir.xy;
    u_xlat9.xy = (-u_xlat1.xy) * float2(2.0, 2.0) + input.uv.xy;
    u_xlat16_2.xyz = tex2Dbias(_BlitTexture, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.0606257617, 0.0606257617, 0.0606257617);
    u_xlat9.xy = (-u_xlat1.xy) * float2(3.0, 3.0) + input.uv.xy;
    u_xlat16_2.xyz = tex2Dbias(_BlitTexture, float4(u_xlat9.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.xyz = u_xlat16_2.xyz * float3(0.00597978989, 0.00597978989, 0.00597978989) + u_xlat16_3.xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(0.241842851, 0.241842851, 0.241842851) + u_xlat16_3.xyz;
    u_xlat16_0.xyz = tex2Dbias(_BlitTexture, float4(input.uv.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(0.383103192, 0.383103192, 0.383103192) + u_xlat16_3.xyz;
    u_xlat0.xy = _BlurDir.xy * _BlitTexture_TexelSize.xy + input.uv.xy;
    u_xlat16_0.xyz = tex2Dbias(_BlitTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(0.241842851, 0.241842851, 0.241842851) + u_xlat16_3.xyz;
    u_xlat0.xy = u_xlat1.xy * float2(2.0, 2.0) + input.uv.xy;
    u_xlat8.xy = u_xlat1.xy * float2(3.0, 3.0) + input.uv.xy;
    u_xlat16_1.xyz = tex2Dbias(_BlitTexture, float4(u_xlat8.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_0.xyz = tex2Dbias(_BlitTexture, float4(u_xlat0.xy, 0, _GlobalMipBias.x)).xyz;
    u_xlat16_3.xyz = u_xlat16_0.xyz * float3(0.0606257617, 0.0606257617, 0.0606257617) + u_xlat16_3.xyz;
    result.xyz = u_xlat16_1.xyz * float3(0.00597978989, 0.00597978989, 0.00597978989) + u_xlat16_3.xyz;
    result.w = 1.0;
    return result;

}
ENDCG
}
}
}
