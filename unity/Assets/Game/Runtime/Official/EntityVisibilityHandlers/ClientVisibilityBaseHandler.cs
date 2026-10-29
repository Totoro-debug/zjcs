// Migrated original application source; see Map14 visibility-handlers receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver=Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Awaiter=Cysharp.Threading.Tasks.UniTask.Awaiter;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StorySourceFade;
using System;



namespace Zjcs.Official.EntityVisibilityHandlers
{
public abstract class ClientVisibilityBaseHandler : IClientVisibilityHandler
{
	protected bool mVisible;

	public abstract ClientVisibilityType VisibilityType { get; }

	public ECViewEntityData Entity { get; private set; }

	public bool Visible
	{
		get
		{
			return mVisible;
		}
		protected set
		{
			if (mVisible != value)
			{
				mVisible = value;
				OnVisibleChanged?.Invoke(mVisible);
			}
		}
	}

	public event Action<bool> OnVisibleChanged;

	public virtual void Init(ECViewEntityData ent, bool isNew)
	{
		Visible = false;
		Entity = ent;
	}

	public virtual void Release()
	{
		Entity = null;
	}
}

}
