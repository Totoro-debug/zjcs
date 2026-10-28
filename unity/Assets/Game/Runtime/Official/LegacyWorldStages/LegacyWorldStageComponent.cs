// Consumed original MyComponent members, selected mechanically.
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.LegacyWorldStages
{
public abstract class LegacyWorldStageComponent : Zjcs.Official.EntityLifecycle.MyComponent
{
	protected new ECViewEntityBinding _myEntity;

	public new ECViewEntityBinding myEntity
	{
		get
		{
			if ((Object)(object)_myEntity == (Object)null)
			{
				_myEntity = ((Component)this).GetComponent<ECViewEntityBinding>();
			}
			return _myEntity;
		}
	}

	public override void OnStart(bool newEntity)
	{
	}

	public override void OnEnd(object userData)
	{
	}

	public LegacyWorldStageComponentData Component;

	protected virtual void Awake()
	{
	}

	public override void Bind()
	{
		Component = myEntity.Entity.GetComponent<LegacyWorldStageComponentData>();
	}
}
}
