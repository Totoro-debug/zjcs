// Ported from the verified official source; only dependency/serialization adapters differ.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]


public class TipDotGraphConditionNode : TipDotGraphNode
{
	public override List<string> GetSelecableType()
	{
		if (mSelectableNodeType != null)
		{
			return mSelectableNodeType;
		}
		mSelectableNodeType = new List<string>();
		Type[] types = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly assembly) => assembly == typeof(TipDotConditionNodeBase).Assembly).GetTypes();
		Type baseType = typeof(TipDotConditionNodeBase);
		mSelectableNodeType = (from t in types
			where t.IsSubclassOf(baseType) && !t.IsAbstract
			select t.FullName).ToList();
		return mSelectableNodeType;
	}
}
}
