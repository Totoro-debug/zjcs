// Migrated official status application source; see status view runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Zjcs.Official.BattleStatusPlayables
{
public class MyPlayableOrbitMovement : MyPlayable, IChangePlayableStatusStackCount
{
	[Serializable]
	public struct OrbiterInfo
	{
		[Tooltip("等级下限")]
		public int LevelLowerLimit;

		[Tooltip("等级上限")]
		public int LevelUpperLimit;

		[Tooltip("轨道飞行特效个数")]
		public int OrbiterCount;

		[Tooltip("轨道飞行特效")]
		public GameObject Orbiter;
	}

	[Header("轨道的基本配置")]
	[Tooltip("绕哪个轴旋转，这里默认是绕y轴")]
	public Vector3 Axis = Vector3.up;

	[Tooltip("环绕的目标")]
	public AttachPosCfg AttachCfg;

	[Range(0.1f, 1f)]
	[Tooltip("目标和环绕特效之间的距离")]
	public float OrbitDistance = 0.5f;

	[Tooltip("每秒环绕的角度")]
	[Range(1f, 360f)]
	public float OrbitDegreesPerSec = 180f;

	[Tooltip("轨道特效物体的信息配置")]
	public OrbiterInfo[] OrbiterInfos;

	[Tooltip("默认轨道飞行等级，默认1级")]
	[Range(1f, 10f)]
	public int InitOrbitLevel = 1;

	[Tooltip("特效是否是否面对环绕目标")]
	public bool IsLookAt;

	private Transform mTarget;

	private int mOrbitLevel = -1;

	private int mOrbitInfoIndex = -1;

	private List<Transform> mOrbiters = new List<Transform>();

	private float mAngleBetweenOrbiters;

	private Dictionary<GameObject, ObjectPool<GameObject>> mOrbiterToPoolDict = new Dictionary<GameObject, ObjectPool<GameObject>>();

	private int mCurrentStackOrbitLevel;

	private new void Awake()
	{
		InitOrbiterPool();
	}

	protected override void OnDestroy()
	{
		ClearOrbiterPool();
		base.OnDestroy();
	}

	protected override void InitImpl()
	{
		base.InitImpl();
		mCurrentStackOrbitLevel = InitOrbitLevel;
	}

	protected override void PlayImpl()
	{
		base.PlayImpl();
		mTarget = AttachCfg.GetAttachToBone(this).transform;
		SetOrbitLevel(mCurrentStackOrbitLevel);
		RefreshAngleBetweenOrbiters();
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		bool result = base.ProcessFrameImpl(deltaTime);
		int i = 0;
		for (int count = mOrbiters.Count; i < count; i++)
		{
			Transform val = mOrbiters[i];
			Vector3 val2 = Quaternion.AngleAxis((mAngleBetweenOrbiters * (float)i + Time * OrbitDegreesPerSec) % 360f, Axis) * Vector3.left * OrbitDistance;
			Vector3 position = mTarget.position + val2;
			val.position = position;
			if (IsLookAt)
			{
				val.LookAt(mTarget);
			}
		}
		return result;
	}

	protected override bool PostProcessFrameImpl(float deltaTime)
	{
		return base.PostProcessFrameImpl(deltaTime);
	}

	protected override void StopImpl()
	{
		if (mOrbiters.Count != 0)
		{
			if (TryGetCurrentOrbiterInfo(out var info))
			{
				ObjectPool<GameObject> val = mOrbiterToPoolDict[info.Orbiter];
				foreach (Transform mOrbiter in mOrbiters)
				{
					val.Release(((Component)mOrbiter).gameObject);
				}
			}
			mOrbiters.Clear();
		}
		base.StopImpl();
	}

	public override float GetDuration()
	{
		return -1f;
	}

	// Local pool adaptation: a parent sequence can reset children before their
	// deferred stop is processed. Return live orbiters while their old pool index
	// is valid, then restore selection caches for an unchanged-stack replay.
	public override void ResetData()
	{
		if (mOrbiters.Count != 0) StopImpl();
		mOrbitLevel = -1;
		mOrbitInfoIndex = -1;
		mAngleBetweenOrbiters = 0f;
		mTarget = null;
		base.ResetData();
	}

	public void SetOrbitLevel(int orbitLevel)
	{
		if (mOrbitLevel == orbitLevel)
		{
			return;
		}
		if (!TryGetOrbitInfoIndex(orbitLevel, out var index))
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		else
		{
			mOrbitLevel = orbitLevel;
			if (mOrbitInfoIndex != index)
			{
				int preOrbitInfoIndex = mOrbitInfoIndex;
				mOrbitInfoIndex = index;
				RefreshOrbiters(preOrbitInfoIndex, index);
			}
		}
	}

	private bool TryGetCurrentOrbiterInfo(out OrbiterInfo info)
	{
		info = default;
		if (mOrbitInfoIndex < 0)
		{
			return false;
		}
		int num = OrbiterInfos.Length;
		if (mOrbitInfoIndex >= num)
		{
			return false;
		}
		info = OrbiterInfos[mOrbitInfoIndex];
		return true;
	}

	private bool TryGetOrbitInfoIndex(int orbitLevel, out int index)
	{
		index = 0;
		int i = 0;
		for (int num = OrbiterInfos.Length; i < num; i++)
		{
			OrbiterInfo orbiterInfo = OrbiterInfos[i];
			if (orbiterInfo.LevelLowerLimit <= orbitLevel && orbitLevel <= orbiterInfo.LevelUpperLimit)
			{
				index = i;
				return true;
			}
		}
		return false;
	}

	private void RefreshOrbiters(int preOrbitInfoIndex, int orbitInfoIndex)
	{
		GameObject val = null;
		int num = OrbiterInfos.Length;
		if (0 <= preOrbitInfoIndex && preOrbitInfoIndex < num)
		{
			val = OrbiterInfos[preOrbitInfoIndex].Orbiter;
		}
		OrbiterInfo info = OrbiterInfos[orbitInfoIndex];
		GameObject orbiter = info.Orbiter;
		if ((Object)(object)val != (Object)(object)orbiter)
		{
			ChangeOrbiters(info, val, orbiter);
		}
		else
		{
			AlignOrbiters(info);
		}
		RefreshAngleBetweenOrbiters();
	}

	private void ChangeOrbiters(OrbiterInfo info, GameObject preOrbiter, GameObject orbiter)
	{
		if ((Object)(object)preOrbiter != (Object)null)
		{
			ObjectPool<GameObject> val = mOrbiterToPoolDict[preOrbiter];
			foreach (Transform mOrbiter in mOrbiters)
			{
				val.Release(((Component)mOrbiter).gameObject);
			}
			mOrbiters.Clear();
		}
		if ((Object)(object)orbiter != (Object)null)
		{
			int orbiterCount = info.OrbiterCount;
			ObjectPool<GameObject> val2 = mOrbiterToPoolDict[orbiter];
			for (int i = 0; i < orbiterCount; i++)
			{
				mOrbiters.Add(val2.Get().transform);
			}
		}
	}

	private void AlignOrbiters(OrbiterInfo info)
	{
		GameObject orbiter = info.Orbiter;
		int count = mOrbiters.Count;
		int orbiterCount = info.OrbiterCount;
		if (count < orbiterCount)
		{
			int num = orbiterCount - count;
			ObjectPool<GameObject> val = mOrbiterToPoolDict[orbiter];
			for (int i = 0; i < num; i++)
			{
				mOrbiters.Add(val.Get().transform);
			}
		}
		else if (count > orbiterCount)
		{
			int num2 = count - orbiterCount;
			ObjectPool<GameObject> val2 = mOrbiterToPoolDict[orbiter];
			int count2 = mOrbiters.Count;
			for (int j = 0; j < num2; j++)
			{
				int index = count2 - 1 - j;
				val2.Release(((Component)mOrbiters[index]).gameObject);
			}
			if (num2 > 0)
			{
				mOrbiters.RemoveRange(count2 - num2, num2);
			}
		}
	}

	private void RefreshAngleBetweenOrbiters()
	{
		mAngleBetweenOrbiters = 360f / (float)mOrbiters.Count;
	}

	private void InitOrbiterPool()
	{
		OrbiterInfo[] orbiterInfos = OrbiterInfos;
		for (int i = 0; i < orbiterInfos.Length; i++)
		{
			OrbiterInfo orbiterInfo = orbiterInfos[i];
			GameObject orbiter = orbiterInfo.Orbiter;
			if (!mOrbiterToPoolDict.TryGetValue(orbiter, out var value))
			{
				value = new ObjectPool<GameObject>((Func<GameObject>)(() => StoryPlayableInstantiation.Instantiate(((Component)this).transform, orbiter, ((Component)this).transform, false)), (Action<GameObject>)((GameObject go) =>
				{
					go.SetActive(true);
				}), (Action<GameObject>)((GameObject go) =>
				{
					go.SetActive(false);
				}), (Action<GameObject>)null, true, 1, 10);
				mOrbiterToPoolDict.Add(orbiter, value);
			}
		}
	}

	private void ClearOrbiterPool()
	{
		foreach (KeyValuePair<GameObject, ObjectPool<GameObject>> item in mOrbiterToPoolDict)
		{
			item.Value.Clear();
		}
		mOrbiterToPoolDict.Clear();
	}

	public void SetPlayableStatusStackCount(int count)
	{
		mCurrentStackOrbitLevel = count;
		SetOrbitLevel(mCurrentStackOrbitLevel);
	}

	public MyPlayableOrbitMovement()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
	}
}

}
