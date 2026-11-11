// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class TopListBtn : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button TopBtn;

	private void OnEnable()
	{
		Refresh();
		Services.Player.OnLevelUpdate += MainPlayer_OnLevelUpdate;
		Services.Player.OnSubRankUpdate += MainPlayer_OnSubRankUpdate;
		TopBtn.onClick.AddListener(OnClick);
	}

	private void OnDisable()
	{
		TopBtn.onClick.RemoveListener(OnClick);
		if (Services.Player != null)
		{
			Services.Player.OnLevelUpdate -= MainPlayer_OnLevelUpdate;
			Services.Player.OnSubRankUpdate -= MainPlayer_OnSubRankUpdate;
		}
	}

	private void MainPlayer_OnSubRankUpdate(int obj)
	{
		Refresh();
	}

	private void MainPlayer_OnLevelUpdate(int obj)
	{
		Refresh();
	}

	private void Refresh()
	{
		TopBtn.gameObject.SetActive(!Services.Player.IsFunctionLimit(FunctionType.PlayerTop));
	}

	private void OnClick()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_TopRank_Click");
		Services.PopupPersonalTop("TopPanel_CombatRating", "Manual");
	}
}
}
