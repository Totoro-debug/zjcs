// Migrated selected original source; see ground effects receipt.
using System;
using UnityEngine;

namespace Zjcs.Official.StoryGroundEffects
{
[Serializable]
public class AssetGuidProxy<T> where T : UnityEngine.Object
{
	[SerializeField]
	private string mAssetGuid;

	public string AssetGuid => mAssetGuid;

	public bool IsValid => !string.IsNullOrEmpty(mAssetGuid);
}

}
