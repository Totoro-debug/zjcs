// Ported from framework-source/FancyScrollView/Scroller.cs. Dependency injection, Wwise and animator calls use local bindings.
using System;
using Zjcs.Official.SkillsUI.EasingCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI.FancyScrollView
{

public class Scroller : UIBehaviour, IPointerUpHandler, IEventSystemHandler, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler
{
	[Serializable]
	private class Snap
	{
		public bool Enable;

		public float VelocityThreshold;

		public float Duration;

		public Ease Easing;
	}

	private class AutoScrollState
	{
		public bool Enable;

		public bool Elastic;

		public float Duration;

		public EasingFunction EasingFunction;

		public float StartTime;

		public float EndPosition;

		public Action OnComplete;

		public void Reset()
		{
			Enable = false;
			Elastic = false;
			Duration = 0f;
			StartTime = 0f;
			EasingFunction = DefaultEasingFunction;
			EndPosition = 0f;
			OnComplete = null;
		}

		public void Complete()
		{
			OnComplete?.Invoke();
			Reset();
		}
	}

	[SerializeField]
	private RectTransform viewport;

	[SerializeField]
	private ScrollDirection scrollDirection;

	[SerializeField]
	private MovementType movementType = MovementType.Elastic;

	[SerializeField]
	private float elasticity = 0.1f;

	[SerializeField]
	private float scrollSensitivity = 1f;

	[SerializeField]
	private bool inertia = true;

	[SerializeField]
	private float decelerationRate = 0.03f;

	[SerializeField]
	private Snap snap = new Snap
	{
		Enable = true,
		VelocityThreshold = 0.5f,
		Duration = 0.3f,
		Easing = Ease.InOutCubic
	};

	[SerializeField]
	private bool draggable = true;

	[SerializeField]
	private Scrollbar scrollbar;

	private readonly AutoScrollState autoScrollState = new AutoScrollState();

	private Action<float> onValueChanged;

	private Action<int> onSelectionChanged;

	private Vector2 beginDragPointerPosition;

	private float scrollStartPosition;

	private float prevPosition;

	private float currentPosition;

	private int totalCount;

	private bool hold;

	private bool scrolling;

	private bool dragging;

	private float velocity;

	private static readonly EasingFunction DefaultEasingFunction = Easing.Get(Ease.OutCubic);

	public float ViewportSize
	{
		get
		{



			Rect rect;
			if (scrollDirection != ScrollDirection.Horizontal)
			{
				rect = viewport.rect;
				return rect.size.y;
			}
			rect = viewport.rect;
			return rect.size.x;
		}
	}

	public ScrollDirection ScrollDirection => scrollDirection;

	public MovementType MovementType
	{
		get
		{
			return movementType;
		}
		set
		{
			movementType = value;
		}
	}

	public float Elasticity
	{
		get
		{
			return elasticity;
		}
		set
		{
			elasticity = value;
		}
	}

	public float ScrollSensitivity
	{
		get
		{
			return scrollSensitivity;
		}
		set
		{
			scrollSensitivity = value;
		}
	}

	public bool Inertia
	{
		get
		{
			return inertia;
		}
		set
		{
			inertia = value;
		}
	}

	public float DecelerationRate
	{
		get
		{
			return decelerationRate;
		}
		set
		{
			decelerationRate = value;
		}
	}

	public bool SnapEnabled
	{
		get
		{
			return snap.Enable;
		}
		set
		{
			snap.Enable = value;
		}
	}

	public bool Draggable
	{
		get
		{
			return draggable;
		}
		set
		{
			draggable = value;
		}
	}

	public Scrollbar Scrollbar => scrollbar;

	public float Position
	{
		get
		{
			return currentPosition;
		}
		set
		{
			autoScrollState.Reset();
			velocity = 0f;
			dragging = false;
			UpdatePosition(value);
		}
	}

	public float Velocity => velocity;

	protected override void Start()
	{
		base.Start();
		if (scrollbar != null)
		{
			((UnityEvent<float>)(object)scrollbar.onValueChanged).AddListener((UnityAction<float>)((float x) =>
			{
				UpdatePosition(x * ((float)totalCount - 1f), updateScrollbar: false);
			}));
		}
	}

	public void OnValueChanged(Action<float> callback)
	{
		onValueChanged = callback;
	}

	public void OnSelectionChanged(Action<int> callback)
	{
		onSelectionChanged = callback;
	}

	public void SetTotalCount(int totalCount)
	{
		this.totalCount = totalCount;
	}

	public void ScrollTo(float position, float duration, Action onComplete = null)
	{
		ScrollTo(position, duration, Ease.OutCubic, onComplete);
	}

	public void ScrollTo(float position, float duration, Ease easing, Action onComplete = null)
	{
		ScrollTo(position, duration, Easing.Get(easing), onComplete);
	}

	public void ScrollTo(float position, float duration, EasingFunction easingFunction, Action onComplete = null)
	{
		if (duration <= 0f)
		{
			Position = CircularPosition(position, totalCount);
			onComplete?.Invoke();
			return;
		}
		autoScrollState.Reset();
		autoScrollState.Enable = true;
		autoScrollState.Duration = duration;
		autoScrollState.EasingFunction = easingFunction ?? DefaultEasingFunction;
		autoScrollState.StartTime = Time.unscaledTime;
		autoScrollState.EndPosition = currentPosition + CalculateMovementAmount(currentPosition, position);
		autoScrollState.OnComplete = onComplete;
		velocity = 0f;
		scrollStartPosition = currentPosition;
		UpdateSelection(Mathf.RoundToInt(CircularPosition(autoScrollState.EndPosition, totalCount)));
	}

	public void JumpTo(int index)
	{
		if (index < 0 || index > totalCount - 1)
		{
			throw new ArgumentOutOfRangeException("index");
		}
		UpdateSelection(index);
		Position = index;
	}

	public MovementDirection GetMovementDirection(int sourceIndex, int destIndex)
	{
		float num = CalculateMovementAmount(sourceIndex, destIndex);
		if (scrollDirection != ScrollDirection.Horizontal)
		{
			if (!(num > 0f))
			{
				return MovementDirection.Down;
			}
			return MovementDirection.Up;
		}
		if (!(num > 0f))
		{
			return MovementDirection.Right;
		}
		return MovementDirection.Left;
	}

	void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
	{

		if (draggable && (int)eventData.button == 0)
		{
			hold = true;
			velocity = 0f;
			autoScrollState.Reset();
		}
	}

	void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
	{

		if (draggable && (int)eventData.button == 0)
		{
			if (hold && snap.Enable)
			{
				UpdateSelection(Mathf.Clamp(Mathf.RoundToInt(currentPosition), 0, totalCount - 1));
				ScrollTo(Mathf.RoundToInt(currentPosition), snap.Duration, snap.Easing);
			}
			hold = false;
		}
	}

	void IScrollHandler.OnScroll(PointerEventData eventData)
	{





		if (draggable)
		{
			Vector2 scrollDelta = eventData.scrollDelta;
			scrollDelta.y *= -1f;
			float num;
			if (scrollDirection != ScrollDirection.Horizontal)
			{
				num = ((Mathf.Abs(scrollDelta.x) > Mathf.Abs(scrollDelta.y)) ? scrollDelta.x : scrollDelta.y);
			}
			else
			{
				num = ((Mathf.Abs(scrollDelta.y) > Mathf.Abs(scrollDelta.x)) ? scrollDelta.y : scrollDelta.x);
			}
			if (eventData.IsScrolling())
			{
				scrolling = true;
			}
			float num2 = currentPosition + num / ViewportSize * scrollSensitivity;
			if (movementType == MovementType.Clamped)
			{
				num2 += CalculateOffset(num2);
			}
			if (autoScrollState.Enable)
			{
				autoScrollState.Reset();
			}
			UpdatePosition(num2);
		}
	}

	void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
	{

		if (draggable && (int)eventData.button == 0)
		{
			hold = false;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out beginDragPointerPosition);
			scrollStartPosition = currentPosition;
			dragging = true;
			autoScrollState.Reset();
		}
	}

	void IDragHandler.OnDrag(PointerEventData eventData)
	{




		Vector2 val = default;
		if (draggable && (int)eventData.button == 0 && dragging && RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out val))
		{
			Vector2 val2 = val - beginDragPointerPosition;
			float num = ((scrollDirection == ScrollDirection.Horizontal) ? (0f - val2.x) : val2.y) / ViewportSize * scrollSensitivity + scrollStartPosition;
			float num2 = CalculateOffset(num);
			num += num2;
			if (movementType == MovementType.Elastic && num2 != 0f)
			{
				num -= RubberDelta(num2, scrollSensitivity);
			}
			UpdatePosition(num);
		}
	}

	void IEndDragHandler.OnEndDrag(PointerEventData eventData)
	{

		if (draggable && (int)eventData.button == 0)
		{
			dragging = false;
		}
	}

	private float CalculateOffset(float position)
	{
		if (movementType == MovementType.Unrestricted)
		{
			return 0f;
		}
		if (position < 0f)
		{
			return 0f - position;
		}
		if (position > (float)(totalCount - 1))
		{
			return (float)(totalCount - 1) - position;
		}
		return 0f;
	}

	private void UpdatePosition(float position, bool updateScrollbar = true)
	{
		onValueChanged?.Invoke(currentPosition = position);
		if (scrollbar != null & updateScrollbar)
		{
			scrollbar.value = Mathf.Clamp01(position / Mathf.Max((float)totalCount - 1f, 0.0001f));
		}
	}

	private void UpdateSelection(int index)
	{
		onSelectionChanged?.Invoke(index);
	}

	private float RubberDelta(float overStretching, float viewSize)
	{
		return (1f - 1f / (Mathf.Abs(overStretching) * 0.55f / viewSize + 1f)) * viewSize * Mathf.Sign(overStretching);
	}

	private void Update()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		float num = CalculateOffset(currentPosition);
		if (autoScrollState.Enable)
		{
			float num2 = 0f;
			if (autoScrollState.Elastic)
			{
				num2 = Mathf.SmoothDamp(currentPosition, currentPosition + num, ref velocity, elasticity, float.PositiveInfinity, unscaledDeltaTime);
				if (Mathf.Abs(velocity) < 0.01f)
				{
					num2 = Mathf.Clamp(Mathf.RoundToInt(num2), 0, totalCount - 1);
					velocity = 0f;
					autoScrollState.Complete();
				}
			}
			else
			{
				float num3 = Mathf.Clamp01((Time.unscaledTime - autoScrollState.StartTime) / Mathf.Max(autoScrollState.Duration, float.Epsilon));
				num2 = Mathf.LerpUnclamped(scrollStartPosition, autoScrollState.EndPosition, autoScrollState.EasingFunction(num3));
				if (Mathf.Approximately(num3, 1f))
				{
					autoScrollState.Complete();
				}
			}
			UpdatePosition(num2);
		}
		else if (!dragging && !scrolling && (!Mathf.Approximately(num, 0f) || !Mathf.Approximately(velocity, 0f)))
		{
			float num4 = currentPosition;
			if (movementType == MovementType.Elastic && !Mathf.Approximately(num, 0f))
			{
				autoScrollState.Reset();
				autoScrollState.Enable = true;
				autoScrollState.Elastic = true;
				UpdateSelection(Mathf.Clamp(Mathf.RoundToInt(num4), 0, totalCount - 1));
			}
			else if (inertia)
			{
				velocity *= Mathf.Pow(decelerationRate, unscaledDeltaTime);
				if (Mathf.Abs(velocity) < 0.001f)
				{
					velocity = 0f;
				}
				num4 += velocity * unscaledDeltaTime;
				if (snap.Enable && Mathf.Abs(velocity) < snap.VelocityThreshold)
				{
					ScrollTo(Mathf.RoundToInt(currentPosition), snap.Duration, snap.Easing);
				}
			}
			else
			{
				velocity = 0f;
			}
			if (!Mathf.Approximately(velocity, 0f))
			{
				if (movementType == MovementType.Clamped)
				{
					num = CalculateOffset(num4);
					num4 += num;
					if (Mathf.Approximately(num4, 0f) || Mathf.Approximately(num4, (float)totalCount - 1f))
					{
						velocity = 0f;
						UpdateSelection(Mathf.RoundToInt(num4));
					}
				}
				UpdatePosition(num4);
			}
		}
		if (!autoScrollState.Enable && (dragging || scrolling) && inertia)
		{
			float num5 = (currentPosition - prevPosition) / unscaledDeltaTime;
			velocity = Mathf.Lerp(velocity, num5, unscaledDeltaTime * 10f);
		}
		prevPosition = currentPosition;
		scrolling = false;
	}

	private float CalculateMovementAmount(float sourcePosition, float destPosition)
	{
		if (movementType != MovementType.Unrestricted)
		{
			return Mathf.Clamp(destPosition, 0f, (float)(totalCount - 1)) - sourcePosition;
		}
		float num = CircularPosition(destPosition, totalCount) - CircularPosition(sourcePosition, totalCount);
		if (Mathf.Abs(num) > (float)totalCount * 0.5f)
		{
			num = Mathf.Sign(0f - num) * ((float)totalCount - Mathf.Abs(num));
		}
		return num;
	}

	private float CircularPosition(float p, int size)
	{
		if (size >= 1)
		{
			if (!(p < 0f))
			{
				return p % (float)size;
			}
			return (float)(size - 1) + (p + 1f) % (float)size;
		}
		return 0f;
	}
}

}
