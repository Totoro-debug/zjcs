namespace Zjcs.Official.PartnersUI
{
internal static class PartnerRosterLanguage
{
	public static string GetEnumKey(object obj)
	{
		return $"{obj.GetType().Name}.{obj}";
	}
}
}
