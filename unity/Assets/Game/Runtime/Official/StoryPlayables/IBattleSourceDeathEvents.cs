using System;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
    // Scene equivalent of FightRoleCharacterComponent.OnDie subscriptions.
    public interface IBattleSourceDeathEvents
    {
        IDisposable SubscribeSourceDeath(GameObject source, Action onDie);
    }
}
