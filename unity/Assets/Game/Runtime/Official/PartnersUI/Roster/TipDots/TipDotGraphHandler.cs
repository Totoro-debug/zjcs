// Selected official game implementation; see partner-tipdot-runtime-source-manifest.json.
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.PartnersUI
{
public class TipDotGraphHandler
{
	private Dictionary<string, TipDotNodeGraph> mTemplateGraphDict = new Dictionary<string, TipDotNodeGraph>();
	private readonly Func<string, TipDotNodeGraph> load;
    public TipDotGraphHandler(Func<string, TipDotNodeGraph> load)
    { this.load = load ?? throw new ArgumentNullException(nameof(load)); }

	public TipDotNodeGraph GetNodeGraph(string graphName)
	{

		if (!mTemplateGraphDict.TryGetValue(graphName, out var value))
		{

			value = load(graphName);
			mTemplateGraphDict.Add(graphName, value);
		}
		return value;
	}
}
}
