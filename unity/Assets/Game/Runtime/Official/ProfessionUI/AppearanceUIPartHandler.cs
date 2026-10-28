namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AppearanceUIPartHandler : AppearancePartHandler
{
	[SerializeField]
	protected Component mSkeletonGraphic;

    private IProfessionAppearancePartGraphic Graphic => (IProfessionAppearancePartGraphic)mSkeletonGraphic;

	[SerializeField]
	private Transform mSlot;

	[SerializeField]
	private List<SkinType> IgnoreEffectTypes;

	private Dictionary<SkinType, PartsEntry> mLoadedDict = new Dictionary<SkinType, PartsEntry>();

	private Dictionary<SkinType, string> mLoadedSkinDict = new Dictionary<SkinType, string>();

	private List<PartEntry<IAppearancePart>> mExtraLoadedList;

	private List<string> mExtraWillLoadList;

	public Action OnDrawOrderRefresh;

	private bool mDrawOrderDirty;

	private Stack<PartSlot> mPartSlotsPool = new Stack<PartSlot>();

	private List<PartSlot> mPartSlots = new List<PartSlot>();

	public Dictionary<SkinType, PartsEntry> LoadedDict => mLoadedDict;

	public IReadOnlyList<PartSlot> PartSlots => mPartSlots;

	private void Awake()
	{
		((Component)mSlot).gameObject.SetActive(false);
	}

	private void OnDestroy()
	{
		Release();
		ReleaseExtraParts();
	}

	protected override void InternalSetSkins(Dictionary<SkinType, string> skinDict)
	{
		SkinType skinType = default;
		PartsEntry partsEntry = default;
		foreach (KeyValuePair<SkinType, PartsEntry> item in mLoadedDict)
		{
			skinType = item.Key; partsEntry = item.Value;
			SkinType key = skinType;
			PartsEntry partsEntry2 = partsEntry;
			if (!skinDict.TryGetValue(key, out var value) || !mLoadedSkinDict.TryGetValue(key, out var value2) || !value.Equals(value2))
			{
				partsEntry2.Release();
			}
		}
		mLoadedSkinDict.Clear();
		AppearanceConfig value3 = PartBindings.Config;
		string text = default;
		foreach (KeyValuePair<SkinType, string> item2 in skinDict)
		{
			skinType = item2.Key; text = item2.Value;
			SkinType skinType2 = skinType;
			string text2 = text;
			if (text2 == "emptySkin")
			{
				continue;
			}
			if (!mLoadedDict.TryGetValue(skinType2, out var value4))
			{
				value4 = new PartsEntry();
				mLoadedDict[skinType2] = value4;
			}
			if (value4.Parts.Count == 0)
			{
				if (value3.GetPartConfig(skinType2) != null)
				{
					LoadSkeleton(text2, ref value4);
				}
				else if ((IgnoreEffectTypes == null || !IgnoreEffectTypes.Contains(skinType2)) && PartBindings.HasEffect(text2))
				{
					LoadEffect(text2, ref value4);
				}
			}
			mLoadedSkinDict.Add(skinType2, text2);
		}
		if (mExtraWillLoadList != null)
		{
			foreach (string mExtraWillLoad in mExtraWillLoadList)
			{
				InternalAddExtraPart(mExtraWillLoad);
			}
			mExtraWillLoadList.Clear();
		}
		UpdateSeparatorSlots();
		Graphic.PrepareInstructionsAndRenderers(false);
		UpdatePartSlotDrawOrder();
		foreach (KeyValuePair<SkinType, PartsEntry> item3 in mLoadedDict)
		{
			skinType = item3.Key; partsEntry = item3.Value;
			foreach (IAppearancePart part in partsEntry.Parts)
			{
				part.SkeletonFollower.Initialize();
			}
		}
	}

	public void AddExtraPart(string path)
	{
		if (mLoadedDict.Count == 0)
		{
			Debug.LogWarning("形象还未进行初始化,加入缓冲列表 " + (path));
			if (mExtraWillLoadList == null)
			{
				mExtraWillLoadList = new List<string>();
			}
			mExtraWillLoadList.Add(path);
		}
		else
		{
			InternalAddExtraPart(path);
			UpdateSeparatorSlots();
			Graphic.PrepareInstructionsAndRenderers(false);
		}
	}

	public bool TryGetSkeletonPart(SkinType skinType, out IAppearanceSkeletonPart skeletonPart)
	{
		if (mLoadedDict.TryGetValue(skinType, out var value) && value.Parts.Count > 0 && value.Parts[0] is IAppearanceSkeletonPart appearanceSkeletonPart)
		{
			skeletonPart = appearanceSkeletonPart;
			return true;
		}
		skeletonPart = null;
		return false;
	}

	private void InternalAddExtraPart(string path)
	{
		if (mExtraLoadedList == null)
		{
			mExtraLoadedList = new List<PartEntry<IAppearancePart>>();
		}
		var (val, handle) = LoadPart(path);
		if (!((Object)(object)val == (Object)null))
		{
			IAppearancePart component = val.GetComponent<IAppearancePart>();
			component.Init(((Component)mSkeletonGraphic).gameObject);
			mExtraLoadedList.Add(new PartEntry<IAppearancePart>(component, handle));
		}
	}

	private void LateUpdate()
	{
		UpdatePartSlotDrawOrder();
		mDrawOrderDirty = false;
	}

	private void UpdatePartSlotDrawOrder()
	{

		IProfessionAppearancePartSkeleton skeleton = Graphic.Skeleton;
		if (skeleton == null)
		{
			return;
		}
		IProfessionSkinClipSlot[] items = skeleton.DrawOrder;
		int i = 0;
		for (int count = Graphic.separatorPartInstructions.Count; i < count; i++)
		{
			ProfessionAppearancePartInstruction val = Graphic.separatorPartInstructions[i];
			int j = 0;
			for (int count2 = mPartSlots.Count; j < count2; j++)
			{
				PartSlot partSlot = mPartSlots[j];
				IProfessionSkinClipSlot val2 = items[val.endSlot];
				if (partSlot.Slot == val2 && partSlot.DrawOrder != val.endSlot)
				{
					partSlot.DrawOrder = val.endSlot;
					partSlot.SetParent(Graphic.SeparatorParts, i);
					mDrawOrderDirty = true;
				}
			}
		}
		if (mDrawOrderDirty)
		{
			OnDrawOrderRefresh?.Invoke();
		}
	}

	public override void Release(bool tryUnloadAllAssets = false)
	{
		SkinType skinType = default;
		PartsEntry partsEntry = default;
		foreach (KeyValuePair<SkinType, PartsEntry> item in mLoadedDict)
		{
			skinType = item.Key; partsEntry = item.Value;
			partsEntry.Release(tryUnloadAllAssets);
		}
		mLoadedSkinDict.Clear();
	}

	public void ReleaseExtraParts()
	{
		if (mExtraLoadedList != null)
		{
			foreach (PartEntry<IAppearancePart> mExtraLoaded in mExtraLoadedList)
			{
				mExtraLoaded.Release();
			}
			mExtraLoadedList.Clear();
		}
		mExtraWillLoadList?.Clear();
	}

	private void LoadSkeleton(string skinName, ref PartsEntry entry)
	{
		string playerPartPrefabUI = ProfessionAppearancePartPaths.GetPlayerPartPrefabUI(PlayerPartMode.Integrated, skinName);
		var (val, item) = LoadPart(playerPartPrefabUI);
		if (!((Object)(object)val == (Object)null))
		{
			IAppearancePart component = val.GetComponent<IAppearancePart>();
			component.Init(((Component)mSkeletonGraphic).gameObject);
			entry.Handles.Add(item);
			entry.Parts.Add(component);
		}
	}

	private void LoadEffect(string skinName, ref PartsEntry entry)
	{
		string skinEffectPath = ProfessionAppearancePartPaths.GetSkinEffectPath(AppearanceRenderType.UI, skinName);
		var (val, item) = LoadPart(skinEffectPath);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		Transform transform = val.transform;
		IAppearancePart appearancePart = default;
		for (int num = transform.childCount - 1; num >= 0; num--)
		{
			if (((Component)transform.GetChild(num)).TryGetComponent<IAppearancePart>(out appearancePart))
			{
				appearancePart.Init(((Component)mSkeletonGraphic).gameObject);
				appearancePart.GameObject.transform.SetParent(mSlot);
				entry.Parts.Add(appearancePart);
			}
		}
		SeparatorSlotData separatorSlotData = default;
		if (val.TryGetComponent<SeparatorSlotData>(out separatorSlotData))
		{
			entry.SeparatorSlots = separatorSlotData.SlotNames;
		}
		entry.Handles.Add(item);
		Object.Destroy((Object)(object)val);
	}

	private (GameObject go, IProfessionAppearancePartAsset handle) LoadPart(string path)
	{

		IProfessionAppearancePartAsset assetOperationHandle = PartBindings.LoadPrefab(path);
		GameObject result = assetOperationHandle.Result;
		if ((Object)(object)result == (Object)null)
		{
			Debug.LogError("prefab为空:" + (path));
			return (go: null, handle: null);
		}
		GameObject val = PartBindings.Instantiate(result, mSlot, false);
		val.transform.localPosition = Vector3.zero;
		return (go: val, handle: assetOperationHandle);
	}

	private void UpdateSeparatorSlots()
	{
		foreach (PartSlot mPartSlot in mPartSlots)
		{
			ReturnPartSlot(mPartSlot);
		}
		mPartSlots.Clear();
		Graphic.separatorSlots.Clear();
		Graphic.separatorPartInstructions.Clear();
		SkinType skinType = default;
		PartsEntry partsEntry = default;
		foreach (KeyValuePair<SkinType, PartsEntry> item in mLoadedDict)
		{
			skinType = item.Key; partsEntry = item.Value;
			PartsEntry partsEntry2 = partsEntry;
			foreach (IAppearancePart part in partsEntry2.Parts)
			{
				SetPartSlot(part);
			}
			if (partsEntry2.SeparatorSlots == null)
			{
				continue;
			}
			foreach (string separatorSlot in partsEntry2.SeparatorSlots)
			{
				IProfessionSkinClipSlot val = Graphic.Skeleton.FindSlot(separatorSlot);
				if (val != null && !Graphic.separatorSlots.Contains(val))
				{
					Graphic.separatorSlots.Add(val);
				}
			}
		}
		if (mExtraLoadedList != null)
		{
			foreach (PartEntry<IAppearancePart> mExtraLoaded in mExtraLoadedList)
			{
				SetPartSlot(mExtraLoaded.Part);
			}
		}
		if (DynamicSeparatorSlots.Count > 0 && Graphic.Skeleton != null)
		{
			foreach (string dynamicSeparatorSlot in DynamicSeparatorSlots)
			{
				IProfessionSkinClipSlot val2 = Graphic.Skeleton.FindSlot(dynamicSeparatorSlot);
				if (val2 == null)
				{
					Debug.LogWarning("动态SeparatorSlot未找到 slot:" + (dynamicSeparatorSlot));
				}
				else if (!Graphic.separatorSlots.Contains(val2))
				{
					Graphic.separatorSlots.Add(val2);
				}
			}
		}
		Graphic.enableSeparatorSlots = Graphic.separatorSlots.Count > 0;
	}

	protected override void ApplyDynamicSeparatorSlots()
	{
		if (!((Object)(object)mSkeletonGraphic == (Object)null) && Graphic.Skeleton != null)
		{
			UpdateSeparatorSlots();
			Graphic.PrepareInstructionsAndRenderers(false);
			UpdatePartSlotDrawOrder();
		}
	}

	private void SetPartSlot(IAppearancePart part)
	{
		string slotName = part.SkeletonFollower.SlotName;
		if (!string.IsNullOrEmpty(slotName))
		{
			IProfessionSkinClipSlot val = Graphic.Skeleton.FindSlot(slotName);
			if (val != null && !Graphic.separatorSlots.Contains(val))
			{
				Graphic.separatorSlots.Add(val);
			}
			PartSlot partSlot = FindPartSlot(val);
			if (partSlot == null)
			{
				partSlot = GetOrCreatePartSlot(val);
				mPartSlots.Add(partSlot);
			}
			partSlot.PartList.Add(part);
		}
	}

	private PartSlot FindPartSlot(IProfessionSkinClipSlot slot)
	{
		foreach (PartSlot mPartSlot in mPartSlots)
		{
			if (mPartSlot.Slot == slot)
			{
				return mPartSlot;
			}
		}
		return null;
	}

	private PartSlot GetOrCreatePartSlot(IProfessionSkinClipSlot slot)
	{
		PartSlot partSlot = default;
		if (!mPartSlotsPool.TryPop(out partSlot))
		{
			partSlot = new PartSlot();
		}
		partSlot.Slot = slot;
		return partSlot;
	}

	private void ReturnPartSlot(PartSlot partSlot)
	{
		partSlot.Clear();
		mPartSlotsPool.Push(partSlot);
	}
}

}
