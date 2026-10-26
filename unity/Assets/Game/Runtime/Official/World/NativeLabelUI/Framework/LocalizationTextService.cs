// Only the original CSV service's pure missing-key function is consumed by the binary service.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
public static class LocalizationTextService
{
public static string GetMissingKeyText(string key, bool isShowMissingKeyText)
	{
		return key;
	}
}
}
