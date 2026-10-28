namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LayoutElementClamper = Zjcs.Official.WorldUI.WorldLayoutElementClamper;
using ItemProgress = Zjcs.Official.WorldUI.WorldItemProgress;
using TMPEffect = TMPro.TMPEffect;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ItemIcon : UIBase, IGameObjectPoolItem
{
	private IProfessionItemIconContext mContext;
	private IProfessionItemIconContext Host => mContext ?? throw new InvalidOperationException("Original ItemIcon services are unbound.");
	public void Bind(IProfessionItemIconContext context) => mContext = context ?? throw new ArgumentNullException(nameof(context));

	public enum ItemIconTextType
	{
		Hide,
		Custom,
		ItemCount
	}

	public enum ItemIconBottomTextType
	{
		Hide,
		Custom,
		Require,
		Submit,
		ItemCount
	}

	public enum ItemIconProgressTextType
	{
		Hide,
		Custom,
		Require,
		Submit
	}

	[Serializable]
	public class EffectConfig
	{
		public Quality Quality;

		public GameObject EffectPrefab;

		public ProfessionItemSoundSwitch SoundSwitch;
	}

	public Transform myContent;

	public Image QualityImg;

	public Image QualityShadowImg;

	public Transform BgTransform;

	public LayoutElementClamper CountTextClamper;

	[SerializeField]
	private ImageProxy mIconProxy;

	public MultiLanguageText mFakeIcon;

	[SerializeField]
	private ItemIconAttachment ItemIconAttachment;

	[SerializeField]
	private ItemIconCustomAttachment ItemIconCustomAttachment;

	private const string mCanGetHintKey = "ui_ItemIcon_centerHint_canGet";

	private const string mCanMergeHintKey = "ui_ItemIcon_centerHint_canMerge";

	private GameObject myIconEffect;

	[Header("数量")]
	public MultiLanguageText myText;

	public Image CountBgImage;

	public GameObject myTextGo;

	public MultiLanguageText myBottomText;

	public GameObject BottomGo;

	public TMPEffect BottomEffect;

	[SerializeField]
	protected ItemProgress mProgress;

	[Header("Frame光效")]
	public List<EffectConfig> FrameEffectInfo;

	private EffectConfig mCurrentEffectInfo;

	private GameObject myIconFrameEffect;

	private GameObject mIconFonrEffect;

	private Action<GameObject> mDetailDialogPopupCallback;

	private Vector3? mRawContentScale;

	private Vector3? mOriginalCountTextScale;

	private Color? mOriginalCountTextColor;

	private Color? mOriginalCountBgColor;

	private Button mButton;

	private Action<IProfessionItemData, long, object> mOnItemCountUpdateCache;

	private bool mShowTimeLimit = true;

	private UIClickSound mClickSound;

	public static readonly Vector3 sTextScale = new Vector3(1.4f, 1.4f, 1.4f);

	private SafeAction<ItemIcon> mBtnClickEvent = new SafeAction<ItemIcon>();

	public ItemIconTextType TextType { get; set; }

	public ItemIconBottomTextType BottomTextType { get; set; }

	public ItemIconProgressTextType ProgressTextType { get; set; }

	public long SubmitCountBlackBoard { get; private set; }

	public long RequireCountBlackBoard { get; private set; }




	public Button Button
	{
		get
		{
			if ((Object)(object)mButton == (Object)null)
			{
				mButton = ((Component)this).GetComponent<Button>();
			}
			return mButton;
		}
	}

	public UIClickSound ClickSound
	{
		get
		{
			if ((Object)(object)mClickSound == (Object)null)
			{
				mClickSound = ((Component)this).GetComponent<UIClickSound>();
			}
			return mClickSound;
		}
	}

	public Sprite IconSprite => mIconProxy.Sprite;

	public GameObject IconGameObject => mIconProxy.GameObject;

	public IProfessionItemInfo myInfo { get; set; }

	public IProfessionItemData myItemData { get; set; }

	public void SetShowTimeLimit(bool showTimeLimit)
	{
		mShowTimeLimit = showTimeLimit;
	}

	public virtual void Clear()
	{
		ClearFontEffect();
		ClearBgContent();
		HideText();
		HideBottomText();
		HideAllAttachment();
		SetCenterHint(show: false);
		SetBottomTextColor(ThemeColorType.Light);
		SetBottomTextEffect(isEnable: false);
		HideProgress();
		RemoveAllClickListener();
		SetButtonEnable(enabled: false);
		ResetScale();
		ResetCountTextScale();
		SetCountTextClmaper(enable: false);
		ShowQualityImg();
		mIconProxy.Clear();
		myInfo = null;
		myItemData = null;
		if ((Object)(object)mFakeIcon != (Object)null)
		{
			((Component)mFakeIcon).gameObject.SetActive(false);
		}
		if (mIconProxy.IsVaild)
		{
			mIconProxy.GameObject.SetActive(true);
		}
		ResetInitState();
	}

	protected virtual void Awake()
	{
		mOnItemCountUpdateCache = MainPlayer_OnItemCountUpdate;
		if ((Object)(object)Button != (Object)null)
		{
			((UnityEvent)Button.onClick).AddListener((UnityAction)OnBtnClick);
		}
		if ((Object)(object)myContent != (Object)null)
		{
			mRawContentScale = myContent.localScale;
		}
	}

	public virtual ItemIcon Init(int classId)
	{
		IProfessionItemInfo info = Host.GetItemInfo(classId);
		return Init(info);
	}

	public virtual ItemIcon Init(IProfessionItemInfo info)
	{
		InitWrapper(() =>
		{
			if ((Object)(object)myIconEffect != (Object)null)
			{
				Object.Destroy((Object)(object)myIconEffect);
			}
			myInfo = info;
			InitIcon(info);
			InitQualityImage();
			InitFrameEffectInfo();
		});
		return this;
	}

	public virtual ItemIcon Init(IProfessionItemData data)
	{
		myItemData = data;
		return Init(data.Info);
	}

	private void InitIcon(IProfessionItemInfo info)
	{
		if (!mIconProxy.IsVaild)
		{
			return;
		}
		mIconProxy.Load(info.Icon);
		mIconProxy.GameObject.SetActive((Object)(object)mIconProxy.Sprite != (Object)null || (Object)(object)mFakeIcon == (Object)null);
		TryActivateFakeIcon();
		if (string.IsNullOrEmpty(info.IconEffect))
		{
			return;
		}
		Rect val = mIconProxy.RectTransform.rect;
		float width = val.width;
		val = mIconProxy.Sprite.textureRect;
		float xScale = width / val.width;
		val = mIconProxy.RectTransform.rect;
		float height = val.height;
		val = mIconProxy.Sprite.textureRect;
		float yScale = height / val.height;
		if (mIconProxy.PreserveAspect)
		{
			if (xScale < yScale)
			{
				yScale = xScale;
			}
			else
			{
				xScale = yScale;
			}
		}
		Host.InstantiateAsync(info.IconEffect, mIconProxy.Transform, (GameObject go) =>
		{
			myIconEffect = go;
			myIconEffect.transform.localScale = new Vector3(xScale, yScale, 1f);
		});
	}

	private void TryActivateFakeIcon()
	{
		if (!((Object)(object)mFakeIcon == (Object)null))
		{
			((Component)mFakeIcon).gameObject.SetActive((Object)(object)mIconProxy.Sprite == (Object)null);
			if (!((Object)(object)mIconProxy.Sprite != (Object)null))
			{
				object fakeIcon = myInfo.GetFakeIcon();
				mFakeIcon.SetKey("{0}", fakeIcon);
			}
		}
	}

	public virtual void InitQualityImage()
	{
		if (myItemData != null)
		{
			UpdateQualityImage(myItemData.GetQuality());
		}
		else
		{
			UpdateQualityImage(myInfo.Quality);
		}
	}

	public void UpdateItemQualitty(Quality quality)
	{
		UpdateQualityImage(quality);
		SetFrameEffect(quality);
	}

	public virtual void UpdateQualityImage(Quality quality)
	{
		if ((Object)(object)QualityImg != null)
		{
			QualityImg.sprite = Host.Config.GetQualityBgImage(quality);
		}
	}

	public void InitFrameEffectInfo()
	{
		Quality frameEffect = ((myItemData == null) ? myInfo.Quality : myItemData.GetQuality());
		SetFrameEffect(frameEffect);
	}

	public void SetFrameEffect(Quality quality)
	{
		if ((Object)(object)myIconFrameEffect != (Object)null)
		{
			Object.Destroy((Object)(object)myIconFrameEffect);
		}
		mCurrentEffectInfo = null;
		foreach (EffectConfig item in FrameEffectInfo)
		{
			if (item.Quality == quality)
			{
				mCurrentEffectInfo = item;
				break;
			}
		}
		EffectConfig effectConfig = mCurrentEffectInfo;
		if (effectConfig != null)
		{
			Host.SetSoundSwitch(effectConfig.SoundSwitch, ((Component)this).gameObject);
		}
	}

	public void SetUIGray(bool gray)
	{
		if (!((Object)(object)Host.GrayConfig == (Object)null))
		{
			Host.SetUIGray(Host.GrayConfig, ((Component)this).gameObject, gray);
		}
	}

	protected override void OnEnableAndInited()
	{
		Host.Player.OnItemCountUpdate += mOnItemCountUpdateCache;
		RefreshCenterHintUI();
		UpdateItemTextUI();
		CheckItemTimeLimitInit();
	}

	protected override void OnDisableAndInited()
	{
		if (Host.Player != null)
		{
			Host.Player.OnItemCountUpdate -= mOnItemCountUpdateCache;
		}
		CheckItemTimeLimitStop();
	}

	protected override void OnDestroy()
	{
		mIconProxy.Dispose();
		base.OnDestroy();
	}

	private void MainPlayer_OnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		if (myInfo != null && itemData.Id == myInfo.ClassId)
		{
			OnItemCountUpdate(itemData, preCount, sourceData);
		}
	}

	protected virtual void OnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		UpdateItemTextUI();
	}

	private void UpdateItemTextUI()
	{
		UpdateTextUI();
		UpdateBottomTextUI();
		UpdateProgressTextUI();
	}

	private void UpdateTextUI()
	{
		switch (TextType)
		{
		case ItemIconTextType.Hide:
			HideText();
			break;
		case ItemIconTextType.ItemCount:
			SetCount();
			break;
		case ItemIconTextType.Custom:
			break;
		}
	}

	private void UpdateBottomTextUI()
	{
		switch (BottomTextType)
		{
		case ItemIconBottomTextType.Hide:
			HideBottomText();
			break;
		case ItemIconBottomTextType.ItemCount:
			SetBottomItemCount();
			break;
		case ItemIconBottomTextType.Custom:
		case ItemIconBottomTextType.Require:
		case ItemIconBottomTextType.Submit:
			break;
		}
	}

	private void UpdateProgressTextUI()
	{
		switch (ProgressTextType)
		{
		case ItemIconProgressTextType.Hide:
			HideProgress();
			break;
		case ItemIconProgressTextType.Require:
			SetRequireCount(RequireCountBlackBoard);
			break;
		case ItemIconProgressTextType.Submit:
			SetSubmitCount(SubmitCountBlackBoard);
			break;
		case ItemIconProgressTextType.Custom:
			break;
		}
	}

	public ItemIcon HideText()
	{
		TextType = ItemIconTextType.Hide;
		if ((Object)(object)myTextGo != (Object)null)
		{
			myTextGo.SetActive(false);
		}
		return this;
	}

	public ItemIcon HideQualityImg()
	{
		if ((Object)(object)QualityImg != (Object)null)
		{
			((Component)QualityImg).gameObject.SetActive(false);
		}
		SetShadowImg(show: false);
		return this;
	}

	public ItemIcon ShowQualityImg()
	{
		if ((Object)(object)QualityImg != (Object)null)
		{
			((Component)QualityImg).gameObject.SetActive(true);
		}
		SetShadowImg(show: true);
		return this;
	}

	public void SetShadowImg(bool show)
	{
		if ((Object)(object)QualityShadowImg != (Object)null)
		{
			((Component)QualityShadowImg).gameObject.SetActive(show);
		}
	}

	public ItemIcon HideBottomText()
	{
		BottomTextType = ItemIconBottomTextType.Hide;
		if ((Object)(object)BottomGo != null)
		{
			BottomGo.SetActive(false);
		}
		return this;
	}

	public ItemIcon SetText(ProfessionItemText textKeyArg)
	{
		TextType = ItemIconTextType.Custom;
		return InternalSetText(textKeyArg.Key, textKeyArg.Args);
	}

	public ItemIcon SetTextArg(object arg)
	{
		TextType = ItemIconTextType.Custom;
		return InternalSetText("{0}", arg);
	}

	public ItemIcon SetBottomText(ProfessionItemText textKeyArg)
	{
		BottomTextType = ItemIconBottomTextType.Custom;
		return InternalSetBottomText(textKeyArg.Key, textKeyArg.Args);
	}

	public ItemIcon SetBottomTextArg(object arg)
	{
		BottomTextType = ItemIconBottomTextType.Custom;
		return InternalSetBottomText("{0}", arg);
	}

	public ItemIcon SetTextType(ItemIconTextType type)
	{
		TextType = type;
		UpdateTextUI();
		return this;
	}

	public ItemIcon SetBottomTextType(ItemIconBottomTextType type)
	{
		BottomTextType = type;
		UpdateBottomTextUI();
		return this;
	}

	public ItemIcon SetBottomName()
	{
		BottomTextType = ItemIconBottomTextType.Custom;
		return InternalSetBottomText("{0}", Host.CultureText(Host.GetItemNameKey(myInfo.ClassId)));
	}

	public ItemIcon SetBottomTextEffect(bool isEnable)
	{
		if ((Object)(object)BottomEffect != (Object)null)
		{
			((Behaviour)BottomEffect).enabled = isEnable;
		}
		return this;
	}

	private ItemIcon InternalSetText(string key, params object[] args)
	{
		if ((Object)(object)myText == (Object)null || (Object)(object)myTextGo == (Object)null)
		{
			return this;
		}
		myTextGo.SetActive(true);
		myText.SetKey(key, args);
		return this;
	}

	private ItemIcon InternalSetBottomText(string key, params object[] args)
	{
		if ((Object)(object)BottomGo != (Object)null)
		{
			BottomGo.SetActive(true);
		}
		if ((Object)(object)myBottomText != (Object)null)
		{
			myBottomText.SetKey(key, args);
		}
		return this;
	}

	public virtual ItemIcon SetCount()
	{
		return SetCountCommon();
	}

	public virtual ItemIcon SetCount(long count, string countKey = "ui_ItemIcon_count_key")
	{
		return SetCountCommon(count, countKey);
	}

	public virtual ItemIcon SetCount(float count)
	{
		return SetCountCommon(count);
	}

	protected ItemIcon SetCountCommon()
	{
		TextType = ItemIconTextType.ItemCount;
		if (!myInfo.CanOverlap)
		{
			return HideText();
		}
		long value = ((myItemData != null) ? myItemData.Count : 0);
		InternalSetText("ui_ItemIcon_count_key", Host.Compact(value));
		return this;
	}

	protected ItemIcon SetCountCommon(long count, string countKey = "ui_ItemIcon_count_key")
	{
		TextType = ItemIconTextType.Custom;
		return InternalSetText(countKey, Host.Compact(count));
	}

	protected ItemIcon SetCountCommon(float count)
	{
		TextType = ItemIconTextType.Custom;
		return InternalSetText("ui_ItemIcon_count_key", count);
	}

	public ItemIcon SetCountTextScale(Vector3 scale)
	{
		if (!mOriginalCountTextScale.HasValue)
		{
			mOriginalCountTextScale = ((Component)myText).transform.localScale;
		}
		((Component)myText).transform.localScale = scale;
		return this;
	}

	public void ResetCountTextScale()
	{
		if (mOriginalCountTextScale.HasValue)
		{
			((Component)myText).transform.localScale = mOriginalCountTextScale.Value;
		}
	}

	public ItemIcon SetRequireCount(long requireCount)
	{
		ProgressTextType = ItemIconProgressTextType.Require;
		SetRequireCountInfo(requireCount);
		long currentCount = myItemData?.Count ?? Host.Player.GetItemAmount(myInfo.ClassId);
		UpdateRequireTxt(currentCount, requireCount);
		return this;
	}

	public ItemIcon SetSubmitCount(long submitCount)
	{
		ProgressTextType = ItemIconProgressTextType.Submit;
		SubmitCountBlackBoard = submitCount;
		long currentCount = myItemData?.Count ?? Host.Player.GetItemAmount(myInfo.ClassId);
		UpdateSubmitTxt(currentCount, submitCount);
		return this;
	}

	public ItemIcon SetProgressText(long count, long targetCount, ItemProgress.ProgressType progressType)
	{
		ProgressTextType = ItemIconProgressTextType.Custom;
		switch (progressType)
		{
		case ItemProgress.ProgressType.Require:
			UpdateRequireTxt(count, targetCount);
			break;
		case ItemProgress.ProgressType.Submit:
			UpdateSubmitTxt(count, targetCount);
			break;
		}
		return this;
	}

	public ItemIcon SetCustomProgressText(ProfessionItemText textKeyArgWrap, Color? color = null)
	{
		ProgressTextType = ItemIconProgressTextType.Custom;
		if ((Object)(object)mProgress != (Object)null)
		{
			((Component)mProgress).gameObject.SetActive(true);
			mProgress.Init(textKeyArgWrap.Key, textKeyArgWrap.Args, color);
		}
		return this;
	}

	private ItemIcon SetProgress(long count, long targetCount, ItemProgress.ProgressType type)
	{
		if ((Object)(object)mProgress != (Object)null)
		{
			((Component)mProgress).gameObject.SetActive(true);
			mProgress.Init(count, targetCount, type);
		}
		return this;
	}

	protected void RefreshCenterHintUI()
	{
		bool centerHintObjActive = SetCenterHintUIText();
		ItemIconAttachmentHint attachment = GetAttachment<ItemIconAttachmentHint>(ItemIconAttachmentType.HintTip, autoActive: false);
		if ((Object)(object)attachment != (Object)null)
		{
			attachment.SetCenterHintObjActive(centerHintObjActive);
		}
	}

	protected virtual bool SetCenterHintUIText()
	{
		if (RequireCountBlackBoard <= 0 && SubmitCountBlackBoard <= 0)
		{
			return false;
		}
		if ((myItemData?.Count ?? Host.Player.GetItemAmount(myInfo.ClassId)) >= RequireCountBlackBoard)
		{
			return false;
		}
		if (!Host.TryGetSourceData(myInfo.ClassId, out var resultList))
		{
			return false;
		}
		foreach (IProfessionItemSource item in resultList)
		{
			ItemSourceType sourceType = item.SourceType;
			if (sourceType == ItemSourceType.ItemMerge && Host.CanMerge(myInfo.ClassId, recursion: true))
			{
				SetCenterHintText("ui_ItemIcon_centerHint_canMerge");
				return true;
			}
			if (sourceType == ItemSourceType.MapResource)
			{
				object locationInfos = Host.GetLocationInfos(item.ItemId);
				if (Host.Player.CanGetItemFromMapLocation(locationInfos))
				{
					SetCenterHintText("ui_ItemIcon_centerHint_canGet");
					return true;
				}
			}
		}
		return false;
	}

	public ItemIcon HideProgress()
	{
		ProgressTextType = ItemIconProgressTextType.Hide;
		if ((Object)(object)mProgress != (Object)null)
		{
			((Component)mProgress).gameObject.SetActive(false);
		}
		return this;
	}

	public ItemIcon SetCountTextColor(Color? textColor = null, Color? textBgColor = null)
	{
		if ((Object)(object)myText != (Object)null)
		{
			if (!mOriginalCountTextColor.HasValue)
			{
				mOriginalCountTextColor = myText.Color;
			}
			if (textColor.HasValue)
			{
				myText.Color = textColor.Value;
			}
		}
		if ((Object)(object)CountBgImage != (Object)null)
		{
			if (!mOriginalCountBgColor.HasValue)
			{
				mOriginalCountBgColor = ((Graphic)CountBgImage).color;
			}
			if (textBgColor.HasValue)
			{
				((Graphic)CountBgImage).color = textBgColor.Value;
			}
		}
		return this;
	}

	public void ResetCountTextColor()
	{
		if ((Object)(object)CountBgImage != (Object)null && mOriginalCountBgColor.HasValue)
		{
			((Graphic)CountBgImage).color = mOriginalCountBgColor.Value;
		}
		if ((Object)(object)myText != (Object)null && mOriginalCountTextColor.HasValue)
		{
			myText.Color = mOriginalCountTextColor.Value;
		}
	}

	public ItemIcon SetCenterHintContent(bool show)
	{
		SetAttachment(ItemIconAttachmentType.HintTip, show);
		return this;
	}

	public ItemIcon SetCenterHint(bool show, string key = null)
	{
		SetCenterHintContent(show);
		ItemIconAttachmentHint attachment = GetAttachment<ItemIconAttachmentHint>(ItemIconAttachmentType.HintTip, autoActive: false);
		if ((Object)(object)attachment != (Object)null)
		{
			attachment.SetCenterHintObjActive(show);
			if (show)
			{
				attachment.SetKey(key);
			}
		}
		return this;
	}

	public ItemIcon SetCenterHintText(string key)
	{
		ItemIconAttachmentHint attachment = GetAttachment<ItemIconAttachmentHint>(ItemIconAttachmentType.HintTip, autoActive: false);
		if ((Object)(object)attachment != (Object)null)
		{
			attachment.SetKey(key);
		}
		return this;
	}

	public ItemIcon SetRequireCountInfo(long requireCount)
	{
		RequireCountBlackBoard = requireCount;
		RefreshCenterHintUI();
		return this;
	}

	public ItemIcon SetBottomItemCount()
	{
		BottomTextType = ItemIconBottomTextType.ItemCount;
		return InternalSetBottomText("{0}", Host.Compact(myItemData?.Count ?? 0));
	}

	public ItemIcon SetBottomTextColor(ThemeColorType themeColorType)
	{
		if ((Object)(object)myBottomText != (Object)null)
		{
			myBottomText.Color = Host.Colors.GetTextColor(themeColorType, ColorType.Normal);
		}
		return this;
	}

	private void UpdateSubmitTxt(long currentCount, long submitCount)
	{
		SetProgress(currentCount, submitCount, ItemProgress.ProgressType.Submit);
		mProgress.SetColor(Host.Colors.Green, Host.Colors.White);
	}

	private void UpdateRequireTxt(long currentCount, long requireCount)
	{
		SetProgress(currentCount, requireCount, ItemProgress.ProgressType.Require);
		mProgress.SetColor(Host.Colors.Green, Host.Colors.LightRed);
	}

	public ItemIcon SetScale(float scale)
	{
		myContent.localScale = new Vector3(scale, scale, scale);
		return this;
	}

	public void ResetScale()
	{
		if ((Object)(object)myContent != (Object)null && mRawContentScale.HasValue)
		{
			myContent.localScale = mRawContentScale.Value;
		}
	}

	public void AddBgContent(GameObject bgGo, float scale = 1f)
	{
		if ((Object)(object)BgTransform != (Object)null)
		{
			Host.Instantiate(bgGo, BgTransform).transform.localScale = new Vector3(scale, scale, scale);
		}
	}

	public void AddFontContent(GameObject fontGo)
	{
		if ((Object)(object)myContent != (Object)null)
		{
			if ((Object)(object)mIconFonrEffect != (Object)null)
			{
				Object.Destroy((Object)(object)mIconFonrEffect);
			}
			mIconFonrEffect = Host.Instantiate(fontGo, myContent);
		}
	}

	public void ClearBgContent()
	{
		if ((Object)(object)BgTransform != (Object)null)
		{
			for (int i = BgTransform.childCount - 1; i >= 0; i--) Object.Destroy(BgTransform.GetChild(i).gameObject);
		}
	}

	public void ClearFontEffect()
	{
		if ((Object)(object)mIconFonrEffect != (Object)null)
		{
			Object.Destroy((Object)(object)mIconFonrEffect);
			mIconFonrEffect = null;
		}
	}

	public ItemIcon SetClickPopupInfoDialog(Action<GameObject> popupCallback = null)
	{
		mDetailDialogPopupCallback = popupCallback;
		SetClickListener(PopupInfoDialog);
		return this;
	}

	public virtual ItemIcon SetAttachment(ItemIconAttachmentType attachmentType, bool show)
	{
		if ((Object)(object)ItemIconCustomAttachment != (Object)null && ItemIconCustomAttachment.SetShow(attachmentType, show))
		{
			return this;
		}
		if ((Object)(object)ItemIconAttachment != (Object)null)
		{
			ItemIconAttachment.SetShow(attachmentType, show);
		}
		return this;
	}

	public ItemIcon HideAllAttachment()
	{
		if ((Object)(object)ItemIconCustomAttachment != (Object)null)
		{
			ItemIconCustomAttachment.HideAll();
		}
		if ((Object)(object)ItemIconAttachment != (Object)null)
		{
			ItemIconAttachment.HideAll();
		}
		return this;
	}

	public ItemIconAttachmentCell GetAttachment(ItemIconAttachmentType attachmentType, bool autoActive = true)
	{
		if ((Object)(object)ItemIconCustomAttachment != (Object)null)
		{
			ItemIconAttachmentCell attachment = ItemIconCustomAttachment.GetAttachment(attachmentType);
			if ((Object)(object)attachment != (Object)null)
			{
				return attachment;
			}
		}
		if ((Object)(object)ItemIconAttachment != (Object)null)
		{
			return ItemIconAttachment.GetAttachment(attachmentType, autoActive);
		}
		return null;
	}

	public T GetAttachment<T>(ItemIconAttachmentType attachmentType, bool autoActive = true) where T : ItemIconAttachmentCell
	{
		return GetAttachment(attachmentType, autoActive) as T;
	}

	private void PopupInfoDialog(ItemIcon icon)
	{
		if (myItemData != null)
		{
			Host.PopupInfoDialog(myItemData, this, RequireCountBlackBoard, mDetailDialogPopupCallback);
			return;
		}
		if (myInfo != null)
		{
			Host.PopupInfoDialog(myInfo, this, RequireCountBlackBoard, mDetailDialogPopupCallback);
			return;
		}
		Debug.LogError("PopupInfoDialog Error Info Is Null", this);
	}

	public void PopupInfoDialog(Action<GameObject> callBack = null)
	{
		if (myItemData != null)
		{
			Host.PopupInfoDialog(myItemData, this, RequireCountBlackBoard, callBack);
			return;
		}
		if (myInfo != null)
		{
			Host.PopupInfoDialog(myInfo, this, RequireCountBlackBoard, callBack);
			return;
		}
		Debug.LogError("PopupInfoDialog Error Info Is Null", this);
	}

	public void PopupInfoDialog()
	{
		PopupInfoDialog(this);
	}

	public void SetClickListener(Action<ItemIcon> onClick)
	{
		RemoveAllClickListener();
		mBtnClickEvent.AddListener(onClick);
		SetButtonEnable(enabled: true);
	}

	private void SetButtonEnable(bool enabled)
	{
		((Behaviour)Button).enabled = enabled;
		((Behaviour)ClickSound).enabled = enabled;
	}

	private void OnBtnClick()
	{
		mBtnClickEvent.Invoke(this);
	}

	public void RemoveAllClickListener()
	{
		mBtnClickEvent.ClearListener();
		SetButtonEnable(enabled: false);
	}

	[ContextMenu("设置父节点的缩放")]
	public void SetParentScale()
	{
		RectTransform component = ((Component)((Component)this).transform.parent).GetComponent<RectTransform>();
		RectTransform component2 = ((Component)this).GetComponent<RectTransform>();
		Vector3 one = Vector3.one;
		Rect rect = component.rect;
		Vector3 val = one * rect.width;
		rect = component2.rect;
		((Transform)component).localScale = val / rect.width;
	}

	public GameObject SetFrameEffect(Transform transform = null, bool isShow = true, bool isResetScale = true)
	{
		GameObject val = mCurrentEffectInfo?.EffectPrefab;
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		if (transform == null)
		{
			transform = ((Component)this).transform;
		}
		myIconFrameEffect = Host.Instantiate(val, transform, isResetScale ? EUIInstantiateFlag.All : EUIInstantiateFlag.KeepScale);
		myIconFrameEffect.SetActive(isShow);
		return myIconFrameEffect;
	}

	public GameObject SetFrameEffect(GameObject effectGo, Transform transform = null, bool isShow = true, bool isResetScale = true)
	{
		if ((Object)(object)effectGo == (Object)null)
		{
			return null;
		}
		if (transform == null)
		{
			transform = ((Component)this).transform;
		}
		myIconFrameEffect = Host.Instantiate(effectGo, transform, isResetScale ? EUIInstantiateFlag.All : EUIInstantiateFlag.KeepScale);
		myIconFrameEffect.SetActive(isShow);
		return myIconFrameEffect;
	}

	public void OnGameObjectPoolReset()
	{
		((Component)this).transform.localScale = Vector3.one;
		((Component)this).transform.localPosition = Vector3.zero;
	}

	public void OnGameObjectPoolClear()
	{
		Clear();
	}

	public void LoadIcon(string iconPath)
	{
		mIconProxy.Load(iconPath);
	}

	public void SetItemIcon(IProfessionItemInfo itemInfo)
	{
		LoadIcon(itemInfo.Icon);
	}

	public void SetCountTextClmaper(bool enable)
	{
		if ((Object)(object)CountTextClamper != (Object)null)
		{
			((Behaviour)CountTextClamper).enabled = enable;
		}
	}

	public virtual void SetAgiotage(long agiotage)
	{
	}

	private void CheckItemTimeLimitInit()
	{
		if (mShowTimeLimit)
		{
			IProfessionItemTimeLimitAttachment itemIconAttachmentTimeLimit = GetItemIconAttachmentTimeLimit();
			if (!((Object)(object)itemIconAttachmentTimeLimit == (Object)null))
			{
				itemIconAttachmentTimeLimit.Init(myItemData);
			}
		}
	}

	private void CheckItemTimeLimitStop()
	{
		if (mShowTimeLimit)
		{
			IProfessionItemTimeLimitAttachment itemIconAttachmentTimeLimit = GetItemIconAttachmentTimeLimit();
			if (!((Object)(object)itemIconAttachmentTimeLimit == (Object)null))
			{
				itemIconAttachmentTimeLimit.Stop();
			}
		}
	}

	private IProfessionItemTimeLimitAttachment GetItemIconAttachmentTimeLimit()
	{
		if (myItemData == null || !myItemData.CanExpire())
		{
			return null;
		}
		return GetAttachment(ItemIconAttachmentType.TimeLimit) as IProfessionItemTimeLimitAttachment;
	}

	static ItemIcon()
	{
	}
}

}
