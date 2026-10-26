// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using Zjcs.Offline.World.NativeLabelResources;
using System;
using System.Collections.Generic;


public class LocalizationBinaryTextService : ILocalizationTextService, IDisposable
{
	private ILanguageTypeService mLanguageTypeService;

	private ITextReplacer mTextReplacer;

	private ITextReader mTextReader;

	private ILocalRawFileLoader mRawFileLoader;


	private readonly List<ILanguageTextDataProvider> mLanguageTextDataProviders = new List<ILanguageTextDataProvider>();

	private List<LocalizationBinaryFileReader> mLocalizationBinaryFileReaders = new List<LocalizationBinaryFileReader>();

	public LanguageType CurrentLanguage => mLanguageTypeService.CurrentLanguage;

	public LocalizationBinaryTextService(ILanguageTypeService languageTypeService, ITextReader textReader, IEnumerable<ILanguageTextDataProvider> languageTextDataProviders, ILocalRawFileLoader rawFileLoader)
	{
		mLanguageTypeService = languageTypeService;
		mTextReader = textReader;
		mLanguageTextDataProviders.AddRange(languageTextDataProviders);
		mLanguageTypeService.OnLanguageChanged += OnCurrentLanguageChanged;
		mRawFileLoader = rawFileLoader;
		ReloadData();
	}

	public void Dispose()
	{
		foreach (LocalizationBinaryFileReader mLocalizationBinaryFileReader in mLocalizationBinaryFileReaders)
		{
			mLocalizationBinaryFileReader.Dispose();
		}
		mLocalizationBinaryFileReaders.Clear();
		mLanguageTypeService.OnLanguageChanged -= OnCurrentLanguageChanged;
	}

	public void OnCurrentLanguageChanged(LanguageType type)
	{
		ReloadData();
	}

	public string GetLanguagePath(string path)
	{
		ILocalRawFileLease val = mRawFileLoader.LoadRawFile(string.Format(path, CurrentLanguage));
		try
		{
			return val.GetRawFilePath();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public void AddDataProvider(ILanguageTextDataProvider languageTextDataProvider)
	{
		mLanguageTextDataProviders.Add(languageTextDataProvider);
		foreach (string filePath in languageTextDataProvider.FilePaths)
		{
			mLocalizationBinaryFileReaders.Add(new LocalizationBinaryFileReader(GetLanguagePath(filePath)));
		}
	}

	public void ReloadData()
	{
		foreach (LocalizationBinaryFileReader mLocalizationBinaryFileReader in mLocalizationBinaryFileReaders)
		{
			mLocalizationBinaryFileReader.Dispose();
		}
		mLocalizationBinaryFileReaders.Clear();
		foreach (ILanguageTextDataProvider mLanguageTextDataProvider in mLanguageTextDataProviders)
		{
			foreach (string filePath in mLanguageTextDataProvider.FilePaths)
			{
				mLocalizationBinaryFileReaders.Add(new LocalizationBinaryFileReader(GetLanguagePath(filePath)));
			}
		}
	}

	public void SetReplacer(ITextReplacer textReplacer)
	{
		mTextReplacer = textReplacer;
	}

	public bool TryGetOriginalText(string key, LanguageType language, out string result)
	{


		if (string.IsNullOrEmpty(key))
		{
			result = string.Empty;
			return false;
		}
		foreach (LocalizationBinaryFileReader mLocalizationBinaryFileReader in mLocalizationBinaryFileReaders)
		{
			if (!mLocalizationBinaryFileReader.TryGetTextByKey(key, out result))
			{
				continue;
			}
			if (result == null)
			{
				UnityEngine.Debug.LogError("Original localization key has a null value: " + key + ", language: " + language);
				return false;
			}
			return true;
		}
		result = key;
		return false;
	}

	public string GetText(string key, LanguageType language, params object[] paras)
	{
		if (mLanguageTypeService.IsShowLanguageKey)
		{
			return key;
		}
		if (!TryGetOriginalText(key, language, out var result))
		{
			return LocalizationTextService.GetMissingKeyText(result, mLanguageTypeService.IsShowMissingKeyText);
		}
		if (key == null || mTextReplacer == null)
		{
			return result;
		}
		string result2 = mTextReplacer.ReplaceText(result, paras);
		if (paras != null)
		{
			_ = paras.LongLength;
			return result2;
		}
		return result2;
	}

	public bool ContainsKey(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		foreach (LocalizationBinaryFileReader mLocalizationBinaryFileReader in mLocalizationBinaryFileReaders)
		{
			if (mLocalizationBinaryFileReader.ContainKey(key))
			{
				return true;
			}
		}
		return false;
	}

	public bool TryGetText(string key, out string text)
	{
		return TryGetText(key, CurrentLanguage, out text);
	}

	public bool TryGetText(string key, LanguageType language, out string result)
	{
		result = null;
		foreach (LocalizationBinaryFileReader mLocalizationBinaryFileReader in mLocalizationBinaryFileReaders)
		{
			if (mLocalizationBinaryFileReader.TryGetTextByKey(key, out result))
			{
				return true;
			}
		}
		return false;
	}

	public string GetTextFormated(string key, LanguageType language, params object[] args)
	{
		return string.Format(GetText(key, language), args);
	}
}

}
