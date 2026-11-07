// Original client component; see grid entity runtime source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.BattleStatusPlayables;
using System;

namespace Zjcs.Official.BattleGridItemEntities
{
public class MyActionAgent : MyComponent<ActionAgentComponentData>
{
	public event Action<MyActionAgent, ActionAgentActionData> OnMyStartEvent;

	public event Action<MyActionAgent, ActionAgentActionData> OnMyEndEvent;

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		Component.StartEvent += ActionAgent_OnStart;
		Component.EndEvent += ActionAgent_OnEnd;
	}

	public override void OnEnd(object userData)
	{
		Component.StartEvent -= ActionAgent_OnStart;
		Component.EndEvent -= ActionAgent_OnEnd;
		base.OnEnd(userData);
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
	}

	public void ActionAgent_OnEnd(ActionAgentComponentData actionAgent, ActionAgentActionData action)
	{
		OnMyEndEvent?.Invoke(this, action);
	}
}
}
