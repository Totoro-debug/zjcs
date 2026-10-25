// Windows adaptation of UI/ColorShift, APK 854_156383 shader pathId 6814085886608541704.
// Arithmetic, gamma conversion, alpha quantization and clipping follow its GLES3 programs.
Shader "Zjcs/Official/UI/ColorShift"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _TextureSampleAdd, _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY, Epsilon;
            int _UIVertexColorAlwaysGammaSpace;

            struct AppData { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 mask : TEXCOORD1; };

            Varyings vert(AppData input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                float3 c = input.color.rgb;
                float3 high = c * (c * (c * 0.265884995 + 0.736584008) - 0.00980184041) + 0.00319697009;
                float3 low = c * 0.0849710032 - 0.000163029006;
                float3 converted = float3(c.r < 0.0725490004 ? low.r : high.r,
                    c.g < 0.0725490004 ? low.g : high.g, c.b < 0.0725490004 ? low.b : high.b);
                output.color = float4(_UIVertexColorAlwaysGammaSpace != 0 ? converted : c, input.color.a) * _Color;
                output.color.rgb = max(1.05499995 * pow(max(output.color.rgb, 0), 0.416666657) - 0.0549999997, 0);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float2 pixelSize = output.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 sampleColor = tex2D(_MainTex, input.uv) + _TextureSampleAdd;
                float alpha = sampleColor.a * (round(input.color.a * 255) * 0.00392156886);
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                alpha *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.00100000005);
                #endif
                // The two source comparisons are >=; preserve their tie ordering.
                float4 p = sampleColor.g >= sampleColor.b
                    ? float4(sampleColor.gb, 0, -1.0 / 3.0)
                    : float4(sampleColor.bg, -1, 2.0 / 3.0);
                float4 q = sampleColor.r >= p.x ? float4(sampleColor.r, p.y, p.z, p.x)
                    : float4(p.x, p.y, p.w, sampleColor.r);
                float chroma = q.x - min(q.w, q.y);
                float hue = frac(abs(q.z + (q.w - q.y) / (6 * chroma + Epsilon)) + input.color.r - 0.5);
                float lightness = q.x - chroma * 0.5;
                float saturation = chroma / (1 - abs(2 * lightness - 1) + Epsilon);
                float2 original = float2(saturation, lightness);
                float2 delta = input.color.gb * 2 - 1;
                float2 adjusted = saturate(float2(
                    original.x + delta.x * (delta.x > 0 ? 1 - original.x : original.x),
                    original.y + delta.y * (delta.y > 0 ? 1 - original.y : original.y)));
                float3 hueColor = saturate(abs(frac(hue + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1) - 0.5;
                float3 rgb = hueColor * adjusted.x * (1 - abs(2 * adjusted.y - 1)) + adjusted.y;
                return float4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
