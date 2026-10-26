using System;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace UnityEngine.UI
{

[AddComponentMenu("")]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public abstract class LoopScrollRectBase : UIBehaviour, IInitializePotentialDragHandler, IEventSystemHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler, ICanvasElement, ILayoutElement, ILayoutGroup, ILayoutController
{
	protected enum LoopScrollRectDirection
	{
		Vertical,
		Horizontal
	}

	public enum MovementType
	{
		Unrestricted,
		Elastic,
		Clamped
	}

	public enum ScrollbarVisibility
	{
		Permanent,
		AutoHide,
		AutoHideAndExpandViewport
	}

	[Serializable]
	public class ScrollRectEvent : UnityEvent<Vector2>
	{
	}

	public enum ScrollMode
	{
		ToStart,
		ToCenter,
		JustAppear
	}

	protected LoopScrollPrefabSource mPrefabSource;

	[Tooltip("Total count, negative means INFINITE mode")]
	public int totalCount;

	[NonSerialized]
	[HideInInspector]
	public LoopScrollSizeHelper sizeHelper;

	protected float threshold;

	[Tooltip("Reverse direction for dragging")]
	public bool reverseDirection;

	[Tooltip("Reverse Item Index")]
	public bool reverseIndex;

	protected int itemTypeStart;

	protected int itemTypeEnd;

	private bool m_ContentSpaceInit;

	private float m_ContentSpacing;

	protected float m_ContentLeftPadding;

	protected float m_ContentRightPadding;

	protected float m_ContentTopPadding;

	protected float m_ContentBottomPadding;

	protected GridLayoutGroup m_GridLayout;

	private bool m_ContentConstraintCountInit;

	private int m_ContentConstraintCount;

	[SerializeField]
	protected RectTransform m_Content;

	[SerializeField]
	private bool m_Horizontal = true;

	[SerializeField]
	private bool m_Vertical = true;

	[SerializeField]
	private MovementType m_MovementType = MovementType.Elastic;

	[SerializeField]
	private float m_Elasticity = 0.1f;

	[SerializeField]
	private bool m_Inertia = true;

	[SerializeField]
	private float m_DecelerationRate = 0.135f;

	[SerializeField]
	private float m_ScrollSensitivity = 1f;

	[SerializeField]
	private RectTransform m_Viewport;

	[SerializeField]
	private Scrollbar m_HorizontalScrollbar;

	[SerializeField]
	private Scrollbar m_VerticalScrollbar;

	[SerializeField]
	private ScrollbarVisibility m_HorizontalScrollbarVisibility;

	[SerializeField]
	private ScrollbarVisibility m_VerticalScrollbarVisibility;

	[SerializeField]
	private float m_HorizontalScrollbarSpacing;

	[SerializeField]
	private float m_VerticalScrollbarSpacing;

	[SerializeField]
	private ScrollRectEvent m_OnValueChanged = new ScrollRectEvent();

	private Vector2 m_PointerStartLocalCursor = Vector2.zero;

	protected Vector2 m_ContentStartPosition = Vector2.zero;

	private RectTransform m_ViewRect;

	protected Bounds m_ContentBounds;

	private Bounds m_ViewBounds;

	private Vector2 m_Velocity;

	private bool m_Dragging;

	private bool m_Scrolling;

	private Vector2 m_PrevPosition = Vector2.zero;

	private Bounds m_PrevContentBounds;

	private Bounds m_PrevViewBounds;

	[NonSerialized]
	private bool m_HasRebuiltLayout;

	[NonSerialized]
	private bool m_PrevPositionValid;

	private Action m_DelayFillCells;

	private bool m_HSliderExpand;

	private bool m_VSliderExpand;

	private float m_HSliderHeight;

	private float m_VSliderWidth;

	[NonSerialized]
	private RectTransform m_Rect;

	private RectTransform m_HorizontalScrollbarRect;

	private RectTransform m_VerticalScrollbarRect;

	private DrivenRectTransformTracker m_Tracker;

	private Coroutine mScrollCoroutine;

	protected int deletedItemTypeStart;

	protected int deletedItemTypeEnd;

	private readonly Vector3[] m_Corners = new Vector3[4];

	public virtual LoopScrollPrefabSource prefabSource
	{
		get
		{
			return mPrefabSource;
		}
		set
		{
			mPrefabSource = value;
		}
	}

	public int ItemTypeStart => itemTypeStart;

	public int ItemTypeEnd => itemTypeEnd;

	protected abstract LoopScrollRectDirection direction { get; set; }

	protected float contentSpacing
	{
		get
		{
			if (m_ContentSpaceInit)
			{
				return m_ContentSpacing;
			}
			m_ContentSpaceInit = true;
			m_ContentSpacing = 0f;
			if ((Object)(object)m_Content != (Object)null)
			{
				HorizontalOrVerticalLayoutGroup component = ((Component)m_Content).GetComponent<HorizontalOrVerticalLayoutGroup>();
				if ((Object)(object)component != (Object)null)
				{
					m_ContentSpacing = component.spacing;
					m_ContentLeftPadding = ((LayoutGroup)component).padding.left;
					m_ContentRightPadding = ((LayoutGroup)component).padding.right;
					m_ContentTopPadding = ((LayoutGroup)component).padding.top;
					m_ContentBottomPadding = ((LayoutGroup)component).padding.bottom;
				}
				m_GridLayout = ((Component)m_Content).GetComponent<GridLayoutGroup>();
				if ((Object)(object)m_GridLayout != (Object)null)
				{
					if (m_GridLayout is LoopScrollGridLayoutGroup loopScrollGridLayoutGroup)
					{
						loopScrollGridLayoutGroup.InitFlexible();
					}
					m_ContentSpacing = GetAbsDimension(m_GridLayout.spacing);
					m_ContentLeftPadding = ((LayoutGroup)m_GridLayout).padding.left;
					m_ContentRightPadding = ((LayoutGroup)m_GridLayout).padding.right;
					m_ContentTopPadding = ((LayoutGroup)m_GridLayout).padding.top;
					m_ContentBottomPadding = ((LayoutGroup)m_GridLayout).padding.bottom;
				}
			}
			return m_ContentSpacing;
		}
	}

	protected int contentConstraintCount
	{
		get
		{
			if (m_ContentConstraintCountInit)
			{
				return m_ContentConstraintCount;
			}
			m_ContentConstraintCountInit = true;
			m_ContentConstraintCount = 1;
			if ((Object)(object)m_Content != (Object)null)
			{
				GridLayoutGroup component = ((Component)m_Content).GetComponent<GridLayoutGroup>();
				if ((Object)(object)component != (Object)null)
				{
					if (component is LoopScrollGridLayoutGroup loopScrollGridLayoutGroup)
					{
						loopScrollGridLayoutGroup.InitFlexible();
					}
					else if ((int)component.constraint == 0)
					{
						Debug.LogWarning((object)"[LoopScrollRect] Flexible not supported yet");
					}
					m_ContentConstraintCount = component.constraintCount;
				}
			}
			return m_ContentConstraintCount;
		}
	}

	protected int StartLine => Mathf.CeilToInt((float)itemTypeStart / (float)contentConstraintCount);

	protected int CurrentLines => Mathf.CeilToInt((float)(itemTypeEnd - itemTypeStart) / (float)contentConstraintCount);

	protected int TotalLines => Mathf.CeilToInt((float)totalCount / (float)contentConstraintCount);

	public RectTransform content
	{
		get
		{
			return m_Content;
		}
		set
		{
			m_Content = value;
		}
	}

	public bool horizontal
	{
		get
		{
			return m_Horizontal;
		}
		set
		{
			m_Horizontal = value;
		}
	}

	public bool vertical
	{
		get
		{
			return m_Vertical;
		}
		set
		{
			m_Vertical = value;
		}
	}

	public MovementType movementType
	{
		get
		{
			return m_MovementType;
		}
		set
		{
			m_MovementType = value;
		}
	}

	public float elasticity
	{
		get
		{
			return m_Elasticity;
		}
		set
		{
			m_Elasticity = value;
		}
	}

	public bool inertia
	{
		get
		{
			return m_Inertia;
		}
		set
		{
			m_Inertia = value;
		}
	}

	public float decelerationRate
	{
		get
		{
			return m_DecelerationRate;
		}
		set
		{
			m_DecelerationRate = value;
		}
	}

	public float scrollSensitivity
	{
		get
		{
			return m_ScrollSensitivity;
		}
		set
		{
			m_ScrollSensitivity = value;
		}
	}

	public RectTransform viewport
	{
		get
		{
			return m_Viewport;
		}
		set
		{
			m_Viewport = value;
			SetDirtyCaching();
		}
	}

	public Scrollbar horizontalScrollbar
	{
		get
		{
			return m_HorizontalScrollbar;
		}
		set
		{
			if ((bool)((Object)(object)m_HorizontalScrollbar))
			{
				((UnityEvent<float>)(object)m_HorizontalScrollbar.onValueChanged).RemoveListener((UnityAction<float>)SetHorizontalNormalizedPosition);
			}
			m_HorizontalScrollbar = value;
			if ((bool)((Object)(object)m_HorizontalScrollbar))
			{
				((UnityEvent<float>)(object)m_HorizontalScrollbar.onValueChanged).AddListener((UnityAction<float>)SetHorizontalNormalizedPosition);
			}
			SetDirtyCaching();
		}
	}

	public Scrollbar verticalScrollbar
	{
		get
		{
			return m_VerticalScrollbar;
		}
		set
		{
			if ((bool)((Object)(object)m_VerticalScrollbar))
			{
				((UnityEvent<float>)(object)m_VerticalScrollbar.onValueChanged).RemoveListener((UnityAction<float>)SetVerticalNormalizedPosition);
			}
			m_VerticalScrollbar = value;
			if ((bool)((Object)(object)m_VerticalScrollbar))
			{
				((UnityEvent<float>)(object)m_VerticalScrollbar.onValueChanged).AddListener((UnityAction<float>)SetVerticalNormalizedPosition);
			}
			SetDirtyCaching();
		}
	}

	public ScrollbarVisibility horizontalScrollbarVisibility
	{
		get
		{
			return m_HorizontalScrollbarVisibility;
		}
		set
		{
			m_HorizontalScrollbarVisibility = value;
			SetDirtyCaching();
		}
	}

	public ScrollbarVisibility verticalScrollbarVisibility
	{
		get
		{
			return m_VerticalScrollbarVisibility;
		}
		set
		{
			m_VerticalScrollbarVisibility = value;
			SetDirtyCaching();
		}
	}

	public float horizontalScrollbarSpacing
	{
		get
		{
			return m_HorizontalScrollbarSpacing;
		}
		set
		{
			m_HorizontalScrollbarSpacing = value;
			SetDirty();
		}
	}

	public float verticalScrollbarSpacing
	{
		get
		{
			return m_VerticalScrollbarSpacing;
		}
		set
		{
			m_VerticalScrollbarSpacing = value;
			SetDirty();
		}
	}

	public ScrollRectEvent onValueChanged
	{
		get
		{
			return m_OnValueChanged;
		}
		set
		{
			m_OnValueChanged = value;
		}
	}

	protected RectTransform viewRect
	{
		get
		{
			if ((Object)(object)m_ViewRect == (Object)null)
			{
				m_ViewRect = m_Viewport;
			}
			if ((Object)(object)m_ViewRect == (Object)null)
			{
				m_ViewRect = (RectTransform)((Component)this).transform;
			}
			return m_ViewRect;
		}
	}

	public Vector2 velocity
	{
		get
		{
			return m_Velocity;
		}
		set
		{
			m_Velocity = value;
		}
	}

	private RectTransform rectTransform
	{
		get
		{
			if ((Object)(object)m_Rect == (Object)null)
			{
				m_Rect = ((Component)this).GetComponent<RectTransform>();
			}
			return m_Rect;
		}
	}

	public Vector2 normalizedPosition
	{
		get
		{
			return new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition);
		}
		set
		{
			SetNormalizedPosition(value.x, 0);
			SetNormalizedPosition(value.y, 1);
		}
	}

	public float horizontalNormalizedPosition
	{
		get
		{
			UpdateBounds();
			if (totalCount > 0 && itemTypeEnd > itemTypeStart)
			{
				GetHorizonalOffsetAndSize(out var totalSize, out var offset);
				if (totalSize <= m_ViewBounds.size.x)
				{
					return (m_ViewBounds.min.x > offset) ? 1 : 0;
				}
				return (m_ViewBounds.min.x - offset) / (totalSize - m_ViewBounds.size.x);
			}
			return 0.5f;
		}
		set
		{
			SetNormalizedPosition(value, 0);
		}
	}

	public float verticalNormalizedPosition
	{
		get
		{
			UpdateBounds();
			if (totalCount > 0 && itemTypeEnd > itemTypeStart)
			{
				GetVerticalOffsetAndSize(out var totalSize, out var offset);
				if (totalSize <= m_ViewBounds.size.y)
				{
					return (offset > m_ViewBounds.max.y) ? 1 : 0;
				}
				return (offset - m_ViewBounds.max.y) / (totalSize - m_ViewBounds.size.y);
			}
			return 0.5f;
		}
		set
		{
			SetNormalizedPosition(value, 1);
		}
	}

	private bool hScrollingNeeded
	{
		get
		{
			if (Application.isPlaying)
			{
				return m_ContentBounds.size.x > m_ViewBounds.size.x + 0.01f;
			}
			return true;
		}
	}

	private bool vScrollingNeeded
	{
		get
		{
			if (Application.isPlaying)
			{
				return m_ContentBounds.size.y > m_ViewBounds.size.y + 0.01f;
			}
			return true;
		}
	}

	public virtual float minWidth => -1f;

	public virtual float preferredWidth => -1f;

	public virtual float flexibleWidth => -1f;

	public virtual float minHeight => -1f;

	public virtual float preferredHeight => -1f;

	public virtual float flexibleHeight => -1f;

	public virtual int layoutPriority => -1;

	protected abstract float GetSize(RectTransform item, bool includeSpacing = true);

	protected abstract float GetDimension(Vector2 vector);

	protected abstract float GetAbsDimension(Vector2 vector);

	protected abstract Vector2 GetVector(float value);

	protected int GetFinalIndex(int index)
	{
		if (!reverseIndex)
		{
			return index;
		}
		return totalCount - index - 1;
	}

	protected virtual bool UpdateItems(ref Bounds viewBounds, ref Bounds contentBounds)
	{
		return false;
	}

	protected LoopScrollRectBase()
	{
	}

	public void ClearCells()
	{
		if (Application.isPlaying)
		{
			for (int num = ((Transform)m_Content).childCount - 1; num >= 0; num--)
			{
				ClearCell(num);
			}
			totalCount = 0;
			itemTypeStart = 0;
			itemTypeEnd = 0;
		}
	}

	protected void ClearCell(int idx)
	{
		Transform child = ((Transform)m_Content).GetChild(idx);
		prefabSource.ReturnObject(child, GetFinalIndex(itemTypeStart + idx));
	}

	public int GetFirstItem(out float offset)
	{
		if (direction == LoopScrollRectDirection.Vertical)
		{
			offset = m_ViewBounds.max.y - m_ContentBounds.max.y;
		}
		else
		{
			offset = m_ContentBounds.min.x - m_ViewBounds.min.x;
		}
		int num = 0;
		if (itemTypeEnd > itemTypeStart)
		{
			Transform child = ((Transform)m_Content).GetChild(0);
			float size = GetSize((RectTransform)(object)((child is RectTransform) ? child : null), includeSpacing: false);
			while (size + offset <= 0f && itemTypeStart + num + contentConstraintCount < itemTypeEnd)
			{
				offset += size;
				num += contentConstraintCount;
				Transform child2 = ((Transform)m_Content).GetChild(num);
				size = GetSize((RectTransform)(object)((child2 is RectTransform) ? child2 : null));
			}
		}
		return num + itemTypeStart;
	}

	public int GetLastItem(out float offset)
	{
		if (direction == LoopScrollRectDirection.Vertical)
		{
			offset = m_ContentBounds.min.y - m_ViewBounds.min.y;
		}
		else
		{
			offset = m_ViewBounds.max.x - m_ContentBounds.max.x;
		}
		int num = 0;
		if (itemTypeEnd > itemTypeStart)
		{
			int childCount = ((Transform)m_Content).childCount;
			Transform child = ((Transform)m_Content).GetChild(childCount - num - 1);
			float size = GetSize((RectTransform)(object)((child is RectTransform) ? child : null), includeSpacing: false);
			while (size + offset <= 0f && itemTypeStart < itemTypeEnd - num - contentConstraintCount)
			{
				offset += size;
				num += contentConstraintCount;
				Transform child2 = ((Transform)m_Content).GetChild(childCount - num - 1);
				size = GetSize((RectTransform)(object)((child2 is RectTransform) ? child2 : null));
			}
		}
		offset = 0f - offset;
		return itemTypeEnd - num - 1;
	}

	public void ScrollToCell(int index, float speed, float offset = 0f, ScrollMode mode = ScrollMode.ToStart)
	{
		if (totalCount >= 0 && (index < 0 || index >= totalCount))
		{
			Debug.LogErrorFormat("invalid index {0}", new object[1] { index });
		}
		else if (speed <= 0f)
		{
			Debug.LogErrorFormat("invalid speed {0}", new object[1] { index });
		}
		else
		{
			StopScroll();
			mScrollCoroutine = ((MonoBehaviour)this).StartCoroutine(ScrollToCellCoroutine(index, speed, offset, mode));
		}
	}

	public void ScrollToCellWithinTime(int index, float time, float offset = 0f, ScrollMode mode = ScrollMode.ToStart)
	{
		index = GetFinalIndex(index);
		if (totalCount >= 0 && (index < 0 || index >= totalCount))
		{
			Debug.LogErrorFormat("invalid index {0}", new object[1] { index });
			return;
		}
		if (time <= 0f)
		{
			Debug.LogErrorFormat("invalid time {0}", new object[1] { time });
			return;
		}
		if (mode == ScrollMode.JustAppear)
		{
			Debug.LogErrorFormat("scroll mode {0} not supported yet.", new object[1] { mode });
			return;
		}
		StopScroll();
		float num = 0f;
		float offset2 = 0f;
		int num2 = (reverseDirection ? GetLastItem(out offset2) : GetFirstItem(out offset2));
		int num3 = index / contentConstraintCount;
		int num4 = num2 / contentConstraintCount;
		if (num3 == num4)
		{
			num = 0f - offset2;
		}
		else if (sizeHelper != null)
		{
			num = GetDimension(sizeHelper.GetItemsSize(num2) - sizeHelper.GetItemsSize(index)) + contentSpacing * (float)(num4 - num3);
			num -= offset2;
		}
		else
		{
			num = (GetAbsDimension((Vector2)(m_ContentBounds.size)) - contentSpacing * (float)(CurrentLines - 1)) / (float)CurrentLines * (float)(num4 - num3) + contentSpacing * (float)(num4 - num3);
			num -= offset2;
		}
		num += offset;
		if (mode == ScrollMode.ToCenter)
		{
			Rect rect = viewRect.rect;
			float absDimension = GetAbsDimension(rect.size);
			num -= absDimension * 0.5f;
		}
		mScrollCoroutine = ((MonoBehaviour)this).StartCoroutine(ScrollToCellCoroutine(index, Mathf.Abs(num) / time, offset, mode));
	}

	private IEnumerator ScrollToCellCoroutine(int index, float speed, float offset, ScrollMode mode)
	{
		bool needMoving = true;
		while (needMoving)
		{
			yield return null;
			if (m_Dragging)
			{
				continue;
			}
			float num;
			if (index < itemTypeStart)
			{
				num = (0f - Time.deltaTime) * speed;
			}
			else if (index >= itemTypeEnd)
			{
				num = Time.deltaTime * speed;
			}
			else
			{
				LoopScrollRectBase loopScrollRectBase = this;
				Rect rect = viewRect.rect;
				Vector3 val = (Vector2)(rect.center);
				rect = viewRect.rect;
				loopScrollRectBase.m_ViewBounds = new Bounds(val, (Vector2)(rect.size));
				Bounds bounds4Item = GetBounds4Item(index);
				float num2 = 0f;
				switch (mode)
				{
				case ScrollMode.ToStart:
					if (direction != LoopScrollRectDirection.Vertical)
					{
						num2 = (reverseDirection ? (bounds4Item.max.x - m_ViewBounds.max.x) : (bounds4Item.min.x - m_ViewBounds.min.x));
					}
					else
					{
						num2 = (reverseDirection ? (m_ViewBounds.min.y - bounds4Item.min.y) : (m_ViewBounds.max.y - bounds4Item.max.y));
					}
					break;
				case ScrollMode.ToCenter:
					num2 = GetDimension((Vector2)(m_ViewBounds.center - bounds4Item.center));
					break;
				default:
				{
					float dimension = GetDimension((Vector2)(m_ViewBounds.min - bounds4Item.min));
					float dimension2 = GetDimension((Vector2)(m_ViewBounds.max - bounds4Item.max));
					if (direction == LoopScrollRectDirection.Vertical)
					{
						if (dimension > 0f)
						{
							num2 = dimension;
						}
						else if (dimension2 < 0f)
						{
							num2 = dimension2;
						}
					}
					else if (dimension < 0f)
					{
						num2 = dimension;
					}
					else if (dimension2 > 0f)
					{
						num2 = dimension2;
					}
					break;
				}
				}
				num2 += offset;
				if (totalCount >= 0)
				{
					if (num2 > 0f && itemTypeEnd == totalCount)
					{
						bounds4Item = GetBounds4Item(totalCount - 1);
						if ((direction == LoopScrollRectDirection.Vertical && bounds4Item.min.y > m_ViewBounds.min.y) || (direction == LoopScrollRectDirection.Horizontal && bounds4Item.max.x < m_ViewBounds.max.x))
						{
							break;
						}
					}
					else if (num2 < 0f && itemTypeStart == 0)
					{
						bounds4Item = GetBounds4Item(0);
						if ((direction == LoopScrollRectDirection.Vertical && bounds4Item.max.y < m_ViewBounds.max.y) || (direction == LoopScrollRectDirection.Horizontal && bounds4Item.min.x > m_ViewBounds.min.x))
						{
							break;
						}
					}
				}
				float num3 = Time.deltaTime * speed;
				if (Mathf.Abs(num2) < num3)
				{
					needMoving = false;
					num = num2;
				}
				else
				{
					num = Mathf.Sign(num2) * num3;
				}
			}
			if (num != 0f)
			{
				Vector2 vector = GetVector(num);
				RectTransform val2 = m_Content;
				val2.anchoredPosition += vector;
				m_PrevPosition += vector;
				m_ContentStartPosition += vector;
				UpdateBounds(updateItems: true);
			}
		}
		StopMovement();
		UpdatePrevData();
	}

	public void StopScroll()
	{
		if (mScrollCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mScrollCoroutine);
		}
		mScrollCoroutine = null;
	}

	protected abstract void ProvideData(Transform transform, int index);

	public void RefreshCells()
	{
		if (!Application.isPlaying || !((Behaviour)this).isActiveAndEnabled)
		{
			return;
		}
		itemTypeEnd = itemTypeStart;
		for (int i = 0; i < ((Transform)m_Content).childCount; i++)
		{
			if (itemTypeEnd < totalCount || totalCount < 0)
			{
				ProvideData(((Transform)m_Content).GetChild(i), itemTypeEnd);
				itemTypeEnd++;
			}
			else
			{
				ClearCell(i);
				i--;
			}
		}
		UpdateBounds(updateItems: true);
		UpdateScrollbars(Vector2.zero);
	}

	public void RefillCellsFromEnd(int endItem = 0, bool alignStart = false, Action onCompletedCallback = null)
	{
		if (!Application.isPlaying)
		{
			return;
		}
		if (IsNeedWaitLayoutComplete())
		{
			EnsureLayoutHasRebuilt();
			m_DelayFillCells = (Action)Delegate.Combine(m_DelayFillCells, (Action)(() =>
			{
				InternalRefillCellsFromEnd(endItem, alignStart, onCompletedCallback);
			}));
		}
		else
		{
			InternalRefillCellsFromEnd(endItem, alignStart, onCompletedCallback);
		}
	}

	protected virtual void InternalRefillCellsFromEnd(int endItem = 0, bool alignStart = false, Action onCompletedCallback = null)
	{
		itemTypeEnd = (reverseDirection ? endItem : (totalCount - endItem));
		itemTypeStart = itemTypeEnd;
		if (totalCount >= 0 && itemTypeStart % contentConstraintCount != 0)
		{
			itemTypeStart = itemTypeStart / contentConstraintCount * contentConstraintCount;
		}
		ReturnToTempPool(!reverseDirection, ((Transform)m_Content).childCount);
		m_Content.anchoredPosition = Vector2.zero;
		Rect rect = viewRect.rect;
		float absDimension = GetAbsDimension(rect.size);
		float num = 0f;
		bool flag = true;
		if (itemTypeStart < itemTypeEnd)
		{
			itemTypeEnd = itemTypeStart;
			float num2 = (reverseDirection ? NewItemAtStart(!flag) : NewItemAtEnd(!flag));
			if (num2 >= 0f)
			{
				flag = false;
				num += num2;
			}
		}
		float num3;
		for (; absDimension > num; num += num3)
		{
			num3 = (reverseDirection ? NewItemAtEnd(!flag) : NewItemAtStart(!flag));
			if (num3 < 0f)
			{
				break;
			}
			flag = false;
		}
		float num4;
		for (; absDimension > num; num += num4)
		{
			num4 = (reverseDirection ? NewItemAtStart(!flag) : NewItemAtEnd(!flag));
			if (num4 < 0f)
			{
				break;
			}
			flag = false;
		}
		num = ((direction != LoopScrollRectDirection.Vertical) ? (num + (m_ContentLeftPadding + m_ContentRightPadding)) : (num + (m_ContentTopPadding + m_ContentBottomPadding)));
		Vector2 anchoredPosition = m_Content.anchoredPosition;
		float absDimension2 = GetAbsDimension(new Vector2(m_ContentLeftPadding + m_ContentRightPadding, m_ContentTopPadding + m_ContentBottomPadding));
		float num5 = (alignStart ? 0f : Mathf.Max(0f, num + absDimension2 - absDimension));
		if (reverseDirection)
		{
			num5 = 0f - num5;
		}
		if (direction == LoopScrollRectDirection.Vertical)
		{
			anchoredPosition.y = num5;
		}
		else
		{
			anchoredPosition.x = 0f - num5;
		}
		m_Content.anchoredPosition = anchoredPosition;
		m_ContentStartPosition = anchoredPosition;
		ClearTempPool();
		LayoutRebuilder.ForceRebuildLayoutImmediate(m_Content);
		UpdateBounds();
		UpdateScrollbars(Vector2.zero);
		StopMovement();
		UpdatePrevData();
		onCompletedCallback?.Invoke();
	}

	public void RefillCells(int startItem = 0, float contentOffset = 0f, Action onCompletedCallback = null)
	{
		if (!Application.isPlaying)
		{
			return;
		}
		if (IsNeedWaitLayoutComplete())
		{
			m_DelayFillCells = (Action)Delegate.Combine(m_DelayFillCells, (Action)(() =>
			{
				InternalRefillCells(startItem, contentOffset, onCompletedCallback);
			}));
		}
		else
		{
			InternalRefillCells(startItem, contentOffset, onCompletedCallback);
		}
	}

	protected virtual void InternalRefillCells(int startItem = 0, float contentOffset = 0f, Action onCompletedCallback = null)
	{
		ReturnToTempPool(reverseDirection, ((Transform)m_Content).childCount);
		itemTypeStart = (reverseDirection ? (totalCount - startItem) : startItem);
		if (totalCount >= 0 && itemTypeStart % contentConstraintCount != 0)
		{
			itemTypeStart = itemTypeStart / contentConstraintCount * contentConstraintCount;
		}
		itemTypeEnd = itemTypeStart;
		m_Content.anchoredPosition = Vector2.zero;
		Rect rect = viewRect.rect;
		float num = GetAbsDimension(rect.size) + Mathf.Abs(contentOffset);
		float num2 = 0f;
		bool flag = true;
		float num3;
		for (; num > num2; num2 += num3)
		{
			num3 = (reverseDirection ? NewItemAtStart(!flag) : NewItemAtEnd(!flag));
			if (num3 < 0f)
			{
				break;
			}
			flag = false;
		}
		float num4;
		for (; num > num2; num2 += num4)
		{
			num4 = (reverseDirection ? NewItemAtEnd(!flag) : NewItemAtStart(!flag));
			if (num4 < 0f)
			{
				break;
			}
			flag = false;
		}
		num2 = ((direction != LoopScrollRectDirection.Vertical) ? (num2 + (m_ContentLeftPadding + m_ContentRightPadding)) : (num2 + (m_ContentTopPadding + m_ContentBottomPadding)));
		Vector2 anchoredPosition = m_Content.anchoredPosition;
		if (anchoredPosition != Vector2.zero)
		{
			if (direction == LoopScrollRectDirection.Vertical)
			{
				float val = anchoredPosition.y - contentOffset;
				rect = viewRect.rect;
				anchoredPosition.y = Math.Max(val, rect.height - num2);
			}
			else
			{
				float val2 = anchoredPosition.x + contentOffset;
				float num5 = num2;
				rect = viewRect.rect;
				anchoredPosition.x = Math.Min(val2, num5 - rect.height);
			}
		}
		m_Content.anchoredPosition = anchoredPosition;
		m_ContentStartPosition = anchoredPosition;
		ClearTempPool();
		LayoutRebuilder.ForceRebuildLayoutImmediate(m_Content);
		UpdateBounds();
		UpdateScrollbars(Vector2.zero);
		StopMovement();
		UpdatePrevData();
		onCompletedCallback?.Invoke();
	}

	private bool IsNeedWaitLayoutComplete()
	{
		if (m_HasRebuiltLayout)
		{
			return false;
		}
		Transform val = ((Component)this).transform;
		HorizontalOrVerticalLayoutGroup val2 = default;
		do
		{
			if (((Component)val).TryGetComponent<HorizontalOrVerticalLayoutGroup>(out val2) && ((Behaviour)val2).enabled && (val2.childControlHeight || val2.childControlWidth))
			{
				return true;
			}
			val = val.parent;
		}
		while ((Object)(object)val != (Object)null);
		return false;
	}

	protected float NewItemAtStart(bool includeSpacing = true)
	{
		if (totalCount >= 0 && itemTypeStart - contentConstraintCount < 0)
		{
			return -1f;
		}
		float num = 0f;
		for (int i = 0; i < contentConstraintCount; i++)
		{
			itemTypeStart--;
			RectTransform fromTempPool = GetFromTempPool(itemTypeStart);
			((Transform)fromTempPool).SetSiblingIndex(deletedItemTypeStart);
			num = Mathf.Max(GetSize(fromTempPool, includeSpacing), num);
		}
		threshold = Mathf.Max(threshold, num * 1.5f);
		if (num > 0f)
		{
			SetDirtyCaching();
			m_HasRebuiltLayout = false;
			m_PrevPositionValid = true;
			if (!reverseDirection)
			{
				Vector2 vector = GetVector(num);
				RectTransform val = m_Content;
				val.anchoredPosition += vector;
				m_PrevPosition += vector;
				m_ContentStartPosition += vector;
			}
		}
		return num;
	}

	protected float DeleteItemAtStart()
	{
		if ((m_Dragging || m_Velocity != Vector2.zero) && totalCount >= 0 && itemTypeEnd >= totalCount - contentConstraintCount)
		{
			return 0f;
		}
		int num = ((Transform)m_Content).childCount - deletedItemTypeStart - deletedItemTypeEnd;
		if (num == 0)
		{
			return 0f;
		}
		float num2 = 0f;
		for (int i = 0; i < contentConstraintCount; i++)
		{
			Transform child = ((Transform)m_Content).GetChild(deletedItemTypeStart);
			RectTransform item = (RectTransform)(object)((child is RectTransform) ? child : null);
			num2 = Mathf.Max(GetSize(item), num2);
			ReturnToTempPool(fromStart: true);
			num--;
			itemTypeStart++;
			if (num == 0)
			{
				break;
			}
		}
		if (num2 > 0f)
		{
			SetDirtyCaching();
			m_HasRebuiltLayout = false;
			m_PrevPositionValid = true;
			if (!reverseDirection)
			{
				Vector2 vector = GetVector(num2);
				RectTransform val = m_Content;
				val.anchoredPosition -= vector;
				m_PrevPosition -= vector;
				m_ContentStartPosition -= vector;
			}
		}
		return num2;
	}

	protected float NewItemAtEnd(bool includeSpacing = true)
	{
		if (totalCount >= 0 && itemTypeEnd >= totalCount)
		{
			return -1f;
		}
		float num = 0f;
		int num2 = ((Transform)m_Content).childCount - deletedItemTypeStart - deletedItemTypeEnd;
		int num3 = contentConstraintCount - num2 % contentConstraintCount;
		for (int i = 0; i < num3; i++)
		{
			RectTransform fromTempPool = GetFromTempPool(itemTypeEnd);
			((Transform)fromTempPool).SetSiblingIndex(((Transform)m_Content).childCount - deletedItemTypeEnd - 1);
			num = Mathf.Max(GetSize(fromTempPool, includeSpacing), num);
			itemTypeEnd++;
			if (totalCount >= 0 && itemTypeEnd >= totalCount)
			{
				break;
			}
		}
		threshold = Mathf.Max(threshold, num * 1.5f);
		if (num > 0f)
		{
			SetDirtyCaching();
			m_HasRebuiltLayout = false;
			m_PrevPositionValid = true;
			if (reverseDirection)
			{
				Vector2 vector = GetVector(num);
				RectTransform val = m_Content;
				val.anchoredPosition -= vector;
				m_PrevPosition -= vector;
				m_ContentStartPosition -= vector;
			}
		}
		return num;
	}

	protected float DeleteItemAtEnd()
	{
		if ((m_Dragging || m_Velocity != Vector2.zero) && totalCount >= 0 && itemTypeStart < contentConstraintCount)
		{
			return 0f;
		}
		int num = ((Transform)m_Content).childCount - deletedItemTypeStart - deletedItemTypeEnd;
		if (num == 0)
		{
			return 0f;
		}
		float num2 = 0f;
		for (int i = 0; i < contentConstraintCount; i++)
		{
			Transform child = ((Transform)m_Content).GetChild(((Transform)m_Content).childCount - deletedItemTypeEnd - 1);
			RectTransform item = (RectTransform)(object)((child is RectTransform) ? child : null);
			num2 = Mathf.Max(GetSize(item), num2);
			ReturnToTempPool(fromStart: false);
			num--;
			itemTypeEnd--;
			if (itemTypeEnd % contentConstraintCount == 0 || num == 0)
			{
				break;
			}
		}
		if (num2 > 0f)
		{
			SetDirtyCaching();
			m_HasRebuiltLayout = false;
			m_PrevPositionValid = true;
			if (reverseDirection)
			{
				Vector2 vector = GetVector(num2);
				RectTransform val = m_Content;
				val.anchoredPosition += vector;
				m_PrevPosition += vector;
				m_ContentStartPosition += vector;
			}
		}
		return num2;
	}

	protected abstract RectTransform GetFromTempPool(int itemIdx);

	protected abstract void ReturnToTempPool(bool fromStart, int count = 1);

	protected abstract void ClearTempPool();

	[Obsolete("SrollToCell(int, float) has been renamed to ScrollToCell(int, float).")]
	public void SrollToCell(int index, float speed)
	{
		ScrollToCell(index, speed);
	}

	[Obsolete("SrollToCellWithinTime(int, float) has been renamed to ScrollToCellWithinTime(int, float).")]
	public void SrollToCellWithinTime(int index, float time)
	{
		ScrollToCellWithinTime(index, time);
	}

	public virtual void Rebuild(CanvasUpdate executing)
	{
		if ((int)executing == 0)
		{
			UpdateCachedData();
		}
		if ((int)executing == 2)
		{
			UpdateBounds();
			UpdateScrollbars(Vector2.zero);
			UpdatePrevData();
			m_HasRebuiltLayout = true;
			m_PrevPositionValid = false;
		}
	}

	public virtual void LayoutComplete()
	{
	}

	public virtual void GraphicUpdateComplete()
	{
	}

	private void UpdateCachedData()
	{
		Transform transform = ((Component)this).transform;
		object horizontalScrollbarRect;
		if (!((Object)(object)m_HorizontalScrollbar == (Object)null))
		{
			Transform transform2 = ((Component)m_HorizontalScrollbar).transform;
			horizontalScrollbarRect = ((transform2 is RectTransform) ? transform2 : null);
		}
		else
		{
			horizontalScrollbarRect = null;
		}
		m_HorizontalScrollbarRect = (RectTransform)horizontalScrollbarRect;
		object verticalScrollbarRect;
		if (!((Object)(object)m_VerticalScrollbar == (Object)null))
		{
			Transform transform3 = ((Component)m_VerticalScrollbar).transform;
			verticalScrollbarRect = ((transform3 is RectTransform) ? transform3 : null);
		}
		else
		{
			verticalScrollbarRect = null;
		}
		m_VerticalScrollbarRect = (RectTransform)verticalScrollbarRect;
		bool flag = (Object)(object)((Transform)viewRect).parent == (Object)(object)transform;
		bool flag2 = !(bool)((Object)(object)m_HorizontalScrollbarRect) || (Object)(object)((Transform)m_HorizontalScrollbarRect).parent == (Object)(object)transform;
		bool flag3 = !(bool)((Object)(object)m_VerticalScrollbarRect) || (Object)(object)((Transform)m_VerticalScrollbarRect).parent == (Object)(object)transform;
		bool flag4 = flag & flag2 & flag3;
		m_HSliderExpand = flag4 && (bool)((Object)(object)m_HorizontalScrollbarRect) && horizontalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
		m_VSliderExpand = flag4 && (bool)((Object)(object)m_VerticalScrollbarRect) && verticalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
		Rect rect;
		float hSliderHeight;
		if (!((Object)(object)m_HorizontalScrollbarRect == (Object)null))
		{
			rect = m_HorizontalScrollbarRect.rect;
			hSliderHeight = rect.height;
		}
		else
		{
			hSliderHeight = 0f;
		}
		m_HSliderHeight = hSliderHeight;
		float vSliderWidth;
		if (!((Object)(object)m_VerticalScrollbarRect == (Object)null))
		{
			rect = m_VerticalScrollbarRect.rect;
			vSliderWidth = rect.width;
		}
		else
		{
			vSliderWidth = 0f;
		}
		m_VSliderWidth = vSliderWidth;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if ((bool)((Object)(object)m_HorizontalScrollbar))
		{
			((UnityEvent<float>)(object)m_HorizontalScrollbar.onValueChanged).AddListener((UnityAction<float>)SetHorizontalNormalizedPosition);
		}
		if ((bool)((Object)(object)m_VerticalScrollbar))
		{
			((UnityEvent<float>)(object)m_VerticalScrollbar.onValueChanged).AddListener((UnityAction<float>)SetVerticalNormalizedPosition);
		}
		CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild((ICanvasElement)(object)this);
		SetDirty();
	}

	protected override void OnDisable()
	{
		CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild((ICanvasElement)(object)this);
		if ((bool)((Object)(object)m_HorizontalScrollbar))
		{
			((UnityEvent<float>)(object)m_HorizontalScrollbar.onValueChanged).RemoveListener((UnityAction<float>)SetHorizontalNormalizedPosition);
		}
		if ((bool)((Object)(object)m_VerticalScrollbar))
		{
			((UnityEvent<float>)(object)m_VerticalScrollbar.onValueChanged).RemoveListener((UnityAction<float>)SetVerticalNormalizedPosition);
		}
		m_Dragging = false;
		m_Scrolling = false;
		m_HasRebuiltLayout = false;
		m_Tracker.Clear();
		m_Velocity = Vector2.zero;
		LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
		base.OnDisable();
	}

	public override bool IsActive()
	{
		if (base.IsActive())
		{
			return (Object)(object)m_Content != (Object)null;
		}
		return false;
	}

	private void EnsureLayoutHasRebuilt()
	{
		if (!m_HasRebuiltLayout && !CanvasUpdateRegistry.IsRebuildingLayout())
		{
			Canvas.ForceUpdateCanvases();
		}
	}

	public virtual void StopMovement()
	{
		m_Velocity = Vector2.zero;
	}

	public virtual void OnScroll(PointerEventData data)
	{
		if (!((UIBehaviour)this).IsActive())
		{
			return;
		}
		EnsureLayoutHasRebuilt();
		UpdateBounds();
		Vector2 scrollDelta = data.scrollDelta;
		scrollDelta.y *= -1f;
		if (vertical && !horizontal)
		{
			if (Mathf.Abs(scrollDelta.x) > Mathf.Abs(scrollDelta.y))
			{
				scrollDelta.y = scrollDelta.x;
			}
			scrollDelta.x = 0f;
		}
		if (horizontal && !vertical)
		{
			if (Mathf.Abs(scrollDelta.y) > Mathf.Abs(scrollDelta.x))
			{
				scrollDelta.x = scrollDelta.y;
			}
			scrollDelta.y = 0f;
		}
		if (data.IsScrolling())
		{
			m_Scrolling = true;
		}
		Vector2 anchoredPosition = m_Content.anchoredPosition;
		anchoredPosition += scrollDelta * m_ScrollSensitivity;
		if (m_MovementType == MovementType.Clamped)
		{
			anchoredPosition += CalculateOffset(anchoredPosition - m_Content.anchoredPosition);
		}
		SetContentAnchoredPosition(anchoredPosition);
		UpdateBounds();
	}

	public virtual void OnInitializePotentialDrag(PointerEventData eventData)
	{
		if ((int)eventData.button == 0)
		{
			m_Velocity = Vector2.zero;
		}
	}

	public virtual void OnBeginDrag(PointerEventData eventData)
	{
		if ((int)eventData.button == 0 && ((UIBehaviour)this).IsActive())
		{
			UpdateBounds();
			m_PointerStartLocalCursor = Vector2.zero;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out m_PointerStartLocalCursor);
			m_ContentStartPosition = m_Content.anchoredPosition;
			m_Dragging = true;
		}
	}

	public virtual void OnEndDrag(PointerEventData eventData)
	{
		if ((int)eventData.button == 0)
		{
			m_Dragging = false;
		}
	}

	public virtual void OnDrag(PointerEventData eventData)
	{
		Vector2 val = default;
		if (!m_Dragging || (int)eventData.button != 0 || !((UIBehaviour)this).IsActive() || !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out val))
		{
			return;
		}
		UpdateBounds();
		Vector2 val2 = val - m_PointerStartLocalCursor;
		Vector2 val3 = m_ContentStartPosition + val2;
		Vector2 val4 = CalculateOffset(val3 - m_Content.anchoredPosition);
		val3 += val4;
		if (m_MovementType == MovementType.Elastic)
		{
			if (val4.x != 0f)
			{
				val3.x -= RubberDelta(val4.x, m_ViewBounds.size.x);
			}
			if (val4.y != 0f)
			{
				val3.y -= RubberDelta(val4.y, m_ViewBounds.size.y);
			}
		}
		SetContentAnchoredPosition(val3);
	}

	protected virtual void SetContentAnchoredPosition(Vector2 position)
	{
		if (!m_Horizontal)
		{
			position.x = m_Content.anchoredPosition.x;
		}
		if (!m_Vertical)
		{
			position.y = m_Content.anchoredPosition.y;
		}
		Vector2 val = position - m_Content.anchoredPosition;
		if (val.sqrMagnitude > 0.001f)
		{
			m_Content.anchoredPosition = position;
			UpdateBounds(updateItems: true);
		}
	}

	protected virtual void LateUpdate()
	{
		if (!(bool)((Object)(object)m_Content))
		{
			return;
		}
		EnsureLayoutHasRebuilt();
		if (m_DelayFillCells != null)
		{
			m_DelayFillCells?.Invoke();
			m_DelayFillCells = null;
		}
		UpdateBounds();
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		Vector2 val = CalculateOffset(Vector2.zero);
		if (!m_Dragging && (val != Vector2.zero || m_Velocity != Vector2.zero))
		{
			Vector2 val2 = m_Content.anchoredPosition;
			for (int i = 0; i < 2; i++)
			{
				if (m_MovementType == MovementType.Elastic && val[i] != 0f)
				{
					float num = m_Velocity[i];
					float num2 = m_Elasticity;
					if (m_Scrolling)
					{
						num2 *= 3f;
					}
					int num3 = i;
					Vector2 anchoredPosition = m_Content.anchoredPosition;
					float num4 = anchoredPosition[i];
					anchoredPosition = m_Content.anchoredPosition;
					val2[num3] = Mathf.SmoothDamp(num4, anchoredPosition[i] + val[i], ref num, num2, float.PositiveInfinity, unscaledDeltaTime);
					if (Mathf.Abs(num) < 1f)
					{
						num = 0f;
					}
					m_Velocity[i] = num;
				}
				else if (m_Inertia)
				{
					ref Vector2 reference = ref m_Velocity;
					int num5 = i;
					reference[num5] *= Mathf.Pow(m_DecelerationRate, unscaledDeltaTime);
					if (Mathf.Abs(m_Velocity[i]) < 1f)
					{
						m_Velocity[i] = 0f;
					}
					num5 = i;
					val2[num5] += m_Velocity[i] * unscaledDeltaTime;
				}
				else
				{
					m_Velocity[i] = 0f;
				}
			}
			if (m_MovementType == MovementType.Clamped)
			{
				val = CalculateOffset(val2 - m_Content.anchoredPosition);
				val2 += val;
			}
			SetContentAnchoredPosition(val2);
		}
		if (m_Dragging && m_Inertia)
		{
			Vector3 val3 = (Vector2)((m_Content.anchoredPosition - m_PrevPosition) / unscaledDeltaTime);
			m_Velocity = (Vector2)(Vector3.Lerp((Vector2)(m_Velocity), val3, unscaledDeltaTime * 10f));
		}
		if (m_ViewBounds != m_PrevViewBounds || m_ContentBounds != m_PrevContentBounds || m_Content.anchoredPosition != m_PrevPosition)
		{
			UpdateScrollbars(val);
			UISystemProfilerApi.AddMarker("ScrollRect.value", (Object)(object)this);
			((UnityEvent<Vector2>)m_OnValueChanged).Invoke(normalizedPosition);
			UpdatePrevData();
		}
		UpdateScrollbarVisibility();
		m_Scrolling = false;
	}

	protected void UpdatePrevData()
	{
		if (!m_PrevPositionValid)
		{
			if ((Object)(object)m_Content == (Object)null)
			{
				m_PrevPosition = Vector2.zero;
			}
			else
			{
				m_PrevPosition = m_Content.anchoredPosition;
			}
		}
		m_PrevViewBounds = m_ViewBounds;
		m_PrevContentBounds = m_ContentBounds;
	}

	public void GetHorizonalOffsetAndSize(out float totalSize, out float offset)
	{
		if (sizeHelper != null)
		{
			totalSize = sizeHelper.GetItemsSize(TotalLines).x + contentSpacing * (float)(TotalLines - 1);
			offset = m_ContentBounds.min.x - sizeHelper.GetItemsSize(StartLine).x - contentSpacing * (float)StartLine;
			return;
		}
		float num = m_ContentLeftPadding + m_ContentRightPadding;
		float num2 = (m_ContentBounds.size.x - num - contentSpacing * (float)(CurrentLines - 1)) / (float)CurrentLines;
		totalSize = num2 * (float)TotalLines + contentSpacing * (float)(TotalLines - 1) + num;
		offset = m_ContentBounds.min.x - num2 * (float)StartLine - contentSpacing * (float)StartLine;
	}

	public void GetVerticalOffsetAndSize(out float totalSize, out float offset)
	{
		if (sizeHelper != null)
		{
			totalSize = sizeHelper.GetItemsSize(TotalLines).y + contentSpacing * (float)(TotalLines - 1);
			offset = m_ContentBounds.max.y + sizeHelper.GetItemsSize(StartLine).y + contentSpacing * (float)StartLine;
			return;
		}
		float num = m_ContentTopPadding + m_ContentBottomPadding;
		float num2 = (m_ContentBounds.size.y - num - contentSpacing * (float)(CurrentLines - 1)) / (float)CurrentLines;
		totalSize = num2 * (float)TotalLines + contentSpacing * (float)(TotalLines - 1) + num;
		offset = m_ContentBounds.max.y + num2 * (float)StartLine + contentSpacing * (float)StartLine;
	}

	private void UpdateScrollbars(Vector2 offset)
	{
		if ((bool)((Object)(object)m_HorizontalScrollbar))
		{
			if (m_ContentBounds.size.x > 0f && totalCount > 0)
			{
				GetHorizonalOffsetAndSize(out var totalSize, out var _);
				m_HorizontalScrollbar.size = Mathf.Clamp01((m_ViewBounds.size.x - Mathf.Abs(offset.x)) / totalSize);
			}
			else
			{
				m_HorizontalScrollbar.size = 1f;
			}
			m_HorizontalScrollbar.value = horizontalNormalizedPosition;
		}
		if ((bool)((Object)(object)m_VerticalScrollbar))
		{
			if (m_ContentBounds.size.y > 0f && totalCount > 0)
			{
				GetVerticalOffsetAndSize(out var totalSize2, out var _);
				m_VerticalScrollbar.size = Mathf.Clamp01((m_ViewBounds.size.y - Mathf.Abs(offset.y)) / totalSize2);
			}
			else
			{
				m_VerticalScrollbar.size = 1f;
			}
			m_VerticalScrollbar.value = verticalNormalizedPosition;
		}
	}

	private void SetHorizontalNormalizedPosition(float value)
	{
		SetNormalizedPosition(value, 0);
	}

	private void SetVerticalNormalizedPosition(float value)
	{
		SetNormalizedPosition(value, 1);
	}

	protected virtual void SetNormalizedPosition(float value, int axis)
	{
		if (totalCount <= 0 || itemTypeEnd <= itemTypeStart)
		{
			return;
		}
		EnsureLayoutHasRebuilt();
		UpdateBounds();
		Vector2 anchoredPosition = m_Content.anchoredPosition;
		float num = anchoredPosition[axis];
		float totalSize;
		float offset;
		if (axis == 0)
		{
			GetHorizonalOffsetAndSize(out totalSize, out offset);
			if (totalSize >= m_ViewBounds.size.x)
			{
				num += m_ViewBounds.min.x - value * (totalSize - m_ViewBounds.size.x) - offset;
			}
		}
		else
		{
			GetVerticalOffsetAndSize(out totalSize, out offset);
			if (totalSize >= m_ViewBounds.size.y)
			{
				num -= offset - value * (totalSize - m_ViewBounds.size.y) - m_ViewBounds.max.y;
			}
		}
		Vector3 val = (Vector2)(m_Content.anchoredPosition);
		if (Mathf.Abs(val[axis] - num) > 0.01f)
		{
			val[axis] = num;
			m_Content.anchoredPosition = (Vector2)(val);
			m_Velocity[axis] = 0f;
			UpdateBounds(updateItems: true);
		}
	}

	private static float RubberDelta(float overStretching, float viewSize)
	{
		return (1f - 1f / (Mathf.Abs(overStretching) * 0.55f / viewSize + 1f)) * viewSize * Mathf.Sign(overStretching);
	}

	protected override void OnRectTransformDimensionsChange()
	{
		SetDirty();
	}

	public virtual void CalculateLayoutInputHorizontal()
	{
	}

	public virtual void CalculateLayoutInputVertical()
	{
	}

	public virtual void SetLayoutHorizontal()
	{
		m_Tracker.Clear();
		Rect rect;
		if (m_HSliderExpand || m_VSliderExpand)
		{
			m_Tracker.Add((Object)(object)this, viewRect, (DrivenTransformProperties)16134);
			viewRect.anchorMin = Vector2.zero;
			viewRect.anchorMax = Vector2.one;
			viewRect.sizeDelta = Vector2.zero;
			viewRect.anchoredPosition = Vector2.zero;
			LayoutRebuilder.ForceRebuildLayoutImmediate(m_Content);
			rect = viewRect.rect;
			Vector3 val = (Vector2)(rect.center);
			rect = viewRect.rect;
			m_ViewBounds = new Bounds(val, (Vector2)(rect.size));
			m_ContentBounds = GetBounds();
		}
		if (m_VSliderExpand && vScrollingNeeded)
		{
			viewRect.sizeDelta = new Vector2(0f - (m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);
			LayoutRebuilder.ForceRebuildLayoutImmediate(m_Content);
			rect = viewRect.rect;
			Vector3 val2 = (Vector2)(rect.center);
			rect = viewRect.rect;
			m_ViewBounds = new Bounds(val2, (Vector2)(rect.size));
			m_ContentBounds = GetBounds();
		}
		if (m_HSliderExpand && hScrollingNeeded)
		{
			viewRect.sizeDelta = new Vector2(viewRect.sizeDelta.x, 0f - (m_HSliderHeight + m_HorizontalScrollbarSpacing));
			rect = viewRect.rect;
			Vector3 val3 = (Vector2)(rect.center);
			rect = viewRect.rect;
			m_ViewBounds = new Bounds(val3, (Vector2)(rect.size));
			m_ContentBounds = GetBounds();
		}
		if (m_VSliderExpand && vScrollingNeeded && viewRect.sizeDelta.x == 0f && viewRect.sizeDelta.y < 0f)
		{
			viewRect.sizeDelta = new Vector2(0f - (m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);
		}
	}

	public virtual void SetLayoutVertical()
	{
		UpdateScrollbarLayout();
		Rect rect = viewRect.rect;
		Vector3 val = (Vector2)(rect.center);
		rect = viewRect.rect;
		m_ViewBounds = new Bounds(val, (Vector2)(rect.size));
		m_ContentBounds = GetBounds();
	}

	private void UpdateScrollbarVisibility()
	{
		UpdateOneScrollbarVisibility(vScrollingNeeded, m_Vertical, m_VerticalScrollbarVisibility, m_VerticalScrollbar);
		UpdateOneScrollbarVisibility(hScrollingNeeded, m_Horizontal, m_HorizontalScrollbarVisibility, m_HorizontalScrollbar);
	}

	private static void UpdateOneScrollbarVisibility(bool xScrollingNeeded, bool xAxisEnabled, ScrollbarVisibility scrollbarVisibility, Scrollbar scrollbar)
	{
		if (!(bool)((Object)(object)scrollbar))
		{
			return;
		}
		if (scrollbarVisibility == ScrollbarVisibility.Permanent)
		{
			if (((Component)scrollbar).gameObject.activeSelf != xAxisEnabled)
			{
				((Component)scrollbar).gameObject.SetActive(xAxisEnabled);
			}
		}
		else if (((Component)scrollbar).gameObject.activeSelf != xScrollingNeeded)
		{
			((Component)scrollbar).gameObject.SetActive(xScrollingNeeded);
		}
	}

	private void UpdateScrollbarLayout()
	{
		if (m_VSliderExpand && (bool)((Object)(object)m_HorizontalScrollbar))
		{
			m_Tracker.Add((Object)(object)this, m_HorizontalScrollbarRect, (DrivenTransformProperties)5378);
			m_HorizontalScrollbarRect.anchorMin = new Vector2(0f, m_HorizontalScrollbarRect.anchorMin.y);
			m_HorizontalScrollbarRect.anchorMax = new Vector2(1f, m_HorizontalScrollbarRect.anchorMax.y);
			m_HorizontalScrollbarRect.anchoredPosition = new Vector2(0f, m_HorizontalScrollbarRect.anchoredPosition.y);
			if (vScrollingNeeded)
			{
				m_HorizontalScrollbarRect.sizeDelta = new Vector2(0f - (m_VSliderWidth + m_VerticalScrollbarSpacing), m_HorizontalScrollbarRect.sizeDelta.y);
			}
			else
			{
				m_HorizontalScrollbarRect.sizeDelta = new Vector2(0f, m_HorizontalScrollbarRect.sizeDelta.y);
			}
		}
		if (m_HSliderExpand && (bool)((Object)(object)m_VerticalScrollbar))
		{
			m_Tracker.Add((Object)(object)this, m_VerticalScrollbarRect, (DrivenTransformProperties)10756);
			m_VerticalScrollbarRect.anchorMin = new Vector2(m_VerticalScrollbarRect.anchorMin.x, 0f);
			m_VerticalScrollbarRect.anchorMax = new Vector2(m_VerticalScrollbarRect.anchorMax.x, 1f);
			m_VerticalScrollbarRect.anchoredPosition = new Vector2(m_VerticalScrollbarRect.anchoredPosition.x, 0f);
			if (hScrollingNeeded)
			{
				m_VerticalScrollbarRect.sizeDelta = new Vector2(m_VerticalScrollbarRect.sizeDelta.x, 0f - (m_HSliderHeight + m_HorizontalScrollbarSpacing));
			}
			else
			{
				m_VerticalScrollbarRect.sizeDelta = new Vector2(m_VerticalScrollbarRect.sizeDelta.x, 0f);
			}
		}
	}

	protected void UpdateBounds(bool updateItems = false)
	{
		Rect rect = viewRect.rect;
		Vector3 val = (Vector2)(rect.center);
		rect = viewRect.rect;
		m_ViewBounds = new Bounds(val, (Vector2)(rect.size));
		m_ContentBounds = GetBounds();
		if ((Object)(object)m_Content == (Object)null)
		{
			return;
		}
		if ((Application.isPlaying & updateItems) && UpdateItems(ref m_ViewBounds, ref m_ContentBounds))
		{
			EnsureLayoutHasRebuilt();
			m_ContentBounds = GetBounds();
		}
		Vector3 contentSize = m_ContentBounds.size;
		Vector3 contentPos = m_ContentBounds.center;
		Vector2 contentPivot = m_Content.pivot;
		AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
		m_ContentBounds.size = contentSize;
		m_ContentBounds.center = contentPos;
		if (movementType != MovementType.Clamped)
		{
			return;
		}
		Vector2 zero = Vector2.zero;
		if (m_ViewBounds.max.x > m_ContentBounds.max.x)
		{
			zero.x = Math.Min(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);
		}
		else if (m_ViewBounds.min.x < m_ContentBounds.min.x)
		{
			zero.x = Math.Max(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);
		}
		if (m_ViewBounds.min.y < m_ContentBounds.min.y)
		{
			zero.y = Math.Max(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);
		}
		else if (m_ViewBounds.max.y > m_ContentBounds.max.y)
		{
			zero.y = Math.Min(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);
		}
		if (zero.sqrMagnitude > float.Epsilon)
		{
			contentPos = (Vector2)(m_Content.anchoredPosition + zero);
			if (!m_Horizontal)
			{
				contentPos.x = m_Content.anchoredPosition.x;
			}
			if (!m_Vertical)
			{
				contentPos.y = m_Content.anchoredPosition.y;
			}
			AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
		}
	}

	internal static void AdjustBounds(ref Bounds viewBounds, ref Vector2 contentPivot, ref Vector3 contentSize, ref Vector3 contentPos)
	{
		Vector3 val = viewBounds.size - contentSize;
		if (val.x > 0f)
		{
			contentPos.x -= val.x * (contentPivot.x - 0.5f);
			contentSize.x = viewBounds.size.x;
		}
		if (val.y > 0f)
		{
			contentPos.y -= val.y * (contentPivot.y - 0.5f);
			contentSize.y = viewBounds.size.y;
		}
	}

	protected Bounds GetBounds()
	{
		if ((Object)(object)m_Content == (Object)null)
		{
			return default;
		}
		m_Content.GetWorldCorners(m_Corners);
		Matrix4x4 viewWorldToLocalMatrix = ((Transform)viewRect).worldToLocalMatrix;
		return InternalGetBounds(m_Corners, ref viewWorldToLocalMatrix);
	}

	internal static Bounds InternalGetBounds(Vector3[] corners, ref Matrix4x4 viewWorldToLocalMatrix)
	{
		Vector3 val = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 val2 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		for (int i = 0; i < 4; i++)
		{
			Vector3 val3 = viewWorldToLocalMatrix.MultiplyPoint3x4(corners[i]);
			val = Vector3.Min(val3, val);
			val2 = Vector3.Max(val3, val2);
		}
		Bounds result = new Bounds(val, Vector3.zero);
		result.Encapsulate(val2);
		return result;
	}

	private Bounds GetBounds4Item(int index)
	{
		if ((Object)(object)m_Content == (Object)null)
		{
			return default;
		}
		int num = index - itemTypeStart;
		if (num < 0 || num >= ((Transform)m_Content).childCount)
		{
			return default;
		}
		Transform child = ((Transform)m_Content).GetChild(num);
		RectTransform val = (RectTransform)(object)((child is RectTransform) ? child : null);
		if ((Object)(object)val == (Object)null)
		{
			return default;
		}
		val.GetWorldCorners(m_Corners);
		Matrix4x4 viewWorldToLocalMatrix = ((Transform)viewRect).worldToLocalMatrix;
		return InternalGetBounds(m_Corners, ref viewWorldToLocalMatrix);
	}

	private Vector2 CalculateOffset(Vector2 delta)
	{
		if (totalCount < 0 || movementType == MovementType.Unrestricted)
		{
			return delta;
		}
		Bounds contentBounds = m_ContentBounds;
		if (m_Horizontal)
		{
			GetHorizonalOffsetAndSize(out var totalSize, out var offset);
			Vector3 center = contentBounds.center;
			center.x = offset;
			contentBounds.Encapsulate(center);
			center.x = offset + totalSize;
			contentBounds.Encapsulate(center);
		}
		if (m_Vertical)
		{
			GetVerticalOffsetAndSize(out var totalSize2, out var offset2);
			Vector3 center2 = contentBounds.center;
			center2.y = offset2;
			contentBounds.Encapsulate(center2);
			center2.y = offset2 - totalSize2;
			contentBounds.Encapsulate(center2);
		}
		return InternalCalculateOffset(ref m_ViewBounds, ref contentBounds, m_Horizontal, m_Vertical, m_MovementType, ref delta);
	}

	internal static Vector2 InternalCalculateOffset(ref Bounds viewBounds, ref Bounds contentBounds, bool horizontal, bool vertical, MovementType movementType, ref Vector2 delta)
	{
		Vector2 zero = Vector2.zero;
		if (movementType == MovementType.Unrestricted)
		{
			return zero;
		}
		Vector2 val = (Vector2)(contentBounds.min);
		Vector2 val2 = (Vector2)(contentBounds.max);
		if (horizontal)
		{
			val.x += delta.x;
			val2.x += delta.x;
			float num = viewBounds.max.x - val2.x;
			float num2 = viewBounds.min.x - val.x;
			if (num2 < -0.001f)
			{
				zero.x = num2;
			}
			else if (num > 0.001f)
			{
				zero.x = num;
			}
		}
		if (vertical)
		{
			val.y += delta.y;
			val2.y += delta.y;
			float num3 = viewBounds.max.y - val2.y;
			float num4 = viewBounds.min.y - val.y;
			if (num3 > 0.001f)
			{
				zero.y = num3;
			}
			else if (num4 < -0.001f)
			{
				zero.y = num4;
			}
		}
		return zero;
	}

	protected void SetDirty()
	{
		if (((UIBehaviour)this).IsActive())
		{
			LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
		}
	}

	protected void SetDirtyCaching()
	{
		if (((UIBehaviour)this).IsActive())
		{
			CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild((ICanvasElement)(object)this);
			LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
		}
	}
}

}
