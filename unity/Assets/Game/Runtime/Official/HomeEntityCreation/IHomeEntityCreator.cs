// Selected original home entity creation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityCreation
{
using Object = UnityEngine.Object;
public interface IHomeEntityCreator<TKey> : IHomeEntityCreator where TKey : Enum
{
	GameObject GetOrCreate(TKey key, IHomeEntityManagerContext context);

	void OnSkinChanged(TKey key, int id, IHomeEntityManagerContext context, out bool isCreateNew);
}
public interface IHomeEntityCreator
{
	void ReleaseAll(IHomeEntityManagerContext context);

	void ReCreateAll(IHomeEntityManagerContext context);
}
}
