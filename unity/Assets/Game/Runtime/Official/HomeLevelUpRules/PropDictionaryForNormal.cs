// Selected original 884 implementation; configuration and logging boundaries only.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Official.HomeIncomeRules;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeLevelUpRules
{

public class PropDictionaryForNormal : Dictionary<PropType, long>, IPropDictionary, IReadOnlyDictionary<PropType, long>, IEnumerable<KeyValuePair<PropType, long>>, IEnumerable, IReadOnlyCollection<KeyValuePair<PropType, long>>
{
	public PropDictionaryForNormal()
	{
	}

	public PropDictionaryForNormal(IDictionary<PropType, long> dictionary)
		: base(dictionary)
	{
	}

	void IPropDictionary.TrimExcess()
	{
		base.TrimExcess();
	}
}

}
