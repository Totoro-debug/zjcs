// Migrated original tree-playable application source; see source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryFollowBone;
using System;
using UnityEngine;


namespace Zjcs.Official.World.Map13TreePlayables
{
public class ApplyByBone : ApplyOnInit
{
	[Tooltip("骨骼名字")]
	public string BoneName;

	[Tooltip("是否控制骨骼激活，若勾选，则在playable停止时隐藏骨骼")]
	public bool ControlBoneActive = true;

	[Tooltip("从什么物体上拿骨骼")]
	public AttachAgentGoType AttachAgentGoType = AttachAgentGoType.Source;

	[Tooltip("不限制目标组件类型时，将apply组件设置成target对象transform")]
	public bool ApplyTargetOfNullAttr;

	private AttachAgent mAttachAgent;

	protected override void Awake()
	{
		base.Awake();
		myPlayable.OnStop += new Action<MyPlayable>(OnPlayableStop);
	}

	protected override void OnDestroy()
	{
		myPlayable.OnStop -= new Action<MyPlayable>(OnPlayableStop);
		base.OnDestroy();
	}

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		mAttachAgent = GetAttachAgent(playable);
		if ((Object)(object)mAttachAgent == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
			return;
		}
		Transform val = (ControlBoneActive ? mAttachAgent.SetBoneActiveStatus(BoneName, isActive: true) : mAttachAgent.GetBone(BoneName));
		if ((Object)(object)val == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		else
		{
			MyPlayableApplyUtils.SetPlayableApplyComponent(playable, applyAttr, ((Component)val).gameObject, ApplyTargetOfNullAttr);
		}
	}

	private void OnPlayableStop(MyPlayable playable)
	{
		if (ControlBoneActive)
		{
			mAttachAgent.SetBoneActiveStatus(BoneName, isActive: false);
		}
		mAttachAgent = null;
	}

	private AttachAgent GetAttachAgent(MyPlayable playable)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected Obj, but got Unknown
		AttachAgent result = null;
		GameObject val = null;
		switch (AttachAgentGoType)
		{
		case AttachAgentGoType.Source:
			val = playable.mySource;
			break;
		case AttachAgentGoType.Target:
			val = playable.myTarget;
			break;
		case AttachAgentGoType.AttachAgentGoFromParam:
			val = (GameObject)playable.TryGetParam("ChainedHitCharacterGo");
			break;
		}
		if ((Object)(object)val != (Object)null)
		{
			result = val.GetComponent<AttachAgent>();
		}
		return result;
	}
}

}
