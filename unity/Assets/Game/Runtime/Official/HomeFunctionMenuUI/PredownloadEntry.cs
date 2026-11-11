// Original menu peripheral component; local service and observable bindings.
using System.Linq;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomePlayerPanelUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class PredownloadEntry : MonoBehaviour
{
	public IHomeMenuPredownloadServices Services { private get; set; }

	[SerializeField]
	private Button mEntryBtn;

	[SerializeField]
	private bool mClearNeedTipOnClick = true;

	private HomePanelDisposableBag mDisposableBag;

	private CancellationTokenSource mRefreshCts;






	protected void Awake()
	{
		mDisposableBag = default;
		if (mEntryBtn != null)
		{
			HomeMenuSubscriptions.SubscribeClick(mEntryBtn,OnEntryBtnClick).AddTo(ref mDisposableBag);
		}
		Services.SubscribeStateUpdates(OnPredownloadStateUpdate).AddTo(ref mDisposableBag);
	}

	protected void OnDestroy()
	{
		mDisposableBag.Dispose();
	}

	protected void OnEnable()
	{
		Refresh();
	}

	protected void OnDisable()
	{
		CancelRefresh();
	}

	private void OnEntryBtnClick()
	{
		if (Services.CurrentDownloadState == PredownloadState.Wait)
		{
			Services.PopupTipDialog();
		}
		else
		{
			Services.PopupProgressDialog((IHomeMenuPanel dialog) =>
			{
				dialog.Init();
			});
		}
		Services.ClickEntry(mClearNeedTipOnClick);
	}

	private void Refresh()
	{
		if (!(mEntryBtn == null))
		{
			CancelRefresh();
			mRefreshCts = new CancellationTokenSource();
			DoRefresh(mRefreshCts.Token).Forget();
		}
	}

	private void CancelRefresh()
	{
		mRefreshCts?.Cancel();
		mRefreshCts?.Dispose();
		mRefreshCts = null;
	}

	private async UniTask DoRefresh(CancellationToken token)
	{
		mEntryBtn.gameObject.SetActive(value: false);
		bool active = await NeedShowEntry(token);
		mEntryBtn.gameObject.SetActive(active);
	}

	private async UniTask<bool> NeedShowEntry(CancellationToken token)
	{
		await UniTask.WaitUntil(() => WaitCondition(), PlayerLoopTiming.Update, token);
		if (Services.CurrentDownloadState == PredownloadState.BlockedByPayd)
		{
			return false;
		}
		long totalDownloadedBytes = Services.TotalDownloadedBytes;
		long currentDownloadedBytes = Services.CurrentDownloadedBytes;
		if (currentDownloadedBytes >= totalDownloadedBytes)
		{
			return false;
		}
		return true;
		bool WaitCondition()
		{
			if (!Services.TimeSynced)
			{
				return false;
			}
			if (Services.CurrentDownloadState == PredownloadState.None)
			{
				return false;
			}
			return true;
		}
	}

	private void OnPredownloadStateUpdate(HomeMenuPredownloadStateUpdate _)
	{
		if (isActiveAndEnabled)
		{
			Refresh();
		}
	}
}
}
