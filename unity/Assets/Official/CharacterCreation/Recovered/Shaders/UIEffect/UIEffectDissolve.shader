Shader "UI/EffectDissolve" {
	Properties {
		_BaseColor ("基础颜色", Vector) = (1,1,1,1)
		_MainTex ("基础贴图", 2D) = "white" {}
		_DissolveTex ("溶解贴图", 2D) = "white" {}
		_DissolveMultX ("对X轴定向溶解", Range(-1, 1)) = 0
		_DissolveMultY ("对Y轴定向溶解", Range(-1, 1)) = 0
		[HideInInspector] _DissolveRotator ("溶解旋转", Vector) = (0,0,0,0)
		_DissolvePanner ("溶解贴图流动XY", Vector) = (0,0,0,0)
		_DisSoftness ("溶解虚边", Range(0.1, 2)) = 0.1
		_AnDissolve ("溶解强度(用于k帧)", Float) = 0
		_EdgeWidth ("边缘宽度 _EdgeWidth", Range(0, 1)) = 0.5
		_EdgeColor1 ("边缘颜色1 _EdgeColor1", Vector) = (1,1,1,1)
		_EdgeColor1Intensity ("边缘颜色1强度 _EdgeColor1Intensity", Float) = 1
		_EdgeColor2 ("边缘颜色2 _EdgeColor2", Vector) = (1,1,1,1)
		_EdgeColor2Intensity ("边缘颜色2强度 _EdgeColor2Intensity", Float) = 1
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