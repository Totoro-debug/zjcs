// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System.Collections.Generic;


public sealed class CultureCustomTextArg : ArgBase
{
	public IReadOnlyDictionary<string, string> Texts { get; }

	public object DefaultText { get; }

	public LanguageType? FallbackLanguage { get; }

	public CultureCustomTextArg(IReadOnlyDictionary<string, string> texts, object defaultText)
		: this(texts, defaultText, null)
	{
	}

	public CultureCustomTextArg(IReadOnlyDictionary<string, string> texts, object defaultText, LanguageType? fallbackLanguage)
	{
		Texts = texts;
		DefaultText = defaultText;
		FallbackLanguage = fallbackLanguage;
	}

	public override string ToString()
	{
		string text = Culture.LanguageType.ToString();
		if (TryGetText(text, out var content))
		{
			return content;
		}
		if (FallbackLanguage.HasValue)
		{
			string text2 = FallbackLanguage.Value.ToString();
			if (text2 != text && TryGetText(text2, out content))
			{
				return content;
			}
		}
		return DefaultText?.ToString() ?? string.Empty;
	}

	private bool TryGetText(string language, out string content)
	{
		content = null;
		if (Texts != null && !string.IsNullOrEmpty(language))
		{
			return Texts.TryGetValue(language, out content);
		}
		return false;
	}
}

}
