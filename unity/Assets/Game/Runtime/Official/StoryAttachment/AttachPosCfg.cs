// Migrated official application source; see attachment transform source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryAttachment
{
[Serializable]
public class AttachPosCfg
{
	[Tooltip("附加类型")]
	public EAttachTo AttachTo;

	[Tooltip("附加目标的骨骼名字")]
	public string myBoneName;

	[Tooltip("相对结束点中心的位置偏移")]
	public Vector3 myOffset;

	[Tooltip("是否使用source-target的连线方向作为z轴正方向")]
	public bool myLinkDir;

	[Tooltip("myOffset是否受到挂接骨骼的本地缩放的影响")]
	public bool myOffsetScale;

	[Tooltip("是否根据目标朝向作为z轴正方向,与myLinkDir互斥")]
	public bool IsRotationTowardsTarget;

	private bool mIsSource2Target = true;

	public bool IsSource2Target()
	{
		bool result = true;
		switch (AttachTo)
		{
		case EAttachTo.Source:
			result = true;
			break;
		case EAttachTo.Target:
			result = false;
			break;
		case EAttachTo.Skill:
			result = true;
			break;
		case EAttachTo.Damage:
			result = false;
			break;
		}
		return result;
	}

	public AttachPosCfg()
	{
	}

	public AttachPosCfg(EAttachTo attachTo)
	{
		AttachTo = attachTo;
		mIsSource2Target = IsSource2Target();
	}

	private Direction GetPlayableParamSource2TargetDirection(MyPlayable playable)
	{
		Direction result = Direction.Up;
		object obj = playable.TryGetParam("ParamSource2TargetDirection");
		if (obj == null)
		{
			return result;
		}
		return (Direction)obj;
	}

	private GameObject GetLinkDirTarget(MyPlayable playable)
	{
		switch (AttachTo)
		{
		case EAttachTo.Source:
			if (!myLinkDir)
			{
				return null;
			}
			return playable.myTarget;
		case EAttachTo.Target:
			if (!myLinkDir)
			{
				return null;
			}
			return playable.mySource;
		case EAttachTo.Skill:
			if (!myLinkDir)
			{
				return null;
			}
			return playable.myTarget;
		case EAttachTo.Damage:
			if (!myLinkDir)
			{
				return null;
			}
			return playable.mySource;
		default:
			return null;
		}
	}

	public static GameObject GetAttachToGo(MyPlayable playable, EAttachTo attachTo)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected Obj, but got Unknown
		return attachTo switch
		{
			EAttachTo.Source => playable.mySource, 
			EAttachTo.Target => playable.myTarget, 
			EAttachTo.Skill => (GameObject)playable.TryGetParam("Skill"), 
			EAttachTo.Damage => (GameObject)playable.TryGetParam("Damage"), 
			_ => (GameObject)null, 
		};
	}

	public GameObject GetAttachToBone(MyPlayable playable)
	{
		GameObject attachToGo = GetAttachToGo(playable, AttachTo);
		if ((Object)(object)attachToGo == (Object)null)
		{
			return null;
		}
		GameObject val = null;
		if (string.IsNullOrEmpty(myBoneName))
		{
			val = attachToGo;
		}
		else
		{
			AttachAgent attachAgent = default;
			if (attachToGo.TryGetComponent<AttachAgent>(out attachAgent))
			{
				Transform bone = attachAgent.GetBone(myBoneName);
				if ((Object)(object)bone != (Object)null)
				{
					val = ((Component)bone).gameObject;
				}
			}
			if ((Object)(object)val == (Object)null)
			{
				val = attachToGo;
			}
		}
		return val;
	}

	public Vector3 CalcPos(MyPlayable playable)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		GameObject attachToBone = GetAttachToBone(playable);
		GameObject linkDirTarget = GetLinkDirTarget(playable);
		Direction playableParamSource2TargetDirection = GetPlayableParamSource2TargetDirection(playable);
		return CalcPos(attachToBone, linkDirTarget, playableParamSource2TargetDirection);
	}

	public Vector3 CalcPos(GameObject go, GameObject target = null, Direction direction = Direction.Up)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)go == (Object)null)
		{
			return Vector3.zero;
		}
		Transform transform = go.transform;
		Quaternion rotation = GetRotation(transform, ((Object)(object)target == (Object)null) ? null : target.transform, direction);
		Vector3 val = (myOffsetScale ? transform.lossyScale : Vector3.one);
		return transform.position + rotation * Vector3.Scale(myOffset, val);
	}

	public Quaternion GetRotation(Transform source, Transform target, Direction direction)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)target != (Object)null)
		{
			Vector3 position = target.position;
			if (position.Equals(source.position))
			{
				return GetRotationByDirection(direction);
			}
			Vector3 one = Vector3.one;
			Vector3 val = target.position - source.position;
			one.x = val.x;
			one.y = val.y;
			return Quaternion.LookRotation(Vector3.forward, one);
		}
		if (IsRotationTowardsTarget)
		{
			return GetRotationByDirection(direction);
		}
		return source.rotation;
	}

	private Quaternion GetRotationByDirection(Direction direction)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Vector3.one;
		switch (direction)
		{
		case Direction.Up:
			val = (mIsSource2Target ? Vector3.up : Vector3.down);
			break;
		case Direction.Down:
			val = (mIsSource2Target ? Vector3.down : Vector3.up);
			break;
		case Direction.Left:
			val = (mIsSource2Target ? Vector3.left : Vector3.right);
			break;
		case Direction.Right:
			val = (mIsSource2Target ? Vector3.right : Vector3.left);
			break;
		}
		return Quaternion.LookRotation(Vector3.forward, val);
	}

	public bool TryCalcPos(MyPlayable playable, out Vector3 pos)
	{
		GameObject attachToBone = GetAttachToBone(playable);
		GameObject linkDirTarget = GetLinkDirTarget(playable);
		Direction playableParamSource2TargetDirection = GetPlayableParamSource2TargetDirection(playable);
		return TryCalcPos(attachToBone, linkDirTarget, out pos, playableParamSource2TargetDirection);
	}

	public bool TryCalcPos(GameObject go, GameObject target, out Vector3 pos, Direction direction)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)go == (Object)null)
		{
			pos = Vector3.zero;
			return false;
		}
		pos = CalcPos(go, target, direction);
		return true;
	}
}

}
