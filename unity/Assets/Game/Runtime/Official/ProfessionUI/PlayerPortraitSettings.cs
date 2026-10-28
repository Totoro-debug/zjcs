namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPortraitSettings : ScriptableObject
{

	[Serializable]
	public class PhotographyParameters
	{
		public List<ProfessionPortraitKind> MyTypes;

		[Header("相片（RT）配置")]
		public int TextureWidth;

		public int TextureHeight;

		[Header("相机（Camera）配置")]
		public float Size;

		[SerializeField]
		private Vector3 mOffset;

		[SerializeField]
		private bool mCaptureParticle;

		[SerializeField]
		private int mDepthBuffer;

		[SerializeField]
		private MonoDictionary<SkinType, long> mAppearanceHashWeights;

		[Header("拍照时排除的部件")]
		[SerializeField]
		private List<SkinType> mExcludeSkinTypes = new List<SkinType>();

		public bool CaptureParticle => mCaptureParticle;

		public int DepthBuffer => mDepthBuffer;

		public List<SkinType> ExcludeSkinTypes => mExcludeSkinTypes;

		public bool HasExcludeConfig => mExcludeSkinTypes.Count > 0;

		public Vector3 GetOffset()
		{

			return mOffset + new Vector3(0f, 0f, -5f);
		}

		public string ToStringForLog()
		{
			string text = base.ToString();
			if (MyTypes != null)
			{
				foreach (ProfessionPortraitKind myType in MyTypes)
				{
					text += $" {myType}";
				}
			}
			return text;
		}

		public long GetHashWeightsBySkinType(SkinType skinType)
		{
			if (!mAppearanceHashWeights.TryGetValue(skinType, out var value))
			{
				return 0L;
			}
			return value;
		}
	}

	[SerializeField]
	private Sprite mDefaultPortrait;

	[SerializeField]
	private Sprite mLoadingPortrait;

	[SerializeField]
	private Sprite mDisablePortrait;

	private CharacterPortraitTexture2D mDefaultTexture;

	private CharacterPortraitTexture2D mLoadingTexture;

	private CharacterPortraitTexture2D mDisableTexture;

	[SerializeField]
	private List<PhotographyParameters> mPhotographyParameters;

	private Dictionary<ProfessionPortraitKind, PhotographyParameters> mSettingDict;

	public CharacterPortraitTexture2D DefaultTexture
	{
		get
		{
			if (mDefaultTexture == null)
			{
				mDefaultTexture = new CharacterPortraitTexture2D(mDefaultPortrait.texture, ((Texture)mDefaultPortrait.texture).width, ((Texture)mDefaultPortrait.texture).height, dontDestroy: true);
			}
			return mDefaultTexture;
		}
	}

	public CharacterPortraitTexture2D LoadingTexture
	{
		get
		{
			if (mLoadingTexture == null)
			{
				mLoadingTexture = new CharacterPortraitTexture2D(mLoadingPortrait.texture, ((Texture)mLoadingPortrait.texture).width, ((Texture)mLoadingPortrait.texture).height, dontDestroy: true);
			}
			return mLoadingTexture;
		}
	}

	public CharacterPortraitTexture2D DisableTexture
	{
		get
		{
			if (mDisableTexture == null)
			{
				mDisableTexture = new CharacterPortraitTexture2D(mDisablePortrait.texture, ((Texture)mDisablePortrait.texture).width, ((Texture)mDisablePortrait.texture).height, dontDestroy: true);
			}
			return mDisableTexture;
		}
	}

	public PhotographyParameters GetParameters(ProfessionPortraitKind type)
	{
		if (mSettingDict == null)
		{
			RefreshDict();
		}
		if (mSettingDict.TryGetValue(type, out var value))
		{
			return value;
		}
		Debug.LogError("请求错误照片配置，不存在的类型：" + type);
		return null;
	}

	private void RefreshDict()
	{
		mSettingDict = new Dictionary<ProfessionPortraitKind, PhotographyParameters>();
		foreach (PhotographyParameters mPhotographyParameter in mPhotographyParameters)
		{
			foreach (ProfessionPortraitKind myType in mPhotographyParameter.MyTypes)
			{
				mSettingDict.Add(myType, mPhotographyParameter);
			}
		}
	}
}

}
