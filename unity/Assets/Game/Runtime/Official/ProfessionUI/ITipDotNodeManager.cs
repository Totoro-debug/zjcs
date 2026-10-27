// Ported from the verified official source; only dependency/serialization adapters differ.
using System;
namespace Zjcs.Official.ProfessionUI
{
    // Exact operations consumed by the original aggregation core. Graph ownership
    // and local player event registration are supplied by the runtime adapter.
    public interface ITipDotNodeManager
    {
        Action OnAddScopeDataCollector { get; set; }
        Action OnRemoveScopeDataCollector { get; set; }
        TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key);
        TipDotNodeBase FindNode(TipDotNodeKey key);
        void AddDirtyConditionNode(TipDotConditionNodeBase node);
        void RemoveDirtyConditionNode(TipDotConditionNodeBase node);
    }
}
