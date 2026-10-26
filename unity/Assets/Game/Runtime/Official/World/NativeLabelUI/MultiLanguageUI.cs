// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI.Framework;
using UnityEngine;

[ExecuteAlways]
public class MultiLanguageUI : MonoBehaviour
{
	protected IMultiLanguageUIService mMultiLanguageUIService { get; set; }

	protected ILanguageTypeService mLanguageTypeService { get; set; }


	// Explicit replacement for the original dependency injection, before Awake.
	public void BindLanguageServices(IMultiLanguageUIService uiService, ILanguageTypeService languageService)
	{
		mMultiLanguageUIService = uiService ?? throw new System.ArgumentNullException(nameof(uiService));
		mLanguageTypeService = languageService ?? throw new System.ArgumentNullException(nameof(languageService));
	}

	protected virtual void Awake()
	{
		mMultiLanguageUIService.RegisterMultiLanguageUI(this);
	}

	protected virtual void Start()
	{
		Refresh(mLanguageTypeService.CurrentLanguage);
	}

	protected virtual void OnDestroy()
	{
		mMultiLanguageUIService?.UnregisterMultiLanguageUI(this);
	}

	public virtual void Refresh(LanguageType languageType)
	{
	}
}

}
