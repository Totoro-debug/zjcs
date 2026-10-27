// Ported from the verified official source; only dependency/serialization adapters differ.


namespace Zjcs.Official.ProfessionUI
{
public class BaseNodeInstanceInfo : INodeInstanceInfo
{
	public static readonly BaseNodeInstanceInfo kDefault = new BaseNodeInstanceInfo();

	public virtual string GetInstanceDesc()
	{
		return "Base";
	}

	public override bool Equals(object obj)
	{
		return obj is BaseNodeInstanceInfo;
	}

	public override int GetHashCode()
	{
		return typeof(BaseNodeInstanceInfo).GetHashCode();
	}
}
}
