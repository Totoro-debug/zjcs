// Selected original home model light/clock implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Spine;
using Spine.Unity;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.ProfessionUI;
using IHomeEntityManager = Zjcs.Official.HomeEntityRuntime.IHomeEntityManager;
namespace Zjcs.Official.HomeModelEnvironment
{
using Object = UnityEngine.Object;
public class HomeTimeDriveClock : MonoBehaviour
{
	[SerializeField]
	private List<SkeletonAnimation> mSkeletonAnimationList;
	public IHomeModelEnvironmentServices Services { get; set; }

	private void Awake()
	{
		foreach (SkeletonAnimation mSkeletonAnimation in mSkeletonAnimationList)
		{
			mSkeletonAnimation.UpdateTiming = (UpdateTiming)0;
		}
	}

	private void Update()
	{
		float dayProgress = Services.HomeDayTime.GetDayProgress(Services.TimeConfiguration);
		foreach (SkeletonAnimation mSkeletonAnimation in mSkeletonAnimationList)
		{
			TrackEntry current2 = mSkeletonAnimation.state.GetCurrent(0);
			current2.TrackTime = dayProgress * current2.Animation.Duration;
			mSkeletonAnimation.Update(0f);
		}
	}
}
}
