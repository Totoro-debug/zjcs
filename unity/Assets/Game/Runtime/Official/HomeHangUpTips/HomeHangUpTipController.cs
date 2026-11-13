// Original selected 884 bed/cart tip logic; explicit owner and scheduling boundaries.
using System;
using System.Collections.Generic;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneUiControls;
namespace Zjcs.Official.HomeHangUpTips
{
public sealed class HomeHangUpTipController : IDisposable
{
    private readonly IHomeHangUpTipServices Services;
    private readonly EFurniture mFurniture;
    private readonly List<int> mHangUpIdList = new List<int>();
    private int mTipLevel = -1;
    private IDisposable mTiming;
    private bool enabled, started, disposed;
    public int TipLevel => mTipLevel;
    public HomeHangUpTipController(EFurniture furniture, IHomeHangUpTipServices services)
    {
        mFurniture = furniture; Services = services ?? throw new ArgumentNullException(nameof(services));
    }
    public void Start()
    {
        if (disposed) throw new ObjectDisposedException(nameof(HomeHangUpTipController));
        if (started) return; started = true;
        Services.MainPlayer.OnHangUpUnlock += OnHangUpUnlock;
        Services.MainPlayer.OnRepairFurniture += OnRepairFurniture;
    }
    public void Enable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(HomeHangUpTipController));
        if (enabled) return; enabled = true;
        if (mFurniture == EFurniture.Bed) Services.MainPlayer.OnLevelUpdate += OnLevelUpdate;
        Services.MainPlayer.OnDebugTimeOffset += OnDebugTimeOffset;
        Services.MainPlayer.OnHangUpDataChanged += OnHangUpDataChanged;
        TryBeginHangUp();
    }
    public void Disable()
    {
        if (!enabled) return; enabled = false;
        if (mFurniture == EFurniture.Bed) Services.MainPlayer.OnLevelUpdate -= OnLevelUpdate;
        Services.MainPlayer.OnHangUpDataChanged -= OnHangUpDataChanged;
        Services.MainPlayer.OnDebugTimeOffset -= OnDebugTimeOffset;
        StopHangUpTiming();
    }
    public void Dispose()
    {
        if (disposed) return; Disable(); StopHangUpTiming(); disposed = true;
        if (started) {
            Services.MainPlayer.OnHangUpUnlock -= OnHangUpUnlock;
            Services.MainPlayer.OnRepairFurniture -= OnRepairFurniture;
        }
    }
    private void StopHangUpTiming() { mTiming?.Dispose(); mTiming = null; }
    private static double HourToSecond(double hour) => hour * 3600.0;
	public bool CanReceiveAward()
	{
		if (Services.MainPlayer.GetHangUpQuickReceiveState(mFurniture))
		{
			return mTipLevel >= 0;
		}
		return false;
	}

	private void OnHangUpUnlock(int hangUpId)
	{
		if (Services.RecordHangUpIdDict.TryGetValue(mFurniture, out var value) && value.Contains(hangUpId))
		{
			RefreshHangUpIdList();
			BeginHangUp();
		}
	}

	private void OnHangUpDataChanged(int hangUpId)
	{
		if (Services.RecordHangUpIdDict.TryGetValue(mFurniture, out var value) && value.Contains(hangUpId))
		{
			BeginHangUp();
		}
	}

	private void OnRepairFurniture(EFurniture furniture)
	{
		if (furniture == mFurniture)
		{
			RefreshHangUpIdList();
			BeginHangUp();
		}
	}

	private void OnDebugTimeOffset(long timeOffset)
	{
		if (Services.MainPlayer.HasFurniture(mFurniture))
		{
			BeginHangUp();
		}
	}

	public void TryBeginHangUp()
	{
		if (!Services.MainPlayer.HasFurniture(mFurniture))
		{
			ResetTipLevel();
			return;
		}
		RefreshHangUpIdList();
		BeginHangUp();
	}

	private void RefreshHangUpIdList()
	{
		mHangUpIdList.Clear();
		IHomeHangUpTipPlayer playerData = Services.PlayerData;
		if (!Services.RecordHangUpIdDict.TryGetValue(mFurniture, out var value))
		{
			return;
		}
		foreach (int item in value)
		{
			if (playerData.HangUpDataDict.TryGetValue(item, out var value2) && value2.IsUnlock)
			{
				mHangUpIdList.Add(item);
			}
		}
	}

	private void BeginHangUp()
	{


		ResetTipLevel();
		GetMaxFillPercentHangUp(out var hangUpId, out var rewardCount, out var capacity);
		if (hangUpId == 0)
		{
			return;
		}
		Services.Settings.TryGetHangUpTipSetting(mFurniture, out var setting);
		double hangUpHourOutput = Services.PlayerData.GetHangUpHourOutput(hangUpId);
		long waitTipTime = 0L;
		List<float> tipLevelList = setting.TipLevelList;
		for (int num = tipLevelList.Count - 1; num >= 0; num--)
		{
			long num2 = (long)((float)capacity * tipLevelList[num]);
			if (rewardCount > 0 && rewardCount >= num2)
			{
				mTipLevel = num;
				break;
			}
		}
		if (mTipLevel != tipLevelList.Count - 1)
		{
			int index = mTipLevel + 1;
			float num3 = (float)capacity * tipLevelList[index];
			waitTipTime = (long)Math.Ceiling(HourToSecond((double)Math.Max(1f, num3 - (float)rewardCount) / hangUpHourOutput));
		}
		if (mTipLevel == -1)
		{
			mTipLevel = CustomTipLevel(rewardCount);
		}
		Services.Publish(new HangUpTipLevelChangeData
		{
			TipLevel = mTipLevel,
			FurnitureType = mFurniture
		});
		Services.SetTipState(mFurniture, mTipLevel);
		StopHangUpTiming();
		if (Services.InMyHome)
		{
			if (waitTipTime > 0) mTiming = Services.Wait((float)waitTipTime, TryBeginHangUp);
		}
	}

	private void GetMaxFillPercentHangUp(out int hangUpId, out long rewardCount, out long capacity)
	{
		rewardCount = 0L;
		capacity = 0L;
		hangUpId = 0;
		float num = 0f;
		IHomeHangUpTipPlayer playerData = Services.PlayerData;
		for (int i = 0; i < mHangUpIdList.Count; i++)
		{
			int num2 = mHangUpIdList[i];
			long hangUpAwardCount = playerData.GetHangUpAwardCount(num2);
			long hangUpMaxCount = playerData.GetHangUpMaxCount(num2);
			float num3 = 1f * (float)hangUpAwardCount / (float)hangUpMaxCount;
			if (i == 0 || num3 > num)
			{
				num = num3;
				hangUpId = num2;
				rewardCount = hangUpAwardCount;
				capacity = hangUpMaxCount;
			}
		}
	}

	private void ResetTipLevel()
	{
		mTipLevel = -1;
		Services.SetTipState(mFurniture, mTipLevel);
	}
	private int CustomTipLevel(long rewardCount)
	{
		if (mFurniture != EFurniture.Bed) return -1;
		IHomeHangUpTipPlayer mainPlayer = Services.MainPlayer;
		long levelUpExp = Services.GetLevelUpExp(mainPlayer.Level);
		long num = (long)((float)levelUpExp * Services.Settings.ExpForLevelUpPercentTip);
		if (levelUpExp <= 0)
		{
			return -1;
		}
		if (rewardCount < num)
		{
			return -1;
		}
		return 0;
	}
	private void OnLevelUpdate(int level)
	{
		if (!Services.MainPlayer.CanGetExp())
		{
			TryBeginHangUp();
		}
	}
}
}
