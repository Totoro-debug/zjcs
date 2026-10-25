Shader "Zjcs/Combat/TimelineSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [HideInInspector] PixelSnap ("Pixel Snap", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color, _RendererColor;
            float2 _GlobalMipBias;
            struct Input { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color * _RendererColor;
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }
            half4 frag(Output input) : SV_Target
            {
                return tex2Dbias(_MainTex, float4(input.uv, 0, _GlobalMipBias.x)) * input.color;
            }
            ENDCG
        }
    }
}
