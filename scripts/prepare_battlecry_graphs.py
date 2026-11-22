"""Convert Battlecry's current original attack and action-advance status graphs."""
from pathlib import Path
from official_apk import ROOT
from official_source import OfficialSource
from battle_native_script_bindings import shared_bindings
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    family_root = ROOT / 'unity/Assets/Official/Combat/BattlecryGraphs'
    for sidecar in [family_root.with_suffix('.meta'), *family_root.rglob('*.meta')]:
        if sidecar.is_file() and sidecar.with_suffix('').is_dir():
            original = sidecar.read_bytes().decode('utf-8')
            normalized = '\n'.join(line.rstrip() for line in original.splitlines()) + '\n'
            if original != normalized:
                sidecar.write_bytes(normalized.encode('utf-8'))
    mapped = dict(MAPPED)
    shared = shared_bindings()
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
        -109006141: ('EntityActions', 'MyAction'),
        -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
        -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
        238634487: ('BattleGridItemEntities', 'MyFightStatusRoundInterval'),
        1640720687: shared['MAPPED'][1640720687]})
    build(DEST=family_root / 'Resources/Combat/BattlecryGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/battlecry-current-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/battlecry-current-graphs-source-manifest.json',
        ROOTS={}, graph_roots={24192: 'Prefabs/EcEntity/Fight/FightSkill/24192/24192.prefab',
            24384: 'Prefabs/EcEntity/Fight/FightSkill/24384/24384.prefab'},
        MAPPED=mapped,
        family='BattlecryGraphs', shader_prefix='BattlecryGraph_', generator=Path(__file__),
        native_manifest=source.version,
        native_cache=ROOT / '.tools/source-packages/current' / source.version / 'destroyer-battlecry-statuses',
        native_export_name='native-export',
        limits=['The owning skill verifies attack snapshots, unique effects, immediate next-action advance, cultivation, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
