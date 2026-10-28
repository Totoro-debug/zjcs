// Complete original map-display application class; see source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.StoryPlayables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.MapDisplays
{
public class MapDisplayManager : IDisposable, IMapDisplayManager
{
	private Dictionary<EntityMapDisplayInfo, MapDisplayPoolHandle> mMapDisplayPoolHandles;

	private List<EntityMapDisplayInfo> mClearHandleTemp;

	private Transform mPoolRoot;

	private IDisposable mLowMemorySubscribeDisposable;

	public IMapDisplayRoot MapRootManagerProxy { get; set; }

	public IMapDisplayResourceLoader ResourceLoader { get; set; }

	public IServiceProvider ServiceProvider { get; set; }

	public GAction LowMemory { get; set; }

	public IMapDisplayQueries Queries { get; set; }

	public void Initialize()
	{

		mMapDisplayPoolHandles = new Dictionary<EntityMapDisplayInfo, MapDisplayPoolHandle>();
		mClearHandleTemp = new List<EntityMapDisplayInfo>();
		mPoolRoot = new GameObject("MapDisplayManager").transform;
		mPoolRoot.SetParent(((Component)MapRootManagerProxy.MapRoot).transform);
		((Component)mPoolRoot).gameObject.SetActive(false);
		if (!Queries.IsMapForbidLowMemoryDeal())
		{
			mLowMemorySubscribeDisposable = LowMemory.Subscribe(OnLowMemory);
		}
	}

	public void Dispose()
	{
		mLowMemorySubscribeDisposable?.Dispose();
		mLowMemorySubscribeDisposable = null;
		foreach (MapDisplayPoolHandle value in mMapDisplayPoolHandles.Values)
		{
			value.Dispose();
		}
		mMapDisplayPoolHandles.Clear();
		if ((Object)(object)mPoolRoot != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)mPoolRoot).gameObject);
		}
	}

	public IECViewDriver Acquire(int classId, int mapClassId, string areaTag, GroundType groundType, string tag, Transform content)
	{


		if ((Object)(object)content == (Object)null)
		{
			return null;
		}
		int groupId = Queries.GetMapGroupId(mapClassId);
		EntityMapDisplayInfo entityMapDisplayInfo = Queries.Selector.Get(classId, groupId, mapClassId, areaTag, groundType, tag);
		if (entityMapDisplayInfo == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return null;
		}
		if (!mMapDisplayPoolHandles.TryGetValue(entityMapDisplayInfo, out var value))
		{
			value = new MapDisplayPoolHandle(entityMapDisplayInfo, ResourceLoader, ServiceProvider);
			mMapDisplayPoolHandles.Add(entityMapDisplayInfo, value);
		}
		IECViewDriver iECViewDriver = value.Acquire();
		if (iECViewDriver == null)
		{
			return null;
		}
		Transform transform = ((Component)(MonoBehaviour)iECViewDriver).transform;
		transform.SetParent(content);
		transform.SetAsFirstSibling();
		transform.localPosition = Vector3.zero;
		((Component)transform).gameObject.SetActive(true);
		return iECViewDriver;
	}

	public void Recycle(int classId, int mapClassId, string areaTag, GroundType groundType, string tag, IECViewDriver driver)
	{

		if ((Object)(object)MapRootManagerProxy.MapRoot == (Object)null)
		{
			return;
		}
		int groupId = Queries.GetMapGroupId(mapClassId);
		EntityMapDisplayInfo entityMapDisplayInfo = Queries.Selector.Get(classId, groupId, mapClassId, areaTag, groundType, tag);
		MapDisplayPoolHandle value;
		if (entityMapDisplayInfo == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
		else if (!mMapDisplayPoolHandles.TryGetValue(entityMapDisplayInfo, out value))
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
		else if (value.Recycle(driver))
		{
			Transform transform = ((Component)(MonoBehaviour)driver).transform;
			((Component)transform).gameObject.SetActive(false);
			transform.SetParent(mPoolRoot);
		}
	}

	private void OnLowMemory()
	{
		foreach (KeyValuePair<EntityMapDisplayInfo, MapDisplayPoolHandle> mMapDisplayPoolHandle in mMapDisplayPoolHandles)
		{
			MapDisplayPoolHandle value = mMapDisplayPoolHandle.Value;
			if (value.LoanedCount <= 0)
			{
				value.Dispose();
				mClearHandleTemp.Add(mMapDisplayPoolHandle.Key);
			}
		}
		for (int num = mClearHandleTemp.Count - 1; num >= 0; num--)
		{
			mMapDisplayPoolHandles.Remove(mClearHandleTemp[num]);
			mClearHandleTemp.RemoveAt(num);
		}
		UnityEngine.Debug.Log("MapDisplayManager.OnLowMemory ended");
	}
}

}
