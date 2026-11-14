// Selected original 884 player-status implementation; storage/configuration boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomePlayerStatusRules
{
public sealed class PlayerStatusEffectManager
{
    private readonly long playerId;
    private readonly Func<int, StatusEffectInfo> configuration;
    private readonly Func<DateTime> clock;
    private readonly IDictionary<int, PlayerStatusEffectDataWrap> records;
    public PlayerStatusEffectManager(long playerId, IDictionary<int, PlayerStatusEffectDataWrap> records,
        Func<int, StatusEffectInfo> configuration, Func<DateTime> clock)
    {
        this.playerId = playerId; this.records = records ?? throw new ArgumentNullException(nameof(records));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    private bool TryGetValue(int id, out PlayerStatusEffectDataWrap value) => records.TryGetValue(id, out value);
    private void Add(int id, PlayerStatusEffectDataWrap value) => records.Add(id, value);
	public PlayerStatusEffectDataWrap GainStatusEffect(int classId, EStatusSourceType statusSourceType)
	{
		if (!TryGetValue(classId, out var obj))
		{
			PlayerStatusEffectDataWrap playerStatusEffectDataWrap = PlayerStatusEffectDataWrap.CreateNew(playerId, classId, configuration, clock);
			playerStatusEffectDataWrap.StatusSourceType = statusSourceType;
			Add(classId, playerStatusEffectDataWrap);
			return playerStatusEffectDataWrap;
		}
		obj.StatusSourceType = statusSourceType;
		obj.Reset();
		return obj;
	}

}

}
