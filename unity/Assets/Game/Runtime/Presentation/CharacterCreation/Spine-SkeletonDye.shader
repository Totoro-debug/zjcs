Shader "Zjcs/CharacterCreation/OfficialSpineDye" {
	Properties {
		_Cutoff ("Shadow alpha cutoff", Range(0,1)) = 0.1
		[NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
		[NoScaleOffset] _DyeTex ("Dye Mask", 2D) = "black" {}
		_DyeHsv1 ("Dye HSV 1", Vector) = (0,0,0,0)
		_DyeHsv2 ("Dye HSV 2", Vector) = (0,0,0,0)
		_DyeHsv3 ("Dye HSV 3", Vector) = (0,0,0,0)
		[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput("Straight Alpha Texture", Int) = 0
		[HideInInspector] _StencilRef("Stencil Reference", Float) = 1.0
		[HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 8
	}

	SubShader {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
		Cull Off
		ZWrite Off
		Blend One OneMinusSrcAlpha
		Lighting Off

		Stencil {
			Ref[_StencilRef]
			Comp[_StencilComp]
			Pass Keep
		}

		Pass {
			Name "Normal"
			CGPROGRAM
			#pragma shader_feature _ _STRAIGHT_ALPHA_INPUT
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "../../../../Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"

			sampler2D _MainTex;
			sampler2D _DyeTex;
			float4 _DyeHsv1;
			float4 _DyeHsv2;
			float4 _DyeHsv3;

			struct VertexInput {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 vertexColor : COLOR;
			};

			struct VertexOutput {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 vertexColor : COLOR;
			};

			VertexOutput vert (VertexInput v) {
				VertexOutput o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.vertexColor = PMAGammaToTargetSpace(v.vertexColor);
				return o;
			}

			float3 rgbToHsv(float3 color) {
				float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp(float4(color.bg, k.wz), float4(color.gb, k.xy), step(color.b, color.g));
				float4 q = lerp(float4(p.xyw, color.r), float4(color.r, p.yzx), step(p.x, color.r));
				float delta = q.x - min(q.w, q.y);
				float epsilon = 1.0e-10;
				return float3(abs(q.z + (q.w - q.y) / (6.0 * delta + epsilon)),
					delta / (q.x + epsilon), q.x);
			}

			float3 hsvToRgb(float3 hsv) {
				float3 p = abs(frac(hsv.xxx + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
				return hsv.z * lerp(float3(1.0, 1.0, 1.0), saturate(p - 1.0), hsv.y);
			}

			float4 frag (VertexOutput i) : SV_Target {
				float4 texColor = tex2D(_MainTex, i.uv);
				float3 dyeMask = tex2D(_DyeTex, i.uv).rgb;
				float3 hsvChange = step(0.1, dyeMask.r) * _DyeHsv1.xyz
					+ step(0.1, dyeMask.g) * _DyeHsv2.xyz
					+ step(0.1, dyeMask.b) * _DyeHsv3.xyz;
				float3 hsv = rgbToHsv(texColor.rgb);
				hsv.x = frac(abs(hsv.x) + hsvChange.x);
				hsv.y *= 1.0 + hsvChange.y;
				hsv.z *= 1.0 + hsvChange.z;
				texColor.rgb = hsvToRgb(hsv);

				#if defined(_STRAIGHT_ALPHA_INPUT)
				texColor.rgb *= texColor.a;
				#endif
				return texColor * i.vertexColor;
			}
			ENDCG
		}
	}
}
