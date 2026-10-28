// Original client/shared input source; see world input source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using WorldStageComponent = Zjcs.Official.LegacyWorldStages.LegacyWorldStageComponentData;
using EntityOwnershipType = Zjcs.Official.EntityTipDotViews.EntityOwnershipType;
using IMyVisibility = Zjcs.Official.EntityLifecycle.IMyVisibility;
namespace Zjcs.Official.EntityWorldInput
{

[RequireComponent(typeof(MyWorldStage))]
public class WorldStageInput : AWorldEntityInput
{
	private MyEntitySource myEntitySource;

	private WorldInputResourceBag mWorldInputResourceBag;

	private MyEntity mMyEntity;

		public IWorldInputResourceLoader ResourceLoader { get; set; }

		public IWorldInputRootLookup MapRootManagerProxy { get; set; }

		public IWorldInputPlayerContext MapMyCharacterManager { get; set; }

	public IWorldInputEntityState EntityState { get; set; }
    public IWorldInputPresentation Presentation { get; set; }
	public override bool ClickOverriden => false;

	public override MyEntity MyEntity => mMyEntity;

	protected WorldStageComponent mWorldStageComponent
	{
		get
		{
			ECViewEntityData entity = mMyEntity.Entity;
			if (entity == null || EntityState.IsRemoving(entity) || EntityState.HasEnded(entity))
			{
				return null;
			}
			WorldStageComponent component = mMyEntity.Entity.GetComponent<WorldStageComponent>();
			if (component == null)
			{
				return null;
			}
			return component;
		}
	}

	public override WorldEntityInputPriority Priority
	{
		get
		{
			if ((Object)(object)myEntitySource == (Object)null && !((Component)this).TryGetComponent<MyEntitySource>(out myEntitySource))
			{
				return WorldEntityInputPriority.Lowest;
			}
			if (myEntitySource.Component != null && myEntitySource.Component.IsMine(EntityOwnershipType.Player, MapMyCharacterManager.MainPlayerId))
			{
				return WorldEntityInputPriority.Medium;
			}
			return WorldEntityInputPriority.Lowest;
		}
	}

	protected virtual void Awake()
	{

		mWorldInputResourceBag = default;
		mMyEntity = ((Component)this).GetComponent<MyEntity>();
	}

	protected virtual void OnDestroy()
	{
		mWorldInputResourceBag.Dispose();
	}

	public override Sprite GetEntitySprite()
	{
		IMyStageBehaviour myStageBehaviour = MyStageBehaviourUtils.GetMyStageBehaviour(MapRootManagerProxy, mWorldStageComponent.Entity);
		if (myStageBehaviour == null)
		{
			return GetEcViewIcon();
		}
		int monsterId = myStageBehaviour.GetMonsterId();
		string spriteName = string.Empty;
		if (monsterId != -1)
		{
			WorldInputMonsterInfo monsterInfo = Presentation.GetMonsterInfo(monsterId);
			spriteName = ((monsterInfo != null) ? Presentation.GetMonsterIcon(monsterInfo.IconId) : string.Empty);
		}
		return mWorldInputResourceBag.Add(ResourceLoader.LoadSprite(spriteName)).Result;
	}

	public override string GetEntityMenuItemKey()
	{
		return Presentation.GetMapEntityNameKey(mMyEntity.Entity, ((Zjcs.Official.EntityContainers.MyContainer)mMyEntity.myContainer).ClassId);
	}

	public override AWorldEntityInput GetRedirectInput()
	{
		WorldStageComponent interactionRedirectStage = mWorldStageComponent.InteractionRedirectStage;
		if (interactionRedirectStage == null)
		{
			return base.GetRedirectInput();
		}
		if (!MapRootManagerProxy.TryGetEntity(interactionRedirectStage.Entity.Id, out var myEnt))
		{
			return base.GetRedirectInput();
		}
		return ((Component)myEnt).GetComponent<AWorldEntityInput>() ?? base.GetRedirectInput();
	}

	public override bool ClickByColliderCheck()
	{
		return false;
	}

	public override bool ClickByGridCheck()
	{
		if (!CheckLimitedByTeam())
		{
			return false;
		}
		if (mWorldStageComponent == null)
		{
			return false;
		}
		IWorldInputMapCharacter mapCharacterFromController = MapMyCharacterManager.GetMapCharacterFromController();
		if (mapCharacterFromController != null && mapCharacterFromController.Character?.IsLeader == false)
		{
			return false;
		}
		MyEntitySource component = ((Component)this).GetComponent<MyEntitySource>();
		if ((Object)(object)component != (Object)null && !component.Component.CanClick())
		{
			return false;
		}
		MyEntityPlayerInteraction component2 = ((Component)MyEntity).GetComponent<MyEntityPlayerInteraction>();
		if ((Object)(object)component2 != (Object)null && component2.Component.IsPlayerInteracting(MapMyCharacterManager.MainPlayerId))
		{
			return false;
		}
		IMyVisibility visibility = MyEntity.Visibility;
		if (visibility != null && !visibility.IsVisible)
		{
			return false;
		}
		if (!IsECClientVisibility())
		{
			return false;
		}
		if (!((IWorldStageOperationState)mWorldStageComponent).IsActive())
		{
			return false;
		}
		return true;
	}

	public override bool ConflictWithInput(AWorldEntityInput input)
	{
		if (input is IWorldPlayerInput worldPlayerInput)
		{
			return worldPlayerInput.IsMyself;
		}
		return false;
	}

	public override void OnClick(Pos2d pos, bool isLogicClick)
	{
		WorldStageComponent component = mMyEntity.Entity.GetComponent<WorldStageComponent>();
		if (component != null)
		{
			MapInput?.ClickStage(component, pos, isLogicClick);
		}
	}

	private Sprite GetEcViewIcon()
	{
		ECViewDriver componentInChildren = ((Component)mMyEntity).GetComponentInChildren<ECViewDriver>(true);
		if ((Object)(object)componentInChildren == (Object)null)
		{
			return null;
		}
		ECWorldStageView view = componentInChildren.GetView<ECWorldStageView>();
		if ((Object)(object)view.EntityIcon != (Object)null)
		{
			return view.EntityIcon.sprite;
		}
		return null;
	}

	private bool IsECClientVisibility()
	{
		ECViewDriver componentInChildren = ((Component)mMyEntity).GetComponentInChildren<ECViewDriver>(true);
		if ((Object)(object)componentInChildren == (Object)null)
		{
			return true;
		}
		return componentInChildren.GetView<ECClientVisibilityView>()?.IsVisible ?? true;
	}
}

}
