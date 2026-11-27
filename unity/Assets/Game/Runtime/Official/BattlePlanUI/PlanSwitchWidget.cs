// Complete original shared plan widget; see source receipt.
namespace Zjcs.Official.BattlePlanUI
{
using System;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.ProfessionUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using ContentModerationClickMask = Zjcs.Official.WorldUI.WorldContentModerationClickMask;
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlanSwitchWidget : UIBase
{
    private IBattlePlanWidgetServices services;
    private IBattlePlanWidgetServices Services => services ?? throw new InvalidOperationException("Bind original plan widget services before initialization.");
    public void Configure(IBattlePlanWidgetServices value) { services = value ?? throw new ArgumentNullException(nameof(value)); }

	public enum PlanIndexSource
	{
		UiState,
		ActivePlanIndex
	}

	[SerializeField]
	private Button mPrevPlanBtn;

	[SerializeField]
	private Button mNextPlanBtn;

	[SerializeField]
	private MultiLanguageText mPlanNameText;

	[SerializeField]
	private ContentModerationClickMask mInputClickMask;

	[SerializeField]
	private TMP_InputField mPlanNameInput;

	[SerializeField]
	private Button mClearBtn;

	[SerializeField]
	private Scrollbar mPlanSelectedBar;

	[SerializeField]
	private bool mAllowRename = true;

	[SerializeField]
	private bool mShowClearButton = true;

	[SerializeField]
	private bool mSkipEmptyPlan = true;

	[SerializeField]
	private bool mHideWhenLocked = true;

	private PlanIndexSource mPlanIndexSource;

	private BattlePlanType mPlanType = BattlePlanType.Skill;

	private int mPlanIndex;

	private int mMaxCount;

	private string mOldPlanName = string.Empty;

	public int PlanIndex => mPlanIndex;

	public event Action<int> OnPlanChanged;

	public event Action<int> OnClearClicked;

	private void Awake()
	{






		((UnityEvent)mPrevPlanBtn.onClick).AddListener((UnityAction)OnPrevPlanBtnClick);
		((UnityEvent)mNextPlanBtn.onClick).AddListener((UnityAction)OnNextPlanBtnClick);
		if ((Object)(object)mInputClickMask != (Object)null)
		{
			mInputClickMask.SetClickCallback(OnClickCustomMask);
		}
		if ((Object)(object)mClearBtn != (Object)null)
		{
			((UnityEvent)mClearBtn.onClick).AddListener((UnityAction)OnClearBtnClick);
		}
	}

	public void Init(BattlePlanType planType, PlanIndexSource planIndexSource, int selectPlan = 0, bool skipEmptyPlan = true, bool allowRename = true, bool showClearButton = true)
	{
		InitWrapper(() =>
		{
			mPlanType = planType;
			mPlanIndexSource = planIndexSource;
			mMaxCount = GetPlanMaxCount(planType);
			mSkipEmptyPlan = skipEmptyPlan;
			mAllowRename = allowRename;
			mShowClearButton = showClearButton;
			if (planIndexSource == PlanIndexSource.ActivePlanIndex)
			{
				mPlanIndex = Services.GetActivePlanIndex(planType);
			}
			else
			{
				mPlanIndex = selectPlan;
			}
		});
		if (!((Component)this).gameObject.activeSelf)
		{
			((Component)this).gameObject.SetActive(true);
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if ((Object)(object)mPlanNameInput != (Object)null)
		{
			((UnityEvent<string>)(object)mPlanNameInput.onEndEdit).AddListener((UnityAction<string>)OnPlanNameChanged);
		}
		Services.OnBattlePlanNameChanged += new Action<BattlePlanType, int, string, string>(MainPlayer_OnBattlePlanNameChanged);
		RefreshView();
	}

	protected override void OnDisableAndInited()
	{
		if ((Object)(object)mPlanNameInput != (Object)null)
		{
			((UnityEvent<string>)(object)mPlanNameInput.onEndEdit).RemoveListener((UnityAction<string>)OnPlanNameChanged);
		}
		if (services != null)
		{
			Services.OnBattlePlanNameChanged -= new Action<BattlePlanType, int, string, string>(MainPlayer_OnBattlePlanNameChanged);
		}
		base.OnDisableAndInited();
	}

	public void RefreshView()
	{
		if (!RefreshLocked())
		{
			mOldPlanName = Services.GetBattlePlanName(mPlanType, mPlanIndex, Services.GetPlanName(mPlanType, mPlanIndex));
			if ((Object)(object)mPlanNameText != (Object)null)
			{
				mPlanNameText.SetKey("{0}", mOldPlanName);
				((Component)mPlanNameText).gameObject.SetActive(!mAllowRename);
			}
			if ((Object)(object)mPlanNameInput != (Object)null)
			{
				((Component)mPlanNameInput).gameObject.SetActive(mAllowRename);
				mPlanNameInput.text = mOldPlanName;
			}
			RefreshPlanSelectedBar();
			if ((Object)(object)mClearBtn != (Object)null)
			{
				((Component)mClearBtn).gameObject.SetActive(mShowClearButton && mPlanIndex != 0);
			}
		}
	}

	private bool RefreshLocked()
	{
		bool flag = Services.IsPlanLocked();
		if ((mHideWhenLocked & flag) || IsPlanFunctionDisabled())
		{
			((Component)this).gameObject.SetActive(false);
			return true;
		}
		((Component)this).gameObject.SetActive(true);
		return false;
	}

	private bool IsPlanFunctionDisabled()
	{
		return Services.IsPlanFunctionDisabled(mPlanType);
	}

	private void RefreshPlanSelectedBar()
	{
		if ((Object)(object)mPlanSelectedBar == (Object)null || mMaxCount <= 1)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < mMaxCount; i++)
		{
			if (!ShouldSkip(i))
			{
				if (i < mPlanIndex)
				{
					num2++;
				}
				num++;
			}
		}
		mPlanSelectedBar.numberOfSteps = num;
		mPlanSelectedBar.size = 1f / (float)num;
		mPlanSelectedBar.value = ((num > 1) ? ((float)num2 / (float)(num - 1)) : 0f);
	}

	private void OnPrevPlanBtnClick()
	{
		SwitchTo(GetPrevPlanIndex(mPlanIndex));
	}

	private void OnNextPlanBtnClick()
	{
		SwitchTo(GetNextPlanIndex(mPlanIndex));
	}

	public void SwitchTo(int planIndex)
	{
		if (planIndex != mPlanIndex)
		{
			mPlanIndex = planIndex;
			RefreshView();
			OnPlanChanged?.Invoke(mPlanIndex);
		}
	}

	public void RollbackTo(int planIndex)
	{
		if (planIndex != mPlanIndex)
		{
			mPlanIndex = planIndex;
			RefreshView();
		}
	}

	public void SetInteractable(bool interactable)
	{
		((Selectable)mPrevPlanBtn).interactable = interactable;
		((Selectable)mNextPlanBtn).interactable = interactable;
		if ((Object)(object)mClearBtn != (Object)null)
		{
			((Selectable)mClearBtn).interactable = interactable;
		}
	}

	private int GetPrevPlanIndex(int planIndex)
	{
		int num = (planIndex - 1 + mMaxCount) % mMaxCount;
		if (ShouldSkip(num))
		{
			return GetPrevPlanIndex(num);
		}
		return num;
	}

	private int GetNextPlanIndex(int planIndex)
	{
		int num = (planIndex + 1) % mMaxCount;
		if (ShouldSkip(num))
		{
			return GetNextPlanIndex(num);
		}
		return num;
	}

	private bool ShouldSkip(int planIndex)
	{
		if (!mSkipEmptyPlan || planIndex == 0)
		{
			return false;
		}
		return !Services.IsBattlePlanContentUsable(mPlanType, planIndex);
	}

	private int GetPlanMaxCount(BattlePlanType planType) { return Services.GetPlanMaxCount(planType); }

	private void OnClickCustomMask()
	{
		if (string.IsNullOrEmpty(Services.GetPlanName(mPlanType, mPlanIndex)))
		{
			return;
		}
		Services.Confirm(this, "ui_battle_plan_contentmoderation_confirm", () =>
        {
            RequestChangePlanName(string.Empty, custom: false);
        });
	}

	private void OnPlanNameChanged(string str)
	{
		if (string.Equals(mOldPlanName, str))
		{
			return;
		}
		int num = Services.CheckSkillPlanNameLegal(str);
		if (num != 0)
		{
			if (num == 1149)
			{
				int characterMaxCount = Services.GetCharacterMaxCount();
				Services.ShowError(702, characterMaxCount);
			}
			else
			{
				Services.ShowError(num);
			}
			RestorePlanNameInput();
		}
		else
		{
			RequestChangePlanName(str, custom: true);
		}
	}

	private void RequestChangePlanName(string name, bool custom)
	{
		BattlePlanType planType = mPlanType;
		int planIndex = mPlanIndex;
		PlayerChangeBattlePlanNameRequest req = new PlayerChangeBattlePlanNameRequest
		{
			PlanType = planType,
			PlanIndex = planIndex,
			Name = name,
			Custom = custom
		};
		Services.ChangePlanName(req, (bool success) =>
		{
			if (planType == mPlanType && planIndex == mPlanIndex)
			{
				if (!success)
				{
					RestorePlanNameInput();
				}
				else
				{
					Services.SystemMessage("ChangeSkillPlanNameSuc");
					mOldPlanName = Services.GetBattlePlanName(planType, planIndex, custom ? name : string.Empty);
					if ((Object)(object)mPlanNameText != (Object)null)
					{
						mPlanNameText.SetKey("{0}", mOldPlanName);
					}
					if ((Object)(object)mPlanNameInput != (Object)null)
					{
						mPlanNameInput.text = mOldPlanName;
					}
				}
			}
		});
	}

	private void RestorePlanNameInput()
	{
		if (!((Object)(object)mPlanNameInput == (Object)null))
		{
			mPlanNameInput.text = mOldPlanName;
		}
	}

	private void MainPlayer_OnBattlePlanNameChanged(BattlePlanType planType, int planIndex, string oldName, string newName)
	{
		if (planType == mPlanType && planIndex == mPlanIndex)
		{
			mOldPlanName = Services.GetBattlePlanName(mPlanType, planIndex, newName);
			if ((Object)(object)mPlanNameText != (Object)null)
			{
				mPlanNameText.SetKey("{0}", mOldPlanName);
			}
			if ((Object)(object)mPlanNameInput != (Object)null)
			{
				mPlanNameInput.text = mOldPlanName;
			}
		}
	}

	private void OnClearBtnClick()
	{
		OnClearClicked?.Invoke(mPlanIndex);
	}
}

}
