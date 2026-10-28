// Migrated source application/query code; see map13-entity-state-views receipt.
using Object = UnityEngine.Object;
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13EntityStateViews
{
[Serializable]
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
