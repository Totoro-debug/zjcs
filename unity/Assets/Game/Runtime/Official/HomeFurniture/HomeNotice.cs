// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class HomeNotice : UIBase
{
	[SerializeField]
	private WorldMultiLanguageText mRepairNoticeTxt;

	[SerializeField]
	private WorldStateSwitcher<bool> mShowStateSwitcher;

	[SerializeField]
	private string mCustomRepairNotice;

	private IHomeFurnitureCondition mNoticeCondition;

	private IHomeFurnitureCondition mUnlockCondition;

	private Action mMetNoticeConditionCallback;

	private Action mMetUnlockConditionCallback;

	private bool mIsShowNotice;

	private bool mIsUnlock;

	private IHomeFurnitureConditionNotice mConditionNotice;

	private IDisposable mConditionHandlerRegistration;
    public IHomeFurnitureServices Services { get; set; }

    public void Bind(WorldMultiLanguageText text, WorldStateSwitcher<bool> states,
        string customRepairNotice, IHomeFurnitureServices services)
    {
        mRepairNoticeTxt = text;
        mShowStateSwitcher = states;
        mCustomRepairNotice = customRepairNotice;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

	public bool ShowNotice => mIsShowNotice;

	public void Init(IHomeFurnitureCondition noticeCondition, IHomeFurnitureCondition unlockCondition, Action metNoticeConditionCallback, Action metUnlockConditionCallback)
	{
		InitWrapper(() =>
		{
			mConditionNotice = null;
			mNoticeCondition = noticeCondition;
			mUnlockCondition = unlockCondition;
			mMetNoticeConditionCallback = metNoticeConditionCallback;
			mMetUnlockConditionCallback = metUnlockConditionCallback;
			UpdateCondition();
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Player.OnDebugTimeOffset += OnTimeOffset;
		UpdateCondition();
		if (!mIsShowNotice)
		{
			mConditionHandlerRegistration = Services.SubscribeCondition(mNoticeCondition, OnConditionHandle);
		}
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		if (mConditionHandlerRegistration != null)
		{
			mConditionHandlerRegistration.Dispose();
			mConditionHandlerRegistration = null;
		}
		if (Services.Player != null)
		{
			Services.Player.OnDebugTimeOffset -= OnTimeOffset;
		}
	}

	private void Update()
	{
		if (mIsShowNotice && mConditionNotice != null && mConditionNotice.OnUpdate(Time.deltaTime))
		{
			OnConditionHandle();
		}
	}

	private void OnConditionHandle()
	{
		bool flag = mIsShowNotice;
		bool flag2 = mIsUnlock;
		UpdateCondition();
		if (!flag && mIsShowNotice)
		{
			mMetNoticeConditionCallback?.Invoke();
		}
		if (!flag2 && mIsUnlock)
		{
			mMetUnlockConditionCallback?.Invoke();
		}
	}

	private void OnTimeOffset(long _)
	{
		OnConditionHandle();
	}

	private void UpdateCondition()
	{
		mIsUnlock = Services.InMyHome && mUnlockCondition.IsMet();
		mIsShowNotice = Services.InMyHome && (mNoticeCondition == null || mNoticeCondition.IsMet()) && !mIsUnlock;
		mShowStateSwitcher.SetState(mIsShowNotice);
		if (mIsShowNotice)
		{
			if (mConditionNotice == null)
			{
				mConditionNotice = Services.CreateConditionNotice(mUnlockCondition);
			}
			if (mConditionNotice != null)
			{
				mConditionNotice.OnNoticeTxtUpdate = SetNoticeText;
				mConditionNotice.Refresh();
			}
		}
	}

	private void SetNoticeText(string key, object[] args)
	{
		if (!string.IsNullOrEmpty(mCustomRepairNotice))
		{
			mRepairNoticeTxt.SetKey(mCustomRepairNotice, args);
		}
		else
		{
			mRepairNoticeTxt.SetKey(key, args);
		}
	}

	public void ShowConditionTip()
	{
		if (mConditionNotice != null)
		{
			Services.SystemMessage(mConditionNotice.MessageTip());
		}
	}
}

}
