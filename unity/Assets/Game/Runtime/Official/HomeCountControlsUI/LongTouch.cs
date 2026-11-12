// Original quantity control; language/color/input services explicitly bound.
namespace Zjcs.Official.HomeCountControlsUI
{
using Object=UnityEngine.Object;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class LongTouch : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler
{
    public Func<Vector2?> PointerPositionOverride { get; set; }

	public float InitInterval = 0.5f;

	public float IntervalChangeValue = 0.05f;

	public float MinInterval = 0.1f;

	public int AccelerateChangeCount = 10;

	public float AccelerateChangeExponent = 1.5f;

	public bool AutoReset;

	private const int kDefaultDragThreshold = 10;

	private bool mIsButtonPressed;

	private float mIntervalStartTime;

	private float mLongTouchInterval;

	private int mInvokeLongTouchCallbackCount;

	private Vector2 mPressPosition;

	private int mPointerId;

	private PointerEventData mPointerEventData;

	public event Action<long> OnLongTouch;

	private float CalculateLongTouchInterval()
	{
		if (mLongTouchInterval <= MinInterval)
		{
			return mLongTouchInterval;
		}
		float val = InitInterval - IntervalChangeValue * (float)mInvokeLongTouchCallbackCount;
		mLongTouchInterval = Math.Max(val, MinInterval);
		return mLongTouchInterval;
	}

	public void OnPointerDown(PointerEventData eventData)
	{


		InitData();
		mIsButtonPressed = true;
		mIntervalStartTime = Time.time;
		mPressPosition = eventData.position;
		mPointerId = eventData.pointerId;
		mPointerEventData = eventData;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		InitData();
	}

	private void InitData()
	{


		mLongTouchInterval = InitInterval;
		mIsButtonPressed = false;
		mIntervalStartTime = 0f;
		mInvokeLongTouchCallbackCount = 0;
		mPressPosition = Vector2.zero;
		mPointerId = int.MinValue;
		mPointerEventData = null;
	}

	private long GetChangeValue()
	{
		if (mInvokeLongTouchCallbackCount < AccelerateChangeCount)
		{
			return 1L;
		}
		return (long)Math.Ceiling(Math.Pow(AccelerateChangeExponent, mInvokeLongTouchCallbackCount - AccelerateChangeCount));
	}

	private void InvokeLongTouchCallback()
	{
		OnLongTouch?.Invoke(GetChangeValue());
		mInvokeLongTouchCallbackCount++;
		mIntervalStartTime = Time.time;
		CalculateLongTouchInterval();
		if (AutoReset)
		{
			Reset();
		}
	}

	private void Update()
	{
		if (mIsButtonPressed)
		{
			if (ShouldCancelLongTouchBecauseOfDrag())
			{
				InitData();
			}
			else if (Time.time - mIntervalStartTime > mLongTouchInterval)
			{
				InvokeLongTouchCallback();
			}
		}
	}

	private bool ShouldCancelLongTouchBecauseOfDrag()
	{




		if (mPointerEventData != null && mPointerEventData.dragging)
		{
			return true;
		}
		int num = (((Object)(object)EventSystem.current != (Object)null) ? EventSystem.current.pixelDragThreshold : 10);
		Vector2 val = GetCurrentPointerPosition() - mPressPosition;
		return val.sqrMagnitude > (float)(num * num);
	}

	private Vector2 GetCurrentPointerPosition()
	{
        var injected = PointerPositionOverride?.Invoke();
        if (injected.HasValue) return injected.Value;





		if (mPointerId >= 0)
		{
			int touchCount = Input.touchCount;
			for (int i = 0; i < touchCount; i++)
			{
				Touch touch = Input.GetTouch(i);
				if (touch.fingerId == mPointerId)
				{
					return touch.position;
				}
			}
		}
		return (Vector2)Input.mousePosition;
	}

	protected void Reset()
	{
		InitData();
	}
}

}
