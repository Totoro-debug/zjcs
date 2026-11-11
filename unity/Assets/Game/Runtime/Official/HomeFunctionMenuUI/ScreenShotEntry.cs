// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System;
using UnityEngine;
using UnityEngine.UI;

public class ScreenShotEntry : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	[SerializeField]
	private Button mEntryBtn;



	protected void Awake()
	{
		mEntryBtn.onClick.AddListener(OnClickBtn);
	}

	private void OnClickBtn()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_Photo_Click");
		Services.OpenScreenShot();
	}
}
}
