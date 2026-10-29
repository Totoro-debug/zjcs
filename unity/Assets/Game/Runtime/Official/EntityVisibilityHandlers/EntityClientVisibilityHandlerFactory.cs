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
using System.Collections.Generic;

namespace Zjcs.Official.EntityVisibilityHandlers
{
public class EntityClientVisibilityHandlerFactory : IEntityClientVisibilityHandlerFactory
{
	private static HashSet<ClientVisibilityType> sForbidClientVisibilityTypeSet = new HashSet<ClientVisibilityType>
	{
		ClientVisibilityType.NPCVisibility,
		ClientVisibilityType.ConditionVisibility
	};

	private Dictionary<ClientVisibilityType, Queue<IClientVisibilityHandler>> mClientVisibilityBaseHandlerPools = new Dictionary<ClientVisibilityType, Queue<IClientVisibilityHandler>>();

	private List<ClientVisibilityType> mClientVisibilityTypes;

	public IServiceProvider ServiceProvider { get; set; }

	void IEntityClientVisibilityHandlerFactory.GetHandlers(ClientVisibilityType visibilityType, ref List<IClientVisibilityHandler> handlers)
	{
		if (mClientVisibilityTypes == null)
		{
			mClientVisibilityTypes = new List<ClientVisibilityType>();
			foreach (ClientVisibilityType value in Enum.GetValues(typeof(ClientVisibilityType)))
			{
				if (value != ClientVisibilityType.None)
				{
					mClientVisibilityTypes.Add(value);
				}
			}
		}
		foreach (ClientVisibilityType mClientVisibilityType in mClientVisibilityTypes)
		{
			if (visibilityType.HasFlag(mClientVisibilityType))
			{
				IClientVisibilityHandler oneHandler = GetOneHandler(mClientVisibilityType);
				if (oneHandler != null)
				{
					handlers.Add(oneHandler);
				}
			}
		}
	}

	IClientVisibilityHandler IEntityClientVisibilityHandlerFactory.GetHandler(ClientVisibilityType visibilityType)
	{
		return GetOneHandler(visibilityType);
	}

	void IEntityClientVisibilityHandlerFactory.ReturnHanlder(IClientVisibilityHandler clientVisibilityHandler)
	{
		if (clientVisibilityHandler != null)
		{
			if (!mClientVisibilityBaseHandlerPools.TryGetValue(clientVisibilityHandler.VisibilityType, out var value))
			{
				value = new Queue<IClientVisibilityHandler>();
				mClientVisibilityBaseHandlerPools.Add(clientVisibilityHandler.VisibilityType, value);
			}
			value.Enqueue(clientVisibilityHandler);
		}
	}

	private IClientVisibilityHandler GetOneHandler(ClientVisibilityType visibilityType)
	{
		if (sForbidClientVisibilityTypeSet.Contains(visibilityType))
		{
			return null;
		}
		if (!mClientVisibilityBaseHandlerPools.TryGetValue(visibilityType, out var value))
		{
			return CreateHandler(visibilityType);
		}
		if (value.Count > 0)
		{
			return value.Dequeue();
		}
		return CreateHandler(visibilityType);
	}

	private IClientVisibilityHandler CreateHandler(ClientVisibilityType requestType)
	{
		return requestType switch
		{
			ClientVisibilityType.WaitRefreshAction => (IClientVisibilityHandler)VisibilityHandlerConstruction.CreateInstance<ClientVisibilityWaitActionHandler>(ServiceProvider), 
			ClientVisibilityType.WaitAwardItemInited => VisibilityHandlerConstruction.CreateInstance<ClientVisibilityWaitAwardItemInitedHandler>(ServiceProvider), 
			ClientVisibilityType.Hide => VisibilityHandlerConstruction.CreateInstance<ClientVisibilityHideHanlder>(ServiceProvider), 
			_ => null, 
		};
	}
}

}
