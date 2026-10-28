namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public struct YooAssetRequest : IOnDemandRequest
{
	public YooAssetTextureHandle MainTextureHandle;

	public YooAssetTextureHandle DyeTextureHandle;

	public bool WasRequested => MainTextureHandle.WasRequested;

	public bool WasSuccessfullyLoaded => MainTextureHandle.WasSuccessfullyLoaded;

	public bool IsTarget(Texture texture)
	{
		return (Object)(object)MainTextureHandle.Result == (Object)(object)texture;
	}

	public void Release()
	{
		MainTextureHandle.Release();
		if (DyeTextureHandle.IsValid)
		{
			DyeTextureHandle.Release();
		}
	}
}

}
