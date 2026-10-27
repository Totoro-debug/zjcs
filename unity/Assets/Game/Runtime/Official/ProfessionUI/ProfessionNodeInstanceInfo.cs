// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
public class ProfessionNodeInstanceInfo : BaseNodeInstanceInfo
{
	public int Profession;

	public ProfessionNodeInstanceInfo(int profession)
	{
		Profession = profession;
	}

	public override string GetInstanceDesc()
	{
		return $"{Profession}";
	}

	public override bool Equals(object obj)
	{
		if (obj is ProfessionNodeInstanceInfo professionNodeInstanceInfo)
		{
			return Profession == professionNodeInstanceInfo.Profession;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<int>(Profession);
	}
}
}
