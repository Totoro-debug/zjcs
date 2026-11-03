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
public class TipDotSystemController : IProfessionTipDotSystemController
{
	private ITipDotNodeManager NodeManager { get; set; }
    public TipDotSystemController(ITipDotNodeManager manager)
    { NodeManager = manager ?? throw new ArgumentNullException(nameof(manager)); }

	public bool LinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> onStateChangedCall)
	{
		TipDotNodeBase tipDotNodeBase = NodeManager.FindNodeOrCreate(key);
		if (tipDotNodeBase != null)
		{
			tipDotNodeBase.AddUiStateListener(onStateChangedCall);
			onStateChangedCall?.Invoke(tipDotNodeBase.TipDotInfo);
			return true;
		}
		return false;
	}

	public bool UnlinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> onStateChangedCall)
	{
		TipDotNodeBase tipDotNodeBase = NodeManager.FindNode(key);
		if (tipDotNodeBase != null)
		{
			tipDotNodeBase.RemoveUiStateListener(onStateChangedCall);
			return true;
		}
		return false;
	}

	public bool LinkNodeStateChange(TipDotNodeKey key, Action<TipDotStateInfo> onStateChangedCall)
	{
		return LinkNodeStateChange(key, null, onStateChangedCall);
	}

	public bool UnlinkNodeStateChange(TipDotNodeKey key, Action<TipDotStateInfo> onStateChangedCall)
	{
		return UnlinkNodeStateChange(key, null, onStateChangedCall);
	}
}
}
