// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{

internal abstract class CultureTextFormatterBase : ICultureTextFormatter
{
	public abstract LanguageType LanguageType { get; }

	public virtual string GetText(string key)
	{
		return LanguageManager.Instance.GetText(LanguageType, key);
	}

	public virtual string GetTextFormatted(string key, params object[] args)
	{
		return LanguageManager.Instance.GetTextFormated(LanguageType, key, args);
	}
}

}
