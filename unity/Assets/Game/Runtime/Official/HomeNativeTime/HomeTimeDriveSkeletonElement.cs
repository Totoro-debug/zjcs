// Original client home animation clock; local message boundaries only.
namespace Zjcs.Official.HomeNativeTime
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

public class HomeTimeDriveSkeletonElement : MonoBehaviour
{
	public List<SkeletonAnimation> SkeletonAnimations;
	public IHomeTimeDriveEvents Publisher { get; set; }

	private void OnEnable()
	{
		Publisher.Publish(new HomeTimeDriveSkeletonData
		{
			SkeletonAnimationList = SkeletonAnimations,
			IsRegister = true
		});
	}

	private void OnDisable()
	{
		Publisher.Publish(new HomeTimeDriveSkeletonData
		{
			SkeletonAnimationList = SkeletonAnimations,
			IsRegister = false
		});
	}
}

}
