namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
[DisallowMultipleComponent]
[ExecuteAlways]
public class AppearanceUILite : MonoBehaviour, IProfessionAppearanceUnloadable
{
	public Component SkeletonGraphic;

	public LevelOfResolution LevelOfResolution = LevelOfResolution.High;

	private YooAssetTextureLoader mTextureLoader;

	private bool mUnloadTextureImmediately;

	private List<IDisposable> mDisposableBag;

    private IProfessionAppearanceUILiteBindings mBindings;
    private IProfessionAppearanceUILiteBindings Bindings => mBindings ?? ProfessionAppearanceUILiteBindings.Required;
    private IProfessionAppearanceUIAssetManager AppearanceAssetManager => Bindings.AssetManager;
    private IProfessionAppearanceUILiteGraphic GraphicGraph => (IProfessionAppearanceUILiteGraphic)SkeletonGraphic;
    public void Configure(IProfessionAppearanceUILiteBindings bindings)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	public YooAssetTextureLoader TextureLoader
	{
		get
		{
			if (!mTextureLoader)
			{
                string name = GraphicGraph.SkeletonDataName;
                string skeletonPath = Bindings.Config.GetSkeletonPath(name);
                string textureLoaderPath = $"{skeletonPath}/{name}_TextureLoader_{LevelOfResolution}.asset";
                var handle = Bindings.LoadAsset(textureLoaderPath);
                if (mDisposableBag == null) mDisposableBag = new List<IDisposable>();
                mDisposableBag.Add(handle);
                mTextureLoader = (YooAssetTextureLoader)handle.Result;
			}
			return mTextureLoader;
		}
	}

	private void Reset()
	{
		SkeletonGraphic = GetComponent<UnityEngine.UI.Graphic>();
	}

	private void OnEnable()
	{

		mDisposableBag = default;
		UseSharedMaterials();
	}

	private void OnDisable()
	{
		ReturnSharedMaterials();
		if (mDisposableBag != null) foreach (var handle in mDisposableBag) handle.Dispose();
	}

	public void MarkUnloadTextureImmediately()
	{
		mUnloadTextureImmediately = true;
	}

	private void UseSharedMaterials()
	{
		if (!TextureLoader || AppearanceAssetManager == null)
		{
			return;
		}
		GraphicGraph.UpdateMesh();
		AppearanceAssetManager.AddTextureLoaderRef(TextureLoader, (Object)(object)this);
		TextureLoader.TryInit();
		if (!GraphicGraph.allowMultipleCanvasRenderers)
		{
			Texture mainTexture = GraphicGraph.mainTexture;
			Texture val = null;
			TextureLoader.RequestLoadTexture(mainTexture, ref val, (Action<Texture>)((Texture loadedTexture) =>
			{
				GraphicGraph.OverrideTexture = loadedTexture;
			}));
			TextureLoader.AddLoadedTextureRef(mainTexture);
			return;
		}
		Dictionary<Texture, Texture> textureOverrideDict = GraphicGraph.CustomTextureOverride;
		Texture[] items = GraphicGraph.TexturesMultipleCanvasRenderers;
		foreach (Texture placeholderTexture in items)
		{
			if (!((Object)(object)placeholderTexture == (Object)null) && !textureOverrideDict.ContainsKey(placeholderTexture))
			{
				Texture val2 = null;
				TextureLoader.RequestLoadTexture(placeholderTexture, ref val2, (Action<Texture>)((Texture loadedTexture) =>
				{
					textureOverrideDict.Add(placeholderTexture, loadedTexture);
				}));
				TextureLoader.AddLoadedTextureRef(placeholderTexture);
			}
		}
	}

	private void ReturnSharedMaterials()
	{
		if (!mTextureLoader || AppearanceAssetManager == null)
		{
			return;
		}
		IProfessionAppearanceUIAssetManager appearanceAssetManager = AppearanceAssetManager;
		if (appearanceAssetManager != null)
		{
			if (!GraphicGraph.allowMultipleCanvasRenderers)
			{
				mTextureLoader.RemoveLoadedTextureRef(GraphicGraph.OverrideTexture);
				GraphicGraph.OverrideTexture = null;
			}
			else
			{
				Dictionary<Texture, Texture> customTextureOverride = GraphicGraph.CustomTextureOverride;
				Texture val = default;
				Texture val2 = default;
				foreach (KeyValuePair<Texture, Texture> item in customTextureOverride)
				{
					item.Deconstruct(out val, out val2);
					Texture targetTexture = val2;
					mTextureLoader.RemoveLoadedTextureRef(targetTexture);
				}
				customTextureOverride.Clear();
			}
			appearanceAssetManager.RemoveTextureLoaderRef(mTextureLoader, (Object)(object)this);
		}
		mTextureLoader.TryUnloadUnusedTextures(mUnloadTextureImmediately);
	}
}

}
