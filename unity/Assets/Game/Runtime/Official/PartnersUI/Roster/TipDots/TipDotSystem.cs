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
public class TipDotSystem : IProfessionTipDotSystem, IDisposable
{
	public static bool sTempLimitShowTip = true;
    private readonly PartnerTipDotRuntimeBindings bindings;
    private IDisposable playerSubscription;
    public bool TempLimitShowTip => sTempLimitShowTip;
    public TipDotGraphType? GetGraphType(string name) => bindings.GetGraphType(name);
    public TipDotSystem(TipDotNodeManager manager, ITipDotBanManager bans, PartnerTipDotRuntimeBindings bindings)
    {
        NodeManager = manager ?? throw new ArgumentNullException(nameof(manager));
        TipDotBanManager = bans ?? throw new ArgumentNullException(nameof(bans));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }

	private bool mInited;

	private bool mIsShowTipDot = true;

	public SafeAction OnTipDotSystemInited { get; set; } = new SafeAction();

	public SafeAction OnTipDotSystemPreClear { get; set; } = new SafeAction();
	private TipDotNodeManager NodeManager { get; set; }
	private ITipDotBanManager TipDotBanManager { get; set; }

	public Action OnTipDotShowChanged { get; set; }

	public bool Inited => mInited;

	public bool IsShowTipDot => mIsShowTipDot;

	public void Initialize()
	{

		AddListener();
	}

	public void Dispose()
	{

		RemoveListener();

	}

	private void AddListener()
	{
		playerSubscription = bindings.SubscribePlayerUpdate(OnPlayerUpdate);
	}

	private void RemoveListener()
	{
		playerSubscription?.Dispose(); playerSubscription = null;
	}

	public void OnPlayerUpdate()
	{
		if (bindings.HasPlayer())
		{
			InitSystem();
		}
		else
		{
			ClearSystem();
		}
	}

	private void ClearSystem()
	{
		if (mInited)
		{
			OnTipDotSystemPreClear?.Invoke();
			NodeManager.Release();
			mInited = false;
		}
		TipDotBanManager.ClearTipDotBan();
	}

	private void InitSystem()
	{
		if (!mInited)
		{
			NodeManager.Init();
			OnTipDotSystemInited?.Invoke();
			mInited = true;
		}
	}

	public void SetShowTipDot(bool isShow)
	{
		mIsShowTipDot = isShow;
		OnTipDotShowChanged?.Invoke();
	}
}
}
