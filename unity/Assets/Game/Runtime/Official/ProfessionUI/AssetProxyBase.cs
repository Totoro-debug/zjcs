namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public abstract class AssetProxyBase<T> : IAssetProxy<T> where T : Object
{
	[SerializeField]
	private string mAssetGuid;

	public string Guid => mAssetGuid;

	public bool IsValid => !string.IsNullOrEmpty(mAssetGuid);

	public abstract T Load();

	public abstract void LoadAsync(Action<T> callback);

	public abstract void Unload();
}

}
