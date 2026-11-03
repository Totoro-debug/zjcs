// Complete selected official client source; see skill-view runtime receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
[RequireComponent(typeof(MyGridTransform), typeof(MyTransform))]
public class MyFightSkill : MyComponent<FightSkillComponent, FightSkillComponentInfo>, IFightGetUsePlayable
{
	[Tooltip("是否优先使用位置作为播放终点")]
	public bool PreferredPosToPlayable = true;

	[Tooltip("释放者对技能目标的播放效果")]
	public GameObject myPlayablePrefab;

	[Tooltip("技能上方向playable")]
	public GameObject UpPlayablePrefab;

	[Tooltip("技能下方向playable")]
	public GameObject DownPlayablePrefab;

	[Tooltip("技能左方向playable")]
	public GameObject LeftPlayablePrefab;

	[Tooltip("技能右方向playable")]
	public GameObject RightPlayablePrefab;

	[Tooltip("显示技能重播")]
	public bool ShowSkillReplay = true;

	private GameObject mPlayableTempTargetGo;

	private MyPlayable mPlayable;

	private Dictionary<Direction, GameObject> mDirctionPlayablePrefab = new Dictionary<Direction, GameObject>();

	public MyEntity Source => ViewServices.GetEntity(Component.SrcEntityId);

	public MyEntity Target => ViewServices.GetEntity(Component.TargetId);

	public IMyPlayableManager myPlayableManager { get; set; }

	// Explicit original entity lookup and root-grid query boundary.
	public IFightSkillViewServices ViewServices { get; set; }

	protected override void Awake()
	{
		base.Awake();
		mDirctionPlayablePrefab.Add(Direction.Up, UpPlayablePrefab);
		mDirctionPlayablePrefab.Add(Direction.Down, DownPlayablePrefab);
		mDirctionPlayablePrefab.Add(Direction.Left, LeftPlayablePrefab);
		mDirctionPlayablePrefab.Add(Direction.Right, RightPlayablePrefab);
	}

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		if (!((Object)(object)Source == (Object)null))
		{
			DoSourceTurn();
			if (!Component.Interrupted)
			{
				Component.OnInterruped += new Action<FightSkillComponent>(OnSkillInterruped);
				PlayPlayable();
			}
			if (Component.SkillSlot >= 0)
			{
				((Component)Source).GetComponent<MyFightSkillAgent>().SendSkillAppliedEvent(this);
			}
		}
	}

	public override void OnRemove(object userData)
	{
		if (Component != null)
		{
			Component.OnInterruped -= new Action<FightSkillComponent>(OnSkillInterruped);
		}
		if (Component.SkillSlot >= 0 && (Object)(object)Source != (Object)null)
		{
			((Component)Source).GetComponent<MyFightSkillAgent>().SendSkillEndedEvent(this);
		}
		base.OnRemove(userData);
	}

	protected override void OnDestroy()
	{
		if ((Object)(object)mPlayableTempTargetGo != (Object)null)
		{
			Object.Destroy((Object)(object)mPlayableTempTargetGo);
			mPlayableTempTargetGo = null;
		}
		base.OnDestroy();
	}

	private void OnSkillInterruped(FightSkillComponent skill)
	{
		if ((Object)(object)mPlayable != (Object)null)
		{
			mPlayable.Stop();
		}
	}

	private void DoSourceTurn()
	{
		if (Info.FlipSource)
		{
			Source.myGridTransform.ResetScale();
		}
	}

	private GameObject GetPlayablePrefab()
	{
		GameObject val = null;
		if ((Object)(object)myGridTransform != (Object)null)
		{
			val = mDirctionPlayablePrefab[myGridTransform.Direction];
		}
		if ((Object)(object)val == (Object)null)
		{
			val = myPlayablePrefab;
		}
		return val;
	}

	private void PlayPlayable()
	{
		GameObject playablePrefab = GetPlayablePrefab();
		if ((Object)(object)playablePrefab != (Object)null)
		{
			Dictionary<string, object> param = new Dictionary<string, object> {
			{
				"Skill",
				((Component)this).gameObject
			} };
			mPlayable = myPlayableManager.Play(playablePrefab, ((Component)Source).gameObject, GetPlayableTargetGo(), 1f, null, param);
		}
	}

	public GameObject GetPlayableTargetGo()
	{
		if (!PreferredPosToPlayable && Info.IsEntityTargetType() && (Object)(object)Target != (Object)null)
		{
			return ((Component)Target).gameObject;
		}
		if ((Object)(object)mPlayableTempTargetGo == (Object)null)
		{
			mPlayableTempTargetGo = new GameObject($"skill_{Component.Entity.ClassId}_target");
		}
		mPlayableTempTargetGo.transform.SetParent(((Component)this).transform.parent);
		mPlayableTempTargetGo.transform.localPosition = ViewServices.GridToPos(Component.TargetPos);
		return mPlayableTempTargetGo;
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)myPlayablePrefab != (Object)null)
			{
				playables.Add(myPlayablePrefab);
			}
			if ((Object)(object)UpPlayablePrefab != (Object)null)
			{
				playables.Add(UpPlayablePrefab);
			}
			if ((Object)(object)DownPlayablePrefab != (Object)null)
			{
				playables.Add(DownPlayablePrefab);
			}
			if ((Object)(object)LeftPlayablePrefab != (Object)null)
			{
				playables.Add(LeftPlayablePrefab);
			}
			if ((Object)(object)RightPlayablePrefab != (Object)null)
			{
				playables.Add(RightPlayablePrefab);
			}
		}
	}

	public void UpdateDirectionPlyables()
	{
		mDirctionPlayablePrefab.Clear();
		mDirctionPlayablePrefab.Add(Direction.Up, UpPlayablePrefab);
		mDirctionPlayablePrefab.Add(Direction.Down, DownPlayablePrefab);
		mDirctionPlayablePrefab.Add(Direction.Left, LeftPlayablePrefab);
		mDirctionPlayablePrefab.Add(Direction.Right, RightPlayablePrefab);
	}
}

}
