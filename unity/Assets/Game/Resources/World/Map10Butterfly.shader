Shader "Zjcs/World/Map10ButterflyNoiseMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _Color ("Color", Color) = (1,1,1,1)
        _ColorIntensity ("Color Intensity", Float) = 4
        _IntensityAffectAlpha ("Intensity Affects Alpha", Float) = 0
        _WorldClipHeight ("World Clip Height", Float) = -1000000
        _EnableExternalAlpha ("External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ USE_MAINTEX
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainColor;
            float4 _Color;
            float _ColorIntensity;
            float _IntensityAffectAlpha;
            float _WorldClipHeight;
            float _EnableExternalAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float worldY : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldY = mul(unity_ObjectToWorld, input.vertex).y;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                #if defined(USE_MAINTEX)
                fixed4 textureColor = tex2D(_MainTex, input.uv);
                #else
                fixed4 textureColor = 1.0;
                #endif
                fixed4 color = textureColor * input.color * _MainColor * _Color;
                color.rgb *= max(_ColorIntensity, 0.0);
                color.a *= lerp(1.0, saturate(textureColor.a * _ColorIntensity), saturate(_IntensityAffectAlpha));
                color.a *= 1.0 - saturate(_EnableExternalAlpha) * (1.0 - textureColor.a);
                if (_WorldClipHeight > -999999.0) clip(_WorldClipHeight - input.worldY);
                return color;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
