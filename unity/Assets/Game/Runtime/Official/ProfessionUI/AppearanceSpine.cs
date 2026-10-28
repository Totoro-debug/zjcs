namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class AppearanceSpine : MonoBehaviour, IAppearanceComponent
{
	public SkinTextureAsset SkinTextureAsset;

	[SerializeField]
	protected Component mPartHandler;

	private IProfessionAppearanceSkeletonComponent mSkeletonComponent;

	private ScriptableObject mSkeletonDataAsset;

	public Action OnSkinUpdate;

	private readonly List<string> mSkinNames = new List<string>();

	private IProfessionAppearanceSkin mSkin;

	public ScriptableObject SpineAnimationSplitBinarySo { get; private set; }

    private IProfessionAppearanceSpineBindings mBindings;
    private IProfessionAppearanceSpineBindings Bindings => mBindings ?? ProfessionAppearanceSpineBindings.Required;
    public IProfessionAppearanceCharacterBindings ResourceLoader { get; private set; }
    public void Configure(IProfessionAppearanceSpineBindings bindings, IProfessionAppearanceCharacterBindings resourceLoader)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); ResourceLoader = resourceLoader; }

	public IProfessionAppearanceSkeletonComponent SkeletonComponent => mSkeletonComponent ?? (mSkeletonComponent = Bindings.GetSkeletonComponent(this));

	public ScriptableObject SkeletonDataAsset => mSkeletonDataAsset;

	public abstract YooAssetTextureLoader TextureLoader { get; }

	public bool Valid => (Object)(object)mSkeletonDataAsset != (Object)null;

	public bool UseSplitAnimation => (Object)(object)SpineAnimationSplitBinarySo != (Object)null;

	public IEnumerable<string> Skins => mSkinNames;

	protected virtual void Awake()
	{
	}

	protected virtual void OnEnable()
	{
	}

	protected virtual void OnDisable()
	{
	}

	protected virtual void OnSkinUpdated()
	{
		try
		{
			OnSkinUpdate?.Invoke();
		}
		catch (Exception exception)
		{
            Debug.LogError("OnSkinUpdate Error\n" + exception);
		}
	}

	protected virtual void OnDestroy()
	{
		Release(tryUnloadAllAssets: false);
	}

	protected abstract void InitSkeleton(ScriptableObject skeletonDataAsset);

	public void Init(ScriptableObject skeletonDataAsset, ScriptableObject spineAnimationSplitBinarySo)
	{
		if (!skeletonDataAsset)
		{
            Debug.LogError("skeleton asset is null");
		}
		else if (!((Object)(object)skeletonDataAsset == (Object)(object)mSkeletonDataAsset))
		{
			if (mSkeletonDataAsset)
			{
				Release(tryUnloadAllAssets: false);
			}
			mSkeletonDataAsset = skeletonDataAsset;
			SpineAnimationSplitBinarySo = spineAnimationSplitBinarySo;
			InitSkeleton(skeletonDataAsset);
		}
	}

	protected void SetupSkin(List<string> skins)
	{

		mSkinNames.Clear();
		if (mSkin == null)
		{
			mSkin = Bindings.CreateSkin("combineSkin");
		}
		else
		{
			mSkin.Clear();
		}
		IProfessionAppearanceSkeletonData skeletonData = Bindings.GetSkeletonData(mSkeletonDataAsset, true);
		foreach (string skin in skins)
		{
			IProfessionAppearanceSkin val = skeletonData.FindSkin(skin);
			if (val == null)
			{
                Debug.LogWarning("skin is not exist:" + skin);
			}
			else
			{
				mSkin.AddSkin(val);
				mSkinNames.Add(skin);
			}
		}
	}

	public virtual void SetSkins(List<string> skins)
	{
		SetupSkin(skins);
		ApplySkin();
	}

	protected void ApplySkin()
	{
		SetSkeletonSkins(mSkin);
		if (mSkeletonComponent != null && mSkeletonComponent.Skeleton != null)
		{
			mSkeletonComponent.Skeleton.SetToSetupPose();
			OnSkinUpdated();
		}
	}

	public bool TryGetMaterial(string skinName, out Material material)
	{
		material = GetMaterial(skinName);
		return (Object)(object)material != (Object)null;
	}

	protected abstract Material GetMaterial(string skinName);

	public virtual void SetDye(string skinName, Dictionary<int, HsvChange> hsvMap)
	{

		Material material = GetMaterial(skinName);
		if ((Object)(object)material == (Object)null)
		{
            Debug.LogWarning("不存在指定的皮肤材质:" + skinName);
			return;
		}
		int num = default;
		HsvChange hsvChange = default;
		foreach (KeyValuePair<int, HsvChange> item in hsvMap)
		{
			num = item.Key; hsvChange = item.Value;
			int channel = num;
			HsvChange hsvChange2 = hsvChange;
			Vector3 vector = new Vector3(hsvChange2.OffsetH, hsvChange2.OffsetScaleS, hsvChange2.OffsetScaleV);
			AppearanceMaterialUtils.SetDye(material, channel, vector);
		}
	}

	protected SkinTextureConfig GetSkinTextureConfig(string skinName)
	{
		SkinTextureConfig config = SkinTextureAsset.GetConfig(skinName);
		if (config == null)
		{
            Debug.LogError("The skinName is not exist:" + skinName);
			return null;
		}
		return config;
	}

	public virtual void Release(bool tryUnloadAllAssets)
	{
		IProfessionAppearanceSkin val = mSkin;
		if (val != null)
		{
			val.Clear();
		}
		mSkinNames.Clear();
		if (tryUnloadAllAssets && mSkeletonComponent != null)
		{
			IProfessionAppearanceSkeleton skeleton = mSkeletonComponent.Skeleton;
			if (skeleton != null)
			{
				skeleton.UpdateCache();
				skeleton.SetToSetupPose();
			}
			if ((Object)(object)TextureLoader != (Object)null)
			{
				TextureLoader.TryUnloadUnusedTextures(ignoreRequestFrame: true);
			}
		}
	}

	public void SetSkeletonSkins(IProfessionAppearanceSkin newSkin)
	{
		if (SkeletonComponent == null)
		{
			return;
		}
		IProfessionAppearanceSkeleton skeleton = SkeletonComponent.Skeleton;
		if (((skeleton != null) ? skeleton.Data : null) == null)
		{
			return;
		}
		if (skeleton.Skin != newSkin)
		{
			skeleton.SetSkin(newSkin);
			return;
		}
		if (newSkin != null)
		{
            for (int i = 0, count = skeleton.SlotCount; i < count; i++)
            {
                string attachmentName = skeleton.SetupAttachmentName(i);
                if (attachmentName != null)
                {
                    object attachment = newSkin.GetAttachment(i, attachmentName);
                    if (attachment != null) skeleton.SetSlotAttachment(i, attachment);
                }
            }
		}
		skeleton.UpdateCache();
	}
}

}
