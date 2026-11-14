using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private OfficialBattleUnitSpawnPlayback unitSpawns;
        private OfficialBattleUnitSpawnPlayback UnitSpawns => unitSpawns
            ?? (unitSpawns = new OfficialBattleUnitSpawnPlayback(CreateSummonView,
                id => Body(id) != null || restoredNativeUnitIds != null && restoredNativeUnitIds.Contains(id)));

        private void CreateSummonView(FightBattleHitSelectionSnapshot.SpawnedUnit receipt)
        {
            var state = latestCheckpoint?.Units.Find(value => value != null && value.Id == receipt.UnitId);
            if (state == null || state.Hp <= 0 || state.Ended) return;
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
