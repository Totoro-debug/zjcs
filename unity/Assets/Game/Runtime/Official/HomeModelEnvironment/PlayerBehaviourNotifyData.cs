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
public struct PlayerBehaviourNotifyData
{
	public string BehaviourId;

	public BehaviourActTiming ActTiming;

	public bool Immediately;
}
}
