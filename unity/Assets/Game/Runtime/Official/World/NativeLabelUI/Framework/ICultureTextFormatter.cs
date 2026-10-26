// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{

public interface ICultureTextFormatter
{
	LanguageType LanguageType { get; }

	string GetText(string key);

	string GetTextFormatted(string key, params object[] args);
}

}
