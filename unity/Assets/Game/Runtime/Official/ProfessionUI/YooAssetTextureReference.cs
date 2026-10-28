namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[Serializable]
public struct YooAssetTextureReference : ITargetTextureReference
{
	[SerializeField]
	public SpineTextureAssetProxy AssetReference;
}

}
