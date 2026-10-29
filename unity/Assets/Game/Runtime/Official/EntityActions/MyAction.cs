// Migrated original application source; see Map14 visibility-handlers receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityActions
{
public class MyAction : MyComponent<ActionComponentData, object>
{
	public GameObject myEffect;

	public bool myEffectStopOnDestroy = true;

	public GameObject myEndEffect;

	public bool myEndEffectStopOnDestroy = true;

	public bool PlayOnStart = true;

	[Header("Playable持续时间（小于0表示无限时长）")]
	public float Duration = -1f;

	[Tooltip("循环的Playable")]
	public GameObject LoopEffectPlayable;

	[Tooltip("循环playable是否互斥")]
	public bool LoopEffectPlayableIsMutext;

	private GameObject mActionEffect;

	protected MyPlayable myPlayable;

	protected MyPlayable myEndPlayable;

	protected MyPlayable myLoopPlayable;

	private float mPlayTime;

	public MyEntity myApplyEntity => ((Zjcs.Official.EntityContainers.MyContainer)myContainer).GetEntity(Component.ApplyId);

	public GameObject ActionEffect
	{
		get
		{
			if ((Object)(object)mActionEffect == (Object)null)
			{
				mActionEffect = GetEffect();
			}
			return mActionEffect;
		}
	}

	public IMyPlayableManager mMyPlayableManager { get; set; }

	public IMyPlayableCenter mMyPlayableCenter { get; set; }

	public override void OnStart(bool newEntity)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)ActionEffect == (Object)null)
		{
			return;
		}
		if ((Object)(object)myApplyEntity == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Original MyAction apply entity is absent.");
		}
		else if (PlayOnStart)
		{
			Play(Duration);
		}
	}

	public override void OnEnd(object userData)
	{
		base.OnEnd(userData);
		mActionEffect = null;
	}

	public virtual void Play(float durtion = -1f)
	{
		if ((Object)(object)ActionEffect == (Object)null)
		{
			return;
		}
		myPlayable = PlayPlayable();
		mPlayTime = 0f;
		Duration = durtion;
		if (!((Object)(object)LoopEffectPlayable != (Object)null))
		{
			return;
		}
		if (LoopEffectPlayableIsMutext)
		{
			if (mMyPlayableCenter.GetPlayableRef(myApplyEntity.Id, myEntity.ClassId) > 0)
			{
				mMyPlayableCenter.AddPlayableRef(myApplyEntity.Id, myEntity.ClassId);
				return;
			}
			myLoopPlayable = mMyPlayableManager.Play(LoopEffectPlayable, ((Component)myApplyEntity).gameObject, null);
			mMyPlayableCenter.AddPlayableRef(myApplyEntity.Id, myEntity.ClassId, myLoopPlayable);
		}
		else
		{
			myLoopPlayable = mMyPlayableManager.Play(LoopEffectPlayable, ((Component)myApplyEntity).gameObject, null);
		}
	}

	protected virtual void Update()
	{
		if (!(Duration < 0f) && !((Object)(object)myPlayable == (Object)null) && myPlayable.myStatus != MyPlayable.PlayableStatus.Stop)
		{
			mPlayTime += Time.deltaTime;
			if (mPlayTime >= Duration)
			{
				Stop();
			}
		}
	}

	protected virtual MyPlayable PlayPlayable()
	{
		return mMyPlayableManager.Play(ActionEffect, ((Component)myApplyEntity).gameObject, null);
	}

	public override void OnRemove(object userData)
	{
		base.OnRemove(userData);
		if ((Object)(object)myPlayable != (Object)null && myEffectStopOnDestroy)
		{
			myPlayable.Stop();
		}
		if ((Object)(object)myEndEffect != (Object)null && (Object)(object)myApplyEntity != (Object)null)
		{
			myEndPlayable = mMyPlayableManager.Play(myEndEffect, ((Component)myApplyEntity).gameObject, null);
			if ((Object)(object)myEndPlayable != (Object)null)
			{
				myEndPlayable.OnStop += new Action<MyPlayable>(OnEndPlayableStop);
			}
		}
		if (LoopEffectPlayableIsMutext)
		{
			mMyPlayableCenter.RemovePlayableRef(myApplyEntity.Id, myEntity.ClassId);
		}
		else if ((Object)(object)myLoopPlayable != (Object)null)
		{
			myLoopPlayable.Stop();
		}
	}

	public void Stop()
	{
		if ((Object)(object)myPlayable != (Object)null)
		{
			myPlayable.Stop();
		}
		if ((Object)(object)myEndEffect != (Object)null && (Object)(object)myApplyEntity != (Object)null)
		{
			myEndPlayable = mMyPlayableManager.Play(myEndEffect, ((Component)myApplyEntity).gameObject, null);
			if ((Object)(object)myEndPlayable != (Object)null)
			{
				myEndPlayable.OnStop += new Action<MyPlayable>(OnEndPlayableStop);
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if ((Object)(object)myPlayable != (Object)null && myEffectStopOnDestroy)
		{
			myPlayable.Stop();
		}
		if ((Object)(object)myEndPlayable != (Object)null && myEndEffectStopOnDestroy)
		{
			myEndPlayable.Stop();
			myEndPlayable.OnStop -= new Action<MyPlayable>(OnEndPlayableStop);
		}
	}

	protected void OnEndPlayableStop(MyPlayable obj)
	{
		myEndPlayable.Stop();
		myEndPlayable.OnStop -= new Action<MyPlayable>(OnEndPlayableStop);
	}

	private GameObject GetEffect()
	{
		return myEffect;
	}
}

}
