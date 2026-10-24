Shader "Zjcs/Opening/NoiseMaskPro"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _ColorIntensity ("Color Intensity", Float) = 1
        _IntensityAffectAlpha ("Intensity Affects Alpha", Float) = 0
        _UseCustomData ("Use Custom Data", Float) = 0
        _UseMainTex ("Use Main Texture", Float) = 1
        _UseNoise ("Use Noise", Float) = 0
        _UseMask ("Use Mask", Float) = 0
        _UseDistortion ("Use Distortion", Float) = 0
        _UseDissolve ("Use Dissolve", Float) = 0
        _EnableDissolve2 ("Enable Dissolve 2", Float) = 0
        _UsePixel ("Use Pixel", Float) = 0
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskRA ("Mask Channel", Float) = 0
        _MaksPower ("Mask Power", Float) = 1
        _Mask_Rota ("Mask Rotation", Float) = 0
        _Mask_Rotator ("Mask Rotation Speed", Float) = 0
        _MaskTex_MoveCenter ("Mask Center", Vector) = (0,0,0.5,0.5)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _UseSoftDissolve ("Use Soft Dissolve", Float) = 0
        _Noise_Rota ("Noise Rotation", Float) = 0
        _Noise_Rotator ("Noise Speed", Float) = 0
        _NoiseTex_MoveCenter ("Noise Center", Vector) = (0,0,0.5,0.5)
        _DistortionTex ("Distortion Texture", 2D) = "gray" {}
        _Distortion ("Distortion", Float) = 0
        _DistortionTex_Rota ("Distortion Rotation", Float) = 0
        _DistortionTex_Rotator ("Distortion Speed", Float) = 0
        _DistortionTex_MoveCenter ("Distortion Center", Vector) = (0,0,0.5,0.5)
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        _DissolveProgress ("Dissolve Progress", Range(0,1)) = 0
        _DissolveRange ("Dissolve Range", Float) = 0.5
        _DissolveSoftness ("Dissolve Softness", Float) = 0.05
        _DissolveControl ("Dissolve Control", Float) = 0.5
        _DissolveFlip ("Dissolve Flip", Float) = 0
        _DissolveMoveSmooth ("Dissolve Smooth", Float) = 1
        _DissolveAlphaControl ("Dissolve Alpha", Float) = 0
        _DissolveColor ("Dissolve Edge Color", Color) = (1,1,1,1)
        _DissolveAxisDir ("Dissolve Axis", Vector) = (0,0,0,0)
        _DissolveTex_MoveCenter ("Dissolve Center", Vector) = (0,0,0.5,0.5)
        _DissolveTex_Rota ("Dissolve Rotation", Float) = 0
        _DissolveTex_Rotator ("Dissolve Speed", Float) = 0
        _CenterDissolve ("Center Dissolve", Float) = 0
        _UseDissolve2 ("Use Dissolve 2", Float) = 0
        _Maintex_Rota ("Main Rotation", Float) = 0
        _Maintex_Rotator ("Main Rotation Speed", Float) = 0
        _MainTex_MoveCenter ("Main Center", Vector) = (0,0,0,0)
        _ClampMainUV ("Clamp Main UV", Float) = 0
        _PixelSize ("Pixel Size", Float) = 0
        _BloomFactor ("Bloom Factor", Float) = 0
        _UseWind2D ("Use Wind", Float) = 0
        _WindDirection ("Wind Direction", Vector) = (-1,0,0,0)
        _WindAmplitude ("Wind Amplitude", Float) = 0
        _WindFlutterAmplitude ("Wind Flutter", Float) = 0
        _WindSpeed ("Wind Speed", Float) = 0
        _WindWaveFrequency ("Wind Frequency", Float) = 1
        _WindAnchorStart ("Wind Anchor Start", Float) = 0
        _WindAnchorFalloff ("Wind Anchor Falloff", Float) = 1
        _WorldClipHeight ("World Clip Height", Float) = -1000000
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _SrcBlend ("Source Blend", Float) = 5
        _DstBlend ("Destination Blend", Float) = 1
        _BlendOp ("Blend Operation", Float) = 0
        _BlendMode ("Blend Mode", Float) = 1
        _CullMode ("Cull", Float) = 0
        _ZWrite ("Z Write", Float) = 0
        _ZTest ("Z Test", Float) = 4
        _ColorMask ("Color Mask", Float) = 15
        _Stencil ("Stencil ID", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _HitEffectColor ("Hit Effect Color", Color) = (0,0,0,1)
        _HitEffectBlend ("Hit Effect Blend", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend]
        Cull [_CullMode]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        ColorMask [_ColorMask]
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _MaskTex, _NoiseTex, _DistortionTex, _DissolveTex;
            float4 _MainTex_ST, _MaskTex_ST, _NoiseTex_ST, _DistortionTex_ST, _DissolveTex_ST;
            float4 _MainColor, _DissolveColor, _DissolveAxisDir;
            float4 _MainTex_MoveCenter, _MaskTex_MoveCenter, _NoiseTex_MoveCenter;
            float4 _DistortionTex_MoveCenter, _DissolveTex_MoveCenter;
            float4 _WindDirection, _ClipRect;
            float _ColorIntensity, _IntensityAffectAlpha, _UseCustomData;
            float _MaskRA, _MaksPower, _Mask_Rota, _Mask_Rotator, _Noise_Rota, _Noise_Rotator;
            float _Distortion, _DistortionTex_Rota, _DistortionTex_Rotator;
            float _DissolveProgress, _DissolveRange, _DissolveSoftness, _DissolveControl;
            float _DissolveAlphaControl, _DissolveTex_Rota, _DissolveTex_Rotator;
            float _CenterDissolve, _UseDissolve2, _DissolveFlip, _DissolveMoveSmooth;
            float _Maintex_Rota, _Maintex_Rotator, _UseSoftDissolve, _ClampMainUV, _PixelSize;
            float _UseWind2D, _WindAmplitude, _WindFlutterAmplitude, _WindSpeed, _WindWaveFrequency;
            float _WindAnchorStart, _WindAnchorFalloff, _WorldClipHeight, _HitEffectBlend;
            float _UseMainTex, _UseMask, _UseNoise, _UseDistortion, _UseDissolve, _EnableDissolve2, _UsePixel;
            float4 _HitEffectColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 customData : TEXCOORD1;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 mainUv : TEXCOORD0;
                float2 noiseUv : TEXCOORD1;
                float2 maskUv : TEXCOORD2;
                float2 distortionUv : TEXCOORD3;
                float2 worldPos : TEXCOORD5;
                float2 sourceUv : TEXCOORD6;
                float4 color : COLOR;
            };
            float2 rotateUv(float2 uv, float2 center, float radians)
            {
                float sine = sin(radians), cosine = cos(radians);
                float2 p = uv - center;
                return float2(p.x * cosine - p.y * sine, p.x * sine + p.y * cosine) + center;
            }
            float2 animatedUv(float2 uv, float4 tex_ST, float4 moveCenter, float rota, float rotator)
            {
                uv = uv * tex_ST.xy + tex_ST.zw - moveCenter.zw;
                float angle = frac(_Time.y * rota) * 6.283 - rota;
                angle = (angle * rotator + rota) * 6.28318548;
                uv = rotateUv(uv, float2(0.0, 0.0), -angle);
                return uv + moveCenter.zw + moveCenter.xy * _Time.y;
            }
            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                if (_UseWind2D > 0.5)
                {
                    float anchor = saturate((v.uv.y - _WindAnchorStart) * max(0.001, _WindAnchorFalloff));
                    float wave = sin(_Time.y * _WindSpeed + v.vertex.y * _WindWaveFrequency);
                    world.xy += _WindDirection.xy * anchor * (_WindAmplitude * wave + _WindFlutterAmplitude * sin(_Time.y * _WindSpeed * 1.7 + v.vertex.x));
                }
                o.vertex = mul(UNITY_MATRIX_VP, world);
                float2 mainUv = v.uv + (_UseCustomData > 0.5 ? v.customData : 0.0);
                o.mainUv = animatedUv(mainUv, _MainTex_ST, _MainTex_MoveCenter,
                    _Maintex_Rota, _Maintex_Rotator);
                o.noiseUv = animatedUv(v.uv, _NoiseTex_ST, _NoiseTex_MoveCenter,
                    _Noise_Rota, _Noise_Rotator);
                o.maskUv = animatedUv(v.uv, _MaskTex_ST, _MaskTex_MoveCenter,
                    _Mask_Rota, _Mask_Rotator);
                o.distortionUv = animatedUv(v.uv, _DistortionTex_ST, _DistortionTex_MoveCenter,
                    _DistortionTex_Rota, _DistortionTex_Rotator);
                o.sourceUv = v.uv;
                o.worldPos = world.xy;
                o.color = v.color * _MainColor;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 mainUv = i.mainUv;
                float2 noiseUv = i.noiseUv;
                float2 dissolveUv = i.sourceUv;
                if (_UseDistortion > 0.5)
                {
                    float2 distortion = tex2D(_DistortionTex, i.distortionUv).xy * _Distortion;
                    mainUv += distortion;
                    noiseUv += distortion;
                    dissolveUv += distortion;
                }
                if (_UsePixel > 0.5 && _PixelSize > 0.000001)
                    mainUv = floor(mainUv * _PixelSize) / _PixelSize;
                if (_ClampMainUV > 0.5) mainUv = saturate(mainUv);

                float4 baseColor = _UseMainTex > 0.5 ? tex2D(_MainTex, mainUv) : 1.0;
                if (_UseNoise > 0.5) baseColor *= tex2D(_NoiseTex, noiseUv);
                baseColor *= i.color;

                float intensity = _ColorIntensity;
                float intensityAlpha = 1.0 + _IntensityAffectAlpha * (intensity - 1.0);
                float outputAlpha = baseColor.a * intensityAlpha;
                float3 outputRgb = baseColor.rgb * intensity;

                if (_UseDissolve > 0.5)
                {
                    dissolveUv = dissolveUv * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
                    float dissolve = tex2D(_DissolveTex, dissolveUv).r;
                    float threshold = _DissolveProgress - _DissolveControl;
                    threshold += _DissolveAlphaControl * (1.0 - outputAlpha) * (1.0 - threshold);
                    float delta = dissolve - threshold;
                    clip(delta);

                    float range = max(0.0001, _DissolveRange);
                    float softness = max(0.0001, _DissolveSoftness);
                    float edgeBase = 1.0 - smoothstep(0.0, range * 0.25, delta);
                    float edgeSoft = (1.0 - smoothstep(0.0, range, delta - softness)) * _DissolveColor.a;
                    float edgeMix = lerp(edgeBase, edgeSoft, saturate(_UseSoftDissolve));
                    float dissolveEdge = 1.0 + edgeMix * (_DissolveColor.a - 1.0);
                    float dissolveAlpha = lerp(dissolveEdge, smoothstep(0.0, softness, delta),
                        saturate(_UseSoftDissolve));
                    float edgeAlpha = lerp(outputAlpha, 1.0 - outputAlpha, saturate(_UseSoftDissolve));
                    float3 edgeColor = _DissolveColor.rgb * edgeAlpha;
                    outputRgb = lerp(outputRgb, edgeColor, dissolveEdge);
                    outputAlpha *= dissolveAlpha;
                }

                if (_UseMask > 0.5)
                {
                    float4 mask = tex2D(_MaskTex, i.maskUv);
                    float maskValue = lerp(mask.r, mask.a, _MaskRA) * _MaksPower;
                    outputRgb *= saturate(maskValue);
                    outputAlpha *= saturate(maskValue);
                }
                clip(i.worldPos.y - _WorldClipHeight);
                clip(_ClipRect.z - i.worldPos.x);
                clip(_ClipRect.w - i.worldPos.y);
                clip(i.worldPos.x - _ClipRect.x);
                clip(i.worldPos.y - _ClipRect.y);

                clip(outputAlpha - 0.001);
                float hit = saturate(_HitEffectBlend);
                outputRgb = lerp(outputRgb, _HitEffectColor.rgb, hit * _HitEffectColor.a);
                outputAlpha = max(outputAlpha, hit * _HitEffectColor.a * i.color.a);
                return float4(outputRgb, outputAlpha);
            }
            ENDCG
        }
    }
}
