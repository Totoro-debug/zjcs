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

	private readonly Dictionary<SkinType, int> mOverrideSkins = new Dictionary<SkinType, int>();

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
		mAppearanceData = data;
		if (mAppearanceRuntimeData == null)
		{
			mAppearanceRuntimeData = new AppearanceRuntimeData();
		}
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
		if (mDataOverride != runtimeDataOverride)
		{
			mDataOverride = runtimeDataOverride;
			if (mAppearanceRuntimeData != null)
			{
				UpdateAppearance();
			}
		}
	}

	private void UpdateAppearance()
	{
		mOverrideSkins.Clear();
		mDataOverride?.CollectOverrideSkins(mSex, mOverrideSkins);
		List<string> appendSkins = PlayerBindings.Config.GetAppendSkins(AppendSkinType);
		PlayerBindings.BuildAppearanceRuntimeData(mSex, mProfession, mAppearanceData, appendSkins, mAppearanceRuntimeData, mOverrideSkins);
		mDataOverride?.ApplyOverride(mAppearanceRuntimeData);
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
		mOverrideSkins.Clear();
		AppearanceCharacter.Release();
	}
}

}
