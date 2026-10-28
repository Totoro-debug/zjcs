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

public abstract class MapControllerEntityRadarTipBase : MonoBehaviour, IGameObjectPoolItem
{
	public enum RadarTipType
	{
		Combinate,
		StageCost,
		StageCamp,
		FastKill
	}

	public HorizontalAnchor HorizontalAnchor;

	public VerticalAnchor VerticalAnchor;

	public Vector3 FixedOffset;

	[Header("Anim")]
	public Animation Animation;

	public string ShowClip;

	public string HideClip;

	private bool mAnimPlaying;

	protected Action<MapControllerEntityRadarTipBase> mOnHideAnimCompletedCallback;

	public abstract RadarTipType TipType { get; }

	public MyEntity MyEntity { get; private set; }

	public virtual void Init(MyEntity myEnt)
	{





		MyEntity = myEnt;
		Transform transform = ((Component)this).transform;
		transform.localPosition = GetTipPosition(myEnt);
		if (transform.lossyScale.x < 0f)
		{
			Vector3 localScale = transform.localScale;
			localScale.x *= -1f;
			transform.localScale = localScale;
		}
	}

	public abstract bool CanShowTip(MyEntity entity);

	protected virtual Vector3 GetTipPosition(MyEntity entity)
	{



		return MapMyEntityUtils.GetOffsetByAnchor(entity, HorizontalAnchor, VerticalAnchor) + FixedOffset;
	}

	public virtual void Show()
	{
		if (!((Object)(object)Animation == (Object)null))
		{
			Animation.Play(ShowClip);
		}
	}

	public virtual void Hide(bool immidietaly, Action<MapControllerEntityRadarTipBase> hideCallback = null)
	{
		mOnHideAnimCompletedCallback = hideCallback;
		if (immidietaly)
		{
			OnHideAnimCompleted();
			return;
		}
		if ((Object)(object)Animation == (Object)null)
		{
			OnHideAnimCompleted();
			return;
		}
		mAnimPlaying = true;
		Animation.Play(HideClip);
	}

	protected virtual void OnHideAnimCompleted()
	{
		mAnimPlaying = false;
		mOnHideAnimCompletedCallback?.Invoke(this);
		mOnHideAnimCompletedCallback = null;
	}

	private void OnEnable()
	{
		if (mAnimPlaying)
		{
			OnHideAnimCompleted();
		}
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{

		((Component)this).transform.localScale = Vector3.one;
	}
}

}
