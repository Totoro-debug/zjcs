// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using System;
using System.Text;
using Zjcs.Official.World.NativeLabelUI.Common;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Text))]
public sealed class MultiLanguageText : MultiLanguageUI
{
	public string myKey;

	private object[] mArgs;

	private ITextProcessor mRawKeyValueProcessor;

	private ITextProcessor mLocalizedTextProcessor;

	private TMP_Text mCacheText;

	private TMPEffect mEffect;

	private TMPColorGradient mColorGradient;

	public ILocalizationTextService LocalizationTextService { get; set; }

	public IMultiLanguageFontService MultiLanguageFontService { get; set; }

	private TMP_Text CacheText
	{
		get
		{
			if ((Object)(object)mCacheText == (Object)null)
			{
				mCacheText = ((Component)this).GetComponent<TMP_Text>();
			}
			return mCacheText;
		}
	}

	public string Text => CacheText.text;

	public Color Color
	{
		get
		{

			return ((Graphic)CacheText).color;
		}
		set
		{

			((Graphic)CacheText).color = value;
		}
	}

	private TMPEffect Effect
	{
		get
		{
			if ((Object)(object)mEffect == (Object)null)
			{
				mEffect = ((Component)CacheText).GetComponent<TMPEffect>();
			}
			return mEffect;
		}
	}

	private TMPColorGradient ColorGradient
	{
		get
		{
			if ((Object)(object)mColorGradient == (Object)null)
			{
				mColorGradient = ((Component)CacheText).GetComponent<TMPColorGradient>();
			}
			return mColorGradient;
		}
	}

	public float FontSize
	{
		get
		{
			return CacheText.fontSize;
		}
		set
		{
			CacheText.fontSize = value;
		}
	}

	public RectTransform RectTransform => CacheText.rectTransform;

	public event Action OnTextRefresh;

	public event Action OnFontChanged;

	public void SetKey(string key)
	{
		SetKey(key, null, null, null);
	}

	public void SetKey(string key, params object[] args)
	{
		SetKey(key, null, null, args);
	}

	public void SetKey(string key, ITextProcessor rawKeyValueProcessor, ITextProcessor localizedTextProcessor, params object[] args)
	{
		CacheText.text = string.Empty;
		myKey = key;
		mArgs = args;
		mRawKeyValueProcessor = rawKeyValueProcessor;
		mLocalizedTextProcessor = localizedTextProcessor;
		Refresh(mLanguageTypeService.CurrentLanguage);
	}

	public void Format(params object[] args)
	{
		Format(null, null, args);
	}

	public void Format(ITextProcessor rawKeyValueProcessor, ITextProcessor localizedTextProcessor, params object[] args)
	{
		mArgs = args;
		mRawKeyValueProcessor = rawKeyValueProcessor;
		mLocalizedTextProcessor = localizedTextProcessor;
		Refresh(mLanguageTypeService.CurrentLanguage);
	}

	public void SetColor(Color color)
	{

		if ((Object)(object)ColorGradient != (Object)null)
		{
			((Behaviour)ColorGradient).enabled = false;
		}
		((Graphic)CacheText).color = color;
	}

	public void SetColor(Gradient gradient)
	{
		if (!((Object)(object)ColorGradient == (Object)null))
		{
			((Behaviour)ColorGradient).enabled = true;
			ColorGradient.SetGradientColor(gradient);
		}
	}

	public Color GetOutlineColor()
	{


		TMPEffect effect = Effect;
		if (!((Object)(object)effect == (Object)null))
		{
			return effect.outlineColor;
		}
		return Color.white;
	}

	public void SetOutlineColor(Color color)
	{


		TMPEffect effect = Effect;
		if (!((Object)(object)effect == (Object)null))
		{
			effect.outlineColor = color;
		}
	}

	public override void Refresh(LanguageType languageType)
	{
		base.Refresh(languageType);
		RefreshFont();
		Refresh(languageType, LocalizationTextService);
	}

	public void Refresh(LanguageType languageType, ILocalizationTextService localizationTextService)
	{


		if (string.IsNullOrEmpty(myKey))
		{
			return;
		}
		if (mArgs == null || mArgs.Length == 0)
		{
			string text = localizationTextService.GetText(myKey, languageType);
			SetLanguageText(GetRawKeyValueProcessed(text));
		}
		else
		{
			try
			{
				string text2 = localizationTextService.GetText(myKey, languageType);
				SetLanguageText(string.Format(GetRawKeyValueProcessed(text2), mArgs));
			}
			catch (Exception ex)
			{
				Debug.LogError($"Language key:{myKey}, args count:{mArgs.Length}\n{ex}", this);
			}
		}
		OnTextRefresh?.Invoke();
	}

	public static string GetKeyBySplit(int count, string splitKey)
	{
		if (count <= 0)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(splitKey);
			}
			stringBuilder.Append($"{{{i}}}");
		}
		return stringBuilder.ToString();
	}

	private void SetLanguageText(string text)
	{
		if (mLocalizedTextProcessor != null)
		{
			text = mLocalizedTextProcessor.Process(text);
		}
		CacheText.text = text;
	}

	private string GetRawKeyValueProcessed(string rawKeyValue)
	{
		if (string.IsNullOrEmpty(rawKeyValue) || mRawKeyValueProcessor == null)
		{
			return rawKeyValue;
		}
		return mRawKeyValueProcessor.Process(rawKeyValue);
	}

	private void RefreshFont()
	{
		MultiLanguageFontAsset multiLanguageFontAsset = MultiLanguageFontService.GetMultiLanguageFontAsset();
		if (multiLanguageFontAsset != null)
		{
			bool flag = false;
			TMP_FontAsset fontAsset = multiLanguageFontAsset.FontAsset;
			if ((Object)(object)fontAsset != (Object)null && (Object)(object)CacheText.font != (Object)(object)fontAsset)
			{
				CacheText.font = fontAsset;
				flag = true;
				OnFontChanged?.Invoke();
			}
			if (flag && (Object)(object)Effect != (Object)null && ((Behaviour)Effect).enabled)
			{
				Effect.SetupEffect(true);
			}
		}
	}
}

}
