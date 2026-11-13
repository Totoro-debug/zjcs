using System;

namespace Zjcs.Official.BattleBgm
{
    [Serializable] public sealed class DomainTypeCondition
    {
        public int DomainType;
        public bool Not;

	public bool IsMet(int domainType)
	{
		bool flag = false;
		flag = domainType == DomainType;
		if (!Not)
		{
			return flag;
		}
		return !flag;
	}

    }
}
