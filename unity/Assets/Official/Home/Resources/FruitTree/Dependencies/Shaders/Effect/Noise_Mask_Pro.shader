Shader "Effect/Noise_Mask_Pro" {
	Properties {
		[Header(Main)] [HDR] _MainColor ("MainColor", Vector) = (1,1,1,1)
		_ColorIntensity ("ColorIntensity", Range(0, 20)) = 1
		[Toggle] _IntensityAffectAlpha ("Intensity Affect Alpha", Float) = 0
		[Header(MainTex)] _MainTex ("MainTex", 2D) = "white" {}
		_MainTex_MoveCenter ("MainTex_Move&Center", Vector) = (0,0,0,0)
		[Enum(Rotate_Maintex,0,Move_Maintex,1)] _Maintex_Rotator ("Maintex_Rotator", Float) = 0
		_Maintex_Rota ("Maintex_Rota", Float) = 0
		[Toggle] _UseCustomData ("Use CustomData", Float) = 0
		[Toggle] _ClampMainUV ("Clamp MainUV", Float) = 0
		[Header(NoiseTex)] _NoiseTex ("NoiseTex", 2D) = "white" {}
		_NoiseTex_MoveCenter ("NoiseTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[Enum(Rotate_Noise,0,Move_Noise,1)] _Noise_Rotator ("Noise_Rotator", Float) = 0
		_Noise_Rota ("Noise_Rota", Float) = 0
		[Header(MaskTex)] _MaskTex ("MaskTex", 2D) = "white" {}
		[Toggle] _MaskRA ("MaskRA", Float) = 0
		_MaskTex_MoveCenter ("MaskTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[Enum(Rotate_Mask,0,Move_Mask,1)] _Mask_Rotator ("Mask_Rotator", Float) = 0
		_Mask_Rota ("Mask_Rota", Float) = 0
		_MaksPower ("MaksPower", Float) = 1
		[Header(DistortionTex)] _DistortionTex ("DistortionTex", 2D) = "white" {}
		_Distortion ("Distortion", Range(0, 1)) = 0
		_DistortionTex_MoveCenter ("DistortionTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[Enum(Rotator_DistortionTex,0,Move_DistortionTex,1)] _DistortionTex_Rotator ("DistortionTex_Rotator", Float) = 0
		_DistortionTex_Rota ("DistortionTex_Rota", Float) = 0
		[Header(Dissolve)] [Toggle] _UseDissolve2 ("Use Dissolve2", Float) = 0
		_DissolveTex ("DissolveTex", 2D) = "white" {}
		_DissolveProgress ("DissolveProgress", Range(0, 1)) = 0
		[HDR] _DissolveColor ("DissolveColor", Vector) = (0.5,0.5,0.5,0.5)
		_DissolveRange ("DissolveRange", Range(0, 1)) = 0.5
		_DissolveControl ("_DissolveControl", Range(0, 1)) = 0.5
		[Toggle] _DissolveAlphaControl ("_DissolveAlphaControl", Float) = 0
		[Toggle] _CenterDissolve ("中心溶解", Float) = 0
		_DissolveMoveSmooth ("Dissolve Move Smooth", Float) = 1
		[Toggle] _DissolveFlip ("Dissolve Flip", Float) = 1
		_DissolveAxisDir ("Dissolve Axis Dir", Vector) = (0,0,0,0)
		_DissolveTex_MoveCenter ("DissolveTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[Enum(Rotator_DissolveTex,0,Move_DissolveTex,1)] _DissolveTex_Rotator ("DistortionTex_Rotator", Float) = 0
		_DissolveTex_Rota ("DistortionTex_Rota", Float) = 0
		[Toggle] _UseSoftDissolve ("Use Soft Dissolve", Float) = 0
		_DissolveSoftness ("Dissolve Softness", Range(0.001, 0.5)) = 0.05
		_PixelSize ("Pixel Size", Float) = 0
		_BloomFactor ("Bloom Factor", Float) = 0
		[Header(2D Wind Deform)] [Toggle(USE_WIND2D)] _UseWind2D ("Use 2D Wind", Float) = 0
		_WindAnchorStart ("Wind Anchor Start", Range(0, 1)) = 0.05
		_WindAnchorFalloff ("Wind Anchor Falloff", Range(0.1, 2)) = 1
		_WindDirection ("Wind Direction 2D", Vector) = (-1,0,0,0)
		_WindAmplitude ("Wind Amplitude", Range(0, 0.5)) = 0.05
		_WindSpeed ("Wind Speed", Range(0, 5)) = 2
		_WindWaveFrequency ("Wind Wave Frequency", Range(0, 4)) = 3
		_WindFlutterAmplitude ("Wind Flutter Amplitude", Range(0, 0.2)) = 0.05
		[Space] [Header(Rendering)] [Enum(Cull Off,0, Cull Front,1, Cull Back,2)] _CullMode ("Culling", Float) = 0
		[KeywordEnum(Additive,Blend,Opaque,Cutout,Transparent,Subtractive,Modulate,BlendZ)] _BlendMode ("Blend Mode", Float) = 1
		_SrcBlend ("SrcBlend", Float) = 5
		_DstBlend ("DstBlend", Float) = 10
		[HideInInspector] _BlendOp ("__blendop", Float) = 0
		_ZWrite ("ZWrite On", Float) = 1
		[Header(Stencil)] [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 14
		_ZTest ("ZTest", Float) = 4
		_WorldClipHeight ("WorldClipHeight", Float) = -1000000
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

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/InternalErrorShader"
	//CustomEditor "_NoiseMaskGUI"
}