// Migrated original MonsterUI; see activity-monster-ui-runtime-source-manifest.json.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.ActivityBattleUI
{
public class MonsterUI : MonoBehaviour
{
	public Transform Slot;

	private int mMonsterId;

	private readonly List<IDisposable> mOperations = new List<IDisposable>();

	// The original scene container supplies parser, loader and injected instantiation.
	[NonSerialized] public Func<int, int?> MonsterIconId;
	[NonSerialized] public IProfessionImageResourceLoader ResourceLoader;
	[NonSerialized] public Func<GameObject, Transform, GameObject> InstantiatePrefab;

	private void OnDestroy()
	{
		for (int i = 0; i < mOperations.Count; i++) mOperations[i].Dispose();
		mOperations.Clear();
	}

	public void Init(int monsterId)
	{
		if (mMonsterId == monsterId)
		{
			return;
		}
		mMonsterId = monsterId;
		// Original TransformExtensions.DestroyChildren defaults: include inactive, no detach.
		for (int i = Slot.childCount - 1; i >= 0; i--)
			UnityEngine.Object.Destroy(Slot.GetChild(i).gameObject);
		if (MonsterIconId == null || ResourceLoader == null || InstantiatePrefab == null)
			throw new InvalidOperationException("Original MonsterUI scene services are unbound.");
		int? iconId = MonsterIconId(monsterId);
		if (!iconId.HasValue)
		{
			Debug.LogError("不存在怪物配置，id:" + monsterId, this);
			return;
		}
		string monsterSpineIcon = $"Appearance/Monster/{iconId.Value}/MonsterIcon_{iconId.Value}";
		var operation = ResourceLoader.LoadAsset<GameObject>(monsterSpineIcon);
		mOperations.Add(operation);
		GameObject result = operation.Result;
		if ((UnityEngine.Object)(object)result == (UnityEngine.Object)null)
		{
			Debug.LogError("不存在Icon:" + monsterSpineIcon, this);
		}
		else
		{
			InstantiatePrefab(result, Slot).transform.localPosition = Vector3.zero;
		}
	}
}
}
