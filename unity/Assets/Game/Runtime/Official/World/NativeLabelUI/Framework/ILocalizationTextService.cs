// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using Object = UnityEngine.Object;

public interface ILocalizationTextService
{
	LanguageType CurrentLanguage { get; }

	void ReloadData();

	bool ContainsKey(string key);

	bool TryGetOriginalText(string key, LanguageType language, out string result);

	string GetText(string key, LanguageType language, params object[] paras);

	string GetTextFormated(string key, LanguageType language, params object[] args);

	void SetReplacer(ITextReplacer textReplacer);

	bool TryGetText(string key, LanguageType language, out string result);

	bool TryGetText(string key, out string text);

	void AddDataProvider(ILanguageTextDataProvider languageTextDataProvider);
}

}
