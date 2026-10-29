namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionPlayerPropItem : MonoBehaviour, IGameObjectPoolItem, IWindowOperator
{
	public Image myBg;

	public Image myIcon;

	public WorldMultiLanguageText myPropTypeText;

	[Header("无预览时显示")]
	public WorldMultiLanguageText myPropValueText;

	[Header("有预览时显示")]
	public GameObject myPropPreviewObj;

	public GameObject myEffect;

	public Button PropDetailBtn;

	public WorldMultiLanguageText PropValueFromText;

	public WorldMultiLanguageText PropValueToText;

	[SerializeField]
	private WorldStateSwitcher<bool> mActiveStateSwitcher;

	[SerializeField]
	private ProfessionBlessPropBtn mBlessPropBtn;

	public float myChangeTextTime = 0.2f;

	public float myEffectDuration = 0.55f;

	[Header("只用valueText显示所有信息")]
	public bool UseOneText;

	public Color DefaultTextColor;

	public Color PreviewTextColor;

	protected bool mIsMainPropShowPreviewColor;

	protected bool mIsExtraPropShowPreviewColor;

	protected ProfessionPropertyLanguage.IColorfulPropStr mPropFromValue;

	private ProfessionPropertyLanguage.IColorfulPropStr mPropToValue;

	protected PropType mPropType;

	private bool mIsShowPreview;

	private bool mIsValueNegative;

	[Header("预览对比值增减颜色不同")]
	public bool AdjustPreviewToColor;

	public Color PreviewIncreaseColor;

	public Color PreviewDecreaseColor;

	[Header("符号(限非预览)")]
	public bool ShowSign;

	[SerializeField]
	private Image mLineIcon;

	private object mDescribleArg;

	private string mFormatKey = "{0}";

	private ProfessionResourceOperationBag mDisposableBag;

	private bool mNeedSpaceBeforeValue;

	private Func<Sprite> mGetPropIconFunc;


    private IProfessionPropertyBindings mBindings;
    private IProfessionPropertyBindings Bindings => mBindings ?? ProfessionPropertyBindings.Required;
    public void Configure(IProfessionPropertyBindings bindings)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }


	public GameObject Operator => ((Component)this).gameObject;

	private void Awake()
	{
		mDisposableBag = new ProfessionResourceOperationBag();
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
	}

    public void Init(string propName, string value, Sprite icon, string nextValue = null,
        bool instant = true, bool isBless = false, bool isNegative = false,
        string valueFormatKey = "{0}", bool previewIncreasing = false)
    {
        mFormatKey = valueFormatKey;
        var previousIcon = mGetPropIconFunc;
        mGetPropIconFunc = () => icon;
        try { Init(new ProfessionComplexProp
        {
            PropName = propName, CurValue = 0, NextValue = previewIncreasing ? 1 : 0,
            CurValueObj = ProfessionComplexProp.MakePropValueObj(value),
            NextValueObj = nextValue == null ? null : ProfessionComplexProp.MakePropValueObj(nextValue),
            IsBless = isBless, IsPercentValueNegative = isNegative
        }, instant); }
        finally { mGetPropIconFunc = previousIcon; }
    }

	public virtual void Init(PropType propType, long value, long nextValue = -1L, bool instant = true, object arg = null, bool isBless = false, string formatKey = "{0}")
	{
		Init(propType, ProfessionPropertyConfig.GetPropTypeName(propType), value, nextValue, instant, arg, isBless, formatKey);
	}

	public virtual void Init(PropType propType, string propName, long value, long nextValue = -1L, bool instant = true, object arg = null, bool isBless = false, string fomatKey = "{0}")
	{
		mFormatKey = fomatKey;
		Init(new ProfessionComplexProp
		{
			PropType = propType,
			PropName = propName,
			CurValue = value,
			NextValue = nextValue,
			CurValueObj = ProfessionComplexProp.MakePropValueObj(propType, value),
			NextValueObj = ((nextValue != -1) ? ProfessionComplexProp.MakePropValueObj(propType, value, nextValue) : null),
			IsBless = isBless
		}, instant, arg);
	}

	public virtual void Init(string propName, long value, long nextValue = -1L, bool instant = true, object arg = null, bool isBless = false)
	{
		Init(propName, value.ToString(), (nextValue > 0) ? nextValue.ToString() : null, instant, arg, isBless);
	}

	public void Init(string propName, string value, string nextValue = null, bool instant = true, object arg = null, bool isBless = false)
	{
		Init(new ProfessionComplexProp
		{
			PropName = propName,
			CurValueObj = ProfessionComplexProp.MakePropValueObj(value),
			NextValueObj = ((nextValue != null) ? ProfessionComplexProp.MakePropValueObj(nextValue) : null),
			IsBless = isBless
		}, instant, arg);
	}

	public void Init(string propName, ProfessionPropertyConfig.PropItemValue value, ProfessionPropertyConfig.PropItemValue nextValue = null, bool instant = true, object arg = null, bool isBless = false)
	{
		Init(new ProfessionComplexProp
		{
			PropType = value.PropValue.PropType,
			PropName = propName,
			CurValueObj = value,
			NextValueObj = nextValue,
			IsBless = isBless
		}, instant, arg);
	}

	public void Init(PropType propType, ProfessionPropertyConfig.PropItemValue value, ProfessionPropertyConfig.PropItemValue nextValue = null, bool instant = true, object arg = null, bool isBless = false)
	{
		Init(new ProfessionComplexProp
		{
			PropType = propType,
			PropName = ProfessionPropertyConfig.GetPropTypeName(propType),
			CurValueObj = value,
			NextValueObj = nextValue,
			IsBless = isBless
		}, instant, arg);
	}

	public virtual void Init(ProfessionComplexProp prop, bool instant = true, object arg = null)
	{
		mPropType = prop.PropType;
		mDescribleArg = arg;
		if ((Object)(object)myPropTypeText != (Object)null)
		{
			myPropTypeText.SetKey(prop.PropName);
		}
		mPropFromValue = prop.CurValueObj;
		mPropToValue = prop.NextValueObj;
		mIsShowPreview = prop.IsShowPreview;
		mIsValueNegative = prop.IsPercentValueNegative;
		if (AdjustPreviewToColor && (Object)(object)PropValueToText != (Object)null)
		{
			PropValueToText.Color = (prop.IsPercentValueIncreasing ? PreviewIncreaseColor : PreviewDecreaseColor);
		}
		mActiveStateSwitcher.SetState(prop.IsBless);
		if (instant)
		{
			ApplyText();
		}
		ApplyIcon();
	}

	private void OnEnable()
	{
		if ((Object)(object)PropDetailBtn != (Object)null)
		{
			PropDetailBtn.onClick.AddListener(ShowPropDetail);
		}
	}

	private void OnDisable()
	{
		if ((Object)(object)PropDetailBtn != (Object)null)
		{
			PropDetailBtn.onClick.RemoveListener(ShowPropDetail);
		}
		StopEffect();
	}

	private void ShowPropDetail()
	{
		if (mPropType == PropType.None)
		{
			return;
		}
		int entryId = Bindings.GetProperty(mPropType).EntryId;
		if (entryId > 0)
		{
			string spritePath = Bindings.GetEntryIcon(entryId);
			ProfessionItemText nameStr = new ProfessionItemText { Key = ProfessionPropertyLanguage.GetEntryNameKey(entryId) };
			ProfessionItemText descStr = new ProfessionItemText { Key = ProfessionPropertyLanguage.GetEntryDescKey(entryId), Args = new object[] { mDescribleArg } };
			Bindings.PopupEntryDetail(this, spritePath, nameStr, descStr, controlXPos: false);
		}
	}

	protected virtual void ApplyText()
	{
		if ((Object)(object)myPropValueText != (Object)null)
		{
			((Component)myPropValueText).gameObject.SetActive(!mIsShowPreview);
			PopulateValue(myPropValueText, mPropFromValue);
		}
		if ((Object)(object)myPropPreviewObj != (Object)null)
		{
			myPropPreviewObj.SetActive(mIsShowPreview);
		}
		if ((Object)(object)PropValueFromText != (Object)null)
		{
			PropValueFromText.SetKey(mFormatKey, mPropFromValue);
		}
		if ((Object)(object)PropValueToText != (Object)null && mPropToValue != null)
		{
			if (DefaultTextColor.a > 0f && PreviewTextColor.a > 0f)
			{
				(bool, bool) tuple = mPropToValue.IsPropSameWith(mPropFromValue);
				bool item = tuple.Item1;
				bool item2 = tuple.Item2;
				Color value = (item ? DefaultTextColor : PreviewTextColor);
				Color value2 = (item2 ? DefaultTextColor : PreviewTextColor);
				PropValueToText.SetKey(mFormatKey, mPropToValue.ToString(value, value2, Bindings.Language));
			}
			else
			{
				PropValueToText.SetKey(mFormatKey, mPropToValue);
			}
		}
	}

	private void ApplyIcon()
	{
		if (!((Object)(object)myIcon == (Object)null))
		{
			Sprite propIcon = GetPropIcon();
			((Component)myIcon).gameObject.SetActive((Object)(object)propIcon != (Object)null);
			myIcon.sprite = propIcon;
		}
	}

	private Sprite GetPropIcon()
	{
		if (mGetPropIconFunc != null)
		{
			return mGetPropIconFunc?.Invoke();
		}
		if (mPropType == PropType.None)
		{
			return null;
		}
		ProfessionPropertyDefinition propCfgInfo = Bindings.GetProperty(mPropType);
		return mDisposableBag.Add(Bindings.Resources.LoadAsset<Sprite>("Assets/Image/" + propCfgInfo.SmallIcon)).Result;
	}

	public void SetNeedSpaceBeforeValue(bool needSpaceBeforeValue)
	{
		mNeedSpaceBeforeValue = needSpaceBeforeValue;
		ApplyText();
	}

	private void PopulateValue(WorldMultiLanguageText text, object value)
	{
		if (mNeedSpaceBeforeValue)
		{
			value = Bindings.TextFormatted("ui_space_before_value", value);
		}
		if (ShowSign && !mIsValueNegative)
		{
			text.SetKey("ui_common_prop_plus", value);
		}
		else if (UseOneText)
		{
			string text2 = Bindings.Text(ProfessionPropertyConfig.GetPropTypeName(mPropType));
			text.SetKey(mFormatKey, text2, value);
		}
		else
		{
			text.SetKey(mFormatKey, value);
		}
	}

	public IEnumerator PlayEffect(bool isForceShow = false)
	{
		if (!((Object)(object)myEffect == (Object)null) && (isForceShow || CheckCanShowEffect()))
		{
			myEffect.SetActive(true);
			yield return (object)new WaitForSeconds(myChangeTextTime);
			ApplyText();
			yield return (object)new WaitForSeconds(myEffectDuration - myChangeTextTime);
			myEffect.SetActive(false);
		}
	}

	private bool CheckCanShowEffect()
	{
		return mIsShowPreview;
	}

	public void StopEffect()
	{
		ApplyText();
		if ((Object)(object)myEffect != (Object)null)
		{
			myEffect.SetActive(false);
		}
	}

	public void SetBgVisible(bool visible)
	{
		if (!((Object)(object)myBg == (Object)null))
		{
			((Behaviour)myBg).enabled = visible;
		}
	}

	public void SetLineVisible(bool visible)
	{
		if (!((Object)(object)mLineIcon == (Object)null))
		{
			((Behaviour)mLineIcon).enabled = visible;
		}
	}

	public void InitBlessPropBtn(int equipRank, int serverRank, PropType type, long rawValue, long blessValue, bool isBless)
	{
		if ((Object)(object)mBlessPropBtn != (Object)null)
		{
			mBlessPropBtn.Init(equipRank, serverRank, type, rawValue, blessValue, isBless);
		}
	}

	public void InitBlessPropBtn(int serverRank, string propName, ProfessionPropertyConfig.PropItemValue value, ProfessionPropertyConfig.PropItemValue blessValue, bool hasBlessLevel)
	{
		if ((Object)(object)mBlessPropBtn != (Object)null)
		{
			mBlessPropBtn.Init(serverRank, propName, value, blessValue, hasBlessLevel);
		}
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}

}
