namespace Zjcs.Official.PartnersUI
{
// Exact official LanguageUtils key builders. Localization is provided by WorldMultiLanguageText.
internal static class PartnerUiLanguage
{
	public static string GetEnumKey(object obj)
	{
		return $"{obj.GetType().Name}.{obj}";
	}

	public static string GetProfessionNameKey(Profession profession)
	{
		return $"profession_name_{profession}";
	}
}
}
