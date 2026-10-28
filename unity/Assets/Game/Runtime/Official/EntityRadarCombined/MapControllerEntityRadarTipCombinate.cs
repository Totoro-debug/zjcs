// Original combined radar tip; see map14 combined-tip source receipt.
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityRadar;
namespace Zjcs.Official.EntityRadarCombined
{

public class MapControllerEntityRadarTipCombinate : MapControllerEntityRadarTipBase
{
	[SerializeField]
	private float mHideTime = 0.5f;

	[SerializeField]
	private List<MapControllerEntityRadarTipBase> mMapControllerEntityRadarTipPrefabs;

	private List<MapControllerEntityRadarTipBase> mMapControllerEntityRadarTips = new List<MapControllerEntityRadarTipBase>();

	private CancellationTokenSource mCancellationToken;

	private bool mIsFadeOut;

	public override RadarTipType TipType => RadarTipType.Combinate;

	private void OnEnable()
	{
		if (mIsFadeOut)
		{
			OnHideAnimCompleted();
			mIsFadeOut = false;
		}
	}

	private void OnDisable()
	{
		ClearToken();
	}

	public override bool CanShowTip(MyEntity entity)
	{
		foreach (MapControllerEntityRadarTipBase mMapControllerEntityRadarTipPrefab in mMapControllerEntityRadarTipPrefabs)
		{
			if (mMapControllerEntityRadarTipPrefab.CanShowTip(entity))
			{
				return true;
			}
		}
		return false;
	}

	public override void Init(MyEntity myEnt)
	{
		base.Init(myEnt);
		ClearTips();
		CreateTips(myEnt);
		foreach (MapControllerEntityRadarTipBase mMapControllerEntityRadarTip in mMapControllerEntityRadarTips)
		{
			mMapControllerEntityRadarTip.Init(myEnt);
		}
	}

	public override void Hide(bool immidietaly, Action<MapControllerEntityRadarTipBase> hideCallback = null)
	{

		foreach (MapControllerEntityRadarTipBase mMapControllerEntityRadarTip in mMapControllerEntityRadarTips)
		{
			mMapControllerEntityRadarTip.Hide(immidietaly);
		}
		if (immidietaly)
		{
			mOnHideAnimCompletedCallback = hideCallback;
			OnHideAnimCompleted();
			return;
		}
		base.Hide(immidietaly);
		ClearToken();
		mCancellationToken = new CancellationTokenSource();
		UniTaskExtensions.Forget(DelayHide(mCancellationToken.Token, hideCallback));
	}

	public override void Show()
	{
		base.Show();
		foreach (MapControllerEntityRadarTipBase mMapControllerEntityRadarTip in mMapControllerEntityRadarTips)
		{
			mMapControllerEntityRadarTip.Show();
		}
		ClearToken();
		mIsFadeOut = false;
	}

	private void CreateTips(MyEntity myEnt)
	{
		foreach (MapControllerEntityRadarTipBase mMapControllerEntityRadarTipPrefab in mMapControllerEntityRadarTipPrefabs)
		{
			if (mMapControllerEntityRadarTipPrefab.CanShowTip(myEnt))
			{
				MapControllerEntityRadarTipBase tip = MapControllerEntityRadarTipManager.Instance.GetTip(((Component)this).transform, mMapControllerEntityRadarTipPrefab);
				mMapControllerEntityRadarTips.Add(tip);
			}
		}
	}

	private void ClearTips()
	{
		foreach (MapControllerEntityRadarTipBase mMapControllerEntityRadarTip in mMapControllerEntityRadarTips)
		{
			MapControllerEntityRadarTipManager.Instance.RecycleTip(mMapControllerEntityRadarTip);
		}
		mMapControllerEntityRadarTips.Clear();
	}

    // Async syntax reconstructed directly from the original MoveNext above.
    private async UniTask DelayHide(CancellationToken cancellationToken, Action<MapControllerEntityRadarTipBase> hideCallback = null)
    {
        mOnHideAnimCompletedCallback = hideCallback;
        mIsFadeOut = true;
        await UniTask.WaitForSeconds(mHideTime, false, (PlayerLoopTiming)8, cancellationToken, false);
        ClearTips();
        OnHideAnimCompleted();
        mIsFadeOut = false;
        ClearToken();
    }

	private void ClearToken()
	{
		if (mCancellationToken != null)
		{
			mCancellationToken.Cancel();
			mCancellationToken.Dispose();
			mCancellationToken = null;
		}
	}
}

}
