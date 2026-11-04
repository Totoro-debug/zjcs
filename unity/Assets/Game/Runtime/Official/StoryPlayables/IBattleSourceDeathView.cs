using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
    // Scene-owned equivalent of the original ECFightRoleCharacterView lookup.
    public interface IBattleSourceDeathView
    {
        void ForcePlayDiePlayable(GameObject source);
    }

    public interface IBattleSourceDeathPlayable
    {
        void BindSourceDeathView(IBattleSourceDeathView view);
    }
}
