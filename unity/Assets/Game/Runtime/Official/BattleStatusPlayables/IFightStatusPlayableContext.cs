using System;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
    // Boundary to the existing local status entity. Notifications are emitted at
    // the corresponding component transitions, rather than inferred from frames.
    public interface IFightStatusPlayableState
    {
        int StackedCount { get; }
        bool CanStackCount { get; }
        bool Forbidden { get; }
        event Action<IFightStatusPlayableState, int, int> OnStatusStackUpdate;
        event Action<IFightStatusPlayableState> OnInvoked;
        event Action<IFightStatusPlayableState, bool> OnForbiddenChanged;
    }

    public interface IFightStatusPlayableContext
    {
        IFightStatusPlayableState State { get; }
        GameObject Owner { get; }
        GameObject Creator { get; }
        GameObject Master { get; }
        MyFightStatusAgent Agent { get; }
        int? ShareId { get; }
        bool HasIcon { get; }
    }
}
