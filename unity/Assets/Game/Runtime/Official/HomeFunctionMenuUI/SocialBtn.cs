// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class SocialBtn : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	[SerializeField]
	private Button mSocialClickBtn;

	[SerializeField]
	private GameObject mRoot;

	private bool mWaitUnlock;


	private void OnEnable()
	{
		mSocialClickBtn.onClick.AddListener(OnBtnClick);
		RefreshVisible();
		RegisterUnlockConditionIfNeeded();
	}

	private void OnDisable()
	{
		UnregisterUnlockCondition();
		mSocialClickBtn.onClick.RemoveListener(OnBtnClick);
	}

	private void OnBtnClick()
	{
		Services.RemoveNewFunction(FunctionType.Social);
		Services.PopSocial();
	}

	private void OnUnlockCallback()
	{
		if (mWaitUnlock && IsUnlocked())
		{
			UnregisterUnlockCondition();
			RefreshVisible();
		}
	}

	private void RegisterUnlockConditionIfNeeded()
	{
		if (!IsUnlocked())
		{
			HomeMenuFunctionLimitInfo functionLimitInfo = Services.GetFunctionLimit(FunctionType.Social);
			if (functionLimitInfo?.Condition != null)
			{
				Services.ConditionHandler.Register(functionLimitInfo.Condition, OnUnlockCallback);
				mWaitUnlock = true;
			}
		}
	}

	private void UnregisterUnlockCondition()
	{
		if (mWaitUnlock)
		{
			HomeMenuFunctionLimitInfo functionLimitInfo = Services.GetFunctionLimit(FunctionType.Social);
			if (functionLimitInfo?.Condition != null)
			{
				Services.ConditionHandler.Deregister(functionLimitInfo.Condition, OnUnlockCallback);
			}
			mWaitUnlock = false;
		}
	}

	private void RefreshVisible()
	{
		GetRoot().SetActive(IsUnlocked());
	}

	private GameObject GetRoot()
	{
		if (mRoot != null)
		{
			return mRoot;
		}
		if (!(mSocialClickBtn != null))
		{
			return gameObject;
		}
		return mSocialClickBtn.gameObject;
	}

	private bool IsUnlocked()
	{
		if (Services.Player == null)
		{
			return false;
		}
		return !Services.Player.IsFunctionLimit(FunctionType.Social);
	}
}
}
