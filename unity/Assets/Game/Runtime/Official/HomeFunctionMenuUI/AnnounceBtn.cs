// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class AnnounceBtn : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button AnnounceClickBtn;


	private void OnEnable()
	{
		AnnounceClickBtn.onClick.AddListener(OnBtnClick);
	}

	private void OnDisable()
	{
		AnnounceClickBtn.onClick.RemoveListener(OnBtnClick);
	}

	private void OnBtnClick()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_Announce_Click");
		Services.PopAnnounce();
	}
}
}
