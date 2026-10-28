// Migrated official application source; see map14-story-attachment-runtime source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryAttachment
{
public class AttachAgent : MonoBehaviour
{
	[HideInInspector]
	public List<string> myBoneNameList = new List<string>();

	[HideInInspector]
	public List<Transform> myBoneList = new List<Transform>();

	private Dictionary<string, Transform> myBoneMap = new Dictionary<string, Transform>();

	private Dictionary<Transform, int> mBoneActiveCountDict = new Dictionary<Transform, int>();

	private void Awake()
	{
		Init();
	}

	public void Init()
	{
		AttachmentRegistryUtils.FillMapByList(myBoneNameList, myBoneList, ref myBoneMap);
	}

	public void GenerateBoneMap()
	{
		myBoneMap.Clear();
		myBoneMap.Add("", ((Component)this).transform);
		FillBoneMapRecurive(((Component)this).gameObject.transform);
		AttachmentRegistryUtils.FillListByMapSort(myBoneMap, myBoneNameList, myBoneList);
	}

	public void Clear()
	{
		myBoneMap.Clear();
		myBoneMap.Add("", ((Component)this).transform);
	}

	public void AddBoneNode(string name, Transform bone)
	{
		if ((Object)(object)bone == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		else if (!myBoneMap.TryAdd(name, bone))
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
	}

	public bool TryAddBoneEntry(string boneName, Transform bone)
	{
		if ((Object)(object)bone == (Object)null)
		{
			return false;
		}
		boneName = boneName?.Trim();
		if (string.IsNullOrEmpty(boneName))
		{
			return false;
		}
		if (myBoneNameList.Contains(boneName))
		{
			return false;
		}
		myBoneNameList.Add(boneName);
		myBoneList.Add(bone);
		return true;
	}

	public void RemoveBoneEntryAt(int index)
	{
		if (index >= 0 && index < myBoneNameList.Count && index < myBoneList.Count)
		{
			myBoneNameList.RemoveAt(index);
			myBoneList.RemoveAt(index);
		}
	}

	private void FillBoneMapRecurive(Transform parentBone)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected Obj, but got Unknown
		myBoneMap[((Object)((Component)parentBone).gameObject).name.Trim()] = parentBone;
		foreach (Transform item in parentBone)
		{
			Transform val = item;
			if (((Component)val).gameObject.activeSelf)
			{
				FillBoneMapRecurive(val);
			}
		}
	}

	public Transform GetBone(string boneName)
	{
		if (myBoneMap == null || myBoneMap.Count == 0)
		{
			Init();
		}
		myBoneMap.TryGetValue(boneName, out var value);
		return value;
	}

	public Transform SetBoneActiveStatus(string boneName, bool isActive)
	{
		Transform bone = GetBone(boneName);
		if ((Object)(object)bone == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
			return bone;
		}
		if (!mBoneActiveCountDict.TryGetValue(bone, out var value))
		{
			value = (((Component)bone).gameObject.activeSelf ? 1 : 0);
			mBoneActiveCountDict.Add(bone, value);
		}
		if (isActive)
		{
			if (++value > 0)
			{
				((Component)bone).gameObject.SetActive(true);
			}
		}
		else if (--value <= 0)
		{
			((Component)bone).gameObject.SetActive(false);
		}
		mBoneActiveCountDict[bone] = value;
		return bone;
	}
}

}
