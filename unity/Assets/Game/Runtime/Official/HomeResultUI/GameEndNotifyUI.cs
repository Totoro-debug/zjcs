// Original selected HomeUI result notification; service/type bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeResultUI
{
using System;
using UnityEngine;
using UnityEngine.UI;

public class GameEndNotifyUI : MonoBehaviour
{
	public IHomeResultServices Services { private get; set; }
	public Button Button;

	public GameCurrentStageText GameCurrentStageText;

	private long mResultId;

	private IHomeFightResult mResult;

	private void Awake()
	{
		Button.onClick.AddListener(HandleOnClick);
	}

	public void Init(long resultId, IHomeFightResult gameResultMsg)
	{
		mResultId = resultId;
		mResult = gameResultMsg;
		GameCurrentStageText.ShowStageText(mResult.StageId);
	}

	public void ResetData()
	{
		mResult = null;
	}

	private void HandleOnClick()
	{
		TryShowGameResult();
	}

	private void OperPlayerReceivedGameAward()
	{
		if (Services.Player.HasGameResultByResultId(mResultId))
		{
			Services.OperPlayerReceivedGameAward(mResultId, invokeByPlayer: false);
		}
	}

	private void TryShowGameResult()
	{
		if (mResult == null)
		{
			return;
		}
		IHomeResultStage stageInfo = Services.StageInfoParser.Get(mResult.StageId);
		if (!stageInfo.ShowEndPanel)
		{
			OperPlayerReceivedGameAward();
			return;
		}
		Type type = Services.GetGameEndPanelType(stageInfo);
		Services.Windows.Popup(type, (GameObject go) =>
		{
			if (go != null && Services.Windows.TryGetEndPanel(go, out var component))
			{
				component.InitMsg(mResult);
			}
			else
			{
				Debug.LogError("[Fight] game end panel component mismatch, stageType:" + stageInfo.Type + ", resultId:" + mResultId + ", panelType:" + type);
			}
		}).OnComplete += (Action<GameObject>)((GameObject go) =>
		{
			OperPlayerReceivedGameAward();
		});
	}
}
}
