// Complete current original application source; see track runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.EntityLifecycle;
using UnityEngine;

namespace Zjcs.Official.BattleTrackPlayables
{
public class AnimationTrack : MyTrack
{
	public const int FrameRate = 60;

	public Vector3[] myPosList;

	public Quaternion[] myRotationList;

	public Vector3[] myScaleList;

	public int myFrameCount;

	public override float GetDuration()
	{
		return (float)(myPosList.Length - 1) / 60f;
	}

	public override void Evaluate(float time, Vector3 direction, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		base.Evaluate(time, direction, out pos, out rot, out scale);
		float num = time * 60f;
		if (num < 0f || num > (float)myFrameCount)
		{
			if (myLoopType == LoopType.Loop)
			{
				num %= (float)myFrameCount;
			}
			else if (myLoopType == LoopType.Pingpong)
			{
				int num2 = (int)(num / (float)myFrameCount);
				num %= (float)myFrameCount;
				if (num2 % 2 == 1)
				{
					num = (float)myFrameCount - num;
				}
			}
		}
		num = Mathf.Max(0f, num);
		int num3 = (int)num;
		float num4 = num - (float)num3;
		if (myPosList == null || myPosList.Length == 0)
		{
			pos = Vector3.zero;
		}
		else if (num3 >= myFrameCount - 1)
		{
			pos = myPosList[myFrameCount - 1];
		}
		else
		{
			pos = Vector3.Lerp(myPosList[num3], myPosList[num3 + 1], num4);
		}
		if (myRotationList == null || myRotationList.Length == 0)
		{
			if (myPosList == null || myPosList.Length == 0)
			{
				rot = Quaternion.identity;
			}
			else
			{
				Vector3 forward = Vector3.forward;
				if (num3 > 0 && num3 < myPosList.Length - 1)
				{
					forward = myPosList[num3 + 1] - myPosList[num3 - 1];
				}
				else
				{
					forward = ((num3 != 0) ? (myPosList[myPosList.Length - 1] - myPosList[myPosList.Length - 2]) : (myPosList[1] - myPosList[0]));
				}
				rot = Quaternion.LookRotation(forward);
			}
		}
		else if (num3 >= myFrameCount - 1)
		{
			rot = myRotationList[myFrameCount - 1];
		}
		else
		{
			rot = Quaternion.Lerp(myRotationList[num3], myRotationList[num3 + 1], num4);
		}
		if (myScaleList == null || myScaleList.Length == 0)
		{
			scale = Vector3.one;
		}
		else if (num3 >= myFrameCount - 1)
		{
			scale = myScaleList[myFrameCount - 1];
		}
		else
		{
			scale = Vector3.Lerp(myScaleList[num3], myScaleList[num3 + 1], num4);
		}
	}
}

}
