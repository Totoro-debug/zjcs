"""Restore the current native Desperate Valor current-HP stack status."""
from pathlib import Path
from official_source import OfficialSource
from current_battle_native_skill_graphs import verified_noise_mask_binding
from current_battle_native_status_graphs import build_current_status_graphs

if __name__ == '__main__':
    build_current_status_graphs(
        family='DesperateValorStatusGraphs', catalog_name='desperate-valor-current-status-graphs',
        cache_name='desperate-valor', entities=[25034, 24242, 25131],
        source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
                         ('BattleGridItemEntities', 'MyFightStatusHpDecreaseUnit', True),
                         ('BattleGridItemEntities', 'MyFightStatusHpIncreaseUnit', True),
                         ('BattleGridItemEntities', 'MyFightStatusStackChange', True),
                         ('BattleGridItemEntities', 'MyFightStatusProp', True)],
        generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'desperate-valor',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
