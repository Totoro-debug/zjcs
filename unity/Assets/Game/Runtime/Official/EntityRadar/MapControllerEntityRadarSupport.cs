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

[RequireComponent(typeof(MyEntity))]
public class MapControllerEntityRadarSupport : MonoBehaviour, IMapControllerEntityRadarSupport
{
	public IRadarRemovalEvents RemovalEvents { get; set; }

	[SerializeField]
	protected MapControllerEntityRadarTipBase mRadarTipPrefab;

	[SerializeField]
	protected Transform mRadarEffectSlot;

	protected MyEntity mMyEntity;

	protected MapControllerEntityRadarTipProviderBase mRadarTipProvider;

	protected MapControllerEntityRadarTipProviderBase mOverriderRadarTipProvider;

	public MapControllerEntityRadarTipBase RadarTipPrefab => mRadarTipPrefab;

	public MyEntity MyEntity => mMyEntity;

	public GameObject GammObject => ((Component)this).gameObject;

	public Transform RadarEffectSlot => mRadarEffectSlot;

	public MyContainer MyContainer
	{
		get
		{
			if (!((Object)(object)MyEntity == (Object)null))
			{
				return (MyContainer)MyEntity.myContainer;
			}
			return null;
		}
	}

	public MapControllerEntityRadarTipProviderBase RadarTipProvider => mOverriderRadarTipProvider ?? mRadarTipProvider;

	private void Awake()
	{
		mMyEntity = ((Component)this).GetComponent<MyEntity>();
		InitProvider();
		InitData();
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

	protected virtual void OnDisable()
	{
		mRadarTipProvider.OnDisable();
		mOverriderRadarTipProvider = null;
	}
}

}
