// Original client/framework implementation; see entity loading source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityLoading
{
public class MyEntityLoadHandler : IEntityLoadHandler, IDisposable
{
	public struct MyEntityRequestInfo
	{
		public MyEntityRequestInfo(ECViewEntityData entity, bool newEntity, MyEntityProvider goProvider, Action<MyEntity> callback)
		{ Entity = entity; NewEntity = newEntity; GoProvider = goProvider; Callback = callback; }

		public ECViewEntityData Entity;

		public bool NewEntity;

		public MyEntityProvider GoProvider;

		public Action<MyEntity> Callback;
	}

	public abstract class MyEntityProvider
	{
		protected readonly IServiceProvider Provider;

		protected ECViewEntityData mEntity;

		public bool IsLoadCompleted { get; protected set; }

		public MyEntityProvider(ECViewEntityData entity, IServiceProvider serviceProvider)
		{
			mEntity = entity;
			Provider = serviceProvider;
		}

		public abstract void Load(Action onDone);

		public abstract void Unload();

		public abstract GameObject Create(Transform parent, Vector3 localPosition);
	}

	public class MyEntityPrefabProvider : MyEntityProvider
	{
		private GameObject mPrefab;

		private AssetOperationHandle<GameObject> mAssetHandle;

		private IResourceLoader mResourceLoader;

		private IMyEntityIndexHandler mMyEntityIndexHandler;

		public GameObject Prefab => mPrefab;

		public MyEntityPrefabProvider(ECViewEntityData entity, IServiceProvider serviceProvider)
			: base(entity, serviceProvider)
		{
			mResourceLoader = EntityLoadServices.Require<IResourceLoader>(serviceProvider);
			mMyEntityIndexHandler = EntityLoadServices.Require<IMyEntityIndexHandler>(serviceProvider);
		}

		public override GameObject Create(Transform parent, Vector3 localPosition)
		{




			return EntityLoadServices.Require<IEntityLoadInstantiation>(Provider).Instantiate(mPrefab, parent.position + localPosition, Quaternion.identity, parent);
		}

		public override void Load(Action onDone)
		{
			if (IsLoadCompleted)
			{
				onDone?.Invoke();
				return;
			}
			if (mAssetHandle != null)
			{
				mAssetHandle.Completed += (AssetOperationHandle<GameObject> _) =>
				{
					onDone?.Invoke();
				};
				return;
			}
			string entityPath = mMyEntityIndexHandler.GetEntityPath(mEntity.ClassId);
			mAssetHandle = mResourceLoader.LoadAssetAsync<GameObject>(entityPath);
			mAssetHandle.Completed += (AssetOperationHandle<GameObject> handle) =>
			{


				if ((int)handle.Status == 3 || (Object)(object)handle.Result == (Object)null)
				{
					UnityEngine.Debug.LogError("Original entity loading diagnostic");
				}
				mPrefab = handle.Result;
				IsLoadCompleted = true;
				onDone?.Invoke();
			};
		}

		public void GetComponentAsync<T>(Action<T> callback) where T : Component
		{
			if ((Object)(object)mPrefab != (Object)null)
			{
				callback?.Invoke(mPrefab.GetComponent<T>());
			}
			else if (mAssetHandle != null)
			{
				mAssetHandle.Completed += (AssetOperationHandle<GameObject> _) =>
				{
					OnLoaded();
				};
			}
			else
			{
				Load(OnLoaded);
			}
			void OnLoaded()
			{
				callback?.Invoke(((Object)(object)mPrefab != (Object)null) ? mPrefab.GetComponent<T>() : default(T));
			}
		}

		public override void Unload()
		{
			IsLoadCompleted = false;
			mPrefab = null;
			if (mAssetHandle != null)
			{
				mAssetHandle.Dispose();
				mAssetHandle = null;
			}
		}
	}

	private static readonly Vector3 kCreatePositionOffset = new Vector3(0f, -10000f, -10000f);

	private const float kTTLSecondes = 60f;

	private IEntityLoadContainer mMyContainer;

	private Queue<MyEntityRequestInfo> mCreateMyEntityCallbackQueue = new Queue<MyEntityRequestInfo>();

	private Dictionary<int, MyEntityProvider> mMyEntityGoCaches = new Dictionary<int, MyEntityProvider>();

	protected TTLGameObjectPoolCollection<int, MyEntity> mMyEntityPoolCollection;

	private IDisposable mLowMemorySubscription;

	private bool mFixPosBeforeReturn;

	private bool mLoadingMyEntity;

	public static Comparison<ECViewEntityData> sEntityIdSortFunc { get; } = (ECViewEntityData a, ECViewEntityData b) => a.Id.CompareTo(b.Id);

	private IServiceProvider ServiceProvider { get; set; }

	private IApplicationService ApplicationService { get; set; }

	public MyEntityLoadHandler(IServiceProvider serviceProvider, IEntityLoadContainer container, bool fixPosBeforeReturn = false)
	{
		ServiceProvider = serviceProvider;
		mMyEntityPoolCollection = new TTLGameObjectPoolCollection<int, MyEntity>(EntityLoadServices.Require<IEntityPoolScheduler>(serviceProvider));
		mMyContainer = container;
		mFixPosBeforeReturn = fixPosBeforeReturn;
		ApplicationService = EntityLoadServices.Require<IApplicationService>(ServiceProvider);
		if (!EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).IsMapForbidLowMemoryDeal)
		{
			mLowMemorySubscription = ApplicationService.SubscribeLowMemory(OnLowMemory);
		}
	}

	public void Dispose()
	{
		mLowMemorySubscription?.Dispose();
		mCreateMyEntityCallbackQueue.Clear();
		foreach (MyEntityProvider value in mMyEntityGoCaches.Values)
		{
			value.Unload();
		}
		mMyEntityGoCaches.Clear();
		mMyEntityPoolCollection.Dispose();
	}

	public IEnumerator LoadForInit(Action<MyEntity> onLoadedEntity)
	{
		IEntityLoadSharedContainer container = mMyContainer.Container;
		EcEntityIdentifier id = container.RootEntity.Id;
		List<ECViewEntityData> entList = new List<ECViewEntityData>(container.EntityDict.Count);
		foreach (KeyValuePair<EcEntityIdentifier, ECViewEntityData> item in container.EntityDict)
		{
			if (id != item.Key)
			{
				entList.Add(item.Value);
			}
		}
		entList.Sort(sEntityIdSortFunc);
		bool rootEntityLoaded = false;
		CreateMyEntity(container.RootEntity, newEntity: false, (MyEntity myEntity) =>
		{
			onLoadedEntity?.Invoke(myEntity);
			rootEntityLoaded = true;
		});
		yield return (object)new WaitUntil((Func<bool>)(() => rootEntityLoaded));
		yield return LoadEntitiesYield(entList, onLoadedEntity);
	}

	private IEnumerator LoadEntitiesYield(List<ECViewEntityData> entList, Action<MyEntity> onLoadedEntity)
	{
		IEntityLoadSharedContainer container = mMyContainer.Container;
		UnityEngine.Debug.Log("Original entity loading diagnostic");
		List<MyEntity> loadEntityList = new List<MyEntity>();
		int totalCount = entList.Count;
		foreach (ECViewEntityData ent in entList)
		{
			CreateMyEntity(ent, newEntity: false, (MyEntity myEntity) =>
			{
				if ((Object)(object)myEntity != (Object)null)
				{
					loadEntityList.Add(myEntity);
				}
				else
				{
					int num = totalCount - 1;
					totalCount = num;
				}
			});
		}
		yield return (object)new WaitUntil((Func<bool>)(() => loadEntityList.Count >= totalCount));
		foreach (MyEntity item in loadEntityList)
		{
			if (container.EntityDict.ContainsKey(item.Id))
			{
				onLoadedEntity?.Invoke(item);
				continue;
			}
			UnityEngine.Debug.LogWarning("Original entity loading diagnostic");
		}
		UnityEngine.Debug.Log("Original entity loading diagnostic");
	}

	public void PreLoadGameObject(int entityClassId, Action<MyEntity> callback = null)
	{
		ECViewEntityData commonPrefab = EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).GetCommonPrefab(entityClassId);
		if (commonPrefab == null)
		{
			UnityEngine.Debug.LogError("Original entity loading diagnostic");
		}
		else
		{
			CreateMyEntity(commonPrefab, newEntity: true, callback ?? new Action<MyEntity>(DestroyMyEntity));
		}
	}

	private MyEntityProvider GetEntityProvider(ECViewEntityData entity)
	{
		if (!mMyEntityGoCaches.TryGetValue(entity.ClassId, out var value))
		{
			value = new MyEntityPrefabProvider(entity, ServiceProvider);
			mMyEntityGoCaches.Add(entity.ClassId, value);
		}
		return value;
	}

	private bool TryGetEntityProvider(ECViewEntityData entity, out MyEntityProvider provider)
	{
		return mMyEntityGoCaches.TryGetValue(entity.ClassId, out provider);
	}

	public void GetPrefabComponentAsync<T>(ECViewEntityData entity, Action<T> callback) where T : Component
	{
		if (GetEntityProvider(entity) is MyEntityPrefabProvider myEntityPrefabProvider)
		{
			myEntityPrefabProvider.GetComponentAsync(callback);
		}
		else
		{
			callback?.Invoke(default);
		}
	}

	public void CreateMyEntity(ECViewEntityData entity, bool newEntity, Action<MyEntity> callback)
	{
		MyEntityProvider entityProvider = GetEntityProvider(entity);
		MyEntityRequestInfo item = new MyEntityRequestInfo(entity, newEntity, entityProvider, callback);
		mCreateMyEntityCallbackQueue.Enqueue(item);
		entityProvider.Load(OnLoadMyEntity);
	}

	public void OnBeforeRemoveAllEntities()
	{
	}

	public void DestroyMyEntity(MyEntity ent)
	{

		if ((Object)(object)ent == (Object)null)
		{
			return;
		}
		if (EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).UseGameObjectPool)
		{
			TTLGameObjectPool<MyEntity> orAddMyEntityPool = GetOrAddMyEntityPool(ent.Entity);
			if (orAddMyEntityPool == null)
			{
				Object.Destroy((Object)(object)((Component)ent).gameObject);
				return;
			}
			if (mFixPosBeforeReturn)
			{
				((Component)ent).transform.position = new Vector3(-5000f, -5000f, 0f);
			}
			orAddMyEntityPool.Return(ent);
		}
		else
		{
			GetEntityProvider(ent.Entity);
			Object.Destroy((Object)(object)((Component)ent).gameObject);
		}
	}

	private void OnLoadMyEntity()
	{
		using (ProfilerScope.Sample("MyEntityLoadHandler.OnLoadMyEntity"))
		{
			if (mLoadingMyEntity)
			{
				return;
			}
			mLoadingMyEntity = true;
			try
			{
				while (mCreateMyEntityCallbackQueue.Count > 0)
				{
					MyEntityRequestInfo req = mCreateMyEntityCallbackQueue.Peek();
					if (!req.GoProvider.IsLoadCompleted)
					{
						break;
					}
					mCreateMyEntityCallbackQueue.Dequeue();
					try
					{
						DoCreateMyEntity(req);
					}
					catch (Exception exception)
					{
						UnityEngine.Debug.LogException(exception);
						req.Callback?.Invoke(null);
					}
				}
			}
			finally
			{
				mLoadingMyEntity = false;
			}
		}
	}

	private void DoCreateMyEntity(MyEntityRequestInfo req)
	{
		if ((Object)(object)mMyContainer.Host == (Object)null || (Object)(object)mMyContainer.Host.gameObject == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Original entity loading diagnostic");
		}
		else
		{
			DoCreateMyEntity(req.Entity, req.NewEntity, req.Callback, req.GoProvider);
		}
	}

	private void DoCreateMyEntity(ECViewEntityData entity, bool newEntity, Action<MyEntity> callback, MyEntityProvider entityProvider)
	{
		MyEntity orCreateMyEntity = GetOrCreateMyEntity(entity, entityProvider);
		if ((Object)(object)orCreateMyEntity == (Object)null)
		{
			UnityEngine.Debug.LogError("Original entity loading diagnostic");
			callback?.Invoke(null);
		}
		else
		{
			orCreateMyEntity.Bind(mMyContainer.LifecycleContainer, entity, newEntity);
			if (EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).IsRemoving(entity))
			{
				orCreateMyEntity.ImmediateToEnd();
				DestroyMyEntity(orCreateMyEntity);
				callback?.Invoke(null);
			}
			else
			{
				((Component)orCreateMyEntity).gameObject.SetActive(true);
				callback?.Invoke(orCreateMyEntity);
			}
		}
	}

	private MyEntity GetOrCreateMyEntity(ECViewEntityData entity, MyEntityProvider provider)
	{




		Vector3 entitySpawnLocalPosition = GetEntitySpawnLocalPosition(entity);
		if (EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).UseGameObjectPool)
		{
			MyEntity myEntity = GetOrAddMyEntityPool(entity)?.Borrow(mMyContainer.EntitySlot, autoActive: false);
			if ((Object)(object)myEntity == (Object)null)
			{
				return null;
			}
			((Component)myEntity).transform.localPosition = entitySpawnLocalPosition;
			return myEntity;
		}
		return provider.Create(mMyContainer.EntitySlot, entitySpawnLocalPosition).GetComponent<MyEntity>();
	}

	private TTLGameObjectPool<MyEntity> GetOrAddMyEntityPool(ECViewEntityData entity)
	{
		if (!mMyEntityPoolCollection.TryGetPool(entity.ClassId, out var pool))
		{
			TryGetEntityProvider(entity, out var provider);
			if (!(provider is MyEntityPrefabProvider myEntityPrefabProvider) || (Object)(object)myEntityPrefabProvider.Prefab == (Object)null)
			{
				UnityEngine.Debug.LogError("Original entity loading diagnostic");
				return null;
			}
			pool = new TTLGameObjectPool<MyEntity>(new GameObjectPoolEmbed<MyEntity>(mMyContainer.Host.transform, myEntityPrefabProvider.Prefab), 60f);
			mMyEntityPoolCollection.TryAdd(entity.ClassId, pool);
		}
		return pool;
	}

	public virtual void OnLowMemory()
	{
		HashSet<int> hashSet = new HashSet<int>();
		foreach (MyEntityRequestInfo item in mCreateMyEntityCallbackQueue)
		{
			hashSet.Add(item.Entity.ClassId);
		}
		mMyEntityPoolCollection.Release();
		List<int> list = new List<int>();
		int num = default;
		MyEntityProvider myEntityProvider = default;
		foreach (KeyValuePair<int, MyEntityProvider> mMyEntityGoCache in mMyEntityGoCaches)
		{
			num = mMyEntityGoCache.Key;
			myEntityProvider = mMyEntityGoCache.Value;
			int num2 = num;
			if (!hashSet.Contains(num2) && !mMyContainer.ExistEntityGroupByClassId(num2))
			{
				list.Add(num2);
			}
		}
		foreach (int item2 in list)
		{
			if (mMyEntityGoCaches.TryGetValue(item2, out var value))
			{
				mMyEntityPoolCollection.Remove(item2);
				value.Unload();
				mMyEntityGoCaches.Remove(item2);
			}
		}
		UnityEngine.Debug.Log("Original entity loading diagnostic");
	}

	private Vector3 GetEntitySpawnLocalPosition(ECViewEntityData ent)
	{


		if (!EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).HasGridGround(ent))
		{
			return Vector3.zero;
		}
		return kCreatePositionOffset;
	}

	static MyEntityLoadHandler()
	{


	}
}

}
