// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{

public abstract class ArgBase
{
	protected LanguageCulture Culture => LanguageCultureResolver.Resolve(LanguageManager.Instance.CurrentLanguage);

	public abstract override string ToString();
}

}
