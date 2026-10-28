using System;
using Zjcs.Official.World.NativeMap13Labels;

namespace Zjcs.Official.World.Map13MiniMap
{
public class MapMeCharacterUIBase : UIBase
{
	protected IMiniMapCharacterController myMapController;

	public IMiniMapCharacterManager MapMyCharacterManager { get; set; }

	protected override void Start()
	{
		base.Start();
		if (MapMyCharacterManager.MyMeUiCharacter != null)
		{
			Instance_OnMeCharacterUpdated(null, MapMyCharacterManager.MyMeUiCharacter);
		}
		MapMyCharacterManager.OnMeMapControllerUpdated += new Action<IMiniMapCharacterController, IMiniMapCharacterController>(Instance_OnMeCharacterUpdated);
	}

	protected override void OnDestroy()
	{
		MapMyCharacterManager.OnMeMapControllerUpdated -= new Action<IMiniMapCharacterController, IMiniMapCharacterController>(Instance_OnMeCharacterUpdated);
		base.OnDestroy();
	}

	private void Instance_OnMeCharacterUpdated(IMiniMapCharacterController preCtrl, IMiniMapCharacterController curCtrl)
	{
		if (MapMyCharacterManager.MyMeUiCharacter == null)
		{
			ResetInitState();
			return;
		}
		InitWrapper(() =>
		{
			myMapController = MapMyCharacterManager.MyMeUiCharacter;
			InitSub();
		});
	}

	protected virtual void InitSub()
	{
	}
}

}
