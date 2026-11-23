// Selected original home entity creation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityCreation
{
using Object = UnityEngine.Object;
public class FurnitureEntityCreator : BaseHomeEntityCreator<EFurniture>
{
	private readonly Dictionary<EFurniture, EFurniture> mCreateMapping;

	public FurnitureEntityCreator(Dictionary<EFurniture, Transform> slotDict, MonoDictionary<EFurniture, EFurniture> createMapping)
		: base(slotDict)
	{
		mCreateMapping = new Dictionary<EFurniture, EFurniture>();
		if (createMapping == null)
		{
			return;
		}
		EFurniture eFurniture = default;
		EFurniture eFurniture2 = default;
		foreach (KeyValuePair<EFurniture, EFurniture> item in createMapping)
		{
			item.Deconstruct(out eFurniture, out eFurniture2);
			EFurniture key = eFurniture;
			EFurniture value = eFurniture2;
			mCreateMapping[key] = value;
		}
	}

	protected override EFurniture ResolveKey(EFurniture key)
	{
		return (mCreateMapping.TryGetValue(key, out var value) ? value : key);
	}

	protected override int GetCurrentSkinId(EFurniture key, IHomeEntityManagerContext context)
	{
		return context.PlayerData.GetFurnitureSkinId(key);
	}

	protected override string GetPrefabPath(int id, IHomeEntityManagerContext context)
	{
		if (TryGetSkinOverridePrefabName(id, context, out var prefabName))
		{
			return context.PrefabPaths.GetFurniturePrefabPath(id, prefabName);
		}
		return context.PrefabPaths.GetFurniturePrefabPath(id);
	}

	protected override void OnAfterSkinChanged(EFurniture key, int id, IHomeEntityManagerContext manager)
	{
		manager.FurnitureChangePublisher.Publish(new HomeFurnitureChangeData
		{
			Furniture = key
		});
	}

	protected override string GetEntityTypeName()
	{
		return "家具";
	}
}
}
