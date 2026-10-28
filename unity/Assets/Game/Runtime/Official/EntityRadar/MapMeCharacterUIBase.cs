// Original client radar implementation; see entity radar source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Official.EntityContainers;
using MyContainer = Zjcs.Official.EntityContainers.MyContainer;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using IGameObjectPoolItem = Zjcs.Official.StoryPlayables.IGameObjectPoolItem;
namespace Zjcs.Official.EntityRadar
{

public class MapMeCharacterUIBase : UIBase
{
	protected IRadarCharacterController myMapController;

		public IRadarCharacterManager MapMyCharacterManager { get; set; }

	protected override void Start()
	{
		base.Start();
		if (MapMyCharacterManager.MyMeUiCharacter != null)
		{
			Instance_OnMeCharacterUpdated(null, MapMyCharacterManager.MyMeUiCharacter);
		}
		MapMyCharacterManager.OnMeMapControllerUpdated += new Action<IRadarCharacterController, IRadarCharacterController>(Instance_OnMeCharacterUpdated);
	}

	protected override void OnDestroy()
	{
		MapMyCharacterManager.OnMeMapControllerUpdated -= new Action<IRadarCharacterController, IRadarCharacterController>(Instance_OnMeCharacterUpdated);
		base.OnDestroy();
	}

	private void Instance_OnMeCharacterUpdated(IRadarCharacterController preCtrl, IRadarCharacterController curCtrl)
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
