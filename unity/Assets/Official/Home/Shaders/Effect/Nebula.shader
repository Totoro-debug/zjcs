Shader "Effect/Nebula" {
	Properties {
		[Header(Textures)] _NoiseTex ("Noise Texture (RGBA)", 2D) = "white" {}
		_MaskTex ("Mask Texture (R)", 2D) = "white" {}
		[Header(Colors)] _CoreColor ("Core Color", Vector) = (1,0.9,0.5,1)
		_EdgeColor ("Edge Color", Vector) = (0.2,0.1,0.6,1)
		_BgColor ("Background Color", Vector) = (0.05,0.05,0.1,0)
		[Header(Settings)] _Scale ("Texture Scale", Float) = 1
		_WarpStrength ("Warp Strength", Range(0, 0.1)) = 0.1
		_Sparsity ("Sparsity (Island Effect)", Range(1, 10)) = 4
		_Speed ("Flow Speed", Float) = 0.2
		_Density ("Density", Range(0.01, 3)) = 1.5
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
}