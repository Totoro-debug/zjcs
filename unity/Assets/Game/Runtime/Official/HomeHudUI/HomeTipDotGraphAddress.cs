using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeHudUI
{
    // Name/type/scope identity for native UI selectors, not a runtime node graph.
    public sealed class HomeTipDotGraphAddress : ProfessionGraphAssetBase
    {
        public TipDotGraphType Type;
        public TipDotGraphScope Scope;
        public TextAsset sourceGraphData;
        public string originalIdentity;
    }
}
