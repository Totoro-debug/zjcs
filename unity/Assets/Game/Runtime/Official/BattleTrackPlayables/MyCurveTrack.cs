// Complete current original application source; see track runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.EntityLifecycle;
using UnityEngine;

namespace Zjcs.Official.BattleTrackPlayables
{
public class MyCurveTrack : MyTrack
{
	[Tooltip("轨迹时长")]
	public float myDuration = 1f;

	[Tooltip("轨迹在水平方向上的偏移")]
	public AnimationCurve myCurveX = new AnimationCurve(new Keyframe[2]
	{
		new Keyframe(0f, 0f),
		new Keyframe(1f, 0f)
	});

	[Tooltip("轨迹在垂直方向上的偏移")]
	public AnimationCurve myCurveY = new AnimationCurve(new Keyframe[2]
	{
		new Keyframe(0f, 0f),
		new Keyframe(1f, 0f)
	});

	public override float GetDuration()
	{
		return myDuration;
	}

	public override void Evaluate(float time, Vector3 direction, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		base.Evaluate(time, direction, out pos, out rot, out scale);
		pos = CalcPos(time);
		Vector3? prePos = null;
		Vector3? nextPos = null;
		if (time >= 0.0166667f)
		{
			prePos = CalcPos(time - 0.0166667f);
		}
		if (time <= myDuration - 0.0166667f)
		{
			nextPos = CalcPos(time + 0.0166667f);
		}
		CalcRotAndScale(prePos, pos, nextPos, out rot, out scale);
	}

	private Vector3 CalcPos(float time)
	{
		float num = myCurveX.Evaluate(time);
		float num2 = myCurveY.Evaluate(time);
		return new Vector3(num, num2, 0f);
	}

	public MyCurveTrack()
	{
	}
}

}
