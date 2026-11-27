using System;
using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
    // The original dynamic graph operations shared by building and partner nodes.
    public interface IDynamicTipDotNodeManager : ITipDotNodeManager
    {
        Action<TipDotNodeKey> OnAddNode { get; set; }
        Action<TipDotNodeKey> OnReduceNode { get; set; }
        void ConnectNodes(TipDotNodeKey parent, List<TipDotNodeKey> children);
    }
}
