// Migrated official hit presentation; see hit-view-runtime source receipt.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
[RequireComponent(typeof(MyGridTransform), typeof(MyTransform))]
public class MyFightHitBase<T, TInfo> : MyComponent<T, TInfo>, IFightGetUsePlayable, IFightHitViewBinding where T : FightHitBaseComponent where TInfo : FightHitBaseComponentInfo
{
	[Tooltip("Hit自身位置格子的playable，source和target为hit本身")]
	public GameObject myPlayable;

	[Tooltip("Hit设置范围内所有格子的playable")]
	public GameObject myScopePlayable;

	[Tooltip("格子上的Playable是否跟随配置的hit延迟后再播放")]
	public bool ScopePlayableFollowDelay = true;

	[Tooltip("格子位置的Z值是否跟随Hit的Z值")]
	public bool ScopeZFollowHitZ;

	[Tooltip("从Source指向Hit自身位置格子的playable")]
	public GameObject SourceToMePlayable;

	[Tooltip("设置playable ParamSource2TargetDirection 给SourceToMePlayable")]
	public bool ShouldSetPlayableDirectinParamToSouceToMePlayable;

	[Tooltip("SourceToMePlayable 的起点：施法者/偏移；或与技能主 Playable 的 target 一致")]
	public EFightHitSourceToMeAnchor SourceToMeAnchor;

	[Tooltip("Playable表现上是否使用偏移")]
	public bool IsApplyOffsetToPlayable;

	private List<GameObject> mTmpObjs = new List<GameObject>();

	private Coroutine mCoro;

	private MyGridTransform mGridTransform;

	private IDisposable mOnHitPosChangeDisposable;

	public int DelayOverrideInfoIndex;

	public FightHitViewFormula.SkillHitBaseDelayInfoProvider DelayInfoProvider;
	FightHitViewFormula.SkillHitBaseDelayInfoProvider IFightHitViewBinding.DelayInfoProvider => DelayInfoProvider;

	private Dictionary<string, object> mMyPlayableParams;

	public IMyPlayableManager mMyPlayableManager { get; set; }
	public IFightHitViewServices ViewServices { get; set; }

	public override void OnStart(bool newEntity)
	{


		base.OnStart(newEntity);
		UpdateDelayInfo();
		mGridTransform = ((Component)this).gameObject.GetComponent<MyGridTransform>();
		DoBeforeDelay();
		if (mCoro != null)
		{
			((MonoBehaviour)this).StopCoroutine(mCoro);
		}
		if (((Behaviour)this).enabled)
		{
			mCoro = ((MonoBehaviour)this).StartCoroutine(DelayPlay());
		}
		else
		{
			Debug.LogError($"[Fight] MyFightHitBase: start a inactive gameobject coroutine, classId:{mGridTransform.myEntity.ClassId}");
		}
		mOnHitPosChangeDisposable = Component.OnHitPosChange.Subscribe(OnHitPosChange);
	}

	public void OnDisable()
	{
		if (mCoro != null)
		{
			((MonoBehaviour)this).StopCoroutine(mCoro);
		}
	}

	public override void OnEnd(object userData)
	{
		mGridTransform = null;
		base.OnEnd(userData);
	}

	public override void OnRemove(object userData)
	{
		foreach (GameObject mTmpObj in mTmpObjs)
		{
			Object.Destroy((Object)(object)mTmpObj);
		}
		if (mCoro != null)
		{
			((MonoBehaviour)this).StopCoroutine(mCoro);
		}
		mOnHitPosChangeDisposable.Dispose();
		mOnHitPosChangeDisposable = null;
		base.OnRemove(userData);
	}

	private void UpdateDelayInfo()
	{






		FightHitBaseComponentInfo info = Component.Info;
		DelayInfoProvider = new FightHitViewFormula.SkillHitBaseDelayInfoProvider
		{
			Offset = info.Offset,
			TargetOffset = info.TargetOffset,
			RotateCenterOffset = info.RotateCenterOffset,
			IsUseCenterRotation = info.IsUseCenterRotation,
			DelayDistFactor = info.DelayDistFactor
		};
		MyFightHitExtDelayInfoOverriderByStatusStack myFightHitExtDelayInfoOverriderByStatusStack = default;
		if (TryGetMySkill(out var skill) && ((Component)skill).TryGetComponent<MyFightHitExtDelayInfoOverriderByStatusStack>(out myFightHitExtDelayInfoOverriderByStatusStack))
		{
			int overriderIndex = myFightHitExtDelayInfoOverriderByStatusStack.GetOverriderIndex();
			if (overriderIndex >= 0)
			{
				FightHitExtDelayInfoOverriderByStatusStackComponentInfo info2 = myFightHitExtDelayInfoOverriderByStatusStack.Component.Info;
				DelayOverrideInfoIndex = overriderIndex;
				FightHitExtDelayInfoOverriderByStatusStackComponent.SetValue(ref DelayInfoProvider.Offset, info2.Offsets, overriderIndex);
				FightHitExtDelayInfoOverriderByStatusStackComponent.SetValue(ref DelayInfoProvider.TargetOffset, info2.TargetOffsets, overriderIndex);
				FightHitExtDelayInfoOverriderByStatusStackComponent.SetValue(ref DelayInfoProvider.RotateCenterOffset, info2.RotationCenterOffsets, overriderIndex);
				FightHitExtDelayInfoOverriderByStatusStackComponent.SetValue(ref DelayInfoProvider.DelayDistFactor, info2.DelayDistFactors, overriderIndex);
				FightHitExtDelayInfoOverriderByStatusStackComponent.SetValue(ref DelayInfoProvider.IsUseCenterRotation, info2.IsUseCenterRotations, overriderIndex);
			}
		}
	}

	private void OnHitPosChange(Pos2d newPos)
	{
		mGridTransform.SetPositionByGrid(newPos);
	}

	private void DoBeforeDelay()
	{
		if ((Object)(object)SourceToMePlayable != (Object)null)
		{
			GameObject val = CreateSourceGo();
			if ((Object)(object)val != (Object)null)
			{
				mMyPlayableManager.Play(SourceToMePlayable, val, ((Component)this).gameObject, 1f, null, ShouldSetPlayableDirectinParamToSouceToMePlayable ? GetPlayableParam() : null);
			}
		}
	}

	private GameObject CreateSourceGo()
	{






		if (!TryGetMySkill(out var skill) || (Object)(object)skill.Source == (Object)null)
		{
			return null;
		}
		if (SourceToMeAnchor == EFightHitSourceToMeAnchor.SkillPlayableTarget)
		{
			return skill.GetPlayableTargetGo();
		}
		if (IsApplyOffsetToPlayable)
		{
			Pos2d pos = myGridTransform.Pos;
			IFightSkillViewServices gridGround = ViewServices;
			bool flipX = skill.Source.myGridTransform.FlipX;
			Vector3 pos2 = FightHitViewFormula.CalcSkillStartPos(gridGround, DelayInfoProvider.IsUseCenterRotation, flipX, GetStartPos(), pos, DelayInfoProvider.Offset, DelayInfoProvider.TargetOffset, DelayInfoProvider.RotateCenterOffset);
			return CreateGo(pos2, ((Component)myEntity).transform.parent);
		}
		return ((Component)skill.Source).gameObject;
	}

	protected virtual Pos2d GetStartPos()
	{
		if (!TryGetMySkill(out var skill) || (Object)(object)skill.Source == (Object)null)
		{
			return default;
		}
		return skill.myGridTransform.Pos;
	}

	private IEnumerator DelayPlay()
	{
		if (TryGetMySkill(out var skill) && !((Object)(object)skill.Source == (Object)null))
		{
			_ = Component.Info;
			Pos2d pos = myGridTransform.Pos;
			IFightSkillViewServices gridGround = ViewServices;
			bool flipX = skill.Source.myGridTransform.FlipX;
			float num = FightHitViewFormula.CalcSkillHitDelay(DelayInfoProvider, GetStartPos(), pos, gridGround, flipX);
			yield return (object)new WaitForSeconds(num);
			DoAfterDelay();
		}
	}

	private void DoAfterDelay()
	{
		if ((Object)(object)myPlayable != (Object)null)
		{
			mMyPlayableManager.Play(myPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
		}
		if ((Object)(object)myScopePlayable != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(PlayScopePlayable());
		}
	}

	private Dictionary<string, object> GetPlayableParam()
	{
		if (mMyPlayableParams == null)
		{
			mMyPlayableParams = new Dictionary<string, object> {
			{
				"ParamSource2TargetDirection",
				Component.GridTransform.Direction
			} };
		}
		else
		{
			mMyPlayableParams["ParamSource2TargetDirection"] = Component.GridTransform.Direction;
		}
		return mMyPlayableParams;
	}

	private IEnumerator PlayScopePlayable()
	{
		float num = 0f;
		List<HitScopCfg> scopes = Component.GetScopes();
		foreach (HitScopCfg scope in scopes)
		{
			float num2 = scope.Delay - num;
			if (ScopePlayableFollowDelay && num2 > 0f)
			{
				yield return (object)new WaitForSeconds(num2);
			}
			num = scope.Delay;
			Pos2d grid = Component.GridTransform.LocalToWorldPos(scope.Pos);
			Vector3 pos = ViewServices.GridToPos(grid);
			if (ScopeZFollowHitZ)
			{
				pos.z = ((Component)this).transform.position.z;
			}
			GameObject val = CreateGo(pos, ((Component)myEntity).transform.parent, "tmp_scope_obj");
			mMyPlayableManager.Play(myScopePlayable, val, val);
		}
	}

	private GameObject CreateGo(Vector3 pos, Transform parent, string name = "")
	{





		GameObject val = new GameObject(name);
		val.transform.SetParent(parent);
		val.transform.localPosition = pos;
		val.transform.localRotation = Quaternion.identity;
		val.transform.localScale = Vector3.one;
		mTmpObjs.Add(val);
		return val;
	}

	public bool TryGetMySkill(out MyFightSkill skill)
	{
		skill = null;
		MyEntity entity = ViewServices.GetEntity(Component.SkillId);
		if ((Object)(object)entity == (Object)null)
		{
			return false;
		}
		skill = ((Component)entity).GetComponent<MyFightSkill>();
		if ((Object)(object)skill == (Object)null)
		{
			return false;
		}
		return true;
	}

	public virtual void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)myPlayable != (Object)null)
			{
				playables.Add(myPlayable);
			}
			if ((Object)(object)myScopePlayable != (Object)null)
			{
				playables.Add(myScopePlayable);
			}
			if ((Object)(object)SourceToMePlayable != (Object)null)
			{
				playables.Add(SourceToMePlayable);
			}
		}
	}
}

}
