namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SpineSkinInfo
{
	public HashSet<int> BlackIdSet = new HashSet<int>();

	public HashSet<int> WhiteIdSet = new HashSet<int>();

	public int Id;

	public SkinType SkinType;

	public string SkinCustomType;

	public SkinUseType SkinUseType;

	public string SkinName;

	public bool HasDye;

	public int RandomWeight;

	public bool HasEffect;

	public List<SkinType> BlackTypeList;

	public List<string> BlackCustomTypeList;

	public List<int> BlackIdList;

	public List<string> WhiteCustomTypeList;

	public List<int> WhiteIdList;

	public List<int> BindingIdList;

	public string BundleName;

	public string DisplayName;

	public const int __ID__ = -1104590546;

}

}
