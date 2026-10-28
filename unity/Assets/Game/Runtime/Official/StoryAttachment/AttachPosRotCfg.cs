// Migrated official application source; see attachment transform source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryAttachment
{
[Serializable]
public class AttachPosRotCfg
{
	public EAttachTo AttachTo;

	public string myBoneName;

	public Vector3 myOffset;

	public Quaternion myRotation = Quaternion.identity;

	public float myScale = 1f;

	public Vector3 ScaleV3
	{
		get
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return Vector3.one * myScale;
		}
	}

	public bool Attach(MyPlayable playable, Transform ts)
	{
		GameObject attachToGo = AttachPosCfg.GetAttachToGo(playable, AttachTo);
		return Attach(attachToGo, ts);
	}

	public bool CalcTransform(GameObject target, Transform follower, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		Transform bone = GetBone(target, myBoneName);
		if ((Object)(object)bone == (Object)null)
		{
			pos = Vector3.zero;
			rot = Quaternion.identity;
			scale = Vector3.one;
			return false;
		}
		pos = bone.TransformPoint(myOffset);
		rot = bone.rotation * myRotation;
		scale = bone.lossyScale * myScale;
		return true;
	}

	public Transform GetBone(MyPlayable playable)
	{
		return GetBone(AttachPosCfg.GetAttachToGo(playable, AttachTo), myBoneName);
	}

	public static Transform GetBone(GameObject go, string boneName)
	{
		if ((Object)(object)go == (Object)null)
		{
			return null;
		}
		Transform result = go.transform;
		AttachAgent attachAgent = default;
		if (!string.IsNullOrEmpty(boneName) && go.TryGetComponent<AttachAgent>(out attachAgent))
		{
			Transform bone = attachAgent.GetBone(boneName);
			if ((Object)(object)bone != (Object)null)
			{
				result = bone;
			}
		}
		return result;
	}

	private bool Attach(GameObject go, Transform ts)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		Transform bone = GetBone(go, myBoneName);
		if ((Object)(object)bone == (Object)null)
		{
			return false;
		}
		ts.SetParent(bone);
		ts.SetLocalPositionAndRotation(myOffset, myRotation);
		ts.localScale = ScaleV3;
		return true;
	}

	public AttachPosRotCfg()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
	}
}

}
