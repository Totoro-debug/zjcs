// Ported from the pinned official DragControllerBase.cs; Windows uses local component lifecycle.
namespace Zjcs.Official.WorldUI
{
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(1)]
public abstract class DragControllerBase : MonoBehaviour
{
	public class DragTargetPosChangeEvent : UnityEvent<Vector2>
	{
	}

	public DragControllerData Data;

	[SerializeField]
	private DragTargetPosChangeEvent mOnPosChanged = new DragTargetPosChangeEvent();

	private Vector2 mPointerStartLocalCursor;

	private Vector2 mTargetStartPosition;

	private int mDragTouchCount;

	private Vector2 mPrevPosition;

	private Bounds mPrevTargetBounds;

	protected Vector2 mVelocity;

	protected bool mDragging;

	public DragTargetPosChangeEvent OnPosChanged => mOnPosChanged;

	public bool CanDrag { get; set; } = true;

	public Vector2 ScreenOffsetScale { get; set; }

	protected virtual void OnEnable()
	{

	}

	protected virtual void OnDisable()
	{

		mDragging = false;
		mVelocity = Vector2.zero;
	}

	public void SetBorderBounds(Bounds bounds)
	{

		if (bounds != default(Bounds))
		{
			Data.BorderBounds = bounds;
		}
	}

	public void SetScreenOffsetScale(Camera camera)
	{

		ScreenOffsetScale = new Vector2(camera.orthographicSize * 2f * camera.aspect / Screen.width, camera.orthographicSize * 2f / Screen.height);
	}

	private bool IsActive()
	{
		if (((Behaviour)this).isActiveAndEnabled)
		{
			return Data.Target != null;
		}
		return false;
	}

	protected virtual void OnZoomBegin()
	{

		mDragging = false;
		mVelocity = Vector2.zero;
	}

	protected virtual void OnDragBegin()
	{

		mVelocity = Vector2.zero;
		if (IsActive())
		{
			Data.UpdateBounds();
			ResetDragAnchor();
			mDragging = true;
		}
	}

	private void ResetDragAnchor()
	{

		mPointerStartLocalCursor = (Vector2)Input.mousePosition;
		mTargetStartPosition = (Vector2)Data.Target.position;
		mDragTouchCount = Input.touchCount;
	}

	protected virtual void OnDrag()
	{
		if (Data.DragUpdateMode == DragControllerData.DragUpdateType.OnDragUpdate)
		{
			ManualUpdateDrag();
		}
	}

	protected virtual void OnDragEnd()
	{
		mDragging = false;
	}

	public void ManualUpdateDrag()
	{

		if (!mDragging || !CanDrag || !IsActive())
		{
			return;
		}
		if (Input.touchCount != mDragTouchCount)
		{
			ResetDragAnchor();
			return;
		}
		Vector2 val = (Vector2)Input.mousePosition;
		Data.UpdateBounds();
		Vector2 val2 = mPointerStartLocalCursor - val;
		Vector2 val3 = mTargetStartPosition + val2 * ScreenOffsetScale * Data.Sensitive;
		Vector2 val4 = CalculateOffset(val3 - (Vector2)Data.Target.position);
		val3 += val4;
		if (Data.MyMovementType == MovementType.Elastic)
		{
			if (val4.x != 0f)
			{
				val3.x -= DragControlUtils.RubberDelta(val4.x, Data.TargetBounds.size.x, Data.RubberOverFactor);
			}
			if (val4.y != 0f)
			{
				val3.y -= DragControlUtils.RubberDelta(val4.y, Data.TargetBounds.size.y, Data.RubberOverFactor);
			}
		}
		SetContentAnchoredPosition(val3);
	}

	public void StopVelocity()
	{

		mVelocity = Vector2.zero;
	}

	protected virtual void SetContentAnchoredPosition(Vector2 position)
	{

		Vector3 position2 = Data.Target.position;
		if (!Data.Horizontal)
		{
			position.x = position2.x;
		}
		if (!Data.Vertical)
		{
			position.y = position2.y;
		}
		Vector3 val = (Vector3)position;
		val.z = position2.z;
		if (val != position2)
		{
			if (Data.IsContentPosLimit && Data.MyMovementType != MovementType.Unrestricted)
			{
				val.x = Mathf.Clamp(position.x, Data.ContentPosLimitX.x, Data.ContentPosLimitX.y);
				val.y = Mathf.Clamp(position.y, Data.ContentPosLimitY.x, Data.ContentPosLimitY.y);
			}
			Data.Target.position = val;
			Data.UpdateBounds();
		}
	}

	protected virtual void LateUpdate()
	{
		if (Data.UpdateMode == DragControllerData.UpdateType.LateUpdate)
		{
			ManualUpdate();
		}
	}

	public void ManualUpdate()
	{

		if (Data.Target == null)
		{
			return;
		}
		Data.UpdateBounds();
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		Vector2 val = (Vector2)Data.Target.position;
		Vector2 val2 = CalculateOffset(Vector2.zero);
		if (!mDragging && (val2 != Vector2.zero || mVelocity != Vector2.zero))
		{
			Vector2 val3 = val;
			for (int i = 0; i < 2; i++)
			{
				if (Data.MyMovementType == MovementType.Elastic && val2[i] != 0f)
				{
					float num = mVelocity[i];
					float elasticity = Data.Elasticity;
					val3[i] = Mathf.SmoothDamp(val[i], val[i] + val2[i], ref num, elasticity, float.PositiveInfinity, unscaledDeltaTime);
					if (Mathf.Abs(num) < 1f)
					{
						num = 0f;
					}
					mVelocity[i] = num;
				}
				else if (Data.Inertia)
				{
					ref Vector2 reference = ref mVelocity;
					int num2 = i;
					reference[num2] *= Mathf.Pow(Data.DecelerationRate, unscaledDeltaTime);
					if (Mathf.Abs(mVelocity[i]) < 1f)
					{
						mVelocity[i] = 0f;
					}
					val3[i] += mVelocity[i] * unscaledDeltaTime;
				}
				else
				{
					mVelocity[i] = 0f;
				}
			}
			if (Data.MyMovementType == MovementType.Clamped)
			{
				val2 = CalculateOffset(val3 - val);
				val3 += val2;
			}
			SetContentAnchoredPosition(val3);
		}
		if (mDragging && Data.Inertia)
		{
			Vector3 val4 = (Vector3)((val - mPrevPosition) / unscaledDeltaTime);
			mVelocity = (Vector2)Vector3.Lerp(mVelocity, val4, unscaledDeltaTime * 10f);
		}
		if (Data.TargetBounds != mPrevTargetBounds || val != mPrevPosition)
		{
			((UnityEvent<Vector2>)mOnPosChanged)?.Invoke((Vector2)Data.Target.position);
			UpdatePrevData();
		}
	}

	protected void UpdatePrevData()
	{

		if ((Object)(object)Data.Target == (Object)null)
		{
			mPrevPosition = Vector2.zero;
		}
		else
		{
			mPrevPosition = (Vector2)Data.Target.position;
		}
		mPrevTargetBounds = Data.TargetBounds;
	}

	private Vector2 CalculateOffset(Vector2 delta)
	{

		return DragControlUtils.CalculateOffset(ref Data.BorderBounds, ref Data.TargetBounds, Data.Horizontal, Data.Vertical, Data.MyMovementType, ref delta);
	}

	protected DragControllerBase()
	{

		ScreenOffsetScale = new Vector2(1f, 1f);
		mPointerStartLocalCursor = Vector2.zero;
		mTargetStartPosition = Vector2.zero;
		mPrevPosition = Vector2.zero;

	}
}

}
