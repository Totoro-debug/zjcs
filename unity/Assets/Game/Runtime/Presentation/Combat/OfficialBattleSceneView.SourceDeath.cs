using UnityEngine;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView : IBattleSourceDeathView
    {
        void IBattleSourceDeathView.ForcePlayDiePlayable(GameObject source)
        {
            if (source == null || latestCheckpoint?.Units == null) return;
            foreach (var state in latestCheckpoint.Units)
            {
                if (state == null || state.Hp > 0 || Body(state.Id)?.gameObject != source) continue;
                // ECFightRoleCharacterView first stops the existing death playable,
                // then starts it again. Never synthesize a removed entity's view.
                RemovePendingAnimations(state.Id);
                if (attachedTransforms.TryGetValue(state.Id, out var attached) && attached != null)
                {
                    var view = GetOrCreateAttachedAnimator(state, attached);
                    if (view == null) return;
                    StopAttachedPlayables(view);
                    view.Dying = false;
                    view.DeathFinished = false;
                    if (PlayAttachedAnimation(view, "die_01", false, restart: true)) view.Dying = true;
                }
                else if (units.TryGetValue(state.Id, out var view))
                {
                    view.State = state;
                    view.Dying = false;
                    if (PlayAnimation(view, "die_01", false, restart: true)) view.Dying = true;
                }
                return;
            }
        }
    }
}
