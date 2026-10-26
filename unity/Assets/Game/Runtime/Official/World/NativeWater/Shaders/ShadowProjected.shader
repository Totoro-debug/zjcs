Shader "Zjcs/Official/World/Map13/ShadowProjected" {
Properties {
_ShadowColorMask ("__ShadowColorMask", Float) = 0.0
}
SubShader {
Tags { "RenderType"="Transparent" }
Pass {
Tags { "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest Always
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask 0
Stencil { Ref 5 ReadMask 4 WriteMask 1 Comp NotEqual Pass Replace Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float4 tangent:TANGENT; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION;  };

float _ShadowRadius;
float3 _LightPos;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float2 u_xlat7;
float u_xlat9;

    u_xlat0 = input.color.yyww * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyxy;
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyxy * input.color.xxzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyxy;
    u_xlat0 = u_xlat0 + (-_LightPos.xyxy);
    u_xlat1.x = dot(u_xlat0.xy, u_xlat0.xy);
    u_xlat1.x = rsqrt(u_xlat1.x);
    u_xlat1.xy = u_xlat0.xy * u_xlat1.xx;
    u_xlat0.x = dot(u_xlat0.zw, u_xlat0.zw);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xy = u_xlat0.xx * u_xlat0.zw;
    u_xlat1.z = 0.0;
    u_xlat0.z = 0.0;
    u_xlat0.xyz = u_xlat0.xyz + u_xlat1.xyz;
    u_xlat9 = dot(u_xlat0.xy, u_xlat0.xy);
    u_xlat9 = rsqrt(u_xlat9);
    u_xlat0.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat0.xyz;
    u_xlat1.xyz = input.tangent.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.tangent.xxx + u_xlat1.xyz;
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.tangent.zzz + u_xlat1.xyz;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat9 = max(u_xlat9, 1.17549435e-38);
    u_xlat9 = rsqrt(u_xlat9);
    u_xlat1.xy = float2(u_xlat9,u_xlat9) * u_xlat1.xy;
    u_xlat2.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat2.xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat2.xyz;
    u_xlat2.xyz = u_xlat2.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat7.xy = (-u_xlat2.xy) + _LightPos.xy;
    u_xlat9 = dot(u_xlat7.xy, u_xlat7.xy);
    u_xlat9 = rsqrt(u_xlat9);
    u_xlat7.xy = float2(u_xlat9,u_xlat9) * u_xlat7.xy;
    u_xlat9 = dot(u_xlat7.xy, u_xlat1.xy);
    u_xlat9 = ceil(u_xlat9);
    u_xlat9 = clamp(u_xlat9, 0.0, 1.0);
    u_xlat1.x = _ShadowRadius * 1.41429996;
    u_xlat9 = u_xlat9 * u_xlat1.x;
    u_xlat0.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat0.xyz + u_xlat2.xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


    result = float4(1.0, 1.0, 1.0, 1.0);
    return result;

}
ENDCG
}
Pass {
Tags { "RenderType"="Transparent" }
Cull Off
ZWrite Off
ZTest Always
Blend One Zero, One Zero
BlendOp Add, Add
ColorMask 0
Stencil { Ref 3 ReadMask 1 WriteMask 2 Comp Equal Pass Replace Fail Keep ZFail Keep }
AlphaToMask Off
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct Input { float3 vertex:POSITION; float4 tangent:TANGENT; float4 color:COLOR; };
struct Varying { float4 position:SV_POSITION;  };

float _ShadowRadius;
float3 _LightPos;
Varying vert(Input input) { Varying output = (Varying)0;
float4 u_xlat0;
float4 u_xlat1;
float3 u_xlat2;
float2 u_xlat7;
float u_xlat9;

    u_xlat0 = input.color.yyww * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyxy;
    u_xlat0 = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyxy * input.color.xxzz + u_xlat0;
    u_xlat0 = u_xlat0 + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyxy;
    u_xlat0 = u_xlat0 + (-_LightPos.xyxy);
    u_xlat1.x = dot(u_xlat0.xy, u_xlat0.xy);
    u_xlat1.x = rsqrt(u_xlat1.x);
    u_xlat1.xy = u_xlat0.xy * u_xlat1.xx;
    u_xlat0.x = dot(u_xlat0.zw, u_xlat0.zw);
    u_xlat0.x = rsqrt(u_xlat0.x);
    u_xlat0.xy = u_xlat0.xx * u_xlat0.zw;
    u_xlat1.z = 0.0;
    u_xlat0.z = 0.0;
    u_xlat0.xyz = u_xlat0.xyz + u_xlat1.xyz;
    u_xlat9 = dot(u_xlat0.xy, u_xlat0.xy);
    u_xlat9 = rsqrt(u_xlat9);
    u_xlat0.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat0.xyz;
    u_xlat1.xyz = input.tangent.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.tangent.xxx + u_xlat1.xyz;
    u_xlat1.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.tangent.zzz + u_xlat1.xyz;
    u_xlat9 = dot(u_xlat1.xyz, u_xlat1.xyz);
    u_xlat9 = max(u_xlat9, 1.17549435e-38);
    u_xlat9 = rsqrt(u_xlat9);
    u_xlat1.xy = float2(u_xlat9,u_xlat9) * u_xlat1.xy;
    u_xlat2.xyz = input.vertex.yyy * float4(unity_ObjectToWorld[0][1],unity_ObjectToWorld[1][1],unity_ObjectToWorld[2][1],unity_ObjectToWorld[3][1]).xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][0],unity_ObjectToWorld[1][0],unity_ObjectToWorld[2][0],unity_ObjectToWorld[3][0]).xyz * input.vertex.xxx + u_xlat2.xyz;
    u_xlat2.xyz = float4(unity_ObjectToWorld[0][2],unity_ObjectToWorld[1][2],unity_ObjectToWorld[2][2],unity_ObjectToWorld[3][2]).xyz * input.vertex.zzz + u_xlat2.xyz;
    u_xlat2.xyz = u_xlat2.xyz + float4(unity_ObjectToWorld[0][3],unity_ObjectToWorld[1][3],unity_ObjectToWorld[2][3],unity_ObjectToWorld[3][3]).xyz;
    u_xlat7.xy = (-u_xlat2.xy) + _LightPos.xy;
    u_xlat9 = dot(u_xlat7.xy, u_xlat7.xy);
    u_xlat9 = rsqrt(u_xlat9);
    u_xlat7.xy = float2(u_xlat9,u_xlat9) * u_xlat7.xy;
    u_xlat9 = dot(u_xlat7.xy, u_xlat1.xy);
    u_xlat9 = ceil(u_xlat9);
    u_xlat9 = clamp(u_xlat9, 0.0, 1.0);
    u_xlat1.x = _ShadowRadius * 1.41429996;
    u_xlat9 = u_xlat9 * u_xlat1.x;
    u_xlat0.xyz = float3(u_xlat9,u_xlat9,u_xlat9) * u_xlat0.xyz + u_xlat2.xyz;
    u_xlat1 = u_xlat0.yyyy * float4(UNITY_MATRIX_VP[0][1],UNITY_MATRIX_VP[1][1],UNITY_MATRIX_VP[2][1],UNITY_MATRIX_VP[3][1]);
    u_xlat1 = float4(UNITY_MATRIX_VP[0][0],UNITY_MATRIX_VP[1][0],UNITY_MATRIX_VP[2][0],UNITY_MATRIX_VP[3][0]) * u_xlat0.xxxx + u_xlat1;
    u_xlat0 = float4(UNITY_MATRIX_VP[0][2],UNITY_MATRIX_VP[1][2],UNITY_MATRIX_VP[2][2],UNITY_MATRIX_VP[3][2]) * u_xlat0.zzzz + u_xlat1;
    output.position = u_xlat0 + float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]);
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


    result = float4(1.0, 1.0, 1.0, 1.0);
    return result;

}
ENDCG
}
}
Fallback Off
}
