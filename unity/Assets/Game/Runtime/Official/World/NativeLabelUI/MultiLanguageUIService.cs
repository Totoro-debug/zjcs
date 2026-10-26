// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeLabelUI.Framework;

public class MultiLanguageUIService : IDisposable, IMultiLanguageUIService
{
	private ILanguageTypeService mLanguageTypeService;

	private List<MultiLanguageUI> mTempMultiLanguageUIs = new List<MultiLanguageUI>();

	private HashSet<MultiLanguageUI> mMultiLanguageUIs = new HashSet<MultiLanguageUI>();

	public MultiLanguageUIService(ILanguageTypeService languageTypeService)
	{
		mLanguageTypeService = languageTypeService;
		mLanguageTypeService.OnLanguageChanged += OnCurrentLanguageChanged;
	}

	public void Dispose()
	{
		mLanguageTypeService.OnLanguageChanged -= OnCurrentLanguageChanged;
	}

	public void OnCurrentLanguageChanged(LanguageType type)
	{
		mTempMultiLanguageUIs.Clear();
		mTempMultiLanguageUIs.AddRange(mMultiLanguageUIs);
		foreach (MultiLanguageUI mTempMultiLanguageUI in mTempMultiLanguageUIs)
		{
			mTempMultiLanguageUI.Refresh(type);
		}
	}

	public void RegisterMultiLanguageUI(MultiLanguageUI multiLanguageUI)
	{
		mMultiLanguageUIs.Add(multiLanguageUI);
	}

	public void UnregisterMultiLanguageUI(MultiLanguageUI multiLanguageUI)
	{
		mMultiLanguageUIs.Remove(multiLanguageUI);
	}
}

}
