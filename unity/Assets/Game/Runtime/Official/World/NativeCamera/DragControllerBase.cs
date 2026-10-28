using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
namespace Zjcs.Official.World.NativeCamera
{
[DefaultExecutionOrder(1)]
public abstract class DragControllerBase : MonoBehaviour
{
	public class DragTargetPosChangeEvent : UnityEvent<Vector2>
	{
	}
	public System.Func<Vector3> PointerPosition { get; set; } = () => Input.mousePosition;
	public System.Func<int> TouchCount { get; set; } = () => Input.touchCount;
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
	public IDragControllerManager DragControllerManager { get; set; }
	public DragTargetPosChangeEvent OnPosChanged => mOnPosChanged;
	public bool CanDrag { get; set; } = true;
	public Vector2 ScreenOffsetScale { get; set; }
	protected virtual void OnEnable()
	{
		DragControllerManager.RegisterController(this);
	}
	protected virtual void OnDisable()
	{
		DragControllerManager.UnregisterController(this);
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
		ScreenOffsetScale = CameraUtils.GetOrthographicCameraScreenScale(camera);
	}
	private bool IsActive()
	{
		if (((Behaviour)this).isActiveAndEnabled)
		{
			return (bool)((Object)(object)Data.Target);
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
		mPointerStartLocalCursor = CameraVectorCast.Convert(PointerPosition());
		mTargetStartPosition = CameraVectorCast.Convert(Data.Target.position);
		mDragTouchCount = TouchCount();
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
		if (TouchCount() != mDragTouchCount)
		{
			ResetDragAnchor();
			return;
		}
		Vector2 val = CameraVectorCast.Convert(PointerPosition());
		Data.UpdateBounds();
		Vector2 val2 = mPointerStartLocalCursor - val;
		Vector2 val3 = mTargetStartPosition + val2 * ScreenOffsetScale * Data.Sensitive;
		Vector2 val4 = CalculateOffset(val3 - CameraVectorCast.Convert(Data.Target.position));
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
		Vector3 val = CameraVectorCast.Convert(position);
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
		if (!(bool)((Object)(object)Data.Target))
		{
			return;
		}
		Data.UpdateBounds();
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		Vector2 val = CameraVectorCast.Convert(Data.Target.position);
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
					mVelocity[i] *= Mathf.Pow(Data.DecelerationRate, unscaledDeltaTime);
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
			Vector3 val4 = CameraVectorCast.Convert((val - mPrevPosition) / unscaledDeltaTime);
			mVelocity = CameraVectorCast.Convert(Vector3.Lerp(CameraVectorCast.Convert(mVelocity), val4, unscaledDeltaTime * 10f));
		}
		if (Data.TargetBounds != mPrevTargetBounds || val != mPrevPosition)
		{
			((UnityEvent<Vector2>)mOnPosChanged)?.Invoke(CameraVectorCast.Convert(((Component)Data.Target).transform.position));
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
			mPrevPosition = CameraVectorCast.Convert(Data.Target.position);
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
