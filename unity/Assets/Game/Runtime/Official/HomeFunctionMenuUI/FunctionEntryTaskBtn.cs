// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class FunctionEntryTaskBtn : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button Button;


	private void Awake()
	{
		Button.onClick.AddListener(OnButtonClick);
	}

	protected void OnDestroy()
	{
		Button.onClick.RemoveListener(OnButtonClick);
	}

	private void OnButtonClick()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_Task_Click");
		Services.NavigateTaskPanel(null, null);
	}
}
}
