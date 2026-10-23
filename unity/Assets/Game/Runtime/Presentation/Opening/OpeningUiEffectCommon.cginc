#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct OpeningUiAppData
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    fixed4 color : COLOR;
};

struct OpeningUiVertex
{
    float4 vertex : SV_POSITION;
    float4 worldPosition : TEXCOORD1;
    float2 uv : TEXCOORD0;
    fixed4 color : COLOR;
};

OpeningUiVertex OpeningUiVert(OpeningUiAppData input)
{
    OpeningUiVertex output;
    output.worldPosition = input.vertex;
    output.vertex = UnityObjectToClipPos(input.vertex);
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

fixed4 OpeningUiApplyClip(fixed4 color, float4 worldPosition)
{
    #ifdef UNITY_UI_CLIP_RECT
        color.a *= UnityGet2DClipping(worldPosition.xy, _ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
        clip(color.a - 0.001);
    #endif
    return color;
}
