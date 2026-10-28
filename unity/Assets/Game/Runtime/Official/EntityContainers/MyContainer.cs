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
public class MyContainer : Zjcs.Official.EntityLifecycle.MyContainer, IEntityLoadContainer
{
	public MyContainerAddEntityEventDelegate EntityAddEvent = new MyContainerAddEntityEventDelegate();

	public MyContainerRemoveEntityEventDelegate EntityRemoveEvent = new MyContainerRemoveEntityEventDelegate();

	public MyContainerPrepareReplaceEntityEventDelegate EntityPrepareReplaceEvent = new MyContainerPrepareReplaceEntityEventDelegate();

	public MyContainerReplaceEntityEventDelegate EntityReplaceEvent = new MyContainerReplaceEntityEventDelegate();

	public IClientEntityContainer SharedContainer;
    public IClientEntityServices SharedServices { get; set; }
    public override IECViewMapRootLifetime Container => SharedContainer;
    MonoBehaviour IEntityLoadContainer.Host => this;
    Zjcs.Official.EntityLifecycle.MyContainer IEntityLoadContainer.LifecycleContainer => this;
    IEntityLoadSharedContainer IEntityLoadContainer.Container => SharedContainer;
    Transform IEntityLoadContainer.EntitySlot => myEntitySlot;
    private IDisposable startSubscription, destroySubscription, prepareSubscription, replaceSubscription;

	public Transform myEntitySlot;

	private ILogger mLogger;

	private Dictionary<EcEntityIdentifier, MyEntity> mMyEntityDict = new Dictionary<EcEntityIdentifier, MyEntity>();

	private Dictionary<int, HashSet<MyEntity>> mClassIdMapEntitys = new Dictionary<int, HashSet<MyEntity>>();

	private Action<MyEntity> mCreatEntityAction;

	public IMyContainerHandler MyContainerHandler { get; set; }

	public override ILogger Logger => mLogger ?? (mLogger = CreateLogger($"MyContainer_{SharedContainer?.ClassId ?? (-1)}"));

	public Dictionary<EcEntityIdentifier, MyEntity>.ValueCollection MyEntityList => mMyEntityDict.Values;

	public IReadOnlyDictionary<EcEntityIdentifier, MyEntity> MyEntityDict => mMyEntityDict;

	public long Id => SharedContainer.Id;

	public int ClassId => SharedContainer.ClassId;

	private MyEntity mMyRootEntity;
	public override MyEntity MyRootEntity => mMyRootEntity;

	public IEntityLoadHandler MyEntityLoader { get; private set; }

	public MyClientEntityHandler MyClientEntityHandler { get; private set; }

	public MyClientStoryNPCEntityHandler MyClientStoryNPCEntityHandler { get; private set; }

	public bool Inited { get; private set; }

	public event Action OnInited;

	private void Awake()
	{
		mCreatEntityAction = OnCreatedEntity;
		MyClientEntityHandler = new MyClientEntityHandler(this);
		MyClientStoryNPCEntityHandler = new MyClientStoryNPCEntityHandler(this);
	}

	public IEnumerator Init(IClientEntityContainer container, IEntityLoadHandler loader)
	{
		if (Inited)
		{
			Debug.LogError("Original client container diagnostic");
		}
		SharedContainer = container;
		MyEntityLoader = loader;
		startSubscription = SharedContainer.SubscribeStartEntity(Container_OnEntityStart);
		destroySubscription = SharedContainer.SubscribeDestroyEntity(Container_OnEntityDestroy);
		prepareSubscription = SharedContainer.SubscribePrepareReplaceEntity(Container_OnEntityPrepareReplace);
		replaceSubscription = SharedContainer.SubscribeReplaceEntity(Container_OnEntityReplace);
		yield return MyEntityLoader.LoadForInit(mCreatEntityAction);
		Inited = true;
		MyContainerHandler.RegisterContainer(this, container);
		OnInited?.Invoke();
	}

	private void OnCreatedEntity(MyEntity myEnt)
	{
		if (!((Object)(object)myEnt == (Object)null))
		{
			if (myEnt.Id == SharedContainer.RootEntity.Id)
			{
				mMyRootEntity = myEnt;
			}
			if (AddEntity(myEnt))
			{
				myEnt.MyStart();
				EntityAddEvent.FireEvent(myEnt);
			}
		}
	}

	public void RemoveAllEntities()
	{
		MyEntityLoader?.OnBeforeRemoveAllEntities();
		foreach (MyEntity value in MyEntityDict.Values)
		{
			value.End(null);
			value.Remove(null);
			EntityRemoveEvent.FireEvent(value);
			if (!SharedContainer.Destroying)
			{
				MyEntityLoader.DestroyMyEntity(value);
			}
		}
		mMyEntityDict.Clear();
		mClassIdMapEntitys.Clear();
	}

	public bool TryGetMyEntitysByClassId(int classId, out HashSet<MyEntity> myEntities)
	{
		return mClassIdMapEntitys.TryGetValue(classId, out myEntities);
	}

	public bool ExistEntityGroupByClassId(int entClassId)
	{
		if (mClassIdMapEntitys.TryGetValue(entClassId, out var value))
		{
			return value.Count > 0;
		}
		return false;
	}

	private void OnDestroy()
	{
		MyContainerHandler.UnregisterContainer();
		if (SharedContainer != null)
		{
			startSubscription?.Dispose();
			destroySubscription?.Dispose();
			prepareSubscription?.Dispose();
			replaceSubscription?.Dispose();
		}
		MyEntityLoader?.Dispose();
		RemoveAllEntities();
		MyClientEntityHandler?.RemoveAllClientEntities();
		MyClientStoryNPCEntityHandler?.RemoveAllClientEntities();
		mMyRootEntity = null;
		EntityAddEvent.Clear();
		EntityRemoveEvent.Clear();
		EntityPrepareReplaceEvent.Clear();
		EntityReplaceEvent.Clear();
	}

	public void Container_OnEntityDestroy(ECViewEntityData entity, object userData)
	{
		MyEntity entity2 = GetEntity(entity.Id);
		if (!((Object)(object)entity2 == (Object)null))
		{
			((MonoBehaviour)this).StartCoroutine(DoEntityDestroy(entity2, userData));
		}
	}

	public void DoEntityDestroyImmediate(MyEntity myEnt, object userData)
	{
		myEnt.End(userData);
		RemoveEntity(myEnt);
		myEnt.Remove(userData);
		EntityRemoveEvent.FireEvent(myEnt);
		MyEntityLoader.DestroyMyEntity(myEnt);
	}

	private IEnumerator DoEntityDestroy(MyEntity myEnt, object userData)
	{
		myEnt.End(userData);
		if (myEnt.IsNeedFadeOut())
		{
			yield return myEnt.FadeOut();
		}
		RemoveEntity(myEnt);
		myEnt.Remove(userData);
		EntityRemoveEvent.FireEvent(myEnt);
		MyEntityLoader.DestroyMyEntity(myEnt);
	}

	public void Container_OnEntityStart(ECViewEntityData entity, bool newEntity)
	{
		MyEntityLoader.CreateMyEntity(entity, newEntity, mCreatEntityAction);
	}

	public void Container_OnEntityPrepareReplace(ECViewEntityData sourceEntity, ECViewEntityData tgtEnt)
	{
		if (MyEntityDict.TryGetValue(sourceEntity.Id, out var value))
		{
			value.PrepareReplace();
			EntityPrepareReplaceEvent.FireEvent(value);
		}
	}

	public void Container_OnEntityReplace(ECViewEntityData sourceEntity, EcEntityIdentifier preEntId)
	{
		MyEntity myEntity = default;
		if (mMyEntityDict.Remove(preEntId, out myEntity))
		{
			myEntity.Replace(preEntId);
			mMyEntityDict.Add(myEntity.Id, myEntity);
			EntityReplaceEvent.FireEvent(myEntity, preEntId);
		}
	}

	public MyEntity GetEntity(EcEntityIdentifier id)
	{
		MyEntityDict.TryGetValue(id, out var value);
		return value;
	}

	private bool AddEntity(MyEntity myEnt)
	{


		if (!mMyEntityDict.TryAdd(myEnt.Id, myEnt))
		{
			Debug.LogError("Original client container diagnostic");
			return false;
		}
		if (mClassIdMapEntitys.TryGetValue(myEnt.ClassId, out var value))
		{
			value.Add(myEnt);
		}
		else
		{
			mClassIdMapEntitys.Add(myEnt.ClassId, new HashSet<MyEntity> { myEnt });
		}
		return true;
	}

	private bool RemoveEntity(MyEntity myEnt)
	{
		bool result = mMyEntityDict.Remove(myEnt.Id);
		if (mClassIdMapEntitys.TryGetValue(myEnt.ClassId, out var value))
		{
			value.Remove(myEnt);
		}
		return result;
	}

	public Vector3 ToWorldPos(Vector3 pos)
	{


		return myEntitySlot.TransformPoint(pos);
	}

	public Quaternion ToWorldRot(Quaternion rot)
	{



		return myEntitySlot.rotation * rot;
	}

	public Vector3 ToLocalPos(Vector3 pos)
	{


		return myEntitySlot.InverseTransformPoint(pos);
	}

	public Quaternion ToLocalRot(Quaternion rot)
	{




		return Quaternion.Inverse(myEntitySlot.rotation) * rot;
	}

	public void RegisterLoggeg(ILogger logger)
	{
		mLogger = logger;
	}

	public new static ILogger CreateLogger(string categoryName)
	{
		return Debug.unityLogger;
	}
}

}
