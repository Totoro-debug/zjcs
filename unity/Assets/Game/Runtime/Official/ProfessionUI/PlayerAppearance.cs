namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAppearance : PlayerAppearanceInfo
{
	public AppearanceCharacter AppearanceCharacter;

	public AppendSkinType AppendSkinType;

	[SerializeField]
	private List<APlayerAppearanceInfo> mAppearanceInfoList;

	private AppearanceRuntimeData mAppearanceRuntimeData;

	private IAppearanceRuntimeDataOverride mDataOverride;

	private Dictionary<SkinType, string> mModifiedSkinDict;

	protected Sex mSex;

	protected int mProfession;

	private object mAppearanceData;

	public AppearanceRuntimeData AppearanceRuntimeData => mAppearanceRuntimeData;

	public event Action OnAppearanceRefreshed;

	public override void RefreshAppearance(long playerId, Sex sex, int profession, object data)
	{
		SetPlayerId(playerId);
		RefreshAppearanceImpl(sex, profession, data);
	}

	protected void RefreshAppearanceImpl(Sex sex, int profession, object data)
	{
		List<string> appendSkins = PlayerBindings.Config.GetAppendSkins(AppendSkinType);
		mAppearanceData = data;
		if (mAppearanceRuntimeData == null)
		{
			mAppearanceRuntimeData = new AppearanceRuntimeData();
		}
		PlayerBindings.BuildAppearanceRuntimeData(sex, profession, data, appendSkins, mAppearanceRuntimeData);
		mSex = sex;
		mProfession = profession;
		UpdateAppearance();
		try
		{
			OnAppearanceRefreshed?.Invoke();
		}
		catch (Exception exception)
		{
            Debug.LogException(exception);
		}
	}

	public void SetRuntimeDataOverride(IAppearanceRuntimeDataOverride runtimeDataOverride)
	{
		if (mDataOverride == runtimeDataOverride)
		{
			return;
		}
		mDataOverride = runtimeDataOverride;
		if (mAppearanceRuntimeData == null)
		{
			return;
		}
		if (mModifiedSkinDict != null)
		{
			Dictionary<SkinType, string> skinMap = mAppearanceRuntimeData.SkinMap;
			foreach (KeyValuePair<SkinType, string> item in mModifiedSkinDict)
			{
				skinMap[item.Key] = item.Value;
			}
		}
		UpdateAppearance();
	}

	private void UpdateAppearance()
	{
		if (mDataOverride != null)
		{
			if (mModifiedSkinDict == null)
			{
				mModifiedSkinDict = new Dictionary<SkinType, string>();
			}
			mDataOverride.ApplyOverride(mAppearanceRuntimeData, mAppearanceData, mModifiedSkinDict);
		}
		AppearanceCharacter.SetRuntimeData(mAppearanceRuntimeData);
		foreach (APlayerAppearanceInfo mAppearanceInfo in mAppearanceInfoList)
		{
			if ((Object)(object)mAppearanceInfo != (Object)null)
			{
				mAppearanceInfo.RefreshAppearance(mSex, mProfession, mAppearanceRuntimeData);
			}
		}
	}

	public override void Clear()
	{
		base.Clear();
		mAppearanceRuntimeData = null;
		mAppearanceData = null;
		mModifiedSkinDict?.Clear();
		AppearanceCharacter.Release();
	}
}

}
