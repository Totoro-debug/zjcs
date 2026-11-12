// Original selected home HUD infrastructure; explicit local service bindings.
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.HomeHudUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class PlayAsYouDownloadEntry : MonoBehaviour
{
	public IHomeHudDownloadServices Services { private get; set; }

	[SerializeField]
	private GameObject mRoot;

	[SerializeField]
	private LayoutElement mLayoutElement;

	[SerializeField]
	private MultiLanguageText mProgress;

	[SerializeField]
	private List<RectTransform> mProgressRectTransform;

	[SerializeField]
	private int mHeightMin;

	[SerializeField]
	private int mHeightMax;

	[SerializeField]
	private Button mButton;

	[SerializeField]
	private GameObject mRedDotGo;

	[SerializeField]
	private MultiLanguageText mDownloadStateText;

	private HomePanelDisposableBag mDisposableBag;

	private IHomeHudCondition mCheckPointCondition;







	private int HeightDiff => mHeightMax - mHeightMin;

	protected void Awake()
	{
		if (mHeightMax < mHeightMin)
		{
			Debug.LogError("PlayAsYouDownloadEntry 配置错误！mHeightMax:"+mHeightMax+" > mHeightMin:"+mHeightMin);
		}
	}

	protected void OnEnable()
	{
		mRedDotGo.SetActive(value: false);
		if (Refresh())
		{
			mButton.onClick.AddListener(OnButtonClick);
			Services.Player.OnCollectOnceReward += MainPlayer_OnCollectOnceReward;
			mDisposableBag = default;
			mDisposableBag.Add(Services.SubscribeProgress(OnProgressUpdate));
			mDisposableBag.Add(Services.SubscribeState(OnStateUpdate));
			OnProgressUpdate(default);
			OnStateUpdate(default);
		}
		RegisterCheckpoint();
	}

	protected void OnDisable()
	{
		mDisposableBag.Dispose();
		if (Services.Player != null)
		{
			Services.Player.OnCollectOnceReward -= MainPlayer_OnCollectOnceReward;
		}
		mButton.onClick.RemoveListener(OnButtonClick);
		mRoot.SetActive(value: false);
		UnregisterCheckPoint();
	}

	private bool Refresh()
	{
		bool flag = Services.EnablePlayAsYouDownload && !Services.Player.HasCollectedOnceReward("PlayAsYouDownload");
		mRoot.SetActive(flag);
		mLayoutElement.ignoreLayout = !flag;
		return flag;
	}

	private void MainPlayer_OnCollectOnceReward(string onceAwardTyp)
	{
		Refresh();
	}

	private void OnButtonClick()
	{
		Services.PopupProgress((IHomeHudDownloadProgressDialog dialog) =>
		{
			dialog.Init(allowClickClose: true);
		});
	}

	private void RegisterCheckpoint()
	{
		int playAsYouDownloadCheckConditionId = Services.CheckConditionId;
		mCheckPointCondition = Services.GetCondition(playAsYouDownloadCheckConditionId);
		Services.ConditionHandler.Register(mCheckPointCondition, OnCheckPointConditionUpdate);
		OnCheckPointConditionUpdate();
	}

	private void UnregisterCheckPoint()
	{
		if (mCheckPointCondition != null)
		{
			Services.ConditionHandler.Deregister(mCheckPointCondition, OnCheckPointConditionUpdate);
			mCheckPointCondition = null;
		}
	}

	private void OnCheckPointConditionUpdate()
	{
		if (Services.IsMet(mCheckPointCondition) && !PlayAsYouDownloaded())
		{
			TryPopupPlayAsYouDownload().Forget();
		}
	}

	private bool TryLeaveMap()
	{
		if (Services.MapRootIdentity == null)
		{
			return false;
		}
		Services.LeaveMap().Forget();
		return true;
	}

	private async UniTask TryPopupPlayAsYouDownload()
	{
		await UniTask.WaitUntil(() => Services.IsSplashHidden);
		await Services.PopupProgressGlobal((IHomeHudDownloadProgressDialog dialog) =>
		{
			dialog.Init(allowClickClose: false);
		}, instant: false);
	}

	private bool PlayAsYouDownloaded()
	{
		return Services.PaydHandler.CurrentState == PaydState.Downloaded;
	}

	private void OnProgressUpdate(PlayAsYouDownloadProgressUpdateEvent data)
	{
		long currentDownloadBytes = Services.PaydHandler.CurrentDownloadBytes;
		long totalDownloadBytes = Services.PaydHandler.TotalDownloadBytes;
		float progress = 1f;
		if (totalDownloadBytes > 0)
		{
			progress = (float)currentDownloadBytes / (float)totalDownloadBytes;
		}
		RefreshProgress(progress);
	}

	private void RefreshProgress(float progress)
	{
		mProgress.SetKey($"{Math.Round(progress * 100f)}%");
		UpdateProgressRectTransform(progress);
	}

	private void UpdateProgressRectTransform(float progress)
	{
		float y = (float)HeightDiff * progress + (float)mHeightMin;
		foreach (RectTransform item in mProgressRectTransform)
		{
			Vector2 sizeDelta = item.sizeDelta;
			sizeDelta.y = y;
			item.sizeDelta = sizeDelta;
		}
	}

	private void OnStateUpdate(PlayAsYouDownloadStateUpdateEvent data)
	{
		RefreshRedDot();
		RefreshStateText();
		if (Services.PaydHandler.CurrentState == PaydState.Downloaded)
		{
			RefreshProgress(1f);
		}
	}

	private void RefreshRedDot()
	{
		bool flag = Services.Player.HasCollectedOnceReward("PlayAsYouDownload");
		bool flag2 = Services.PaydHandler.CurrentState == PaydState.Downloaded;
		bool active = !flag & flag2;
		mRedDotGo.SetActive(active);
	}

	private void RefreshStateText()
	{
		if (Services.PaydHandler != null)
		{
			string key = ((Services.PaydHandler.CurrentState == PaydState.Downloaded) ? "payd_downloaded" : "payd_downloading");
			if ((bool)mDownloadStateText)
			{
				mDownloadStateText.SetKey(key);
			}
		}
	}
}
}
