// Original client component; see grid entity runtime source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.BattleStatusPlayables;
using System;
using UnityEngine;

namespace Zjcs.Official.BattleGridItemEntities
{
public class MyFightRoleCharacter : MyComponent<BattleRoleCharacterViewData>
{
	[SerializeField]
	private bool mIsSoul;

	private MyFightRoleCharacter mMasterRole;


	public IBattleGridRoleViewRoot mFightRoot { get; set; }

	public event Action<MyFightRoleCharacter> OnSummonAdded;

	public event Action<MyFightRoleCharacter> OnSummonRemoved;

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		BattleRoleCharacterIdentity character = Component.Character;
		if (mFightRoot.MainPlayerId.HasValue && character != null && character.PlayerId == mFightRoot.MainPlayerId.Value && !mIsSoul)
		{
			mFightRoot.Character = this;
		}
		MyEntity entity = mFightRoot.GetEntity(Component.MasterId);
		if ((Object)(object)entity != (Object)null)
		{
			mMasterRole = ((Component)entity).GetComponent<MyFightRoleCharacter>();
		}
		mFightRoot.AddCharacter(this);
		MasterAddSummon();
	}

	public override void OnRemove(object userData)
	{
		MasterRemoveSummon();
		if (mFightRoot.HasFightRoot)
		{
			mFightRoot.RemoveCharacter(this);
		}
		mMasterRole = null;
		base.OnRemove(userData);
	}

	private void MasterAddSummon()
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)mMasterRole == (Object)null)
		{
			return;
		}
		try
		{
			mMasterRole.OnSummonAdded?.Invoke(this);
		}
		catch (Exception ex)
		{
			Debug.LogException(ex);
		}
	}

	private void MasterRemoveSummon()
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)mMasterRole == (Object)null)
		{
			return;
		}
		try
		{
			mMasterRole.OnSummonRemoved?.Invoke(this);
		}
		catch (Exception ex)
		{
			Debug.LogException(ex);
		}
	}
}
}
