namespace Zjcs.Official.HomeHudUI
{
public class HomePlayerUIBase : Zjcs.Official.ProfessionUI.UIBase
{
	public IHomePlayerUiServices PlayerServices { get; set; }

	protected override void Start()
	{
		base.Start();
		if (PlayerServices.HasPlayer)
		{
			Instance_OnPlayerUpdate();
		}
		if (PlayerServices.ManagerAvailable)
		{
			PlayerServices.OnPlayerUpdate.AddListener(Instance_OnPlayerUpdate);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (PlayerServices.ManagerAvailable)
		{
			PlayerServices.OnPlayerUpdate.RemoveListener(Instance_OnPlayerUpdate);
		}
	}

	private void Instance_OnPlayerUpdate()
	{
		if (!PlayerServices.HasPlayer)
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
