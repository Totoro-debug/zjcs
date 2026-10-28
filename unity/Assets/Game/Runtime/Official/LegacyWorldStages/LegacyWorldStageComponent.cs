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
public abstract class LegacyWorldStageComponent : UnityEngine.MonoBehaviour
{
	protected ECViewEntityBinding _myEntity;

	public ECViewEntityBinding myEntity
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

	public virtual void OnStart(bool newEntity)
	{
	}

	public virtual void OnEnd(object userData)
	{
	}

	public LegacyWorldStageComponentData Component;

	protected virtual void Awake()
	{
	}

	public virtual void Bind()
	{
		Component = myEntity.Entity.GetComponent<LegacyWorldStageComponentData>();
	}
}
}
