// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class StoryMask : MonoBehaviour
{
    public IStoryMaskHost Host { get; private set; }
    private IStoryMaskManager boundManager;
    public void Bind(IStoryMaskHost host, IStoryMaskManager manager)
    {
        if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind story mask before enabling it.");
        Host = host;
        boundManager = manager;
        MaskAssetProxy.LoadPrefab = host.LoadPrefab;
        MaskAssetProxy.Initialize = go => go.GetComponent<StoryMaskGraphic>().Bind(manager);
        mAttachToObjectLocalUIRect = AttachToObjectLocalUI.GetComponent<RectTransform>();
    }
	private static Color sNormalGrayColor = new Color(0f, 0f, 0f, 0.78f);

	public StoryMaskPrefabProxy MaskAssetProxy;

	public Button ClickCallMaskButton;

	public GameObject FullMask;

	public Image ColorMaskImg;

	public AttachToObjUI AttachToObjUI;

	public AttachToObjectLocalUI AttachToObjectLocalUI;

	[SerializeField]
	private Image mNoStoryColorMaskImg;

	[SerializeField]
	private RectTransform mRootTransform;

	private CanvasScaler mCanvasScaler;

	private RectTransform mUITargetRect;

	private Vector3 mUIScale = Vector3.one;

	private RectTransform mAttachToObjectLocalUIRect;

	private StoryMaskGraphic mMaskGraphic;

	private Action mClickCallMaskEvent;

	public bool IsPlayingSlotMask { get; private set; }

	public bool IsPlayingFullMask { get; private set; }

	public bool IsPlayingColorMask { get; private set; }

	private StoryMaskGraphic mGuideMaskGraphic
	{
		get
		{
			if ((Object)(object)mMaskGraphic == (Object)null)
			{
				GameObject val = MaskAssetProxy.Instantiate();
				mMaskGraphic = val.GetComponent<StoryMaskGraphic>();
			}
			return mMaskGraphic;
		}
	}

	private void OnEnable()
	{
		mCanvasScaler = ((Component)this).GetComponentInParent<CanvasScaler>();
	}

	private void OnDisable()
	{
		boundManager?.Reset();
		if (((Object)(object)mMaskGraphic))
		{
			mMaskGraphic.Close();
			Object.Destroy(mMaskGraphic.gameObject);
			mMaskGraphic = null;
		}
		MaskAssetProxy.Unload();
	}

	private void Start()
	{

		mAttachToObjectLocalUIRect = ((Component)AttachToObjectLocalUI).GetComponent<RectTransform>();
		Host.OnWindowClosing += new Action<GameObject>(OnWindowClosing);
		Host.OnWindowClosed += new Action<GameObject>(OnWindowClosed);
		Host.OnOpeningWindowStateChanged += new Action<bool>(OnOpeningWindowStateChanged);
		((UnityEvent)ClickCallMaskButton.onClick).AddListener((UnityAction)OnClickCallMaskClick);
	}

	private void Update()
	{

		if (((Object)(object)mUITargetRect))
		{
			RectTransform val = mAttachToObjectLocalUIRect;
			Rect rect = mUITargetRect.rect;
			val.sizeDelta = rect.size * (Vector2)mUIScale;
		}
	}

	private void OnDestroy()
	{

		mUITargetRect = null;
		if (Host != null)
		{
			Host.OnWindowClosing -= new Action<GameObject>(OnWindowClosing);
			Host.OnWindowClosed -= new Action<GameObject>(OnWindowClosed);
			Host.OnOpeningWindowStateChanged -= new Action<bool>(OnOpeningWindowStateChanged);
		}
		((UnityEvent)ClickCallMaskButton.onClick).RemoveListener((UnityAction)OnClickCallMaskClick);
	}

	public void PlayMask(GameObject gameObject, Vector3 offset, Vector3 scale, Vector3 rectWLeftBottom, Vector3 rectWRightTop)
	{

		Canvas canvas = AttachToObjUI.Canvas;
		RectTransform component = ((Component)AttachToObjUI).GetComponent<RectTransform>();
		component.sizeDelta = GetSize(rectWLeftBottom, rectWRightTop, canvas);

		mRootTransform.pivot = new Vector2(0.5f, 0.5f);
		AttachToObjUI.myOffset = offset;
		AttachToObjUI.AttachObject = gameObject;
		mGuideMaskGraphic.Play(component, scale, MaskSetting.DefaultColor, true, "");
		IsPlayingSlotMask = true;
	}

	public void CloseMask()
	{

		mGuideMaskGraphic.Close();
		AttachToObjUI.AttachObject = null;
		AttachToObjectLocalUI.AttachObject = null;
		mUITargetRect = null;
		IsPlayingSlotMask = false;
	}

	public void Play(GameObject targetGo, MaskSetting setting, Vector2 customSize = default(Vector2))
	{

		string tipKey = ((setting.TipUIKeyId == 0) ? "" : "story_tip_" + setting.TipUIKeyId);
		Play(targetGo, setting, tipKey, customSize);
	}

	public void Play(RectTransform targetRect, MaskSetting setting)
	{
		string tipKey = ((setting.TipUIKeyId == 0) ? "" : "story_tip_" + setting.TipUIKeyId);
		Play(targetRect, setting, tipKey);
	}

	public void Play(Button button, MaskSetting setting)
	{
		RectTransform buttonApplyMaskRect = GetButtonApplyMaskRect(button);
		Play(buttonApplyMaskRect, setting);
	}

	private void Play(RectTransform target, MaskSetting setting, string tipKey = "")
	{

		bool enabled;
		if ((Object)(object)target == (Object)null)
		{

			return;
		}

		Rect rect;

		if ((Object)(object)((Component)target).GetComponentInParent<CanvasScaler>() == (Object)(object)mCanvasScaler)
		{
			mGuideMaskGraphic.Play(target, setting.Scale, setting.MaskColor, setting.ShowMask, tipKey, setting.TipArgs);
			return;
		}
		mRootTransform.pivot = new Vector2(0f, 0f);
		AttachToObjectLocalUI.AttachObject = ((Component)target).gameObject;
		AttachToObjectLocalUI.myOffset = setting.Offset;
		mAttachToObjectLocalUIRect.pivot = target.pivot;
		RectTransform val = mAttachToObjectLocalUIRect;
		rect = target.rect;
		val.sizeDelta = rect.size * (Vector2)setting.Scale;
		mGuideMaskGraphic.Play(mAttachToObjectLocalUIRect, Vector3.one, setting.MaskColor, setting.ShowMask, tipKey, setting.TipArgs);
		mUIScale = setting.Scale;
		mUITargetRect = target;
		IsPlayingSlotMask = true;
	}

	private void Play(GameObject go, MaskSetting setting, string tipKey = "", Vector2 customSize = default(Vector2))
	{

		bool enabled;
		if ((Object)(object)go == (Object)null)
		{

			return;
		}
		mRootTransform.pivot = new Vector2(0.5f, 0.5f);
		RectTransform component = ((Component)AttachToObjUI).GetComponent<RectTransform>();
		component.sizeDelta = ((customSize != default(Vector2)) ? customSize : GetBoundSize(go, AttachToObjUI));

		AttachToObjUI.myOffset = setting.Offset;
		AttachToObjUI.AttachObject = go;
		mGuideMaskGraphic.Play(component, setting.Scale, setting.MaskColor, setting.ShowMask, tipKey, setting.TipArgs);
		IsPlayingSlotMask = true;
	}

	public Vector2 GetBoundSize(GameObject go, AttachToObjUI attachToObjUI)
	{

		Bounds val = new Bounds(Vector3.zero, Vector3.zero);
		bool flag = false;
		Collider[] componentsInChildren = go.GetComponentsInChildren<Collider>();
		bool enabled;
		foreach (Collider val2 in componentsInChildren)
		{
			if (!((Component)val2).gameObject.activeSelf)
			{
				continue;
			}
			if (!((Component)val2).gameObject.activeInHierarchy)
			{

			}
			if (!flag)
			{
				val = val2.bounds;
				flag = true;
			}
			else
			{
				val.Encapsulate(val2.bounds);
			}
		}
		if (flag)
		{
			return GetSize(val.min, val.max, attachToObjUI.Canvas);
		}

		return Vector2.one;
	}

	private Vector2 GetSize(Vector3 minPos, Vector3 maxPos, Canvas canvas)
	{

		Vector3 val = Camera.main.WorldToScreenPoint(minPos);
		Vector3 val2 = Camera.main.WorldToScreenPoint(maxPos);
		return new Vector2(Mathf.Abs(val2.x - val.x), Mathf.Abs(val2.y - val.y)) / canvas.scaleFactor;
	}

	public void PlayFullMask()
	{

		FullMask.SetActive(true);
		IsPlayingFullMask = true;
	}

	public void CloseFullMask()
	{
		FullMask.SetActive(false);
		IsPlayingFullMask = false;

	}

	public void PlayColorMask(Color color = default(Color))
	{

		((Component)ColorMaskImg).gameObject.SetActive(true);
		((Graphic)ColorMaskImg).color = ((color == default(Color)) ? sNormalGrayColor : color);
		IsPlayingColorMask = true;
	}

	public void CloseColorMask()
	{

		((Component)ColorMaskImg).gameObject.SetActive(false);
		IsPlayingColorMask = false;
	}

	public void PlayNoStoryColorMask(Color color = default(Color), bool overrideSort = true)
	{

		((Component)mNoStoryColorMaskImg).gameObject.SetActive(true);
		((Graphic)mNoStoryColorMaskImg).color = ((color == default(Color)) ? sNormalGrayColor : color);
		((Component)mNoStoryColorMaskImg).GetComponent<Canvas>().overrideSorting = overrideSort;
	}

	public void CloseNoStoryColorMask()
	{

		((Component)mNoStoryColorMaskImg).gameObject.SetActive(false);
	}

	public bool HasAnyMaskPlaying()
	{
		if (!IsPlayingSlotMask && !IsPlayingFullMask)
		{
			return IsPlayingColorMask;
		}
		return true;
	}

	public void OpenClickCallMask(Action clickCallFunc)
	{
		((Component)ClickCallMaskButton).gameObject.SetActive(true);
		mClickCallMaskEvent = clickCallFunc;
	}

	public void CloseClickCallMask()
	{
		((Component)ClickCallMaskButton).gameObject.SetActive(false);
		mClickCallMaskEvent = null;
	}

	public RectTransform GetButtonApplyMaskRect(Button button)
	{
		if ((Object)(object)((Selectable)button).targetGraphic != (Object)null)
		{
			return ((Component)((Selectable)button).targetGraphic).gameObject.GetComponent<RectTransform>();
		}
		return ((Component)button).GetComponent<RectTransform>();
	}

	private void OnWindowClosing(GameObject go)
	{
		_ = IsPlayingSlotMask;
	}

	private void OnWindowClosed(GameObject go)
	{
		if (IsPlayingSlotMask)
		{
			CheckShowMask();
		}
	}

	private void OnOpeningWindowStateChanged(bool isOpening)
	{
		if (IsPlayingSlotMask && !isOpening)
		{
			CheckShowMask();
		}
	}

	private void OnClickCallMaskClick()
	{
		mClickCallMaskEvent?.Invoke();
	}

	private void CheckShowMask()
	{
		bool maskState = CanShowMask();
		mGuideMaskGraphic.SetMaskState(maskState);
	}

	public bool CanShowMask()
	{
		GameObject lastWindow = Host.LastNonTipWindow;
		bool hasWindow = lastWindow != null;
		if (AttachToObjectLocalUI.AttachObject)
		{
			if (!hasWindow) return true;
			return AttachToObjectLocalUI.AttachObject.transform.IsChildOf(lastWindow.transform);
		}
		if (AttachToObjUI.AttachObject) return !hasWindow;
		return true;
	}

	public StoryMask()
	{

	}

	static StoryMask()
	{

	}
}

}
