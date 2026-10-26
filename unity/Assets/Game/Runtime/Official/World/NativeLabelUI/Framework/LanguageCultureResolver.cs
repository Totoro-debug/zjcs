// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;
using System.Collections.Generic;


public static class LanguageCultureResolver
{
	private static readonly IReadOnlyDictionary<LanguageType, Lazy<LanguageCulture>> sCultures = new Dictionary<LanguageType, Lazy<LanguageCulture>>
	{
		{
			LanguageType.zh_CN,
			Make<NumberCultureFormatter_CN, TextCultureFormatter_CN, DateCultureFormatter_CN>(LanguageType.zh_CN)
		},
		{
			LanguageType.zh_TW,
			Make<NumberCultureFormatter_TW, TextCultureFormatter_TW, DateCultureFormatter_TW>(LanguageType.zh_TW)
		},
		{
			LanguageType.en_US,
			Make<NumberCultureFormatter_EN, TextCultureFormatter_EN, DateCultureFormatter_EN>(LanguageType.en_US)
		},
		{
			LanguageType.ja,
			Make<NumberCultureFormatter_JA, TextCultureFormatter_JA, DateCultureFormatter_JA>(LanguageType.ja)
		},
		{
			LanguageType.ko,
			Make<NumberCultureFormatter_KO, TextCultureFormatter_KO, DateCultureFormatter_KO>(LanguageType.ko)
		},
		{
			LanguageType.de,
			Make<NumberCultureFormatter_DE, TextCultureFormatter_DE, DateCultureFormatter_DE>(LanguageType.de)
		},
		{
			LanguageType.fr,
			Make<NumberCultureFormatter_FR, TextCultureFormatter_FR, DateCultureFormatter_FR>(LanguageType.fr)
		},
		{
			LanguageType.ru,
			Make<NumberCultureFormatter_RU, TextCultureFormatter_RU, DateCultureFormatter_RU>(LanguageType.ru)
		},
		{
			LanguageType.pt,
			Make<NumberCultureFormatter_PT, TextCultureFormatter_PT, DateCultureFormatter_PT>(LanguageType.pt)
		},
		{
			LanguageType.es,
			Make<NumberCultureFormatter_ES, TextCultureFormatter_ES, DateCultureFormatter_ES>(LanguageType.es)
		}
	};

	public static LanguageCulture Resolve(LanguageType lang)
	{
		if (!sCultures.TryGetValue(lang, out var value))
		{
			throw new ArgumentOutOfRangeException("lang", lang, "Unsupported LanguageType");
		}
		return value.Value;
	}

	private static Lazy<LanguageCulture> Make<TNumber, TText, TDate>(LanguageType lang) where TNumber : ICultureNumberFormatter, new() where TText : ICultureTextFormatter, new() where TDate : ICultureDateFormatter, new()
	{
		return new Lazy<LanguageCulture>(() => new LanguageCulture(lang, new TNumber(), new TText(), new TDate()));
	}
}

}
