// Migrated official source; see map14-story-nodes-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zjcs.Official.StoryNodes
{
public interface IStoryNodeRegisterMgr
{
	event Action<string> OnRegisterNode;

	event Action<string> OnUnregisterNode;

	void RegisterNode(string name, GameObject node);

	void UnregisterNode(string name);

	bool TryGetNode(string name, out GameObject node);

	GameObject GetNode(string name);

	T GetNode<T>(string name) where T : MonoBehaviour;

	UniTask<T> GetNodeAsync<T>(string name, bool isWaitRegister, CancellationToken token) where T : MonoBehaviour;

	UniTask<GameObject> GetNodeAsync(string name, bool isWaitRegister, CancellationToken token);
}

}
