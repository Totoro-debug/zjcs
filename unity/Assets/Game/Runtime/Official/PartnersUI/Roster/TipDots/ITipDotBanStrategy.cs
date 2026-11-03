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
public interface ITipDotBanStrategy
{
	bool IsTipDotBan(TipDotNodeKey nodeKey);

	void ClearTipDotBan();
}
}
