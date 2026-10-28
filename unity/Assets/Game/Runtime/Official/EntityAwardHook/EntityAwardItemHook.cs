// Complete original client award hook; see source receipt.
namespace Zjcs.Official.EntityAwardHook
{
using System.Collections.Generic;
using System;
using Zjcs.Official.EntityLifecycle;
using UnityEngine;

public class EntityAwardItemHook : MonoBehaviour
{
    public IEntityAwardManager AwardManager { get; set; }
	[SerializeField]
	private EntityAwardHandlerPriority mPriority;

	[SerializeField]
	private MyEntity mEntity;

	private List<EntityAwardMessageData> mPlayerAwardMsgs;

	private bool mIsHook;

	private void Reset()
	{
		mEntity = ((Component)this).GetComponent<MyEntity>();
	}

	public void Hook()
	{
		if (!mIsHook)
		{
			AwardManager.RegisterHandler(HandleAward, mPriority);
			mIsHook = true;
		}
	}

	private void OnDestroy()
	{
		if (mIsHook)
		{
			AwardManager.RemoveHandler(HandleAward);
			mIsHook = false;
		}
	}

	public void Unhook()
	{
		if (!mIsHook)
		{
			return;
		}
		AwardManager.RemoveHandler(HandleAward);
		if (AwardManager != null && mPlayerAwardMsgs != null)
		{
			foreach (EntityAwardMessageData mPlayerAwardMsg in mPlayerAwardMsgs)
			{
				AwardManager.ShowAward(mPlayerAwardMsg);
			}
			mPlayerAwardMsgs.Clear();
			mPlayerAwardMsgs = null;
		}
		mIsHook = false;
	}

	private bool HandleAward(EntityAwardMessageData msg)
	{
		if (!(msg.SourceData is EntityMapAwardSourceData asMapEntity))
		{
			return false;
		}
		if (asMapEntity.EntityId != mEntity.Id)
		{
			return false;
		}
		if (mPlayerAwardMsgs == null)
		{
			mPlayerAwardMsgs = new List<EntityAwardMessageData>();
		}
		mPlayerAwardMsgs.Add(msg);
		return true;
	}
}

}
