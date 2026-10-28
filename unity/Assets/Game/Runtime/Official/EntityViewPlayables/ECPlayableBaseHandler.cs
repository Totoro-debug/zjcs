// Migrated official application source; see entity playable view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StoryPlayableManager;
using System;

namespace Zjcs.Official.EntityViewPlayables
{
public abstract class ECPlayableBaseHandler
{
	protected IECPlayableSupporter mECPlayableSupporterView;

	protected IServiceProvider ServiceProvider { get; private set; }

	public virtual void Init(IECPlayableSupporter ecPlayableSupporterView, IServiceProvider serviceProvider)
	{
		ServiceProvider = serviceProvider;
		mECPlayableSupporterView = ecPlayableSupporterView;
	}

	public virtual void Visible()
	{
	}

	public virtual void Invisible()
	{
	}

	public virtual void Release()
	{
	}
}

}
