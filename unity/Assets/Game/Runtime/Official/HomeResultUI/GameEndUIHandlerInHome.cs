// Original selected HomeUI result notification; service/type bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeResultUI
{
using System.Collections.Generic;
using UnityEngine;

public class GameEndUIHandlerInHome : MonoBehaviour
{
	public IHomeResultServices Services { private get; set; }
	public GameEndNotifyUI GameResultNotify;

	public FightDemonBattlefieldAutoPushEndNotifyUI DemonBattlefieldAutoPushEndNotifyUI;

	public List<StageType> BlackTypes;

	private void Awake()
	{
		DemonBattlefieldAutoPushEndNotifyUI.SetShowEndAction(TryShowGameResult);
	}

	private void OnEnable()
	{
		Services.Player.OnGameResultsAdd += MainPlayer_OnGameResultsAdd;
		Services.Player.OnGameResultsRemove += MainPlayer_OnGameResultsRemove;
		TryShowGameResult();
	}

	private void MainPlayer_OnGameResultsRemove(long obj)
	{
		TryShowGameResult();
	}

	private void MainPlayer_OnGameResultsAdd(object result)
	{
		TryShowGameResult();
	}

	private void OnDisable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnGameResultsAdd -= MainPlayer_OnGameResultsAdd;
			Services.Player.OnGameResultsRemove -= MainPlayer_OnGameResultsRemove;
		}
		GameResultNotify.ResetData();
	}

	private bool TryShowNormalGameResult()
	{
		IHomeResultStageParser stageInfoParser = Services.StageInfoParser;
		foreach (KeyValuePair<long, IHomePlayerGameResult> item in Services.Player.GameResultManager)
		{
			IHomePlayerGameResult value = item.Value;
			IHomeResultStage stageInfo = stageInfoParser.Get(value.Result.StageId);
			if (value.Result.IsBackground && !BlackTypes.Contains(stageInfo.Type))
			{
				GameResultNotify.Init(item.Key, value.Result.Data);
				return true;
			}
		}
		return false;
	}

	private void TryShowGameResult()
	{
		if (!DemonBattlefieldAutoPushEndNotifyUI.TryRefreshEntry())
		{
			bool active = TryShowNormalGameResult();
			GameResultNotify.gameObject.SetActive(active);
		}
	}
}
}
