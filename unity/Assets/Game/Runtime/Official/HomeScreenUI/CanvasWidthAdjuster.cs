// Selected original screen adaptation; local platform/service boundaries only.
using UnityEngine;
namespace Zjcs.Official.HomeScreenUI
{

public class CanvasWidthAdjuster : MonoBehaviour
{
	public struct AdaptationResult
	{
		public float ClippedWidthLeft;

		public float ClippedWidthRight;

		public float ClippedHeightTop;

		public float ClippedHeightBottom;

		public Rect BorderSafeArea;

		public Rect RootCanvasArea;

		public static AdaptationResult Default => new AdaptationResult
		{
			ClippedWidthLeft = 0f,
			ClippedWidthRight = 0f,
			ClippedHeightTop = 0f,
			ClippedHeightBottom = 0f,
			BorderSafeArea = new Rect(0f, 0f, 0f, 0f),
			RootCanvasArea = new Rect(0f, 0f, 0f, 0f)
		};

		public bool IsWidthAdapt
		{
			get
			{
				if (!(ClippedWidthLeft > 0f))
				{
					return ClippedWidthRight > 0f;
				}
				return true;
			}
		}

		public bool IsHeightAdapted
		{
			get
			{
				if (!(ClippedHeightTop > 0f))
				{
					return ClippedHeightBottom > 0f;
				}
				return true;
			}
		}

		public override string ToString()
		{
			return $"Clipped LRTB ({ClippedWidthLeft:F0},{ClippedWidthRight:F0},{ClippedHeightTop:F0},{ClippedHeightBottom:F0})\n" + $"BorderSafeArea: {BorderSafeArea}, RootCanvasArea: {RootCanvasArea}";
		}
	}

	[SerializeField]
	private RectTransform mCanvas;

	[SerializeField]
	private RectTransform mContent;

	[SerializeField]
	private RectTransform mLeft;

	[SerializeField]
	private RectTransform mRight;

	private RectTransform mTop;

	private RectTransform mBottom;

	private const float kMaxWhRatio = 121f / 160f;

	private const float kMinWhRatio = 0.45f;

	private int mLastWidth;

	private int mLastHeight;

	private ScreenOrientation mLastOrientation;

	public RectTransform Top
	{
		get
		{
			if (mTop == null)
			{
				if (mLeft == null)
				{
					return null;
				}
				mTop = Object.Instantiate(mLeft, mLeft.parent);
				mTop.name = "TopBorder (Dynamic Generated)";
				mTop.anchorMin = new Vector2(0f, 1f);
				mTop.anchorMax = new Vector2(1f, 1f);
				mTop.pivot = new Vector2(0.5f, 1f);
				mTop.sizeDelta = Vector2.zero;
				mTop.anchoredPosition = Vector2.zero;
			}
			return mTop;
		}
	}

	public RectTransform Bottom
	{
		get
		{
			if (mBottom == null)
			{
				if (mLeft == null)
				{
					return null;
				}
				mBottom = Object.Instantiate(mLeft, mLeft.parent);
				mBottom.name = "BottomBorder (Dynamic Generated)";
				mBottom.anchorMin = new Vector2(0f, 0f);
				mBottom.anchorMax = new Vector2(1f, 0f);
				mBottom.pivot = new Vector2(0.5f, 0f);
				mBottom.sizeDelta = Vector2.zero;
				mBottom.anchoredPosition = Vector2.zero;
			}
			return mBottom;
		}
	}

	private GameObject BorderParentGo
	{
		get
		{
			if (!(mLeft != null))
			{
				return null;
			}
			return mLeft.parent.gameObject;
		}
	}

	public Rect BorderSafeArea => GetAdaptationResult().BorderSafeArea;

	public string EditorDebugLastInfo { get; private set; }

	public AdaptationResult LastAdaptResult { get; private set; }

		public IScreenAdaptService ScreenAdaptService { private get; set; }

	private void Start()
	{
		ScreenAdaptService?.RegisterCanvasWidthAdjuster(this);
		AdjustContentSize();
	}

	private void OnDestroy()
	{
		ScreenAdaptService?.UnregisterCanvasWidthAdjuster(this);
	}

	private void Update()
	{
		if (ScreenMetrics.width != mLastWidth || ScreenMetrics.height != mLastHeight || ScreenMetrics.orientation != mLastOrientation)
		{
			AdjustContentSize();
		}
	}

	public void SetAdaptBorderVisible(bool visible)
	{
		if (BorderParentGo != null)
		{
			BorderParentGo.SetActive(visible);
		}
	}

	private void AdjustContentSize()
	{
		mLastWidth = ScreenMetrics.width;
		mLastHeight = ScreenMetrics.height;
		mLastOrientation = ScreenMetrics.orientation;
		ScreenWidthAdapterUtils.ForceUpdateCanvasScaler(mCanvas);
		RectTransform rectTransform = mContent;
		rectTransform.sizeDelta = Vector2.zero;
		AdaptationResult adaptationResult = (LastAdaptResult = GetAdaptationResult());
		if (adaptationResult.IsWidthAdapt)
		{
			rectTransform.offsetMin = new Vector2(adaptationResult.ClippedWidthLeft, 0f);
			rectTransform.offsetMax = new Vector2(0f - adaptationResult.ClippedWidthRight, 0f);
			if (mLeft != null && mRight != null)
			{
				SafeSetSizeDelta(mLeft, new Vector2(adaptationResult.ClippedWidthLeft, mLeft.sizeDelta.y));
				SafeSetSizeDelta(mRight, new Vector2(adaptationResult.ClippedWidthRight, mRight.sizeDelta.y));
			}
			SafeActive(Top, value: false);
			SafeActive(Bottom, value: false);
		}
		else if (adaptationResult.IsHeightAdapted)
		{
			rectTransform.offsetMin = new Vector2(0f, adaptationResult.ClippedHeightBottom);
			rectTransform.offsetMax = new Vector2(0f, 0f - adaptationResult.ClippedHeightTop);
			if (Top != null && Bottom != null)
			{
				SafeSetSizeDelta(Top, new Vector2(Top.sizeDelta.x, adaptationResult.ClippedHeightTop));
				SafeSetSizeDelta(Bottom, new Vector2(Bottom.sizeDelta.x, adaptationResult.ClippedHeightBottom));
			}
			SafeActive(mLeft, value: false);
			SafeActive(mRight, value: false);
		}
		else
		{
			rectTransform.offsetMin = new Vector2(adaptationResult.ClippedWidthLeft, adaptationResult.ClippedHeightBottom);
			rectTransform.offsetMax = new Vector2(0f - adaptationResult.ClippedWidthRight, 0f - adaptationResult.ClippedHeightTop);
			SafeActive(mLeft, value: false);
			SafeActive(mRight, value: false);
			SafeActive(Top, value: false);
			SafeActive(Bottom, value: false);
		}
	}

	private AdaptationResult GetAdaptationResult()
	{
		AdaptationResult result = AdaptationResult.Default;
		if (mCanvas == null)
		{
			return result;
		}
		Vector2 sizeDelta = mCanvas.sizeDelta;
		result.RootCanvasArea = new Rect(0f, 0f, sizeDelta.x, sizeDelta.y);
		float x = sizeDelta.x;
		float y = sizeDelta.y;
		float num = x / y;
		Rect systemUISafeArea = ScreenWidthAdapterUtils.GetSystemUISafeArea(mCanvas);
		if (num > 121f / 160f)
		{
			float num2 = y * (121f / 160f);
			float x2 = (x - num2) / 2f;
			Rect rect = new Rect(x2, 0f, num2, y);
			float num3 = Mathf.Max(rect.xMin, systemUISafeArea.xMin);
			float num4 = Mathf.Max(x - rect.xMax, x - systemUISafeArea.xMax);
			result.BorderSafeArea = new Rect(num3, 0f, x - num3 - num4, y);
			result.ClippedWidthLeft = ScreenWidthAdapterUtils.NotZeroFloatAdd2(num3);
			result.ClippedWidthRight = ScreenWidthAdapterUtils.NotZeroFloatAdd2(num4);
		}
		else if (num < 0.45f)
		{
			float num5 = x / 0.45f;
			float y2 = (y - num5) / 2f;
			Rect rect2 = new Rect(0f, y2, x, num5);
			float num6 = Mathf.Max(rect2.yMin, systemUISafeArea.yMin);
			float num7 = Mathf.Max(y - rect2.yMax, y - systemUISafeArea.yMax);
			result.BorderSafeArea = new Rect(0f, num6, x, y - num6 - num7);
			result.ClippedHeightBottom = ScreenWidthAdapterUtils.NotZeroFloatAdd2(num6);
			result.ClippedHeightTop = ScreenWidthAdapterUtils.NotZeroFloatAdd2(num7);
		}
		else
		{
			Rect borderSafeArea = new Rect(0f, 0f, x, y);
			result.BorderSafeArea = borderSafeArea;
		}
		return result;
	}

	private static void SafeActive(RectTransform rectTr, bool value)
	{
		if (!(rectTr == null))
		{
			rectTr.gameObject.SetActive(value);
		}
	}

	private static void SafeSetSizeDelta(RectTransform rectTr, Vector2 size)
	{
		if (!(rectTr == null))
		{
			SafeActive(rectTr, value: true);
			rectTr.sizeDelta = size;
		}
	}
}

}
