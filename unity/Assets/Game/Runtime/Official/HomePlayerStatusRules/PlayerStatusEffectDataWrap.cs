// Selected original 884 player-status implementation; storage/configuration boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomePlayerStatusRules
{
public sealed class PlayerStatusEffectDataWrap
{
    public PlayerStatusEffectData Data { get; }
    private readonly Func<int, StatusEffectInfo> configuration;
    private readonly Func<DateTime> clock;
    public PlayerStatusEffectDataWrap(PlayerStatusEffectData data, Func<int, StatusEffectInfo> configuration, Func<DateTime> clock)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    public StatusEffectInfo Info => configuration(ClassId) ?? throw new InvalidOperationException("Unbound status configuration: " + ClassId);
    public List<long> OccupiedFightIds => Data.OccupiedFightIds ?? (Data.OccupiedFightIds = new List<long>());
    public long PlayerId { get => Data.PlayerId; set => Data.PlayerId = value; }
    public int ClassId { get => Data.ClassId; set => Data.ClassId = value; }
    public EStatusSourceType StatusSourceType { get => Data.StatusSourceType; set => Data.StatusSourceType = value; }
    public int ActivedTimes { get => Data.ActivedTimes; set => Data.ActivedTimes = value; }
    public bool IsActive { get => Data.IsActive; set => Data.IsActive = value; }
    public DateTime LastGainedTime { get => Data.LastGainedTime; set => Data.LastGainedTime = value; }
    public bool LastIsReduceOncePerPlay { get => Data.LastIsReduceOncePerPlay; set => Data.LastIsReduceOncePerPlay = value; }
	public static PlayerStatusEffectDataWrap CreateNew(long playerId, int classId, Func<int, StatusEffectInfo> configuration, Func<DateTime> clock)
	{
		var playerStatusEffectDataWrap = new PlayerStatusEffectDataWrap(new PlayerStatusEffectData(), configuration, clock);
		playerStatusEffectDataWrap.PlayerId = playerId;
		playerStatusEffectDataWrap.ClassId = classId;
		playerStatusEffectDataWrap.ActivedTimes = 0;
		playerStatusEffectDataWrap.IsActive = true;
		playerStatusEffectDataWrap.LastGainedTime = clock();
		playerStatusEffectDataWrap.LastIsReduceOncePerPlay = playerStatusEffectDataWrap.Info.IsReduceOncePerPlay;
		return playerStatusEffectDataWrap;
	}

	public int GetKey()
	{
		return ClassId;
	}

	public void Reset()
	{
		IsActive = true;
		ActivedTimes = 0;
		OccupiedFightIds.Clear();
		LastGainedTime = clock();
		LastIsReduceOncePerPlay = Info.IsReduceOncePerPlay;
	}

	public bool TryResetActivedTimesOnReduceOncePerPlayChanged()
	{
		bool isReduceOncePerPlay = Info.IsReduceOncePerPlay;
		if (LastIsReduceOncePerPlay == isReduceOncePerPlay)
		{
			return false;
		}
		LastIsReduceOncePerPlay = isReduceOncePerPlay;
		if (ShouldBeRemove())
		{
			return false;
		}
		if (IsActive && ActivedTimes <= 0)
		{
			return false;
		}
		ActivedTimes = 0;
		IsActive = true;
		return true;
	}

	public void Deactivate()
	{
		IsActive = false;
		OccupiedFightIds.Clear();
	}

	public int GetRemainTimes()
	{
		if (Info.MaxActivateTimes < 0)
		{
			return Info.MaxActivateTimes;
		}
		return Math.Max(0, Info.MaxActivateTimes - ActivedTimes);
	}

	public int GetDisplayRemainTimes()
	{
		if (IsActive)
		{
			return GetRemainTimes();
		}
		if (OccupiedFightIds.Count > 0)
		{
			return 1;
		}
		return 0;
	}

	public bool ShouldBeRemove()
	{
		if (IsActive)
		{
			return false;
		}
		return OccupiedFightIds.Count <= 0;
	}

	public bool IsActiveForFight(long fightUniqueId)
	{
		if (IsActive)
		{
			return true;
		}
		if (fightUniqueId == 0L)
		{
			return false;
		}
		return IsOccupiedBy(fightUniqueId);
	}

	public bool IsOccupiedBy(long fightUniqueId)
	{
		return OccupiedFightIds.Contains(fightUniqueId);
	}

	public bool TryOccupy(long fightUniqueId)
	{
		if (IsOccupiedBy(fightUniqueId))
		{
			return false;
		}
		OccupiedFightIds.Add(fightUniqueId);
		return true;
	}

	public void ReleaseFinishedOccupies(Func<long, bool> isPlayRunning)
	{
		List<long> occupiedFightIds = OccupiedFightIds;
		for (int num = occupiedFightIds.Count - 1; num >= 0; num--)
		{
			if (!isPlayRunning(occupiedFightIds[num]))
			{
				occupiedFightIds.RemoveAt(num);
			}
		}
	}

}

}
