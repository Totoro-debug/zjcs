// Original selected client implementation; only player/parser/reward ports differ.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
public class HomeExperiencePlayerUIBase : UIBase
{
    protected IHomeExperienceServices Bindings { get; private set; }
    public void Configure(IHomeExperienceServices bindings)
    {
        if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind the inactive original experience widget.");
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }

	protected override void Start()
	{
		base.Start();
		if (Bindings.MainPlayer != null)
		{
			Instance_OnPlayerUpdate();
		}
		if (Bindings != null && Bindings.ManagerAvailable)
		{
			Bindings.OnPlayerUpdate.AddListener(Instance_OnPlayerUpdate);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (Bindings != null && Bindings.ManagerAvailable)
		{
			Bindings.OnPlayerUpdate.RemoveListener(Instance_OnPlayerUpdate);
		}
	}

	private void Instance_OnPlayerUpdate()
	{
		if (Bindings.MainPlayer == null)
		{
			ResetInitState();
		}
		else
		{
			InitWrapper(null);
		}
	}
}

}
