Shader "Zjcs/Official/World/Map13/DebugReplacement" {
Properties {

}
SubShader {
Tags { "IGNOREPROJECTOR"="true" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Pass {
Tags { "IGNOREPROJECTOR"="true" "LIGHTMODE"="UniversalForward" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Cull Back
ZWrite On
ZTest LEqual
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
struct Input { float4 vertex:POSITION; float4 uv:TEXCOORD0; float4 uv1:TEXCOORD1; float4 uv2:TEXCOORD2; float4 uv3:TEXCOORD3; float4 color:COLOR; float4 normal:NORMAL; float4 tangent:TANGENT; };
struct Varying { float4 position:SV_POSITION; float4 uv:TEXCOORD0; float4 world:TEXCOORD1; float4 mask:TEXCOORD2; float4 uv3:TEXCOORD3; float4 color:COLOR; float4 uv8:TEXCOORD8; float4 uv9:TEXCOORD9; };

int _DebugVertexAttributeMode;
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
    output.position = float4(UNITY_MATRIX_VP[0][3],UNITY_MATRIX_VP[1][3],UNITY_MATRIX_VP[2][3],UNITY_MATRIX_VP[3][3]) * u_xlat0.wwww + u_xlat1;
    output.uv = input.uv;
    output.world = input.uv1;
    output.mask = input.uv2;
    output.uv3 = input.uv3;
    output.color = input.color;
    output.uv8 = input.normal;
    output.uv9 = input.tangent;
    return output;

}
half4 frag(Varying input) : SV_Target { half4 result = 0;


    switch(_DebugVertexAttributeMode){
        case 1:
            result = input.uv;
            return result;
        case 2:
            result = input.world;
            return result;
        case 3:
            result = input.mask;
            return result;
        case 4:
            result = input.uv3;
            return result;
        case 5:
            result = input.color;
            return result;
        case 6:
            result = input.uv9;
            return result;
        case 7:
            result = input.uv8;
            return result;
        default:
            result = float4(0.0, 0.0, 0.0, 1.0);
            return result;
    }
    return result;

}
ENDCG
}
}
Fallback Off
}
