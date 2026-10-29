Shader "Hidden/JSZX/TextMeshPro" {
	Properties {
		[HideInInspector] _FaceColor ("Face Color", Vector) = (1,1,1,1)
		[HideInInspector] _FaceDilate ("Face Dilate", Range(-1, 1)) = 0
		[HideInInspector] _OutlineColor ("Outline Color", Vector) = (0,0,0,1)
		[HideInInspector] _OutlineWidth ("Outline Thickness", Range(0, 1)) = 0
		[HideInInspector] _OutlineSoftness ("Outline Softness", Range(0, 1)) = 0
		[HideInInspector] _OutlineColor2 ("Outline Color2", Vector) = (0,0,0,1)
		[HideInInspector] _OutlineWidth2 ("Outline Thickness2", Range(0, 1)) = 0
		[HideInInspector] _UnderlayColor ("Border Color", Vector) = (0,0,0,0.5)
		[HideInInspector] _UnderlayOffsetX ("Border OffsetX", Range(-1, 1)) = 0
		[HideInInspector] _UnderlayOffsetY ("Border OffsetY", Range(-1, 1)) = 0
		[HideInInspector] _UnderlayDilate ("Border Dilate", Range(-1, 1)) = 0
		[HideInInspector] _UnderlaySoftness ("Border Softness", Range(0, 1)) = 0
		[HideInInspector] _WeightNormal ("Weight Normal", Float) = 0
		[HideInInspector] _WeightBold ("Weight Bold", Float) = 0.5
		[HideInInspector] _ShaderFlags ("Flags", Float) = 0
		[HideInInspector] _ScaleRatioA ("Scale RatioA", Float) = 1
		[HideInInspector] _ScaleRatioB ("Scale RatioB", Float) = 1
		[HideInInspector] _ScaleRatioC ("Scale RatioC", Float) = 1
		[HideInInspector] _MainTex ("Font Atlas", 2D) = "white" {}
		[HideInInspector] _TextureWidth ("Texture Width", Float) = 512
		[HideInInspector] _TextureHeight ("Texture Height", Float) = 512
		[HideInInspector] _GradientScale ("Gradient Scale", Float) = 5
		[HideInInspector] _ScaleX ("Scale X", Float) = 1
		[HideInInspector] _ScaleY ("Scale Y", Float) = 1
		[HideInInspector] _PerspectiveFilter ("Perspective Correction", Range(0, 1)) = 0.875
		[HideInInspector] _Sharpness ("Sharpness", Range(-1, 1)) = 0
		[HideInInspector] _VertexOffsetX ("Vertex OffsetX", Float) = 0
		[HideInInspector] _VertexOffsetY ("Vertex OffsetY", Float) = 0
		[HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
		[HideInInspector] _MaskSoftnessX ("Mask SoftnessX", Float) = 0
		[HideInInspector] _MaskSoftnessY ("Mask SoftnessY", Float) = 0
		[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
		[HideInInspector] _Stencil ("Stencil ID", Float) = 0
		[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
		[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
		[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
		[HideInInspector] _CullMode ("Cull Mode", Float) = 0
		[HideInInspector] _ColorMask ("Color Mask", Float) = 15
		[HideInInspector] _GlowColor ("Glow Color", Vector) = (0,1,0,0.5)
		[HideInInspector] _GlowOffset ("Glow Offset", Range(-1, 1)) = 0
		[HideInInspector] _GlowInner ("Glow Inner", Range(0, 1)) = 0.05
		[HideInInspector] _GlowOuter ("Glow Outer", Range(0, 1)) = 0.05
		[HideInInspector] _GlowPower ("Glow Power", Range(0, 1)) = 0.75
		[HideInInspector] _SrcAlphaMode ("__src_alpha", Float) = 1
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
}