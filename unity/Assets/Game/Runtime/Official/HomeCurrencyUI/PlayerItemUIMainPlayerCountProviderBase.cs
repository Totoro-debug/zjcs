// Selected original currency count observer; typed player/parser boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyUI
{
using System;

public abstract class PlayerItemUIMainPlayerCountProviderBase : IPlayerItemUICountProvider
{
    protected IHomeCurrencyBindings Bindings { get; }
    protected PlayerItemUIMainPlayerCountProviderBase(IHomeCurrencyBindings bindings)
    { Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	private IHomeCurrencyPlayer mBoundPlayer;

	public long Count
	{
		get
		{
			if (mBoundPlayer == null)
			{
				return 0L;
			}
			return GetCount(mBoundPlayer);
		}
	}

	public event Action<long, bool> OnItemCountUpdate;

	public void Init()
	{
		if (!Bindings.ManagerAvailable)
		{
			UnityEngine.Debug.LogError(GetType().Name + " Init Failed, MeManager.Instance Is NULL");
		}
		else
		{
			Bindings.OnPlayerUpdate.AddListener(OnMainPlayerUpdate);
			RebindMainPlayer(fireCountUpdate: false);
		}
	}

	public void Release()
	{
		if (Bindings.ManagerAvailable)
		{
			Bindings.OnPlayerUpdate.RemoveListener(OnMainPlayerUpdate);
		}
		if (mBoundPlayer != null)
		{
			UnbindPlayer(mBoundPlayer);
			mBoundPlayer = null;
		}
	}

	protected abstract long GetCount(IHomeCurrencyPlayer player);

	protected abstract void BindPlayer(IHomeCurrencyPlayer player);

	protected abstract void UnbindPlayer(IHomeCurrencyPlayer player);

	protected void FireOnItemCountUpdate(long count, bool forceUpdate = false)
	{
		OnItemCountUpdate?.Invoke(count, forceUpdate);
	}

	private void OnMainPlayerUpdate()
	{
		RebindMainPlayer(fireCountUpdate: true);
	}

	private void RebindMainPlayer(bool fireCountUpdate)
	{
		IHomeCurrencyPlayer mainPlayer = Bindings.MainPlayer;
		if (mBoundPlayer != mainPlayer)
		{
			if (mBoundPlayer != null)
			{
				UnbindPlayer(mBoundPlayer);
			}
			mBoundPlayer = mainPlayer;
			if (mBoundPlayer != null)
			{
				BindPlayer(mBoundPlayer);
			}
			if (fireCountUpdate)
			{
				FireOnItemCountUpdate(Count);
			}
		}
	}
}

}
