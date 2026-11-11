// Selected original currency count observer; typed player/parser boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyUI
{
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public class PlayerItemUIPlayerItemCountProvider : PlayerItemUIMainPlayerCountProviderBase
{
	private int mItemId;

	private HomeCurrencyItemInfo mItemInfo;

	private CancellationTokenSource mCancellationTokenSource;

	public PlayerItemUIPlayerItemCountProvider(int itemId, IHomeCurrencyBindings bindings) : base(bindings)
	{
		mItemId = itemId;
	}

	protected override long GetCount(IHomeCurrencyPlayer player)
	{
		return player.GetItemAmount(mItemId);
	}

	protected override void BindPlayer(IHomeCurrencyPlayer player)
	{
		player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		if (Bindings.IsOutputAdditionItem(mItemId))
		{
			player.OnAutoIncreaseItemDataUpdate += new Action<HomeCurrencyItem>(OnAutoIncreasementItemDataUpdate);
		}
		mItemInfo = Bindings.GetItemInfo(mItemId);
		ClearToken();
		if (mItemInfo.HasAutoIncrease)
		{
			mCancellationTokenSource = new CancellationTokenSource();
			AutoRefreshCount(mCancellationTokenSource.Token).Forget();
		}
	}

	protected override void UnbindPlayer(IHomeCurrencyPlayer player)
	{
		ClearToken();
		player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		if (Bindings.IsOutputAdditionItem(mItemId))
		{
			player.OnAutoIncreaseItemDataUpdate -= new Action<HomeCurrencyItem>(OnAutoIncreasementItemDataUpdate);
		}
	}

	private void MainPlayer_OnItemCountUpdate(HomeCurrencyItem itemData, long preCount, object sourceDataa)
	{
		if (itemData.Id == mItemId)
		{
			FireOnItemCountUpdate(itemData.Count);
		}
	}

	private void OnAutoIncreasementItemDataUpdate(HomeCurrencyItem itemDataWrap)
	{
		if (itemDataWrap.Id == mItemId)
		{
			FireOnItemCountUpdate(itemDataWrap.Count, forceUpdate: true);
		}
	}

	private async UniTask AutoRefreshCount(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			FireOnItemCountUpdate(Count);
			await UniTask.WaitForSeconds(1f, ignoreTimeScale: false, PlayerLoopTiming.Update, cancellationToken);
		}
	}

	private void ClearToken()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}
}

}
