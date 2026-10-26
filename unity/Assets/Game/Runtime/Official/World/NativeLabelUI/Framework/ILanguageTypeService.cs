// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using Object = UnityEngine.Object;
using System;


public interface ILanguageTypeService
{
	LanguageType CurrentLanguage { get; set; }

	bool IsShowLanguageKey { get; set; }

	bool IsShowMissingKeyText { get; set; }

	event Action<LanguageType> OnLanguageChanged;
}

}
