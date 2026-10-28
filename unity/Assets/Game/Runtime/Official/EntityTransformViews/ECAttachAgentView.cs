// Migrated complete original application class; see entity transform receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityTransformViews
{
public class ECAttachAgentView : ECViewBase
{
	[Serializable]
	public struct AttachAgentConfig
	{
		public string BoneName;

		public Transform Bone;
	}

	[Serializable]
	public struct AttachAgentConfigs
	{
		public List<AttachAgentConfig> Values;
	}

	[SerializeField]
	public AttachAgentConfigs Configs;

	protected override int mOrder => 0;

	protected override void Load()
	{
		base.Load();
		AttachAgent attachAgent = ((Component)mOwner.transform.parent).gameObject.GetComponent<AttachAgent>();
		if ((Object)(object)attachAgent == (Object)null)
		{
			attachAgent = ((Component)mOwner.transform.parent).gameObject.AddComponent<AttachAgent>();
		}
		attachAgent.Clear();
		foreach (AttachAgentConfig value in Configs.Values)
		{
			attachAgent.AddBoneNode(value.BoneName, value.Bone);
		}
	}
}

}
