// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[Serializable]
public class TipDotGraphDynamicLinkNode : TipDotGraphNode
{
	public override List<string> GetSelecableType()
	{
		if (mSelectableNodeType != null)
		{
			return mSelectableNodeType;
		}
		mSelectableNodeType = new List<string>();
		Type[] types = typeof(TipDotDynamicLinkNodeBase).Assembly.GetTypes();
		Type baseType = typeof(TipDotDynamicLinkNodeBase);
		mSelectableNodeType = (from t in types
			where t.IsSubclassOf(baseType) && !t.IsAbstract
			select t.FullName).ToList();
		return mSelectableNodeType;
	}
}

}
