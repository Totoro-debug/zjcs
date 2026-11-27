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
    protected virtual bool PlayerAvailable => Bindings.MainPlayer != null;
    protected virtual bool PlayerManagerAvailable => Bindings.ManagerAvailable;
    protected virtual SafeAction PlayerUpdateEvent => Bindings.OnPlayerUpdate;

	protected override void Start()
	{
		base.Start();
		if (PlayerAvailable)
		{
			Instance_OnPlayerUpdate();
		}
		if (PlayerManagerAvailable)
		{
			PlayerUpdateEvent.AddListener(Instance_OnPlayerUpdate);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (PlayerManagerAvailable)
		{
			PlayerUpdateEvent.RemoveListener(Instance_OnPlayerUpdate);
		}
	}

	private void Instance_OnPlayerUpdate()
	{
		if (!PlayerAvailable)
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
