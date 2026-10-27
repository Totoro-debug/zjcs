// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public struct TipDotNodeKey : IEquatable<TipDotNodeKey>
{
	public string GraphName;

	public string NodeName;

	public INodeInstanceInfo InstanceInfo;

    public TipDotNodeKey(string graphName, string nodeName, INodeInstanceInfo instanceInfo)
    {
        GraphName = string.IsNullOrEmpty(graphName) ? graphName : string.Intern(graphName);
        NodeName = string.IsNullOrEmpty(nodeName) ? nodeName : string.Intern(nodeName);
        InstanceInfo = instanceInfo;
    }

	public bool IsValid()
	{
		if (!string.IsNullOrEmpty(GraphName))
		{
			return !string.IsNullOrEmpty(NodeName);
		}
		return false;
	}

	public override string ToString()
	{
		string text = ((InstanceInfo != null) ? InstanceInfo.GetInstanceDesc() : string.Empty);
		return GraphName + "." + NodeName + "." + text;
	}

	public bool IsSameGraphNode(string graphName, string nodeName)
	{
		if (graphName == GraphName)
		{
			return nodeName == NodeName;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<string, string, INodeInstanceInfo>(GraphName, NodeName, InstanceInfo);
	}

	public bool Equals(TipDotNodeKey other)
	{
		if (GraphName == other.GraphName && NodeName == other.NodeName)
		{
			return object.Equals(InstanceInfo, other.InstanceInfo);
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is TipDotNodeKey other)
		{
			return Equals(other);
		}
		return false;
	}

	public static bool operator ==(TipDotNodeKey left, TipDotNodeKey right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(TipDotNodeKey left, TipDotNodeKey right)
	{
		return !left.Equals(right);
	}
}
}
