namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
public class ProfessionPlayerUIBase : UIBase
{
    private IProfessionUpgradeBindings mBindings;
    protected IProfessionUpgradeBindings Bindings => mBindings ?? ProfessionUpgradeBindings.Required;
    public void Configure(IProfessionUpgradeBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	protected override void Start()
	{
		base.Start();
		if (Bindings.MainPlayer != null)
		{
			Instance_OnPlayerUpdate();
		}
		if (Bindings.ManagerAvailable)
		{
			Bindings.OnPlayerUpdate.AddListener(Instance_OnPlayerUpdate);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (Bindings.ManagerAvailable)
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
