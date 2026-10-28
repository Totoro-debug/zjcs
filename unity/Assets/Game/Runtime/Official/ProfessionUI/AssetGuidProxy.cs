using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class AssetGuidProxy<T> where T : Object
{
	[SerializeField]
	private string mAssetGuid;

	public string AssetGuid => mAssetGuid;

	public bool IsValid => !string.IsNullOrEmpty(mAssetGuid);
}

}
