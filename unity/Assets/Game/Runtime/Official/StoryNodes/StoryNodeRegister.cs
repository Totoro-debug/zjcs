// Migrated official source; see map14-story-nodes-runtime source receipt.
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryNodes
{
public class StoryNodeRegister : MonoBehaviour
{
	public List<string> StoryNodeKeys;

	public List<GameObject> StoryNodes;

	protected List<string> mMissingRefKeys = new List<string>();

	public IStoryNodeRegisterMgr NodeRegisterManager { get; set; }

	private void OnEnable()
	{
		Register();
	}

	private void OnDisable()
	{
		Unregister();
	}

	protected void Register()
	{
		mMissingRefKeys.Clear();
		int count = StoryNodes.Count;
		int i = 0;
		for (int count2 = StoryNodeKeys.Count; i < count2 && i < count; i++)
		{
			string item = StoryNodeKeys[i];
			if ((Object)(object)StoryNodes[i] == (Object)null)
			{
				mMissingRefKeys.Add(item);
			}
			else
			{
				NodeRegisterManager.RegisterNode(StoryNodeKeys[i], StoryNodes[i]);
			}
		}
		if (mMissingRefKeys.Count > 0)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
	}

	protected void Unregister()
	{
		int i = 0;
		for (int count = StoryNodeKeys.Count; i < count; i++)
		{
			NodeRegisterManager.UnregisterNode(StoryNodeKeys[i]);
		}
	}
}

}
