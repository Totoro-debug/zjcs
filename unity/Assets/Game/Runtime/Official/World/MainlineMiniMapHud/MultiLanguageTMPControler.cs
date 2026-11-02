// Selected original HUD path; see mainline-minimap-hud-code-source-manifest.json.
using System;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
namespace Zjcs.Official.World.Map13MiniMap
{
public class MultiLanguageTMPControler : MonoBehaviour
{
	[Serializable]
	public struct TMPSetting
	{
		public float FontSize;

		public FontStyles FontStyle;

		public TextAlignmentOptions Alignment;

		public bool EnableAutoSizing;

		public float FontSizeMin;

		public float FontSizeMax;

		[Range(0f, 50f)]
		public float CharWidthMaxAdjustment;

		[Range(0f, 50f)]
		public float LineSpacingMaxAdjustment;

		public float CharacterSpacing;

		public float WordSpacing;

		public float LineSpacing;

		public float ParagraphSpacing;

		public bool Equals(TMPSetting other)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			if (Mathf.Approximately(FontSize, other.FontSize) && FontStyle == other.FontStyle && Alignment == other.Alignment && EnableAutoSizing == other.EnableAutoSizing && Mathf.Approximately(FontSizeMin, other.FontSizeMin) && Mathf.Approximately(FontSizeMax, other.FontSizeMax) && Mathf.Approximately(CharWidthMaxAdjustment, other.CharWidthMaxAdjustment) && Mathf.Approximately(LineSpacingMaxAdjustment, other.LineSpacingMaxAdjustment) && Mathf.Approximately(CharacterSpacing, other.CharacterSpacing) && Mathf.Approximately(WordSpacing, other.WordSpacing) && Mathf.Approximately(LineSpacing, other.LineSpacing))
			{
				return Mathf.Approximately(ParagraphSpacing, other.ParagraphSpacing);
			}
			return false;
		}
	}

	[SerializeField]
	private MonoDictionary<LanguageGroup, TMPSetting> mLanguageType2Setting;

	private TextMeshProUGUI mTmpText;

	private TMPSetting? mDefaultSettings;

	public LanguageGroupConfig LanguageGroupConfig { get; set; }

	public ILanguageTypeService LanguageTypeService { get; set; }

	private void Awake()
	{
		if ((Object)(object)mTmpText == (Object)null)
		{
			mTmpText = ((Component)this).GetComponent<TextMeshProUGUI>();
		}
		if ((Object)(object)mTmpText != (Object)null && !mDefaultSettings.HasValue)
		{
			mDefaultSettings = GetDefaultSettings();
		}
	}

	private void OnEnable()
	{
		LanguageTypeService.OnLanguageChanged += OnLanguageChanged;
		Refresh();
	}

	private void OnDisable()
	{
		if (LanguageTypeService != null)
		{
			LanguageTypeService.OnLanguageChanged -= OnLanguageChanged;
		}
	}

	private void OnLanguageChanged(LanguageType languageType)
	{
		Refresh();
	}

	private TMPSetting GetCurrentSettings()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)mTmpText == (Object)null)
		{
			mTmpText = ((Component)this).GetComponent<TextMeshProUGUI>();
			if ((Object)(object)mTmpText == (Object)null)
			{
				return default;
			}
		}
		return new TMPSetting
		{
			FontSize = ((TMP_Text)mTmpText).fontSize,
			FontStyle = ((TMP_Text)mTmpText).fontStyle,
			Alignment = ((TMP_Text)mTmpText).alignment,
			EnableAutoSizing = ((TMP_Text)mTmpText).enableAutoSizing,
			FontSizeMin = ((TMP_Text)mTmpText).fontSizeMin,
			FontSizeMax = ((TMP_Text)mTmpText).fontSizeMax,
			CharWidthMaxAdjustment = ((TMP_Text)mTmpText).characterWidthAdjustment,
			LineSpacingMaxAdjustment = ((TMP_Text)mTmpText).lineSpacingAdjustment,
			CharacterSpacing = ((TMP_Text)mTmpText).characterSpacing,
			WordSpacing = ((TMP_Text)mTmpText).wordSpacing,
			LineSpacing = ((TMP_Text)mTmpText).lineSpacing,
			ParagraphSpacing = ((TMP_Text)mTmpText).paragraphSpacing
		};
	}

	private TMPSetting GetDefaultSettings()
	{
		return GetCurrentSettings();
	}

	private void Refresh()
	{
		LanguageGroup languageGroupByType = LanguageGroupConfig.GetLanguageGroupByType(LanguageTypeService.CurrentLanguage);
		if (!((Object)(object)mTmpText == (Object)null) && LanguageTypeService != null)
		{
			if (!mDefaultSettings.HasValue)
			{
				mDefaultSettings = GetDefaultSettings();
			}
			TMPSetting value = mDefaultSettings.Value;
			TMPSetting tMPSetting = ((!mLanguageType2Setting.TryGetValue(languageGroupByType, out var value2)) ? value : value2);
			if (!GetCurrentSettings().Equals(tMPSetting))
			{
				ApplySettings(tMPSetting);
			}
		}
	}

	private void ApplySettings(TMPSetting s)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)mTmpText == (Object)null))
		{
			((TMP_Text)mTmpText).fontStyle = s.FontStyle;
			((TMP_Text)mTmpText).enableAutoSizing = s.EnableAutoSizing;
			if (s.EnableAutoSizing)
			{
				((TMP_Text)mTmpText).fontSizeMin = s.FontSizeMin;
				((TMP_Text)mTmpText).fontSizeMax = s.FontSizeMax;
				((TMP_Text)mTmpText).characterWidthAdjustment = s.CharWidthMaxAdjustment;
				((TMP_Text)mTmpText).lineSpacingAdjustment = s.LineSpacingMaxAdjustment;
			}
			else
			{
				((TMP_Text)mTmpText).fontSize = s.FontSize;
			}
			((TMP_Text)mTmpText).characterSpacing = s.CharacterSpacing;
			((TMP_Text)mTmpText).wordSpacing = s.WordSpacing;
			((TMP_Text)mTmpText).lineSpacing = s.LineSpacing;
			((TMP_Text)mTmpText).paragraphSpacing = s.ParagraphSpacing;
			((TMP_Text)mTmpText).alignment = s.Alignment;
			((TMP_Text)mTmpText).ForceMeshUpdate(false, false);
		}
	}
}
}
