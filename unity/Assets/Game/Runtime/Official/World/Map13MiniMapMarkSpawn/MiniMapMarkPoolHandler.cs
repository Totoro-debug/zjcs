using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
public class MiniMapMarkPoolHandler
{
	private Transform mDefaultSlot;

	private CancellationTokenSource mCancellationTokenSource = new CancellationTokenSource();

	private Dictionary<string, LinkedList<MiniMapMarkUIBase>> mMarkPoolDict = new Dictionary<string, LinkedList<MiniMapMarkUIBase>>();

	private Dictionary<string, GameObject> mMarkPrefabDict = new Dictionary<string, GameObject>();

	private MiniMapSpawnDisposableBag mDisposableBag;

	private MiniMapSpawnContext Context { get; }

	public MiniMapMarkPoolHandler(Transform defaultSlot, MiniMapSpawnContext context)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		mDefaultSlot = defaultSlot;
		Context = context;
		mDisposableBag = default;
	}

	public void Release()
	{
		StopClearPool();
		mMarkPrefabDict.Clear();
		mMarkPoolDict.Clear();
		mDisposableBag.Dispose();
	}

	public MiniMapMarkUIBase Borrow(int entityClassId, string markPath, Transform slot)
	{
		if (!mMarkPrefabDict.TryGetValue(markPath, out var value))
		{
			value = MiniMapSpawnDisposable.AddTo(Context.LoadMarkPrefab(markPath), ref mDisposableBag).Result;
			if ((Object)(object)value == (Object)null)
			{
				return null;
			}
			mMarkPrefabDict[markPath] = value;
		}
		MiniMapMarkUIBase mariUiByPool = GetMariUiByPool(markPath, slot, value);
		mariUiByPool.OnGameObjectPoolReset();
		((Component)mariUiByPool).gameObject.SetActive(true);
		mariUiByPool.MarkAssetPath = markPath;
		return mariUiByPool;
	}

	public void Return(MiniMapMarkUIBase markUi)
	{
		markUi.OnGameObjectPoolClear();
		if (!mMarkPoolDict.TryGetValue(markUi.MarkAssetPath, out var value))
		{
			Object.Destroy((Object)(object)((Component)markUi).gameObject);
			return;
		}
		((Component)markUi).gameObject.SetActive(false);
		value.AddLast(markUi);
	}

	public void StopClearPool()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}

	public void DelayClearPool(float delayS)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		mCancellationTokenSource = new CancellationTokenSource();
		UniTaskExtensions.Forget(DoLelayDestroYield(delayS, mCancellationTokenSource.Token));
	}

	private MiniMapMarkUIBase GetMariUiByPool(string markPath, Transform slot, GameObject prefab)
	{
		if (!mMarkPoolDict.TryGetValue(markPath, out var value))
		{
			LinkedList<MiniMapMarkUIBase> linkedList = (mMarkPoolDict[markPath] = new LinkedList<MiniMapMarkUIBase>());
			value = linkedList;
		}
		if (value.Count <= 0)
		{
			return Context.InstantiateMark(prefab, slot, false).GetComponent<MiniMapMarkUIBase>();
		}
		MiniMapMarkUIBase value2 = value.Last.Value;
		value.RemoveLast();
		return value2;
	}

private async UniTask DoLelayDestroYield(float delayS, CancellationToken cancellationToken)
    {
        await UniTask.WaitForSeconds(delayS, false, (PlayerLoopTiming)8, cancellationToken, false);
        foreach (LinkedList<MiniMapMarkUIBase> current in mMarkPoolDict.Values)
        {
            if (current.Count > 0)
            {
                LinkedListNode<MiniMapMarkUIBase> node = current.First;
                do
                {
                    GameObject gameObject = node.Value.gameObject;
                    LinkedListNode<MiniMapMarkUIBase> previous = node;
                    node = previous.Next;
                    current.Remove(previous);
                    Object.Destroy(gameObject);
                } while (node != null);
            }
        }
        mCancellationTokenSource = null;
    }
}

}
