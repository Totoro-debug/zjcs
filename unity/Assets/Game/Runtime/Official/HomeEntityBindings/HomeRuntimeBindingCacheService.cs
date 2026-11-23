// Selected original home binding implementation; see the source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityBindings
{
using Object = UnityEngine.Object;
public class HomeRuntimeBindingCacheService : IHomeRuntimeBindingCacheService
{
	private readonly HomeBindingCacheBuilder mBuilder = new HomeBindingCacheBuilder();

	private readonly IHomeEntityManager mHomeEntityManager;

	private readonly Dictionary<EHomeActorBindingProfile, HomeRuntimeBindingSnapshot> mSnapshots = new Dictionary<EHomeActorBindingProfile, HomeRuntimeBindingSnapshot>();

	private bool mDirty = true;

	public HomeRuntimeBindingCacheService(IHomeEntityManager homeEntityManager)
	{
		mHomeEntityManager = homeEntityManager;
	}

	public HomeRuntimeBindingSnapshot GetSnapshot(EHomeActorBindingProfile profile)
	{
		EnsureBuilt();
		if (mSnapshots.TryGetValue(profile, out var value))
		{
			return value;
		}
		HomeRuntimeBindingSnapshot homeRuntimeBindingSnapshot = new HomeRuntimeBindingSnapshot();
		mSnapshots[profile] = homeRuntimeBindingSnapshot;
		return homeRuntimeBindingSnapshot;
	}

	public void MarkDirty()
	{
		mDirty = true;
	}

	private void EnsureBuilt()
	{
		if (mDirty || mSnapshots.Count <= 0)
		{
			mSnapshots.Clear();
			mBuilder.BuildSnapshots(mHomeEntityManager, mSnapshots);
			mDirty = false;
		}
	}
}
}
