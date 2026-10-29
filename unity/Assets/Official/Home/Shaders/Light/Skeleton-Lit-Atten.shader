Shader "Universal Render Pipeline/2D/Spine/Skeleton-Lit-Atten" {
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
		_LightAtten ("Light Atten", Range(0, 1)) = 0
		[MaterialToggle] _ZWrite ("ZWrite", Float) = 0
		[Toggle(_FORDING)] _FordingEnable ("Fording", Float) = 0
		_FordingScale ("Fording Scale", Range(0, 5)) = 1
		_FordingOffset ("Fording Offset", Float) = 0
		_ClipAlpha ("Clip Alpha", Range(0, 1)) = 0.01
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
}