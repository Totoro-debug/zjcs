// Complete current original application source; see track runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.EntityLifecycle;
using UnityEngine;

namespace Zjcs.Official.BattleTrackPlayables
{
public abstract class MyTrack : MonoBehaviour
{
	public enum LoopType
	{
		Once,
		Loop,
		Pingpong
	}

	public LoopType myLoopType;

	public abstract float GetDuration();

	public void Evaluate(float time, Vector3 startPos, Vector3 endPos, out Vector3 pos, out Quaternion rot, out Vector3 scale, float xRangeScale = 1f, float xExtraScale = 1f)
	{
		Vector3 val = endPos - startPos;
		val.z = 0f;
		Evaluate(time, val, out pos, out rot, out scale);
		float magnitude = val.magnitude;
		float num = (1f + (magnitude - 1f) * xRangeScale) * xExtraScale;
		Vector3 val2 = new Vector3(num, magnitude, 1f);
		Quaternion val3 = ((!Mathf.Approximately(val.sqrMagnitude, 0f)) ? Quaternion.FromToRotation(Vector3.up, val) : Quaternion.identity);
		pos = startPos + val3 * Vector3.Scale(pos, val2);
		Vector3 val4 = Vector3.Scale(rot * Vector3.up, val2);
		rot = val3 * Quaternion.FromToRotation(Vector3.up, val4);
	}

	public virtual void Evaluate(float time, Vector3 direction, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		pos = Vector3.zero;
		rot = Quaternion.identity;
		scale = Vector3.one;
	}

	protected void CalcRotAndScale(Vector3? prePos, Vector3 pos, Vector3? nextPos, out Quaternion rot, out Vector3 scale)
	{
		if (nextPos.HasValue && prePos.HasValue)
		{
			rot = Quaternion.LookRotation(Vector3.forward, nextPos.Value - prePos.Value);
		}
		else if (nextPos.HasValue)
		{
			rot = Quaternion.LookRotation(Vector3.forward, nextPos.Value - pos);
		}
		else if (prePos.HasValue)
		{
			rot = Quaternion.LookRotation(Vector3.forward, pos - prePos.Value);
		}
		else
		{
			rot = Quaternion.identity;
		}
		scale = Vector3.one;
	}
}

}
