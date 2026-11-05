// Original MyFightRolePet subscriptions, nested-cast counter and follower callbacks.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityTransformViews;
using UnityEngine;

namespace Zjcs.Official.BattleSkillPlayables
{
public class MyFightRolePet : MonoBehaviour
{
	[SerializeField]
	private GridTransformFollower mGridTransformFollower;

	[SerializeField]
	private MyFightSkillAgent mSkillAgent;

	private Transform mOwnerEntity;
	private bool bound;

	private MyFightSkillAgent mOwnerSkillAgent;

	private int mCurrentSkillCounter;

	private void Reset()
	{
		mSkillAgent = ((Component)this).GetComponent<MyFightSkillAgent>();
	}

	public void Bind(GridTransformFollower follower, MyFightSkillAgent skillAgent,
        MyFightSkillAgent ownerSkillAgent, Transform owner, ECGridTransformData grid)
	{
		mGridTransformFollower = follower; mSkillAgent = skillAgent;
		mOwnerEntity = owner; mOwnerSkillAgent = ownerSkillAgent; bound = true;
		mCurrentSkillCounter = 0;


		mOwnerSkillAgent.OnSkillEnded += OwnerSkillAgent_OnSkillEnded;
		mSkillAgent.OnSkillApplied += MSkillAgent_OnSkillApplied;
		mSkillAgent.OnSkillEnded += MSkillAgent_OnSkillEnded;
		mGridTransformFollower.Init(mOwnerEntity, grid);
	}

	public void Release()
	{
		if (!bound) return;
		bound = false;
		mGridTransformFollower.Release();
		mSkillAgent.OnSkillApplied -= MSkillAgent_OnSkillApplied;
		mSkillAgent.OnSkillEnded -= MSkillAgent_OnSkillEnded;
		if ((Object)(object)mOwnerSkillAgent != (Object)null)
		{
			mOwnerSkillAgent.OnSkillEnded -= OwnerSkillAgent_OnSkillEnded;
			mOwnerSkillAgent = null;
		}
		mOwnerEntity = null;
		mCurrentSkillCounter = 0;

	}

	private void OwnerSkillAgent_OnSkillEnded(MyFightSkill skill)
	{
		mGridTransformFollower.UpdateFlipX();
	}

	private void MSkillAgent_OnSkillApplied(MyFightSkill skill)
	{
		mCurrentSkillCounter++;
		mGridTransformFollower.Pause();
	}

	private void MSkillAgent_OnSkillEnded(MyFightSkill skill)
	{
		mCurrentSkillCounter--;
		if (mCurrentSkillCounter == 0)
		{
			mGridTransformFollower.ProofreadPos();
		}
	}

	private void OnDestroy() { Release(); }
}
}
