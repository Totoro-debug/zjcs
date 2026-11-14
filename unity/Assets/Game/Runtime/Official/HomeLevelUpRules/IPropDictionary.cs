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

public interface IPropDictionary : IReadOnlyDictionary<PropType, long>, IEnumerable<KeyValuePair<PropType, long>>, IEnumerable, IReadOnlyCollection<KeyValuePair<PropType, long>>
{
	void TrimExcess();

	void Add(PropType key, long value);

	bool Remove(PropType key);

	void Clear();
}

}
