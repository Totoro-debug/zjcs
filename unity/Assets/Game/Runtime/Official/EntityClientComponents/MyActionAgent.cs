// Original 871 action-playable client; see mainline character actions source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityClientComponents
{
public class MyActionAgent : MyComponent<ActionAgentComponentData>
{
	[Serializable]
	public class ActionTypePlayable
	{
		[Tooltip("指定ActionType")]
		public ActionType ActionType;

		[Tooltip("ActionType对应播放的Playable。playable的Source和Target都为自己本身。")]
		public GameObject Playable;

		[Tooltip("动作停止时是否停止播放Playable，如冰冻状态结束，停止播放此Playable。若Playable是固定时间则可以不勾选。")]
		public bool StopOnActionEnd;

		[Tooltip("ActionType结束时，对应播放的Playable。playable的Source和Target都为自己本身。")]
		public GameObject EndPlayable;
	}

	[Tooltip("ActionType开始时，对应播放的Playable。playable的Source和Target都为自己本身。")]
	public List<ActionTypePlayable> ActionTypePlayables;

	private Dictionary<ActionType, ActionTypePlayable> mActionTypePlayableDict;

	private Dictionary<EcEntityIdentifier, MyPlayable> mPlayingPlayable;

	private IMyPlayableManager mMyPlayableManager { get; set; }

    public void Configure(IMyPlayableManager playables)
    {
        mMyPlayableManager = playables ?? throw new ArgumentNullException(nameof(playables));
    }

	public event Action<MyActionAgent, ActionAgentActionData> OnMyStartEvent;

	public event Action<MyActionAgent, ActionAgentActionData> OnMyEndEvent;

	protected override void Awake()
	{
		base.Awake();
		int count = ActionTypePlayables.Count;
		if (count <= 0)
		{
			return;
		}
		mActionTypePlayableDict = new Dictionary<ActionType, ActionTypePlayable>(count);
		foreach (ActionTypePlayable actionTypePlayable in ActionTypePlayables)
		{
			mActionTypePlayableDict.Add(actionTypePlayable.ActionType, actionTypePlayable);
		}
	}

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		Component.StartEvent += ActionAgent_OnStart;
		Component.EndEvent += ActionAgent_OnEnd;
	}

	public override void OnEnd(object userData)
	{
		if (mPlayingPlayable != null)
		{
			foreach (KeyValuePair<EcEntityIdentifier, MyPlayable> item in mPlayingPlayable)
			{
				item.Value.Stop(false);
			}
			mPlayingPlayable.Clear();
		}
		Component.StartEvent -= ActionAgent_OnStart;
		Component.EndEvent -= ActionAgent_OnEnd;
		base.OnEnd(userData);
	}

	protected override void OnDestroy()
	{
		mActionTypePlayableDict = null;
		mPlayingPlayable = null;
		base.OnDestroy();
	}

	public bool HasActionType(ActionType actionType)
	{
		return Component.HasActionType(actionType);
	}

	public EcEntityIdentifier FindAction(ActionType actionType)
	{
		return Component.FindAction(actionType);
	}

	public void ActionAgent_OnStart(ActionAgentComponentData actionAgent, ActionAgentActionData action)
	{
		OnMyStartEvent?.Invoke(this, action);
		if (mActionTypePlayableDict == null || !mActionTypePlayableDict.TryGetValue(action.ActionType, out var value))
		{
			return;
		}
		GameObject playable = value.Playable;
		if ((Object)(object)playable == (Object)null)
		{
			return;
		}
		MyPlayable value2 = mMyPlayableManager.Play(playable, ((Component)this).gameObject, ((Component)this).gameObject, 1f, (GameObject)null, (Dictionary<string, object>)null, (IMyPlayableNeedStop)null);
		if (value.StopOnActionEnd)
		{
			if (mPlayingPlayable == null)
			{
				mPlayingPlayable = new Dictionary<EcEntityIdentifier, MyPlayable>();
			}
			mPlayingPlayable.Add(action.EntityId, value2);
			action.EntityEndEvent += Entity_OnEnd;
		}
	}

	public void ActionAgent_OnEnd(ActionAgentComponentData actionAgent, ActionAgentActionData action)
	{
		if (mActionTypePlayableDict != null && mActionTypePlayableDict.TryGetValue(action.ActionType, out var value) && (Object)(object)value.EndPlayable != (Object)null)
		{
			mMyPlayableManager.Play(value.Playable, ((Component)this).gameObject, ((Component)this).gameObject, 1f, (GameObject)null, (Dictionary<string, object>)null, (IMyPlayableNeedStop)null);
		}
		OnMyEndEvent?.Invoke(this, action);
	}

	private void Entity_OnEnd(ActionAgentActionData actionEntity, object mySerializable)
	{
		actionEntity.EntityEndEvent -= Entity_OnEnd;
		EcEntityIdentifier id = actionEntity.EntityId;
		if (mPlayingPlayable != null && mPlayingPlayable.TryGetValue(id, out var value))
		{
			value.Stop(false);
			mPlayingPlayable.Remove(id);
		}
	}
}

}
