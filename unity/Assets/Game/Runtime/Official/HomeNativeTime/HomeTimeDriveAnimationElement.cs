// Original client home animation clock; local message boundaries only.
namespace Zjcs.Official.HomeNativeTime
{
using Object = UnityEngine.Object;
using UnityEngine;

public class HomeTimeDriveAnimationElement : MonoBehaviour
{
	public Animation Animation;
	public IHomeTimeDriveEvents Publisher { get; set; }

	private void OnEnable()
	{
		Publisher.Publish(new HomeTimeDriveAnimationData
		{
			Animation = Animation,
			IsRegister = true
		});
	}

	private void OnDisable()
	{
		Publisher.Publish(new HomeTimeDriveAnimationData
		{
			Animation = Animation,
			IsRegister = false
		});
	}
}

}
