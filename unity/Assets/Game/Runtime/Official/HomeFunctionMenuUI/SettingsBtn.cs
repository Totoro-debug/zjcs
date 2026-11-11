// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class SettingsBtn : MonoBehaviour, IWindowOperator
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button Btn;

	public GameObject Operator => gameObject;

	private void Awake()
	{
		Btn.onClick.AddListener(HandleOnClick);
	}

	private void HandleOnClick()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_Settings_Click");
		Services.PopupSettings(this, (IHomeMenuSettingsDialog panel) =>
		{
			panel.Init(HomeMenuSettingStyle.Normal);
		});
	}
}
}
