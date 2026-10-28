// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionTipDotSystem
    {
        bool IsShowTipDot { get; }
        bool TempLimitShowTip { get; }
        Action OnTipDotShowChanged { get; set; }
        SafeAction OnTipDotSystemInited { get; set; }
        SafeAction OnTipDotSystemPreClear { get; set; }
        TipDotGraphType? GetGraphType(string graphName);
    }
    public interface IProfessionTipDotSystemController
    {
        bool LinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> callback);
        bool UnlinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> callback);
    }
}
