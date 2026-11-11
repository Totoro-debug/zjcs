// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class UserBtn : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button Btn;

	private void Awake()
	{
		Btn.onClick.AddListener(HandleOnClick);
	}

	private void HandleOnClick()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_User_Click");
		Services.PopupUser();
	}
}
}
