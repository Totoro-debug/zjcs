// Complete original map-display application class; see source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Official.MapDisplays
{
[DefaultExecutionOrder(-1)]
[RequireComponent(typeof(ECViewEntityBinding))]
[ExecuteAlways]
public class MyMapDisplay : MonoBehaviour, IMyEntityBindPreEventReceiver, IEventReceiver, IMyEntityRemoveEventReceiver
{
	[SerializeField]
	private ECViewEntityBinding mMyEntity;

	public IMapDisplayQueries Queries { get; set; }

	private int mClassId;

	private string mAreaTag;

	private int mMapClassId;

	private GroundType mGroundType;

	private string mTag;

	private IECViewDriver mECViewDriver;

	public IMapDisplayRoot MapRootManagerProxy { get; set; }

	public IMapDisplayManager MapDisplayManager { get; set; }

	public IMapDisplayPosition MyMapPositionHandler { get; set; }

	void IMyEntityBindPreEventReceiver.MyEntity_OnBindPre(ECViewEntityData ent)
	{
		if (!MapDisplayServices.State(ent).Removing)
		{
			mClassId = ent.ClassId;
			mMapClassId = MapRootManagerProxy.CurrentMapClassId;
			mAreaTag = Queries.GetAreaTag(mMapClassId, MyMapPositionHandler.QueryArea(MapDisplayServices.State(ent).GridPosition));
			mGroundType = MapRootManagerProxy.GetGridGroundType(MapDisplayServices.State(ent).GridPosition);
			mTag = MapDisplayServices.State(ent).EntityTag;
			mECViewDriver = MapDisplayManager.Acquire(mClassId, mMapClassId, mAreaTag, mGroundType, mTag, ((Component)this).transform);
		}
	}

	void IMyEntityRemoveEventReceiver.MyEntity_OnRemove(ECViewEntityBinding entity)
	{
		if (!MapRootManagerProxy.Destroying && mECViewDriver != null)
		{
			if (MapDisplayManager != null)
			{
				MapDisplayManager.Recycle(mClassId, mMapClassId, mAreaTag, mGroundType, mTag, mECViewDriver);
			}
			mECViewDriver = null;
		}
	}

	private void Awake()
	{
		((IMapDisplayEntityEvents)mMyEntity).AddBindPreReceiver(this);
		mMyEntity.AddRemoveReceiver(this);
	}

	private void OnDestroy()
	{
		if (!((Object)(object)mMyEntity == (Object)null))
		{
			((IMapDisplayEntityEvents)mMyEntity).RemoveBindPreReceiver(this);
			mMyEntity.RemoveRemoveReceiver(this);
		}
	}
}

}
