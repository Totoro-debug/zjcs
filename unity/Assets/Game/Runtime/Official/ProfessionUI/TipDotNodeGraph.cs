// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    // Original runtime graph payload. Node ID generation/copy/removal belong to
    // the graph authoring editor and are not performed while reading native data.
    public sealed class TipDotNodeGraph : ProfessionGraphAssetBase
    {
        public TipDotGraphType Type;
        public TipDotGraphScope Scope;
        [NonSerialized] public WeakReference<INodeInstanceInfo> RuntimeShowNodeInstanceInfo =
            new WeakReference<INodeInstanceInfo>(BaseNodeInstanceInfo.kDefault);
    }
}
