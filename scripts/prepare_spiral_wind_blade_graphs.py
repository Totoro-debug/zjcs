"""Convert the current original laceration status and its complete playable graph."""
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from battle_native_script_bindings import shared_bindings
from prepare_battle_status_orbit_assets import MAPPED, build, meta, write_changed


def main():
    source = OfficialSource()
    original = ROOT / '.tools/source-packages/current' / source.version / 'official-code/client-source/MyFightStatusActionStart.cs'
    text = original.read_text(encoding='utf-8-sig')
    assert re.fullmatch(r'\s*using Common;\s*public class MyFightStatusActionStart\s*:\s*'
        r'MyFightStatusBase<FightStatusActionStartComponent,\s*FightStatusActionStartComponentInfo>\s*\{\s*\}\s*', text)
    # The official client marker has no fields or methods. Its server component
    # data uses the same migrated view interface as the other empty status markers.
    marker = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities/MyFightStatusActionStart.cs'
    write_changed(marker, 'using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityViews;\n'
        'namespace Zjcs.Official.BattleGridItemEntities\n{\n'
        'public class MyFightStatusActionStart : MyComponent<IBattleGridComponentViewData>\n{\n}\n}\n')
    meta(marker)
    family_root = ROOT / 'unity/Assets/Official/Combat/SpiralWindBladeGraphs'
    for sidecar in [family_root.with_suffix('.meta'), *family_root.rglob('*.meta')]:
        if sidecar.is_file() and sidecar.with_suffix('').is_dir():
            original_meta = sidecar.read_bytes().decode('utf-8')
            normalized = '\n'.join(line.rstrip() for line in original_meta.splitlines()) + '\n'
            if original_meta != normalized:
                sidecar.write_bytes(normalized.encode('utf-8'))
    mapped = dict(MAPPED)
    shared = shared_bindings()
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
        -109006141: ('EntityActions', 'MyAction'),
        -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
        812704826: ('BattleGridItemEntities', 'MyFightStatusActionStart'),
        1640720687: shared['MAPPED'][1640720687]})
    build(DEST=family_root / 'Resources/Combat/SpiralWindBladeGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/spiral-wind-blade-current-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/spiral-wind-blade-current-graphs-source-manifest.json',
        ROOTS={}, graph_roots={24376: 'Prefabs/EcEntity/Fight/FightSkill/24376/24376.prefab'},
        MAPPED=mapped, reference_scripts={-210918009: shared['reference_scripts'][-210918009]},
        family='SpiralWindBladeGraphs', shader_prefix='SpiralWindBladeGraph_',
        generator=Path(__file__), native_manifest=source.version,
        native_cache=ROOT / '.tools/source-packages/current' / source.version / 'destroyer-tear-statuses',
        native_export_name='native-export',
        limits=['The owning skill verifies wind damage, action-start laceration, cultivation, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
