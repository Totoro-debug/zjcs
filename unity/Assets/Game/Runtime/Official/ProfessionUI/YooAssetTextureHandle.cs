namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public struct YooAssetTextureHandle
{
	private SpineTextureAssetProxy mSpineTextureAssetProxy;

	private int mOperationStatus;

	public Texture Result { get; private set; }

	public bool IsValid => mSpineTextureAssetProxy != null;

	public bool WasRequested
	{
		get
		{

			return (int)mOperationStatus > 0;
		}
	}

	public bool WasSuccessfullyLoaded
	{
		get
		{

			return (int)mOperationStatus == 2;
		}
	}

	public void Load(YooAssetTextureReference mainTextureReference)
	{

		if ((int)mOperationStatus == 0)
		{
			mSpineTextureAssetProxy = mainTextureReference.AssetReference;
			mOperationStatus = 1;
			OnLoadedTexture(mainTextureReference.AssetReference.Load());
		}
	}

	private void OnLoadedTexture(Texture texture)
	{

		Result = texture;
		mOperationStatus = 2;
	}

	public void Release()
	{

		mOperationStatus = 0;
		Result = null;
		mSpineTextureAssetProxy?.Unload();
		mSpineTextureAssetProxy = null;
	}
}

}
