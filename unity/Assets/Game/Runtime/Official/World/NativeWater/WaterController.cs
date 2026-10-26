// Migrated from selected official CustomWater.dll; see Map13 source receipt.
namespace Zjcs.Official.World.NativeWater
{
using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class WaterController : MonoBehaviour
{
	private struct WaterParam : IEquatable<WaterParam>
	{
		public int Id;

		public float WaterLevel;

		public bool FordingEnable;

		public float FordingLevel;

		public Color EdgeColor;

		public Color DeepColor;

		public Color ShallowColor;

		public Texture2D FordingTex;

		public Vector4 FordingParam;

		public bool Equals(WaterParam other)
		{
			return Id == other.Id;
		}
	}

	private static List<WaterParam> WaterParams = new List<WaterParam>();

	[Range(0f, 1f)]
	public float WaterLevel = 0.5f;

	private static readonly int mWaterLevelId = Shader.PropertyToID("_WaterLevel");

	private static readonly int mFordingLevelId = Shader.PropertyToID("_FordingLevel");

	private static readonly int mDeepColorId = Shader.PropertyToID("_DeepColor");

	private static readonly int mShallowColorId = Shader.PropertyToID("_ShallowColor");

	private static readonly int mFordingTexId = Shader.PropertyToID("_FordingTex");

	private static readonly int mFordingParamId = Shader.PropertyToID("_FordingParam");

	private static readonly int mEdgeColorId = Shader.PropertyToID("_EdgeColor");

	public bool FordingEnable;

	[Range(0.01f, 2f)]
	public float FordingLevel = 0.2f;

	public Texture2D FordingTex;

	[Header("贴图平铺缩放")]
	public float TexScale = 0.1f;

	[Header("贴图移速")]
	public float TexSpeed = 0.1f;

	[Header("接触面波动幅度")]
	public float WaveStrength = 0.1f;

	[Header("接触面厚度")]
	public float WaveThick = 0.1f;

	public Color DeepColor = new Color(0.2588235f, 142f / 255f, 164f / 255f, 1f);

	public Color ShallowColor = new Color(147f / 255f, 252f / 255f, 0.9921568f, 1f);

	[Header("边缘颜色")]
	public Color EdgeColor = Color.white;

	private void OnEnable()
	{
		int instanceID = GetInstanceID();
		WaterParam item = new WaterParam
		{
			Id = instanceID,
			WaterLevel = WaterLevel,
			FordingEnable = FordingEnable,
			FordingLevel = FordingLevel,
			DeepColor = DeepColor,
			ShallowColor = ShallowColor,
			EdgeColor = EdgeColor,
			FordingTex = FordingTex,
			FordingParam = new Vector4(TexScale, TexSpeed, WaveStrength, WaveThick)
		};
		WaterParams.Add(item);
		SetGlobal();
	}

	private void OnDisable()
	{
		int instanceID = GetInstanceID();
		for (int i = 0; i < WaterParams.Count; i++)
		{
			if (WaterParams[i].Id == instanceID)
			{
				WaterParams.RemoveAt(i);
				break;
			}
		}
		SetGlobal();
	}

	private void SetGlobal()
	{
		if (WaterParams.Count > 0)
		{
			WaterParam waterParam = WaterParams[WaterParams.Count - 1];
			Shader.SetGlobalFloat(mWaterLevelId, waterParam.WaterLevel);
			if (waterParam.FordingEnable)
			{
				Shader.SetGlobalFloat(mFordingLevelId, waterParam.FordingLevel);
				Shader.SetGlobalColor(mDeepColorId, waterParam.DeepColor);
				Shader.SetGlobalColor(mShallowColorId, waterParam.ShallowColor);
				Shader.SetGlobalColor(mEdgeColorId, waterParam.EdgeColor);
				Shader.SetGlobalTexture(mFordingTexId, waterParam.FordingTex);
				Shader.SetGlobalVector(mFordingParamId, waterParam.FordingParam);
			}
		}
	}
}

}
