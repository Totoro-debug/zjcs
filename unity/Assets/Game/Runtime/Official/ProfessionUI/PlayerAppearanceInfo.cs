namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerAppearanceInfo : MonoBehaviour
{
	protected bool mIsSelf;

	private bool mRegistered;

	private long mPlayerId;

	private ProfessionLiteInfoCallback mProfessionLiteInfoCallback;

	private Action mOnComplete;

    private IProfessionPlayerAppearanceBindings mPlayerBindings;
    protected IProfessionPlayerAppearanceBindings PlayerBindings => mPlayerBindings ?? ProfessionPlayerAppearanceBindings.Required;
    public void ConfigurePlayerAppearance(IProfessionPlayerAppearanceBindings bindings)
    { mPlayerBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	public bool Inited { get; private set; }

	public bool IsSelf => mIsSelf;

	public void Init(long playerId, bool forceUpdateInfo = false, Action onComplete = null)
	{
		SetPlayerId(playerId);
		ReleaseProfessionLiteInfoCallback();
		if (mIsSelf)
		{
			RegisterSelf();
			Refresh();
			Inited = true;
			onComplete?.Invoke();
		}
		else
		{
			mOnComplete = onComplete;
			PlayerBindings.GetLiteInfo(playerId, mProfessionLiteInfoCallback ?? (mProfessionLiteInfoCallback = new ProfessionLiteInfoCallback(InitByInfo)), forceUpdateInfo);
		}
	}

	public virtual void OnDestroy()
	{
		ReleaseProfessionLiteInfoCallback();
	}

	protected virtual void OnEnable()
	{
		if (Inited && mIsSelf)
		{
			RegisterSelf();
			Refresh();
		}
	}

	protected virtual void OnDisable()
	{
		if (mIsSelf)
		{
			UnregisterSelf();
		}
	}

	private void ReleaseProfessionLiteInfoCallback()
	{
		mProfessionLiteInfoCallback?.Release();
		mProfessionLiteInfoCallback = null;
	}

	private void RegisterSelf()
	{
		if (!mRegistered)
		{
			PlayerBindings.MainPlayer.OnAppearanceUpdate += MainPlayer_OnAppearanceUpdate;
			mRegistered = true;
		}
	}

	private void UnregisterSelf()
	{
		if (mRegistered && PlayerBindings.MainPlayer != null)
		{
			PlayerBindings.MainPlayer.OnAppearanceUpdate -= MainPlayer_OnAppearanceUpdate;
			mRegistered = false;
		}
	}

	private void MainPlayer_OnAppearanceUpdate(AppearanceOperateSource operateSource)
	{
		if (operateSource != AppearanceOperateSource.Map && operateSource != AppearanceOperateSource.ProfessionPromote)
		{
			Refresh();
		}
	}

	protected void SetPlayerId(long playerId)
	{
		mPlayerId = playerId;
		mIsSelf = playerId > 0 && PlayerBindings.MainPlayer != null && playerId == PlayerBindings.MainPlayer.Id;
	}

	private void Refresh()
	{
		IProfessionPlayerAppearanceSource mainPlayer = PlayerBindings.MainPlayer;
		if (!PlayerBindings.IsAppearanceValid(mainPlayer.AppearanceData))
		{
			return;
		}
		try
		{
			RefreshAppearance(mPlayerId, mainPlayer.Sex, mainPlayer.Profession, mainPlayer.AppearanceData);
		}
		catch (Exception exception)
		{
			string text = string.Empty;
            foreach (string skinName in PlayerBindings.GetSkinNames(mainPlayer.AppearanceData))
                text = text + skinName + "\n";
            Debug.LogException(exception);
		}
	}

	private void InitByInfo(ProfessionPlayerLiteAppearance info)
	{
		RefreshAppearance(mPlayerId, info.Sex, info.Profession, info.AppearanceData);
		try
		{
			mOnComplete?.Invoke();
		}
		catch (Exception exception)
		{
            Debug.LogException(exception);
		}
		mOnComplete = null;
		Inited = true;
	}

	public abstract void RefreshAppearance(long playerId, Sex sex, int profession, object data);

	public virtual void Clear()
	{
		OnDisable();
		mIsSelf = false;
		Inited = false;
	}
}

}
