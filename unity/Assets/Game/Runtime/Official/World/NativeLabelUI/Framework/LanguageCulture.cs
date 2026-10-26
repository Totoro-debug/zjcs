// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;


public sealed class LanguageCulture
{
	public LanguageType LanguageType { get; }

	public ICultureNumberFormatter Number { get; }

	public ICultureTextFormatter Text { get; }

	public ICultureDateFormatter Date { get; }

	public LanguageCulture(LanguageType languageType, ICultureNumberFormatter number, ICultureTextFormatter text, ICultureDateFormatter date)
	{
		if (number.LanguageType != languageType || text.LanguageType != languageType || date.LanguageType != languageType)
		{
			throw new ArgumentException($"LanguageCulture[{languageType}] 与 formatter 语言不一致");
		}
		LanguageType = languageType;
		Number = number;
		Text = text;
		Date = date;
	}
}

}
