namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AppearancePartHandler : MonoBehaviour
{
	public SkinType[] IgnoreSkinTypes;

	private Dictionary<SkinType, string> mSkinDict;

	private readonly HashSet<string> mDynamicSeparatorSlots = new HashSet<string>();

    private IProfessionAppearancePartBindings mPartBindings;
    protected IProfessionAppearancePartBindings PartBindings => mPartBindings ?? ProfessionAppearancePartBindings.Required;
    public void ConfigureParts(IProfessionAppearancePartBindings bindings)
    { mPartBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	public SafeAction OnSkinChanged { get; } = new SafeAction();

	protected HashSet<string> DynamicSeparatorSlots => mDynamicSeparatorSlots;

	public bool AddSeparatorSlot(string slotName)
	{
		if (string.IsNullOrEmpty(slotName))
		{
			return false;
		}
		if (!mDynamicSeparatorSlots.Add(slotName))
		{
			return false;
		}
		ApplyDynamicSeparatorSlots();
		return true;
	}

	public bool RemoveSeparatorSlot(string slotName)
	{
		if (string.IsNullOrEmpty(slotName))
		{
			return false;
		}
		if (!mDynamicSeparatorSlots.Remove(slotName))
		{
			return false;
		}
		ApplyDynamicSeparatorSlots();
		return true;
	}

	public void ClearDynamicSeparatorSlots()
	{
		if (mDynamicSeparatorSlots.Count != 0)
		{
			mDynamicSeparatorSlots.Clear();
			ApplyDynamicSeparatorSlots();
		}
	}

	protected abstract void ApplyDynamicSeparatorSlots();

	public void SetSkins(Dictionary<SkinType, string> skinDict)
	{
		Dictionary<SkinType, string> skinDict2 = skinDict;
		SkinType[] ignoreSkinTypes = IgnoreSkinTypes;
		if (ignoreSkinTypes != null && ignoreSkinTypes.Length > 0)
		{
			if (mSkinDict == null)
			{
				mSkinDict = new Dictionary<SkinType, string>();
			}
			mSkinDict.Clear();
			foreach (KeyValuePair<SkinType, string> item in skinDict)
			{
				bool flag = false;
				ignoreSkinTypes = IgnoreSkinTypes;
				foreach (SkinType skinType in ignoreSkinTypes)
				{
					if (item.Key == skinType)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					mSkinDict.Add(item.Key, item.Value);
				}
			}
			skinDict2 = mSkinDict;
		}
		InternalSetSkins(skinDict2);
		OnSkinChanged.Invoke();
	}

	protected abstract void InternalSetSkins(Dictionary<SkinType, string> skinDict);

	public abstract void Release(bool tryUnloadAllAssets = false);
}

}
