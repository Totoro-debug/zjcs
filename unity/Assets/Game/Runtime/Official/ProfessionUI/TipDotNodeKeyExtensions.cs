// Ported from the verified official source; only dependency/serialization adapters differ.
namespace Zjcs.Official.ProfessionUI
{
    public static class TipDotNodeKeyExtensions
    {
        public static TipDotNodeKey SetInstanceInfo(this TipDotNodeKey key, INodeInstanceInfo info)
        {
            return new TipDotNodeKey(key.GraphName, key.NodeName, info);
        }
    }
}
