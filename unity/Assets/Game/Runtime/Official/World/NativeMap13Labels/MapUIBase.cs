// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;

public class MapUIBase : UIBase
{
	protected object mMap;

	

	protected override void Start()
	{
		base.Start();
		if (Services.Map != null)
		{
			Instance_OnMapInited();
		}
		Services.Initialized += new Action(Instance_OnMapInited);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Services.Initialized -= new Action(Instance_OnMapInited);
	}

	private void Instance_OnMapInited()
	{
		if (Services.Map != null)
		{
			InitWrapper(() =>
			{
				mMap = Services.Map;
				InitSub();
			});
		}
	}

	protected virtual void InitSub()
	{
	}
}

}
