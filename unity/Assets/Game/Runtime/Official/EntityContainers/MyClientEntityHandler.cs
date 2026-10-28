// Original client container implementation; see entity container source receipt.
using System;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using EcEntitySourceType = Zjcs.Official.World.Map13EntityVisibilityViews.EcEntitySourceType;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityContainers
{
public class MyClientEntityHandler
{
	private MyContainer myContainer;

	private int mClientEntityId;

	private Dictionary<int, MyEntity> mClientEntities = new Dictionary<int, MyEntity>();

	private IEntityLoadHandler mEntityLoadHandler;

	public Dictionary<int, MyEntity> ClientEntities => mClientEntities;

	public MyClientEntityHandler(MyContainer container)
	{
		myContainer = container;
	}

	public void CreateClientEntity(int classId, Pos2d pos, Action<MyEntity> callback, bool enableMove = false, Action<MyEntity> initCallback = null)
	{
		ECViewEntityData commonPrefab = myContainer.SharedServices.GetCommonPrefab(classId);
		if (commonPrefab == null)
		{
			UnityEngine.Debug.LogError("Original entity loading diagnostic");
			callback?.Invoke(null);
			return;
		}
		ECViewEntityData ecEntity = myContainer.SharedServices.Instantiate(commonPrefab);
		if (ecEntity == null)
		{
			UnityEngine.Debug.LogError("Original entity loading diagnostic");
			callback?.Invoke(null);
			return;
		}
		myContainer.SharedServices.SetContainer(ecEntity, myContainer.SharedContainer);
		myContainer.SharedServices.SetIdentifier(ecEntity, NewClientEntityID());
		InitEntityComponentData(ecEntity, pos);
		myContainer.SharedServices.Awake(ecEntity);
		GetMyMapEntityLoadHandler().CreateMyEntity(ecEntity, newEntity: true, (MyEntity ent) =>
		{
			if ((Object)(object)ent == (Object)null)
			{
				UnityEngine.Debug.LogError("Original entity loading diagnostic");
				callback?.Invoke(null);
			}
			else
			{
				initCallback?.Invoke(ent);
				mClientEntities[ent.Id.EntityId] = ent;
				ent.MyStart();
				if (enableMove)
				{
					ClientEntityPrepareForMove(ent);
				}
				callback?.Invoke(ent);
			}
		});
	}

	private EcEntityIdentifier NewClientEntityID()
	{
		mClientEntityId--;
		if (mClientEntityId >= 0)
		{
			mClientEntityId = -1;
		}
		return new EcEntityIdentifier(myContainer.Id, mClientEntityId);
	}

	public MyEntity GetClientEntity(int id)
	{
		mClientEntities.TryGetValue(id, out var value);
		return value;
	}

	public void RemoveClientEntity(int id, Action callback)
	{
		MyEntity clientEntity = GetClientEntity(id);
		if ((Object)(object)clientEntity == (Object)null)
		{
			callback?.Invoke();
			return;
		}
		mClientEntities.Remove(id);
		((MonoBehaviour)myContainer).StartCoroutine(DoRemoveClientEntity(clientEntity, callback));
	}

	private IEnumerator DoRemoveClientEntity(MyEntity ent, Action callback)
	{
		ent.End(null);
		if (ent.IsNeedFadeOut())
		{
			yield return ent.FadeOut();
		}
		ent.Remove(null);
		myContainer.MyEntityLoader.DestroyMyEntity(ent);
		callback?.Invoke();
	}

	public void InitEntityComponentData(ECViewEntityData entity, Pos2d pos2)
	{
		IClientEntitySource component = myContainer.SharedServices.Source(entity);
		if (component != null)
		{
			component.EntitySourceType = EcEntitySourceType.None;
			EntityOwnershipData ownership = new EntityOwnershipData
			{
				Type = EntityOwnershipType.Player,
				OwnerId = myContainer.SharedServices.MainPlayerId
			};
			component.Ownership = ownership;
		}
		IClientEntityGrid component2 = myContainer.SharedServices.Grid(entity);
		if (component2 != null)
		{
			component2.Pos = pos2;
		}
	}

	private void ClientEntityPrepareForMove(MyEntity ent)
	{
		MyTransform myTransform = default;
		if (!((Component)ent).TryGetComponent<MyTransform>(out myTransform))
		{
			MyTransform myTransform2 = ((Component)ent).gameObject.AddComponent<MyTransform>();
			myTransform2.SetZ(isSetZ: true);
			myTransform2.OnStart(newEntity: true);
		}
		if (!myContainer.SharedServices.HasMoveAgent(ent))
		{
			myContainer.SharedServices.AddMoveAgent(ent);
		}
	}

	public void RemoveAllClientEntities()
	{
		foreach (MyEntity value in mClientEntities.Values)
		{
			value.End(null);
			value.Remove(null);
			myContainer.MyEntityLoader.DestroyMyEntity(value);
		}
		mClientEntities.Clear();
	}

	private IEntityLoadHandler GetMyMapEntityLoadHandler()
	{
		if (mEntityLoadHandler == null)
		{
			if (myContainer.MyEntityLoader is IMyMapEntityLoadHandler myMapEntityLoadHandler)
			{
				mEntityLoadHandler = myMapEntityLoadHandler.EntityLoadHandler;
			}
			else
			{
				mEntityLoadHandler = myContainer.MyEntityLoader;
			}
		}
		return mEntityLoadHandler;
	}
}

}
