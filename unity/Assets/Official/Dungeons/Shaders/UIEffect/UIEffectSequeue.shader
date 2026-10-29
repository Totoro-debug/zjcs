Shader "UI/EffectSequeue" {
	Properties {
		_Brightness ("Brightness", Float) = 1
		_Contrast ("Contrast", Float) = 1
		_MainColor ("Main Color", Vector) = (1,1,1,1)
		_MainTex ("Main Tex", 2D) = "white" {}
		_MainPanner ("Main Panner XY", Vector) = (0,0,0,0)
		[Header(SquenceMove)] _SquenceRow ("序列图行数", Float) = 1
		_SquenceColumn ("序列图列数", Float) = 1
		[Toggle(IsSqueueRecycle)] IsSqueueRecycle ("Is Squeue Recycle", Float) = 1
		_SquenceTimes ("序列图播放次数", Float) = 1
		_SquenceRate ("序列图播放进度", Range(0, 1)) = 0
		_SquenceSpeed ("序列图播放速度", Float) = 1
		[HideInInspector] _Rotator ("旋转角度", Vector) = (0,0,0,0)
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		[KeywordEnum(Add, Blend)] [HideInInspector] _BlendMode ("Blend Mode", Float) = 1
		[HideInInspector] _SrcBlend ("SrcBlend", Float) = 5
		[HideInInspector] _DstBlend ("DstBlend", Float) = 10
		[Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 15
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
	//CustomEditor "UIEffectShaderGUI"
}