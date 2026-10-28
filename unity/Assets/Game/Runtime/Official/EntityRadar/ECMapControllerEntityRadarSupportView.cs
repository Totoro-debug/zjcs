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

public class ECMapControllerEntityRadarSupportView : ECViewBase, IMapControllerEntityRadarSupport
{
	public IRadarRemovalEvents RemovalEvents { get; set; }

	[SerializeField]
	protected MapControllerEntityRadarTipBase mRadarTipPrefab;

	[SerializeField]
	protected Transform mRadarEffectSlot;

	protected MyEntity mMyEntity;

	protected MapControllerEntityRadarTipProviderBase mRadarTipProvider;

	protected MapControllerEntityRadarTipProviderBase mOverriderRadarTipProvider;

	protected bool mIsInited;

	public MapControllerEntityRadarTipBase RadarTipPrefab => mRadarTipPrefab;

	public MyEntity MyEntity => mMyEntity;

	public GameObject GammObject => ((Component)mOwner.transform.parent).gameObject;

	public Transform RadarEffectSlot => mRadarEffectSlot;

	public MyContainer MyContainer
	{
		get
		{
			if (!((Object)(object)mMyEntity == (Object)null))
			{
				return (MyContainer)mMyEntity.myContainer;
			}
			return null;
		}
	}

	public MapControllerEntityRadarTipProviderBase RadarTipProvider => mOverriderRadarTipProvider ?? mRadarTipProvider;

	protected override void Load()
	{
		base.Load();
		if (!mIsInited)
		{
			InitProvider();
			InitData();
			mIsInited = true;
		}
		mMyEntity = mOwner.GetComponentInParent<MyEntity>();
	}

	protected virtual void InitProvider()
	{
		mRadarTipProvider = new MapControllerEntityRadarTipProvider(this);
	}

	protected virtual void InitData()
	{
	}

	public void SetOverriderRadarTipProvider(MapControllerEntityRadarTipProviderBase provider)
	{
		MapControllerEntityRadar myOwnerRadar = mRadarTipProvider.MyOwnerRadar;
		mOverriderRadarTipProvider = provider;
		if ((Object)(object)myOwnerRadar != (Object)null)
		{
			mRadarTipProvider.EntityLeave(myOwnerRadar, this);
			mOverriderRadarTipProvider.EntityEnter(myOwnerRadar, this, isInstance: true);
		}
	}

	protected override void Unload()
	{
		mRadarTipProvider.OnDisable();
		mOverriderRadarTipProvider = null;
		base.Unload();
	}
}

}
