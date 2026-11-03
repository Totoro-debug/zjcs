// Complete current original application source; see track runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.EntityLifecycle;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Zjcs.Official.BattleTrackPlayables
{
public class ParabolicTrack : MyTrack
{
	private abstract class ParabolicCalculator
	{
		protected ParabolicTrack mTrack;

		public ParabolicCalculator(ParabolicTrack track)
		{
			mTrack = track;
		}

		public abstract Vector3 CalcPos(float time);

		public abstract void Release();

		public static ParabolicCalculator Create(ParabolicTrack track)
		{
			return track.myLoopType switch
			{
				LoopType.Once => (ParabolicCalculator)new ParabolicOnceCalculator(track),
				LoopType.Loop => null,
				LoopType.Pingpong => new ParabolicBounceCalculator(track),
				_ => null,
			};
		}

		protected static float CalcX(float x0, float x1, float duration, float time)
		{
			float num = (x1 - x0) / duration;
			return x0 + num * time;
		}

		protected static float CalcY(float y0, float y1, float duration, float time, float g)
		{
			float num = (y1 - y0 + 0.5f * g * duration * duration) / duration;
			return y0 + num * time - 0.5f * g * time * time;
		}
	}

	private class ParabolicOnceCalculator : ParabolicCalculator
	{
		public ParabolicOnceCalculator(ParabolicTrack track)
			: base(track)
		{
		}

		public override Vector3 CalcPos(float time)
		{
			return RotatePointAroundPivot(new Vector3(ParabolicCalculator.CalcX(0f, mTrack.mUnitDirection.x, mTrack.mDuration, time), ParabolicCalculator.CalcY(0f, mTrack.mUnitDirection.y, mTrack.mDuration, time, mTrack.mAccelerationOfGravity), 0f), Vector3.zero, mTrack.mQuaternion);
		}

		public override void Release()
		{
		}
	}

	private class ParabolicBounceCalculator : ParabolicCalculator
	{
		private List<float> mBounceTimeRange;

		public ParabolicBounceCalculator(ParabolicTrack track)
			: base(track)
		{
			mBounceTimeRange = CollectionPool<List<float>, float>.Get();
			float num = ((track.mBounceDampingCoefficient == 1f) ? (track.mDuration / (float)track.mBounceCount) : (track.mDuration * (1f - track.mBounceDampingCoefficient) / (1f - Mathf.Pow(track.mBounceDampingCoefficient, (float)track.mBounceCount))));
			float num2 = num;
			mBounceTimeRange.Add(0f);
			mBounceTimeRange.Add(num2);
			for (int i = 1; i < track.mBounceCount; i++)
			{
				num *= track.mBounceDampingCoefficient;
				num2 += num;
				mBounceTimeRange.Add(num2);
			}
		}

		public override void Release()
		{
			CollectionPool<List<float>, float>.Release(mBounceTimeRange);
		}

		public override Vector3 CalcPos(float time)
		{
			return RotatePointAroundPivot(new Vector3(ParabolicCalculator.CalcX(0f, mTrack.mUnitDirection.x, mTrack.mDuration, time), CalcY(time), 0f), Vector3.zero, mTrack.mQuaternion);
		}

		private float CalcY(float time)
		{
			if (Mathf.Approximately(mTrack.mInitDirection.magnitude, 0f))
			{
				return 0f;
			}
			int count = mBounceTimeRange.Count;
			int i;
			for (i = 0; i < count - 1 && (!(time >= mBounceTimeRange[i]) || !(time < mBounceTimeRange[i + 1])); i++)
			{
			}
			if (i >= count - 1)
			{
				i = count - 2;
			}
			float duration = mBounceTimeRange[i + 1] - mBounceTimeRange[i];
			float time2 = time - mBounceTimeRange[i];
			float g = CalcAccelerationOfGravity(0f, 0f, mBounceTimeRange[1], mTrack.mFirstBounceMaxHight);
			return (ParabolicCalculator.CalcY(0f, 0f, duration, time2, g) + ParabolicCalculator.CalcX(0f, mTrack.mInitDirection.y, mTrack.mDuration, time)) / mTrack.mInitDirection.magnitude;
		}

		private float CalcAccelerationOfGravity(float y0, float y1, float duration, float maxH)
		{
			float num = 0.5f * duration * duration;
			float num2 = (0f - Mathf.Sqrt(2f * maxH)) * duration;
			float num3 = y1 - y0;
			float num4 = num2 * num2 - 4f * num * num3;
			if (num4 < 0f)
			{
				return mTrack.mAccelerationOfGravity;
			}
			num4 = Mathf.Sqrt(num4);
			float num5 = Mathf.Max((0f - num2 + num4) / (2f * num), (0f - num2 - num4) / (2f * num));
			return num5 * num5;
		}
	}

	[Tooltip("轨迹持续时间")]
	[SerializeField]
	private float mDuration = 1f;

	[Tooltip("重力加速度")]
	[SerializeField]
	private float mAccelerationOfGravity = 9.8f;

	[Tooltip("弹跳次数")]
	[SerializeField]
	private int mBounceCount = 1;

	[Tooltip("弹跳衰减系数")]
	[Range(0f, 1f)]
	[SerializeField]
	private float mBounceDampingCoefficient = 1f;

	[SerializeField]
	[Tooltip("第一次弹跳的最高高度")]
	private float mFirstBounceMaxHight = 1f;

	private Vector3 mInitDirection;

	private Vector3 mUnitDirection;

	private Quaternion mQuaternion;

	private ParabolicCalculator mCalculator;

	public override float GetDuration()
	{
		return mDuration;
	}

	public override void Evaluate(float time, Vector3 direction, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		base.Evaluate(time, direction, out pos, out rot, out scale);
		if (mCalculator == null)
		{
			mCalculator = ParabolicCalculator.Create(this);
		}
		UpdateDirection(direction);
		pos = mCalculator.CalcPos(time);
		Vector3? prePos = null;
		Vector3? nextPos = null;
		if (time >= 0.0166667f)
		{
			prePos = mCalculator.CalcPos(time - 0.0166667f);
		}
		if (time <= mDuration - 0.0166667f)
		{
			nextPos = mCalculator.CalcPos(time + 0.0166667f);
		}
		CalcRotAndScale(prePos, pos, nextPos, out rot, out scale);
	}

	private void UpdateDirection(Vector3 direction)
	{
		if (!(Vector3.Distance(mInitDirection, direction) < 1E-05f))
		{
			mInitDirection = direction;
			mUnitDirection = direction.normalized * mDuration;
			mQuaternion = Quaternion.FromToRotation(direction, Vector3.up);
		}
	}

	private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
	{
		Vector3 val = point - pivot;
		val = rotation * val;
		point = val + pivot;
		return point;
	}

	private void OnDestroy()
	{
		if (mCalculator != null)
		{
			mCalculator.Release();
		}
	}
}

}
