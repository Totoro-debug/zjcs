// Selected original screen adaptation; local platform/service boundaries only.
using UnityEngine;
using UnityEngine.EventSystems;
namespace Zjcs.Official.HomeScreenUI
{

[ExecuteAlways]
[DefaultExecutionOrder(-1)]
public class SafeArea : UIBehaviour
{
	public float PaddingTop;

	public float PaddingBottom;

	public bool DisableTopAdapt;

	public bool DisableBottomAdapt;

	protected DrivenRectTransformTracker mTracker;

	private RectTransform mSelfRectTr;

	private Vector3[] mCornerCache = new Vector3[4];

	public RectTransform RootCanvasRectTr { get; private set; }

	public CanvasWidthAdjuster WidthAdjuster { get; private set; }

	public bool IsInit { get; private set; }

	private RectTransform SelfRectTr
	{
		get
		{
			if (mSelfRectTr == null)
			{
				mSelfRectTr = transform as RectTransform;
			}
			return mSelfRectTr;
		}
	}

		public IScreenAdaptService ScreenAdaptService { private get; set; }

	public bool Dirty { get; set; }

	public static Rect SystemSafeArea => ScreenMetrics.SafeArea;

	public static Rect SystemSafeAreaWithWidthAdapt(Transform curTr)
	{
		Canvas activeRootCanvas = GetActiveRootCanvas(curTr.gameObject);
		Rect systemSafeArea = SystemSafeArea;
		bool flag;
		if (activeRootCanvas == null)
		{
			UnityEngine.Debug.LogError("SafeArea.SystemSafeAreaWithWidthAdapt 找不到rootCanvas, curTr:");
			return systemSafeArea;
		}
		CanvasWidthAdjuster component = activeRootCanvas.GetComponent<CanvasWidthAdjuster>();
		if (component == null)
		{
			UnityEngine.Debug.LogError("SafeArea.SystemSafeAreaWithWidthAdapt 找不到rootCanvas上的CanvasWidthAdjuster组件, curTr:");
			return systemSafeArea;
		}
		return SystemSafeAreaWithWidthAdaptImpl(component);
	}

	private static Rect SystemSafeAreaWithWidthAdaptImpl(CanvasWidthAdjuster widthAdjuster)
	{
		Rect systemSafeArea = SystemSafeArea;
		bool flag;
		if (widthAdjuster == null)
		{
			UnityEngine.Debug.LogError("SafeArea.SystemSafeAreaWithWidthAdapt 传入的widthAdjuster为null");
			return systemSafeArea;
		}
		Canvas component = widthAdjuster.GetComponent<Canvas>();
		if (component == null || !component.isRootCanvas)
		{
			UnityEngine.Debug.LogError("SafeArea.SystemSafeAreaWithWidthAdapt canvas == null ||!canvas.isRootCanvas canvas == null:");
			return systemSafeArea;
		}
		RectTransform rootCanvasRectTr = widthAdjuster.transform as RectTransform;
		Rect r = ScreenWidthAdapterUtils.ConvertUIRectToScreenPixelRect(widthAdjuster.BorderSafeArea, rootCanvasRectTr);
		return ScreenWidthAdapterUtils.RectIntersection(systemSafeArea, r);
	}

	private static Rect StaticGetApplySafeArea(RectTransform rootCanvasRectTr, CanvasWidthAdjuster widthAdjuster, bool disableBottomAdapt, bool disableTopAdapt, float paddingBottom, float paddingTop)
	{
		if (rootCanvasRectTr == null)
		{
			UnityEngine.Debug.LogError("GetApplySafeArea失败, rectTrRootCanvas为空");
			return new Rect(0f, 0f, 0f, 0f);
		}
		Vector2 sizeDelta = rootCanvasRectTr.sizeDelta;
		Rect r = ((widthAdjuster != null) ? widthAdjuster.BorderSafeArea : new Rect(0f, 0f, sizeDelta.x, sizeDelta.y));
		Rect rect = ScreenWidthAdapterUtils.RectIntersection(ScreenWidthAdapterUtils.GetSystemUISafeArea(rootCanvasRectTr), r);
		float xMin = rect.xMin;
		float xMax = rect.xMax;
		float num = (disableBottomAdapt ? r.yMin : (rect.yMin + paddingBottom));
		float num2 = (disableTopAdapt ? r.yMax : (rect.yMax + paddingTop));
		return new Rect(xMin, num, Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, num2 - num));
	}

	private static Canvas GetActiveRootCanvas(GameObject curGo)
	{
		if (curGo == null)
		{
			return null;
		}
		Transform parent = curGo.transform;
		while (parent != null)
		{
			Canvas component = parent.GetComponent<Canvas>();
			if (component != null && component.enabled && component.isRootCanvas)
			{
				return component;
			}
			parent = parent.parent;
		}
		return null;
	}

	protected override void Awake()
	{
		base.Awake();
		AddTracker();
		SetDefaultSize();
	}

	protected override void Start()
	{
		base.Start();
		if (!IsInit)
		{
			bool flag = (IsInit = TryInit());
			if (!flag)
			{
				UnityEngine.Debug.LogError("SafeArea Init Failed!");
				return;
			}
		}
		ApplySafeArea();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		mTracker.Clear();
	}

	protected override void OnRectTransformDimensionsChange()
	{
		base.OnRectTransformDimensionsChange();
		Dirty = true;
	}

	private void Update()
	{
		if (IsInit && Dirty)
		{
			ApplySafeArea();
			Dirty = false;
		}
	}

	private bool TryInit()
	{
		Canvas componentInParent = GetComponentInParent<Canvas>();
		if (componentInParent == null)
		{
			return false;
		}
		bool flag;
		if (componentInParent.enabled)
		{
			RootCanvasRectTr = componentInParent.rootCanvas.GetComponent<RectTransform>();
		}
		else
		{
			Canvas activeRootCanvas = GetActiveRootCanvas(gameObject);
			if (activeRootCanvas == null)
			{
				UnityEngine.Debug.LogError("SafeArea.TryInit初始化失败, 找不到rootCanvas");
				return false;
			}
			RootCanvasRectTr = activeRootCanvas.GetComponent<RectTransform>();
		}
		if (RootCanvasRectTr != null)
		{
			WidthAdjuster = RootCanvasRectTr.GetComponent<CanvasWidthAdjuster>();
		}
		if (RootCanvasRectTr == null)
		{
			UnityEngine.Debug.LogError("SafeArea.TryInit: 找不到rootCanvas的RectTransform");
		}
		return true;
	}

	public Rect GetApplySafeArea()
	{
		return StaticGetApplySafeArea(RootCanvasRectTr, WidthAdjuster, DisableBottomAdapt, DisableTopAdapt, PaddingBottom, PaddingTop);
	}

	private void SetDefaultSize()
	{
		SelfRectTr.anchorMin = Vector2.zero;
		SelfRectTr.anchorMax = Vector2.one;
		SelfRectTr.pivot = Vector2.zero;
		SelfRectTr.sizeDelta = Vector2.zero;
		SelfRectTr.anchoredPosition = Vector2.zero;
	}

	public void ApplySafeArea()
	{
		if (IsInit)
		{
			Rect applySafeArea = GetApplySafeArea();
			AddTracker();
			SetDefaultSize();
			ApplySafeAreaIsolated(applySafeArea);
		}
	}

	private void ApplySafeAreaIsolated(Rect area)
	{
		SetPanelSizeDelta(area);
		RootCanvasRectTr.GetWorldCorners(mCornerCache);
		Vector2 vector = SelfRectTr.worldToLocalMatrix.MultiplyPoint3x4(mCornerCache[0]);
		SelfRectTr.anchoredPosition += new Vector2(vector.x + area.position.x, vector.y + area.position.y);
	}

	private void SetPanelSizeDelta(Rect area)
	{
		Vector2 size = SelfRectTr.rect.size;
		Vector2 vector = area.size - size;
		SelfRectTr.sizeDelta += vector;
	}

	private void AddTracker()
	{
		mTracker.Clear();
		DrivenTransformProperties drivenProperties = DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.Pivot;
		mTracker.Add(this, SelfRectTr, drivenProperties);
	}
}

}
