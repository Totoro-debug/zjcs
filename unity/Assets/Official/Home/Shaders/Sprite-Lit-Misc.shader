Shader "Universal Render Pipeline/2D/Sprite-Lit-Misc" {
	Properties {
		_MainTex ("Diffuse", 2D) = "white" {}
		[HideInInspector] _Color ("Tint", Vector) = (1,1,1,1)
		[HideInInspector] _RendererColor ("RendererColor", Vector) = (1,1,1,1)
		[Space(20)] _LightAtten ("Light Atten", Range(0, 1)) = 0
		[Space(20)] [Toggle(_DISSOLVE)] _DissolveEnable ("Dissolve", Float) = 0
		[Toggle] _MapDissolve ("Map Dissolve", Float) = 0
		_MapDissolveScale ("Map Dissolve Scale", Float) = 1
		[NoScaleOffset] _DissolveTex ("DissolveTex", 2D) = "black" {}
		_DissolveThreshold ("Dissolve Threshold", Range(0, 1)) = 0
		_DissolveWidth ("Dissolve Width", Range(0, 0.2)) = 0.1
		[HDR] _DissolveColor ("Dissolve Color", Vector) = (0,0,1,1)
		_HitEffectColor ("Hit Effect Color", Vector) = (1,1,1,1)
		_HitEffectBlend ("Hit Effect Blend", Range(0, 1)) = 0
		[HideInInspector] _ZWrite ("Z Write", Float) = 0
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
	Fallback "Sprites/Default"
}