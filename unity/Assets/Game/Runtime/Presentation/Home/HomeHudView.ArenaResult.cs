using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleResultUI;
using Zjcs.Presentation.Character;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private ArenaResultWindow arenaResult;
        private void ShowArenaResult(FightBattleCheckpoint checkpoint, FightBattleReport report, Action closed)
        {
            if (arenaResult) throw new InvalidOperationException("An arena result is already open.");
            var owner = new GameObject("ArenaResultWindow"); owner.transform.SetParent(host.transform, false);
            arenaResult = owner.AddComponent<ArenaResultWindow>();
            try
            {
                arenaResult.Initialize(host, canvas.transform, host.Arena.LastResult, checkpoint, report, closed,
                    NavigateArenaPromotion, (panel, native, state, final) => {
                        if (state == null) native.Bind(new LocalResultActions(null, null));
                        else BindNativeBattleResultReport(panel, native, state, final,
                            (presentation, adapter, lifetime) => arenaResult.BindReportIdentity(
                                presentation, adapter, lifetime, state));
                    });
            }
            catch { arenaResult.Release(); throw; }
        }
        private void NavigateArenaPromotion(CombatRatingPromoteWayType type)
        {
            // Original Skill/Equip handlers leave the fight before consuming
            // ConsoleData.CombatRatingPromoteWay on the home page.
            CloseArena();
            OpenCharacterPage();
            var view = characterRoot.GetComponent<OfficialCharacterPanelView>();
            view.OpenCombatPromotion(type == CombatRatingPromoteWayType.Skill);
        }
    }
}
