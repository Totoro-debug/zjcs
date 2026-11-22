// Original client home animation clock; local message boundaries only.
namespace Zjcs.Official.HomeNativeTime
{
using Object = UnityEngine.Object;
public abstract class HomeEnvDrive
{
	protected IHomeNativeTimeManager mManager;

	public virtual void Init(IHomeNativeTimeManager manager)
	{
		mManager = manager;
	}

	public virtual void OnEnable()
	{
	}

	public virtual void OnDisable()
	{
	}

	public virtual void OnDestroy()
	{
	}
}

}
