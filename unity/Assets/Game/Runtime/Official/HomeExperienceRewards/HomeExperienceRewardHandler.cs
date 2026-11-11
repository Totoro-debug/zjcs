// Migrated selected official implementation; service ports only.
using Zjcs.Official.HomeHudUI;
namespace Zjcs.Official.HomeExperienceRewards
{
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class HomeExperienceRewardHandler : IHomeExperienceRewards, IDisposable
{
    private readonly IHomeExperienceRewardBindings Bindings;
    private bool active;
    public HomeExperienceRewardHandler(IHomeExperienceRewardBindings bindings)
    {
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }
    public void Activate()
    {
        if (active) return;
        active = true;
        OnActivate();
    }
    public void Dispose()
    {
        if (!active) return;
        OnDeactivate();
        active = false;
    }

	private enum Status
	{
		Start,
		GetExp,
		UpgradeDelay,
		UpgradeDisplay,
		End
	}

	private List<Func<long, long, IEnumerator>> mRewardExpDisplays = new List<Func<long, long, IEnumerator>>();

	private List<Func<bool>> mUpgradeDisplayDelays = new List<Func<bool>>();

	private List<Action<int, int>> mUpgradeDisplays = new List<Action<int, int>>();

	private Dictionary<Action<int, int>, float> mUpgradeDisplayTimes = new Dictionary<Action<int, int>, float>();

	private bool mIsUpgrade;

	private CancellationTokenSource mCancellationTokenSource;

	private bool mIsCombatRatingBlock;

	private float mStartTime;

	private Status mStatus;

	public HomeRewardSignal<long, long> OnPlayerExpUpdated { get; private set; } = new HomeRewardSignal<long, long>();

	public long HandledExp
	{
		get
		{
			if (!Bindings.Store.Inited)
			{
				return PlayerRealExp;
			}
			return Bindings.Store.HandledExp;
		}
		private set
		{
			Bindings.Store.HandledExp = value;
		}
	}

	public long PlayerRealExp => Bindings.Player.GetCurrentExp();

	public int HandledLevel
	{
		get
		{
			if (!Bindings.Store.Inited)
			{
				return PlayerRealLevel;
			}
			return Bindings.Store.HandledLevel;
		}
		private set
		{
			Bindings.Store.HandledLevel = value;
		}
	}

	public int PlayerRealLevel => Bindings.Player.Level;

	public bool IsUpgrade => mIsUpgrade;

	public void RegisterRewardExpDisplay(Func<long, long, IEnumerator> rewardExpDisplayDel)
	{
		mRewardExpDisplays.Add(rewardExpDisplayDel);
		if (HandledExp != PlayerRealExp)
		{
			Play();
		}
	}

	public void UnregisterRewardExpDisplay(Func<long, long, IEnumerator> rewardExpDisplayDel)
	{
		mRewardExpDisplays.Remove(rewardExpDisplayDel);
	}

	public void RegisterUpgradeDisplay(Action<int, int> levelUpgradeDisplayDel, float time)
	{
		mUpgradeDisplays.Add(levelUpgradeDisplayDel);
		mUpgradeDisplayTimes.Add(levelUpgradeDisplayDel, time);
	}

	public void UnregisterUpgradeDisplay(Action<int, int> levelUpgradeDisplayDel)
	{
		mUpgradeDisplays.Remove(levelUpgradeDisplayDel);
		mUpgradeDisplayTimes.Remove(levelUpgradeDisplayDel);
	}

	public void RegisterUpgradeDelay(Func<bool> upgradeDisplayDelay)
	{
		PlayEnd();
		mUpgradeDisplayDelays.Add(upgradeDisplayDelay);
		Play();
	}

	public void UnregisterUpgradeDelay(Func<bool> upgradeDisplayDelay)
	{
		PlayEnd();
		mUpgradeDisplayDelays.Remove(upgradeDisplayDelay);
		Play();
	}

	private void OnActivate()
	{
		Bindings.Player.OnItemCountUpdate += OnPlayerItemCountUpdate;
		Bindings.Player.OnLevelUpdate += OnPlayerLevelUpdate;
		Bindings.Player.OnSubRankUpdate += MainPlayer_OnSubRankUpdate;
		Bindings.OnPlayerUpdate.AddListener(OnPlayerUpdate);
		Bindings.Player.OnBlessExpUpdate += MainPlayer_OnBlessExpUpdate;
		IHomeExperienceRewardPlayer mainPlayer = Bindings.Player;
		mainPlayer.OnBlessLevelUp += MainPlayer_OnBlessLevelUp;
		Bindings.Player.OnSeasonRecordDataEndUpdate += MainPlayer_OnSeasonRecordDataUpdate;
		RegisterUpgradeDisplay(DefUpgradeDisplay, 2.2f);
		mIsCombatRatingBlock = false;
		mIsUpgrade = false;
	}

	private void OnDeactivate()
	{
		if (Bindings.Player != null)
		{
			Bindings.Player.OnItemCountUpdate -= OnPlayerItemCountUpdate;
			Bindings.Player.OnLevelUpdate -= OnPlayerLevelUpdate;
			Bindings.Player.OnSubRankUpdate -= MainPlayer_OnSubRankUpdate;
			Bindings.Player.OnBlessExpUpdate -= MainPlayer_OnBlessExpUpdate;
			IHomeExperienceRewardPlayer mainPlayer = Bindings.Player;
			mainPlayer.OnBlessLevelUp -= MainPlayer_OnBlessLevelUp;
			Bindings.Player.OnSeasonRecordDataEndUpdate -= MainPlayer_OnSeasonRecordDataUpdate;
		}
		if (Bindings.ManagerAvailable)
		{
			Bindings.OnPlayerUpdate.RemoveListener(OnPlayerUpdate);
		}
		PlayEnd();
		OnPlayerExpUpdated.ClearListener();
		mRewardExpDisplays.Clear();
		mUpgradeDisplayDelays.Clear();
		mUpgradeDisplays.Clear();
		mUpgradeDisplayTimes.Clear();
	}

	private void OnPlayerUpdate()
	{
		PlayEnd();
	}

	private void MainPlayer_OnSubRankUpdate(int obj)
	{
		Play();
	}

	private void MainPlayer_OnSeasonRecordDataUpdate(object obj)
	{
		Play();
	}

	private void MainPlayer_OnBlessExpUpdate(long pre, long cur)
	{
		OnPlayerExpUpdated?.Invoke(pre, cur);
		Play();
	}

	private void MainPlayer_OnBlessLevelUp(int pre, int cur)
	{
		if (pre >= cur)
		{
			Play();
		}
	}

	private void OnPlayerLevelUpdate(int level)
	{
		mIsUpgrade = level > HandledLevel;
	}

	private void RegisterCombatRatingBlocker()
	{
		if (!mIsCombatRatingBlock)
		{
			if (Bindings.CombatRatingBlockers != null)
			{
				Bindings.CombatRatingBlockers.RegisterBlocker(this);
			}
			mIsCombatRatingBlock = true;
		}
	}

	private void UnregisterCombatRatingBlocker()
	{
		if (mIsCombatRatingBlock)
		{
			if (Bindings.CombatRatingBlockers != null)
			{
				Bindings.CombatRatingBlockers.UnregisterBlocker(this);
			}
			mIsCombatRatingBlock = false;
		}
	}

	private void OnPlayerItemCountUpdate(HomeExperienceItem itemData, long preCount, object awardSourceData)
	{
		if (itemData.Id == Bindings.PlayerExpItemId)
		{
			OnPlayerExpUpdated?.Invoke(preCount, Bindings.Player.GetItemAmount(itemData.Id));
			Play();
		}
	}

	private void Play()
	{
		Clear();
		RegisterCombatRatingBlocker();
		mCancellationTokenSource = new CancellationTokenSource();
		mStartTime = Time.time;
		mStatus = Status.Start;
		Play(mCancellationTokenSource.Token).Forget();
	}

	private async UniTaskVoid Play(CancellationToken cancellationToken)
	{
		await UniTask.NextFrame(cancellationToken);
		if (mRewardExpDisplays.Count == 0)
		{
			PlayEnd();
			return;
		}
		mStatus = Status.GetExp;
		await PlayerGetExp(cancellationToken);
		if (mRewardExpDisplays.Count != 0)
		{
			mStatus = Status.UpgradeDelay;
			await PlayUpgradeDelay(cancellationToken);
		}
		if (mUpgradeDisplays.Count != 0)
		{
			mStatus = Status.UpgradeDisplay;
			await PlayUpgradeDisplay(cancellationToken);
		}
		PlayEnd();
	}

	private async UniTask PlayerGetExp(CancellationToken cancellationToken)
	{
		List<IEnumerator> list = new List<IEnumerator>();
		long handledExp = HandledExp;
		long playerRealExp = PlayerRealExp;
		foreach (Func<long, long, IEnumerator> mRewardExpDisplay in mRewardExpDisplays)
		{
			list.Add(mRewardExpDisplay(handledExp, playerRealExp));
		}
		if (list.Count != 0)
		{
			HandledExp = playerRealExp;
			await PlayRewardExpDisplay(list, cancellationToken);
		}
	}

	private async UniTask PlayRewardExpDisplay(List<IEnumerator> displayers, CancellationToken cancellationToken)
	{
		while (displayers.Count != 0 && !cancellationToken.IsCancellationRequested)
		{
			for (int num = displayers.Count - 1; num >= 0; num--)
			{
				if (!displayers[num].MoveNext())
				{
					displayers.RemoveAt(num);
				}
			}
			await UniTask.NextFrame(cancellationToken);
		}
	}

	private async UniTask PlayUpgradeDelay(CancellationToken cancellationToken)
	{
		_ = HandledExp;
		_ = PlayerRealExp;
		int count = mUpgradeDisplayDelays.Count;
		int delayCount = count;
		while (delayCount != 0 && !cancellationToken.IsCancellationRequested)
		{
			delayCount = count;
			foreach (Func<bool> mUpgradeDisplayDelay in mUpgradeDisplayDelays)
			{
				if (!mUpgradeDisplayDelay())
				{
					delayCount--;
				}
			}
			await UniTask.NextFrame(cancellationToken);
		}
	}

	private async UniTask PlayUpgradeDisplay(CancellationToken cancellationToken)
	{
		if (!mIsUpgrade)
		{
			HandledLevel = PlayerRealLevel;
			return;
		}
		Action<int, int> playerLevelUpgradeDisplayDel = mUpgradeDisplays[mUpgradeDisplays.Count - 1];
		playerLevelUpgradeDisplayDel(HandledLevel, PlayerRealLevel);
		HandledLevel = PlayerRealLevel;
		mIsUpgrade = false;
		await UniTask.WaitForSeconds(mUpgradeDisplayTimes[playerLevelUpgradeDisplayDel], ignoreTimeScale: false, PlayerLoopTiming.Update, cancellationToken);
	}

	private void DefUpgradeDisplay(int preLevel, int postLevel)
	{
		Bindings.ShowLevelUpNotification(preLevel);
	}

	private void PlayEnd()
	{
		mStatus = Status.End;
		UnregisterCombatRatingBlocker();
		Clear();
	}

	private void Clear()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}

	public string GetBlockMessage()
	{
		return string.Format("{0} BlockTime: {1} mStart: {2} message {3}", "IPlayerExpRewardHandler", Time.time - mStartTime, mStatus, GetStatusBlockMessage());
	}

	private string GetStatusBlockMessage()
	{
		return $"mUpgradeDisplayDelays: {mUpgradeDisplayDelays.Count}, mUpgradeDisplayTimes: {mUpgradeDisplayTimes.Count}, mRewardExpDisplays: {mRewardExpDisplays.Count}";
	}
}

}
