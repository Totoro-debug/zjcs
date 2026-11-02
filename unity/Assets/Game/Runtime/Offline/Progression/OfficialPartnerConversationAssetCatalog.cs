using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    public sealed class OfficialPartnerConversationAssetCatalog : ScriptableObject
    {
        [SerializeField] private TextAsset rules;
        public OfficialPartnerConversationCatalog Load(Func<IReadOnlyDictionary<string, object>, IPartnerConversationCondition> bindCondition)
        {
            if (!rules) throw new InvalidOperationException("Original conversation table is unbound.");
            return OfficialPartnerConversationCatalog.Parse(rules.text, bindCondition);
        }
        public static OfficialPartnerConversationAssetCatalog LoadDefault()
        {
            var asset = Resources.Load<OfficialPartnerConversationAssetCatalog>("Partners/OfficialPartnerConversationAssetCatalog");
            return asset ? asset : throw new InvalidOperationException("Original conversation Resources binding is missing.");
        }
    }
}
