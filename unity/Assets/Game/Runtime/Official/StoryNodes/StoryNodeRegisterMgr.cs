// Migrated official source; see map14-story-nodes-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.StoryNodes
{
public class StoryNodeRegisterMgr : IStoryNodeRegisterMgr, IProfessionStoryNodeRegister
{
	private Dictionary<string, GameObject> mStoryRegistedNodes = new Dictionary<string, GameObject>();


	public event Action<string> OnRegisterNode;

	public event Action<string> OnUnregisterNode;

	public void RegisterNode(string name, GameObject node)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)node == (Object)null)
		{
			UnityEngine.Debug.LogError("注册剧情外部节点失败，节点为空，key:" + name);
		}
		else
		{
			mStoryRegistedNodes[name] = node;
			OnRegisterNode?.Invoke(name);
		}
	}

	public void UnregisterNode(string name)
	{
		mStoryRegistedNodes.Remove(name);
		OnUnregisterNode?.Invoke(name);
	}

	public bool TryGetNode(string name, out GameObject node)
	{
		return mStoryRegistedNodes.TryGetValue(name, out node);
	}

	public GameObject GetNode(string name)
	{
		TryGetNode(name, out var node);
		return node;
	}

	public T GetNode<T>(string name) where T : MonoBehaviour
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		GameObject node = GetNode(name);
		if ((Object)(object)node == (Object)null)
		{
			return default;
		}
		T component = node.GetComponent<T>();
		if ((Object)(object)component == (Object)null)
		{
			UnityEngine.Debug.LogError("获取剧情节点失败，key：" + name);
			return default;
		}
		return component;
	}

	public async UniTask<T> GetNodeAsync<T>(string name, bool isWaitRegister, CancellationToken token) where T : MonoBehaviour
	{
		if (isWaitRegister)
			await UniTask.WaitUntil(() => (Object)(object)GetNode<T>(name) != (Object)null, (PlayerLoopTiming)8, token, false);
		return GetNode<T>(name);
	}

	public async UniTask<GameObject> GetNodeAsync(string name, bool isWaitRegister, CancellationToken token)
	{
		if (isWaitRegister)
			await UniTask.WaitUntil(() => (Object)(object)GetNode(name) != (Object)null, (PlayerLoopTiming)8, token, false);
		return GetNode(name);
	}
}

}
