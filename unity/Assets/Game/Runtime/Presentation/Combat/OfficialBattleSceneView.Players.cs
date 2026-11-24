using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private readonly OfficialBattlePlayerAppearanceSource playerAppearance;
        private readonly Func<FightBattleUnitState, OfficialBattlePlayerAppearanceSource> playerAppearanceResolver;
        private OfflineProfessionBattlePlayerResources playerResources;

        private OfficialBattlePlayerAppearanceSource ResolvePlayerAppearance(FightBattleUnitState state)
        {
            var source = playerAppearance?.Matches(state) == true ? playerAppearance
                : playerAppearanceResolver?.Invoke(state);
            return source?.Matches(state) == true ? source : null;
        }

        private bool CanCreatePlayer(FightBattleUnitState state) => ResolvePlayerAppearance(state) != null;

        public void BindPlayerClonePrefab(GameObject instance)
        {
            if (!instance) throw new ArgumentNullException(nameof(instance));
            if (instance.GetComponentsInChildren<MyPlayableSpineClone>(true).Length == 0) return;
            playerResources ??= new OfflineProfessionBattlePlayerResources();
            OfficialBattlePlayerClonePrefab.Bind(instance, playerResources);
        }

        private UnitView TryCreatePlayer(FightBattleUnitState state)
        {
            if (state.IsVoodooDoll) return null;
            var appearance = ResolvePlayerAppearance(state);
            if (appearance == null) return null;
            if (nativePlayableScope == null)
                throw new InvalidOperationException("Original player views require the battle's native skill graph scope.");
            playerResources ??= new OfflineProfessionBattlePlayerResources();
            var model = new OfficialBattlePlayerNativeModelView(state, root, nativePlayableScope, playerResources,
                appearance.PlayerId, appearance.ReadPlayer, appearance.ItemType);
            var view = new UnitView { State = state, NativeModel = model };
            try
            {
                PlayAnimation(view, state.Hp <= 0 ? "die_01" : "idle_01", state.Hp > 0);
                view.Hud = OfficialBattleUnitWorldHudView.Create(hudCatalog, state, model.Transform);
                units.Add(state.Id, view);
                return view;
            }
            catch { view.Hud?.Dispose(); model.Dispose(); throw; }
        }
    }
}
