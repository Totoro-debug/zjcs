// Windows translation of the pinned UI/GameDamage GLES program. The glyph atlas
// contains the original fill/outline; output uses premultiplied alpha.
Shader "Zjcs/Opening/DamageBitmap"
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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _TextureSampleAdd, _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 mask:TEXCOORD2; };
            Output vert(Input v)
            {
                Output o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                float3 linearHigh=v.color.rgb*(v.color.rgb*(v.color.rgb*.265884995+.736584008)-.00980184041)+.00319697009;
                float3 linearLow=v.color.rgb*.0849710032-.000163029006;
                if (_UIVertexColorAlwaysGammaSpace) v.color.rgb=lerp(linearHigh,linearLow,step(v.color.rgb,.0725490004));
                o.color=v.color*_Color;
                o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                float2 pixel=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 rect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(v.vertex.xy*2-rect.xy-rect.zw,.25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixel)));
                return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                i.color.a=round(i.color.a*255)/255;
                fixed4 c=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                    float2 m=saturate((_ClipRect.zw-_ClipRect.xy-abs(i.mask.xy))*i.mask.zw);
                    c.a*=m.x*m.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(c.a-.001);
                #endif
                c.rgb*=c.a;
                return c;
            }
            ENDCG
        }
    }
}
