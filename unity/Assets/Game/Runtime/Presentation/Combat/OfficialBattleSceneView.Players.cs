using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private readonly OfficialBattlePlayerAppearanceSource playerAppearance;
        private OfflineProfessionBattlePlayerResources playerResources;

        private bool CanCreatePlayer(FightBattleUnitState state) => playerAppearance?.Matches(state) == true;

        public void BindPlayerClonePrefab(GameObject instance)
        {
            if (!instance) throw new ArgumentNullException(nameof(instance));
            if (instance.GetComponentsInChildren<MyPlayableSpineClone>(true).Length == 0) return;
            playerResources ??= new OfflineProfessionBattlePlayerResources();
            OfficialBattlePlayerClonePrefab.Bind(instance, playerResources);
        }

        private UnitView TryCreatePlayer(FightBattleUnitState state)
        {
            if (!CanCreatePlayer(state)) return null;
            if (nativePlayableScope == null)
                throw new InvalidOperationException("Original player views require the battle's native skill graph scope.");
            playerResources ??= new OfflineProfessionBattlePlayerResources();
            var model = new OfficialBattlePlayerNativeModelView(state, root, nativePlayableScope, playerResources,
                playerAppearance.PlayerId, playerAppearance.ReadPlayer, playerAppearance.ItemType);
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
