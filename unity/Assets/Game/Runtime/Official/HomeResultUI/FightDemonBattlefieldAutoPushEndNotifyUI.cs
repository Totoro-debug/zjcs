// Original selected HomeUI result notification; service/type bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeResultUI
{
using System;
using UnityEngine;
using UnityEngine.UI;

public class FightDemonBattlefieldAutoPushEndNotifyUI : MonoBehaviour
{
	public IHomeResultServices Services { private get; set; }
	public WorldStateSwitcher<bool> StateSwitcher;

	public Button Button;

	public GameCurrentStageText GameCurrentStageText;

	private ulong mResultKey;

	private IHomeDemonBackPushResult mResult;

	private Action mOnShowEnd;

	public bool TryRefreshEntry()
	{
		bool flag = TryRefreshAutoPushEntryData();
		StateSwitcher.SetState(flag);
		if (flag)
		{
			IHomeResultDemonParser stagePartInfoDemonBattlefieldParser = Services.StagePartInfoDemonBattlefieldParser;
			int entryIndex = mResult.EntryIndex;
			int layer = Math.Clamp(max: stagePartInfoDemonBattlefieldParser.GetMaxLayer(entryIndex), value: mResult.EndLayer + 1, min: 1);
			int id = stagePartInfoDemonBattlefieldParser.GetInfoByIndexAndLayer(entryIndex, layer).Id;
			GameCurrentStageText.ShowStageText(id);
		}
		return flag;
	}

	public void SetShowEndAction(Action onShowEnd)
	{
		mOnShowEnd = onShowEnd;
	}

	private void OnEnable()
	{
		Services.Windows.OnWindowClosed += new Action<GameObject>(OnWindowClosed);
		Button.onClick.AddListener(OnBtnClicked);
	}

	private void OnDisable()
	{
		if (Services.Windows != null)
		{
			Services.Windows.OnWindowClosed -= new Action<GameObject>(OnWindowClosed);
		}
		Button.onClick.RemoveListener(OnBtnClicked);
		ResetData();
	}

	private void OnWindowClosed(GameObject go)
	{
		if (Services.Windows.IsDemonResultPanel(go))
		{
			ResetData();
			if (!TryRefreshEntry())
			{
				mOnShowEnd?.Invoke();
			}
		}
	}

	private void OnBtnClicked()
	{
		if (mResult != null && mResultKey != 0)
		{
			Services.OperPlayerDemonBattlefieldHandleBackPushResult(mResultKey, FightDemonBattlefieldAutoPushEndNotifyUI_OnSuccess);
		}
	}

	private void FightDemonBattlefieldAutoPushEndNotifyUI_OnSuccess(object obj)
	{
		Services.Windows.PopupDemon((IHomeDemonResultPanel panel) =>
		{
			panel.Init(mResult);
		});
	}

	private bool TryRefreshAutoPushEntryData()
	{
		ResetData();
		if (Services.Player.StageDemonBattlefieldData.TryGetOlderBackPushResult(out mResultKey, out mResult))
		{
			return true;
		}
		return false;
	}

	private void ResetData()
	{
		mResultKey = 0uL;
		mResult = null;
	}
}
}
