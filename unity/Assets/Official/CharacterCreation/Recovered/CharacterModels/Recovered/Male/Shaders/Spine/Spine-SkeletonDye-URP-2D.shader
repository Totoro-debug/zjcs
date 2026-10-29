Shader "Universal Render Pipeline/Spine/SkeletonDye-2D" {
	Properties {
		[NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
		[NoScaleOffset] _MaskTex ("Mask", 2D) = "white" {}
		[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0
		[MaterialToggle(_LIGHT_AFFECTS_ADDITIVE)] _LightAffectsAdditive ("Light Affects Additive", Float) = 0
		[MaterialToggle(_TINT_BLACK_ON)] _TintBlack ("Tint Black", Float) = 0
		_Color ("    Light Color", Vector) = (1,1,1,1)
		_Black ("    Dark Color", Vector) = (0,0,0,0)
		[HideInInspector] _StencilRef ("Stencil Reference", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare", Float) = 8
		[Enum(UnityEngine.Rendering.StencilOp)] [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
		[NoScaleOffset] _DyeTex ("Dye Texture", 2D) = "black" {}
		_DyeHsv1 ("Dye Hsv1", Vector) = (0,0,0,1)
		_DyeHsv2 ("Dye Hsv2", Vector) = (0,0,0,1)
		_DyeHsv3 ("Dye Hsv3", Vector) = (0,0,0,1)
		_HitEffectColor ("Hit Effect Color", Vector) = (1,1,1,1)
		_HitEffectBlend ("Hit Effect Blend", Range(0, 1)) = 0
		[NoScaleOffset] _DissolveTex ("DissolveTex", 2D) = "black" {}
		_DissolveThreshold ("Threshold", Range(0, 1)) = 0
		_DissolveLineWidth ("Line Width", Range(0, 0.2)) = 0.1
		[HDR] _DissolveColor ("Dissolve Color", Vector) = (0,0,1,1)
		_DissolveOffsetThreshold ("Vertex Offset Threshold", Range(0, 1)) = 0.5
		_DissolveOffset ("Vertex Offset", Vector) = (0,0,0,0)
		_ExtraStrength ("Extra Strength", Range(0, 1)) = 1
		_IceNoiseTex ("Ice Noise", 2D) = "white" {}
		_BaseStrength ("Base Strength", Range(0, 1)) = 0.5
		_BoundingBox ("Bounding Box", Vector) = (0,0,0,0)
		[NoScaleOffset] _IceTex ("Ice Texture", 2D) = "white" {}
		[Toggle] _Light ("Light", Float) = 1
		_LightAtten ("Light Atten", Float) = 0
		_WorldClipHeight ("WorldClipHeight", Float) = -1000000
		_DitherAlpha ("Dither Alpha", Range(0, 1)) = 1
		_Intensity ("Intensity", Float) = 1
		_ClipLineX ("ClipLineX", Vector) = (0,0,0,1)
		_ClipLineY ("ClipLineY", Vector) = (0,0,0,1)
		_ClipLineC ("ClipLineC", Vector) = (0,0,0,1)
		_ClipDiag ("ClipDiag", Vector) = (0,0,0,0)
		[Toggle] _EnableHairClip ("EnableHairClip", Float) = 0
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;
			float4 _Color;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy) * _Color;
			}

			ENDHLSL
		}
	}
	Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
	//CustomEditor "SkeletonDyeURP2DShaderGUI"
}