// Original ECAttachAgentView; only namespace/base bridge names are adapted.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryAttachment;

namespace Zjcs.Official.SteleEntities
{
    [Serializable]
    public sealed class ECAttachAgentView : ECViewBase
    {
        [Serializable] public struct AttachAgentConfig { public string BoneName; public Transform Bone; }
        [Serializable] public struct AttachAgentConfigs { public List<AttachAgentConfig> Values; }
        [SerializeField] public AttachAgentConfigs Configs;
        protected override int mOrder => 0;
        protected override void Load()
        {
            base.Load();
            AttachAgent attachAgent = mOwner.transform.parent.gameObject.GetComponent<AttachAgent>();
            if (attachAgent == null) attachAgent = mOwner.transform.parent.gameObject.AddComponent<AttachAgent>();
            attachAgent.Clear();
            foreach (AttachAgentConfig value in Configs.Values) attachAgent.AddBoneNode(value.BoneName, value.Bone);
        }
    }
}
