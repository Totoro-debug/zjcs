namespace Zjcs.Official.ProfessionUI
{
using System.Collections.Generic;
using UnityEngine;

public class SpineAnimationSplitBinarySO : ScriptableObject
{
	public const string kStrSoFileTail = "_SplitBinarySO";

	[Header("spine二进制为了节约数据大小, 使用了常量字符串池, 在读取Animation的时候依赖了这个字符串池, 故将其缓存在SO中")]
	[SerializeField]
	public string[] StringArray;

	[Header("被切割出去的Animation的数据二进制文件")]
	[SerializeField]
	public List<SplitSpineBinaryAssetContent> ListAnimationContent;

	[Header("没有被切割出去的Animation的名字列表, 在这个List中的动画会保存在主二进制文件内")]
	[SerializeField]
	public List<string> ListNoSplitAnimationNames;

	private readonly Dictionary<string, SplitSpineBinaryAssetContent> mDicAnimBinaries = new Dictionary<string, SplitSpineBinaryAssetContent>();

	private void TryInit()
	{
		if (mDicAnimBinaries.Count != 0 || ListAnimationContent == null || ListAnimationContent.Count == 0)
		{
			return;
		}
		foreach (SplitSpineBinaryAssetContent item in ListAnimationContent)
		{
			mDicAnimBinaries.Add(item.ContentName, item);
		}
	}

	public string GetAnimationAssetGuid(string aniName)
	{
		TryInit();
		if (!mDicAnimBinaries.TryGetValue(aniName, out var value))
		{
			return null;
		}
		return value.TextAsset.AssetGuid;
	}

	public static string GetSoFileName(string skeletonDataName)
	{
		return skeletonDataName + "_SplitBinarySO.asset";
	}
}

}
