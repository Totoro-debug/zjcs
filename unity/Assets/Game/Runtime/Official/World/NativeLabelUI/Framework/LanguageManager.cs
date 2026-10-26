// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;


public class LanguageManager
{
	private static LanguageManager sInstance;

	private readonly ILocalizationTextService mLocalizationTextService;

	public static LanguageManager Instance => sInstance ?? throw new InvalidOperationException("LanguageManager has not been initialized.");

	public LanguageType CurrentLanguage => mLocalizationTextService.CurrentLanguage;

	private LanguageManager(ILocalizationTextService localizationTextService)
	{
		mLocalizationTextService = localizationTextService ?? throw new ArgumentNullException("localizationTextService");
	}

	public static void Initialize(ILocalizationTextService localizationTextService)
	{
		sInstance = new LanguageManager(localizationTextService);
	}

	public bool ContainsKey(string key)
	{
		return mLocalizationTextService.ContainsKey(key);
	}

	public string GetText(string key, params object[] paras)
	{
		return mLocalizationTextService.GetText(key, mLocalizationTextService.CurrentLanguage, paras);
	}

	public string GetText(LanguageType languageType, string key, params object[] paras)
	{
		return mLocalizationTextService.GetText(key, languageType, paras);
	}

	public bool TryGetText(string key, out string result)
	{
		return mLocalizationTextService.TryGetText(key, out result);
	}

	public bool TryGetText(LanguageType languageType, string key, out string result)
	{
		return mLocalizationTextService.TryGetText(key, languageType, out result);
	}

	public string GetTextFormated(string key, params object[] args)
	{
		return mLocalizationTextService.GetTextFormated(key, mLocalizationTextService.CurrentLanguage, args);
	}

	public string GetTextFormated(LanguageType languageType, string key, params object[] args)
	{
		return mLocalizationTextService.GetTextFormated(key, languageType, args);
	}
}

}
