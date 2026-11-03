using System;
using System.Collections.Generic;
using UnityEngine;

public class GrapplingLine
{
	private LineRenderer mLineRenderer;

	private GrapplingLineSetting mSetting;

	private List<GrapplingLineNode> mLineNodes;

	private GrapplingLineState mState;

	private Vector2 mOriginPos;

	private Vector2 mTargetPos;

	private float mOriginZ;

	private float mTargetZ;

	private Action<GrapplingLineState> mOnStateUpdateCallback;

	public GrapplingLine(LineRenderer lineRenderer, GrapplingLineSetting grapplingLineSetting)
	{
		lineRenderer.useWorldSpace = true;
		mLineRenderer = lineRenderer;
		mSetting = grapplingLineSetting;
		mLineNodes = new List<GrapplingLineNode>();
		Clear();
	}

	public void SetOrigin(Vector3 originPos)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (mLineNodes.Count > 0)
		{
			mOriginPos = originPos;
			mOriginZ = originPos.z;
			GrapplingLineNode value = mLineNodes[0];
			value.Update(mOriginPos);
			value.UpdateZ(mOriginZ);
			mLineNodes[0] = value;
			mLineRenderer.SetPosition(0, value.Position3D);
		}
	}

	public void Play(Vector3 originPos, Vector3 targetPos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = (Vector2)targetPos - (Vector2)originPos;
		float magnitude = val.magnitude;
		if (!(magnitude <= 0f))
		{
			mLineNodes.Clear();
			mOriginPos = originPos;
			mTargetPos = targetPos;
			mOriginZ = originPos.z;
			mTargetZ = targetPos.z;
			int num = Mathf.Max(2, (int)(magnitude / mSetting.LinkLength * (float)mSetting.LinkDensity));
			mLineRenderer.positionCount = num;
			for (int i = 0; i < num; i++)
			{
				List<GrapplingLineNode> list = mLineNodes;
				val = mTargetPos - mOriginPos;
				val = GetOffsetDir(val.normalized, mSetting.DiffusionAngle * Mathf.SmoothStep(1f, 0f, (float)i / (float)num) * (float)(int)Mathf.Pow(-1f, (float)(int)((float)i * mSetting.LinkLength)));
				list.Add(new GrapplingLineNode(originPos, val.normalized));
				mLineRenderer.SetPosition(i, originPos);
			}
			mState = GrapplingLineState.Grappling;
			mOnStateUpdateCallback?.Invoke(mState);
		}
	}

	public void PhysicalFixedUpdate(float process, float fixedDeltaTime)
	{
		switch (mState)
		{
		case GrapplingLineState.Grappling:
			Grappling(process, fixedDeltaTime);
			if (!(process < 1f))
			{
				mState = GrapplingLineState.Holding;
				mOnStateUpdateCallback?.Invoke(mState);
			}
			break;
		case GrapplingLineState.Holding:
			Grappling(1f, fixedDeltaTime);
			if (!(process < 1f))
			{
				mState = GrapplingLineState.Dragging;
				mOnStateUpdateCallback?.Invoke(mState);
			}
			break;
		case GrapplingLineState.Dragging:
			Dragging(process);
			break;
		case GrapplingLineState.Clear:
		case GrapplingLineState.Done:
			break;
		}
	}

	public void Render()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (mState != GrapplingLineState.Clear)
		{
			int count = mLineNodes.Count;
			for (int i = 1; i < count; i++)
			{
				mLineRenderer.SetPosition(i, mLineNodes[i].Position3D);
			}
		}
	}

	public void Clear()
	{
		mLineNodes.Clear();
		mLineRenderer.positionCount = 0;
		mState = GrapplingLineState.Clear;
		mOnStateUpdateCallback?.Invoke(mState);
	}

	public void HookOnStateUpdate(Action<GrapplingLineState> callback)
	{
		mOnStateUpdateCallback = (Action<GrapplingLineState>)Delegate.Combine(mOnStateUpdateCallback, callback);
	}

	public void UnhookOnStateUpdate(Action<GrapplingLineState> callback)
	{
		mOnStateUpdateCallback = (Action<GrapplingLineState>)Delegate.Remove(mOnStateUpdateCallback, callback);
	}

	private void Grappling(float process, float deltaTime)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		int num = mLineNodes.Count - 1;
		GrapplingLineNode value = mLineNodes[num];
		value.Update(SmoothStep(value.Position, mTargetPos, process));
		mLineNodes[num] = value;
		for (int i = 1; i < num; i++)
		{
			GrapplingLineNode value2 = mLineNodes[i];
			Vector2 val = value2.Position + (value2.Position - value2.LastPosition);
			Vector2 val2 = deltaTime * deltaTime * value2.Direction;
			Vector2 val3 = mTargetPos - value2.Position;
			Vector2 val4 = val + val2 * Mathf.SmoothStep(val3.magnitude * mSetting.DiffusionStrength, 0f, process);
			if (IsOverThrough(mOriginPos, mTargetPos, val4))
			{
				val4 = mTargetPos;
			}
			value2.Update(val4);
			mLineNodes[i] = value2;
		}
		for (int j = 0; j < mSetting.Stiffness; j++)
		{
			for (int num2 = num; num2 > 0; num2--)
			{
				GrapplingLineNode value3 = mLineNodes[num2];
				GrapplingLineNode value4 = mLineNodes[num2 - 1];
				Vector2 val5 = value4.Position - value3.Position;
				if (!(val5 == Vector2.zero))
				{
					float magnitude = val5.magnitude;
					float num3 = (magnitude - mSetting.LinkLength) / magnitude;
					if (num2 < num && value3.Position != mTargetPos)
					{
						value3.Update(value3.Position + 0.5f * num3 * val5, value3.LastPosition);
						mLineNodes[num2] = value3;
					}
					if (num2 > 1 && !(value4.Position == mTargetPos))
					{
						value4.Update(value4.Position - 0.5f * num3 * val5, value4.LastPosition);
						mLineNodes[num2 - 1] = value4;
					}
				}
			}
		}
		Vector2 val6 = mTargetPos - mOriginPos;
		float magnitude2 = val6.magnitude;
		for (int num4 = num; num4 > 0; num4--)
		{
			GrapplingLineNode value5 = mLineNodes[num4];
			float num5 = Vector2.Dot(mTargetPos - value5.Position, val6) / magnitude2;
			float num6 = 1f - num5 / magnitude2;
			value5.UpdateZ(Mathf.Lerp(mOriginZ, mTargetZ, num6));
			mLineNodes[num4] = value5;
		}
	}

	private void Dragging(float process)
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		int num = mLineNodes.Count - 1;
		for (int i = 1; i <= num; i++)
		{
			GrapplingLineNode value = mLineNodes[i];
			if (i == num)
			{
				value.Update(Vector2.Lerp(mTargetPos, mOriginPos, process));
			}
			else
			{
				value.Update(SmoothStep(SmoothStep(value.Position, GetPerpendicularPoint(mTargetPos, mOriginPos, value.Position), process), mOriginPos, process));
			}
			mLineNodes[i] = value;
		}
		Vector2 val = mOriginPos - mTargetPos;
		float magnitude = val.magnitude;
		for (int num2 = num; num2 > 0; num2--)
		{
			GrapplingLineNode value2 = mLineNodes[num2];
			float num3 = Vector2.Dot(mOriginPos - value2.Position, val) / magnitude;
			float num4 = 1f - num3 / magnitude;
			value2.UpdateZ(Mathf.Lerp(mTargetZ, mOriginZ, num4));
			mLineNodes[num2] = value2;
		}
		if (!(process < 1f))
		{
			mState = GrapplingLineState.Done;
			mOnStateUpdateCallback?.Invoke(mState);
		}
	}

	private Vector2 SmoothStep(Vector2 from, Vector2 to, float t)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
		return new Vector2(Mathf.Lerp(from.x, to.x, t), Mathf.Lerp(from.y, to.y, t));
	}

	private Vector2 GetOffsetDir(Vector2 dir, float angle)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Cos(angle * ((float)Math.PI / 180f));
		float num2 = Mathf.Sin(angle * ((float)Math.PI / 180f));
		return new Vector2(dir.x * num - dir.y * num2, dir.x * num2 + dir.y * num);
	}

	private Vector2 GetPerpendicularPoint(Vector2 A, Vector2 B, Vector2 P)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		float num = B.x - A.x;
		float num2 = B.y - A.y;
		float num3 = ((P.x - A.x) * num + (P.y - A.y) * num2) / (num * num + num2 * num2);
		return new Vector2(A.x + num3 * num, A.y + num3 * num2);
	}

	private bool IsOverThrough(Vector2 origin, Vector2 target, Vector2 current)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = target - origin;
		return Vector2.Dot(current - origin, val) / Mathf.Pow(val.magnitude, 2f) > 1f;
	}
}
