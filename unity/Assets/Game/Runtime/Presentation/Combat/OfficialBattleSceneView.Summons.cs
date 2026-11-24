using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private readonly Dictionary<int, FightBattleUnitState> liveSummons = new Dictionary<int, FightBattleUnitState>();
        private OfficialBattleUnitSpawnPlayback unitSpawns;
        private OfficialBattleUnitSpawnPlayback UnitSpawns => unitSpawns
            ?? (unitSpawns = new OfficialBattleUnitSpawnPlayback(CreateSummonView,
                id => Body(id) != null || restoredNativeUnitIds != null && restoredNativeUnitIds.Contains(id), CreateLiveSummonView));

        private void RetainLiveSummons(FightBattleCheckpoint checkpoint, HashSet<int> present)
        {
            foreach (var pair in new List<KeyValuePair<int, FightBattleUnitState>>(liveSummons))
            {
                if (checkpoint.Units.Exists(unit => unit != null && unit.Id == pair.Key))
                    liveSummons.Remove(pair.Key);
                else if (Body(pair.Key) != null)
                    present.Add(pair.Key);
            }
        }

        private void CreateLiveSummonView(FightBattleUnitState state, FightBattleHitSelectionSnapshot.SpawnedUnit receipt)
        {
            if (state.Hp <= 0 || state.Ended || liveSummons.ContainsKey(state.Id))
                throw new System.InvalidOperationException("The live summon state is invalid.");
            UnitView view = GetOrCreate(state, new FightGridPoint(receipt.X, receipt.Y));
            if (view == null || Body(state.Id) == null)
                throw new System.InvalidOperationException("The live summon model is unavailable: " + state.Id);
            liveSummons.Add(state.Id, state);
            view.State = state;
            var roster = new List<FightBattleUnitState>(latestCheckpoint.Units) { state };
            view.Hud?.Refresh(state, roster);
            var body = Body(state.Id);
            orientUnit(body, state);
            body.position = gridToWorld(new FightGridPoint(receipt.X, receipt.Y));
            RefreshVoodooLinks();
        }

        private void CreateSummonView(FightBattleHitSelectionSnapshot.SpawnedUnit receipt)
        {
            var state = latestCheckpoint?.Units.Find(value => value != null && value.Id == receipt.UnitId);
            state = UnitLifePlayback.Project(state);
            if (state == null || state.Hp <= 0 || state.Ended && !UnitEndPlayback.Retains(state.Id)) return;
            UnitView view = GetOrCreate(state, new FightGridPoint(receipt.X, receipt.Y));
            if (view == null) return;
            view.State = state;
            view.Hud?.Refresh(state, latestCheckpoint.Units);
            var body = Body(state.Id);
            if (!body) return;
            orientUnit(body, state);
            body.position = gridToWorld(new FightGridPoint(receipt.X, receipt.Y));
        }
    }
}
