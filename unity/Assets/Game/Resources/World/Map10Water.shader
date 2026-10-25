Shader "Zjcs/World/Map10Water2"
{
    Properties
    {
        _WaterMask ("Official Shore Mask", 2D) = "black" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _CausticsTex ("Caustics", 2D) = "white" {}
        _FoamTex ("Foam", 2D) = "white" {}
        _NoiseTex ("Intersection Noise", 2D) = "gray" {}
        _MaskRect ("Mask World Rect", Vector) = (-24,-29,50,53)
        _BaseColor ("Base Color", Color) = (0.259,0.557,0.643,1)
        _ShallowColor ("Shallow Color", Color) = (0.576,0.988,0.992,1)
        _EdgeColor ("Edge Color", Color) = (0.816,0.995,1,1)
        _Color ("Color", Color) = (1,1,1,1)
        _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _Flip ("UV Flip", Vector) = (1,1,1,1)
        _CausticsBrightness ("Caustics Brightness", Float) = 6.3
        _CausticsDistortion ("Caustics Distortion", Float) = 0.15
        _CausticsSpeed ("Caustics Speed", Float) = 0.04
        _CausticsThreshold ("Caustics Threshold", Float) = 0.012
        _CausticsTiling ("Caustics Tiling", Float) = 0.46
        _Depth ("Depth", Float) = 0.5
        _EdgeCutoff ("Edge Cutoff", Float) = 0.338
        _EdgeStr ("Edge Strength", Float) = 6.01
        _EdgeThreshold ("Edge Threshold", Float) = 0.466
        _EnableExternalAlpha ("External Alpha", Float) = 0
        _Expand ("Intersection Expand", Float) = 0
        _FoamCutoff ("Foam Cutoff", Float) = 0.919
        _FoamSpeed ("Foam Speed", Float) = -0.01
        _FoamThreshold ("Foam Threshold", Float) = 0.407
        _FoamTiling ("Foam Tiling", Float) = -0.15
        _IntersectionFalloff ("Intersection Falloff", Float) = 0.636
        _IntersectionRippleDist ("Intersection Ripple Distance", Float) = 1
        _IntersectionRippleStrength ("Intersection Ripple Strength", Float) = 0.519
        _IsPond ("Pond", Float) = 0
        _NoiseFrequency ("Noise Frequency", Float) = 0.06
        _NoiseSpeed ("Noise Speed", Float) = 0.1
        _NoiseTiling ("Noise Tiling", Float) = 0.22
        _NormalSpeed ("Normal Speed", Float) = 0.03
        _NormalStrength ("Normal Strength", Float) = 0.09
        _NormalTiling ("Normal Tiling", Float) = -0.06
        _Test ("Test", Float) = 0.47
        _Test2 ("Test 2", Float) = 1
        _Wave ("Wave", Float) = -0.003
        _WaveFrequency ("Wave Frequency", Float) = 2
        _WaveStrength ("Wave Strength", Float) = 0.003
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _WaterMask;
            sampler2D _BumpMap;
            sampler2D _CausticsTex;
            sampler2D _FoamTex;
            sampler2D _NoiseTex;
            float4 _BumpMap_ST;
            float4 _CausticsTex_ST;
            float4 _FoamTex_ST;
            float4 _NoiseTex_ST;
            float4 _MaskRect;
            float4 _BaseColor;
            float4 _ShallowColor;
            float4 _EdgeColor;
            float4 _Color;
            float4 _RendererColor;
            float4 _FoamColor;
            float4 _Flip;
            float _CausticsBrightness;
            float _CausticsDistortion;
            float _CausticsSpeed;
            float _CausticsThreshold;
            float _CausticsTiling;
            float _Depth;
            float _EdgeCutoff;
            float _EdgeStr;
            float _EdgeThreshold;
            float _EnableExternalAlpha;
            float _Expand;
            float _FoamCutoff;
            float _FoamSpeed;
            float _FoamThreshold;
            float _FoamTiling;
            float _IntersectionFalloff;
            float _IntersectionRippleDist;
            float _IntersectionRippleStrength;
            float _IsPond;
            float _NoiseFrequency;
            float _NoiseSpeed;
            float _NoiseTiling;
            float _NormalSpeed;
            float _NormalStrength;
            float _NormalTiling;
            float _Test;
            float _Test2;
            float _Wave;
            float _WaveFrequency;
            float _WaveStrength;

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
                float2 worldXY : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float4 position = input.vertex;
                float wave = sin((position.x + position.y) * _WaveFrequency + _Time.y) * _WaveStrength + _Wave;
                position.z += wave;
                output.vertex = UnityObjectToClipPos(position);
                output.worldXY = mul(unity_ObjectToWorld, position).xy;
                output.uv = input.uv * _Flip.xy;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 waterUv = input.uv;
                float2 normalUvA = TRANSFORM_TEX((waterUv * _NormalTiling + float2(_Time.y * _NormalSpeed, 0.0)), _BumpMap);
                float2 normalUvB = TRANSFORM_TEX((waterUv.yx * (_NormalTiling * 1.37)
                    - float2(0.0, _Time.y * _NormalSpeed * 0.73)), _BumpMap);
                float3 normalA = UnpackNormal(tex2D(_BumpMap, normalUvA));
                float3 normalB = UnpackNormal(tex2D(_BumpMap, normalUvB));
                float2 normal = (normalA.xy + normalB.xy) * _NormalStrength;

                float noise = tex2D(_NoiseTex, TRANSFORM_TEX((waterUv * _NoiseTiling + _Time.y * _NoiseSpeed), _NoiseTex)).r;
                float2 causticsUv = TRANSFORM_TEX((waterUv * _CausticsTiling + _Time.y * _CausticsSpeed),
                    _CausticsTex)
                    + normal * _CausticsDistortion + (noise - 0.5) * _NoiseFrequency;
                float caustics = max(tex2D(_CausticsTex, causticsUv).r - _CausticsThreshold, 0.0);
                float foamTexture = tex2D(_FoamTex,
                    TRANSFORM_TEX((waterUv * _FoamTiling + _Time.y * _FoamSpeed), _FoamTex)).r;

                float2 maskUv = (input.worldXY - _MaskRect.xy) / max(_MaskRect.zw, float2(0.001, 0.001));
                float shoreMask = tex2D(_WaterMask, maskUv).r;
                float edge = saturate((shoreMask + _Expand - _EdgeCutoff) * _EdgeStr);
                float edgeDetail = saturate((shoreMask - _EdgeThreshold) * _EdgeStr);
                float foamSignal = foamTexture + edgeDetail * _IntersectionRippleStrength
                    + (noise - 0.5) * _IntersectionFalloff;
                float foam = smoothstep(_FoamThreshold, max(_FoamCutoff, _FoamThreshold + 0.001), foamSignal)
                    * saturate(edge * max(_IntersectionRippleDist, 0.0));

                float shallow = saturate(_Depth + noise * _Test * _Test2 + length(normal) * 0.2 + _IsPond * 0.1);
                float4 color = lerp(_BaseColor, _ShallowColor, shallow);
                color.rgb += caustics * _CausticsBrightness * 0.08;
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge * 0.18);
                color = lerp(color, _FoamColor, saturate(foam * _FoamColor.a));
                color *= _Color * _RendererColor * input.color;
                color.a *= lerp(1.0, tex2D(_WaterMask, maskUv).a, saturate(_EnableExternalAlpha));
                return color;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
