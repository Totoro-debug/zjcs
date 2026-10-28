// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using UnityEngine;

namespace Zjcs.Official.StoryFollowBone
{
public static class TransformUtils
{
	public static void CalcTransform(Transform target, Transform follower, Vector3 extraScale, Quaternion extraRotation, Vector3 extraPosition, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		pos = Vector3.zero;
		rot = Quaternion.identity;
		scale = Vector3.one;
		if (!((Object)(object)target == (Object)null) && !((Object)(object)follower == (Object)null))
		{
			Matrix4x4 val = target.localToWorldMatrix * Matrix4x4.TRS(extraPosition, extraRotation, extraScale);
			Matrix4x4 val2 = val;
			if ((Object)(object)follower.parent != (Object)null)
			{
				val2 = follower.parent.worldToLocalMatrix * val;
			}
			pos = val2.GetPosition();
			scale = val2.lossyScale;
			rot = val2.rotation;
		}
	}

	public static void SyncTransform(Transform from, Transform to, TransformSyncType syncType)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		if (syncType != TransformSyncType.None)
		{
			Matrix4x4 localToWorldMatrix = from.localToWorldMatrix;
			Matrix4x4 val = localToWorldMatrix;
			Vector3 lossyScale = val.lossyScale;
			if ((Object)(object)to.parent != (Object)null)
			{
				val = to.parent.worldToLocalMatrix * localToWorldMatrix;
				lossyScale = val.lossyScale;
			}
			if (lossyScale.x < 0f)
			{
				val.m00 /= lossyScale.x;
				val.m10 /= lossyScale.x;
				val.m20 /= lossyScale.x;
			}
			if (lossyScale.y < 0f)
			{
				val.m01 /= lossyScale.y;
				val.m11 /= lossyScale.y;
				val.m12 /= lossyScale.y;
			}
			if (lossyScale.z < 0f)
			{
				val.m02 /= lossyScale.z;
				val.m12 /= lossyScale.z;
				val.m22 /= lossyScale.z;
			}
			if ((syncType & TransformSyncType.Position) != 0)
			{
				to.localPosition = val.GetPosition();
			}
			if ((syncType & TransformSyncType.Scale) != 0)
			{
				to.localScale = lossyScale;
			}
			if ((syncType & TransformSyncType.Rotation) != 0)
			{
				to.localRotation = val.rotation;
			}
			if (localToWorldMatrix != to.localToWorldMatrix)
			{
				Debug.LogWarning((object)"SyncTransform Error!");
			}
		}
	}

	public static void SyncTransform(Transform from, Transform to, TransformSyncType syncType, Vector3 extraScale, Quaternion extraRotation, Vector3 extraPosition)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		if (syncType != TransformSyncType.None)
		{
			Matrix4x4 val = from.localToWorldMatrix * Matrix4x4.TRS(extraPosition, extraRotation, extraScale);
			Matrix4x4 val2 = val;
			Vector3 lossyScale = val2.lossyScale;
			if ((Object)(object)to.parent != (Object)null)
			{
				val2 = to.parent.worldToLocalMatrix * val;
				lossyScale = val2.lossyScale;
			}
			if (lossyScale.x < 0f)
			{
				val2.m00 /= lossyScale.x;
				val2.m10 /= lossyScale.x;
				val2.m20 /= lossyScale.x;
			}
			if (lossyScale.y < 0f)
			{
				val2.m01 /= lossyScale.y;
				val2.m11 /= lossyScale.y;
				val2.m12 /= lossyScale.y;
			}
			if (lossyScale.z < 0f)
			{
				val2.m02 /= lossyScale.z;
				val2.m12 /= lossyScale.z;
				val2.m22 /= lossyScale.z;
			}
			if ((syncType & TransformSyncType.Position) != 0)
			{
				to.localPosition = val2.GetPosition();
			}
			if ((syncType & TransformSyncType.Scale) != 0)
			{
				to.localScale = lossyScale;
			}
			if ((syncType & TransformSyncType.Rotation) != 0)
			{
				to.localRotation = val2.rotation;
			}
			if (val != to.localToWorldMatrix)
			{
				Debug.LogWarning((object)"SyncTransform Error!");
			}
		}
	}

	public static void SyncTransformAccurately(Transform from, Transform to, TransformSyncType syncType)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		if (syncType == TransformSyncType.None)
		{
			return;
		}
		Quaternion val = Quaternion.identity;
		Quaternion val2 = Quaternion.identity;
		if ((syncType & TransformSyncType.Position) != 0)
		{
			Vector3 position = from.position;
			to.position = position;
		}
		if ((syncType & (TransformSyncType.Scale | TransformSyncType.Rotation)) != 0)
		{
			val = from.rotation;
			if ((syncType & TransformSyncType.Rotation) != 0)
			{
				to.rotation = val;
				if ((syncType & TransformSyncType.Scale) != 0)
				{
					val2 = to.localRotation;
				}
			}
			else
			{
				Transform parent = to.parent;
				val2 = ((!((Object)(object)parent != (Object)null)) ? val : (Quaternion.Inverse(parent.rotation) * val));
			}
		}
		if ((syncType & TransformSyncType.Scale) != 0)
		{
			Matrix4x4 val3 = CalculateGlobalSM(from, val);
			Vector3 localScale = new Vector3(val3.m00, val3.m11, val3.m22);
			if ((Object)(object)to.parent != (Object)null)
			{
				Matrix4x4 val4 = CalculateGlobalRS(to.parent);
				Matrix4x4 val5 = Matrix4x4.Rotate(Quaternion.Inverse(val));
				Matrix4x4 val6 = Matrix4x4.Rotate(val2);
				Matrix4x4 val7 = val5 * val4 * val6;
				localScale.x /= val7.m00;
				localScale.y /= val7.m11;
				localScale.z /= val7.m22;
			}
			to.localScale = localScale;
		}
	}

	public static void SyncTransformAccurately(Transform from, Transform to, TransformSyncType syncType, Vector3 extraPosition, Quaternion extraRotation, Vector3 extraScale)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		if (syncType == TransformSyncType.None)
		{
			return;
		}
		bool flag = (syncType & TransformSyncType.Position) != 0;
		bool flag2 = (syncType & TransformSyncType.Rotation) != 0;
		bool flag3 = (syncType & TransformSyncType.Scale) != 0;
		bool num = flag2 | flag3;
		Quaternion val = Quaternion.identity;
		Quaternion val2 = Quaternion.identity;
		if (flag)
		{
			to.position = from.TransformPoint(extraPosition);
		}
		if (num)
		{
			val = from.rotation * ScaleMulQuat(from.localScale, extraRotation);
			if (flag2)
			{
				to.rotation = val;
				if (flag3)
				{
					val2 = to.localRotation;
				}
			}
			else
			{
				Transform parent = to.parent;
				val2 = ((!((Object)(object)parent != (Object)null)) ? val : (Quaternion.Inverse(parent.rotation) * val));
			}
		}
		if (flag3)
		{
			Matrix4x4 val3 = CalculateGlobalSM(from, val, extraRotation, extraScale);
			Vector3 localScale = new Vector3(val3.m00, val3.m11, val3.m22);
			Transform parent2 = to.parent;
			if ((Object)(object)parent2 != (Object)null)
			{
				Matrix4x4 val4 = CalculateGlobalRS(parent2);
				Matrix4x4 val5 = Matrix4x4.Rotate(Quaternion.Inverse(val));
				Matrix4x4 val6 = Matrix4x4.Rotate(val2);
				Matrix4x4 val7 = val5 * val4 * val6;
				localScale.x /= val7.m00;
				localScale.y /= val7.m11;
				localScale.z /= val7.m22;
			}
			to.localScale = localScale;
		}
	}

	private static Matrix4x4 CalculateGlobalSM(Transform from, Quaternion globalRotation)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		Matrix4x4 val = Matrix4x4.Rotate(Quaternion.Inverse(globalRotation));
		Matrix4x4 val2 = CalculateGlobalRS(from);
		return val * val2;
	}

	private static Matrix4x4 CalculateGlobalRS(Transform from)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 zero = Vector3.zero;
		Matrix4x4 val = Matrix4x4.TRS(zero, from.localRotation, from.localScale);
		Transform parent = from.parent;
		while ((Object)(object)parent != (Object)null)
		{
			val *= Matrix4x4.TRS(zero, parent.localRotation, parent.localScale);
			parent = parent.parent;
		}
		return val;
	}

	private static Matrix4x4 CalculateGlobalSM(Transform from, Quaternion globalRotation, Quaternion extraQuat, Vector3 extraScale)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		Matrix4x4 val = Matrix4x4.Rotate(Quaternion.Inverse(globalRotation));
		Matrix4x4 val2 = CalculateGlobalRS(from, extraQuat, extraScale);
		return val * val2;
	}

	private static Matrix4x4 CalculateGlobalRS(Transform from, Quaternion extraQuat, Vector3 extraScale)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		Vector3 zero = Vector3.zero;
		Matrix4x4 val = Matrix4x4.TRS(zero, from.localRotation, from.localScale) * Matrix4x4.TRS(zero, extraQuat, extraScale);
		Transform parent = from.parent;
		while ((Object)(object)parent != (Object)null)
		{
			val *= Matrix4x4.TRS(zero, parent.localRotation, parent.localScale);
			parent = parent.parent;
		}
		return val;
	}

	private static Quaternion ScaleMulQuat(Vector3 scale, Quaternion q)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		return new Quaternion(q.x * Mathf.Sign(scale.y * scale.z), q.y * Mathf.Sign(scale.x * scale.z), q.z * Mathf.Sign(scale.y * scale.z), q.w);
	}
}

}
