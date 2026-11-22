"""Convert the original protective-runes status and its complete playable graph."""
from pathlib import Path
import re
import struct
from Crypto.Hash import MD4

from official_apk import ROOT
from official_source import OfficialSource
from battle_native_script_bindings import shared_bindings
from prepare_battle_status_orbit_assets import MAPPED, build, meta, write_changed


def main():
    source = OfficialSource()
    name = 'MyFightStatusDamageReducePer'
    original = ROOT / '.tools/source-packages/current' / source.version / 'official-code/client-source' / (name + '.cs')
    text = original.read_text(encoding='utf-8-sig')
    assert re.fullmatch(r'\s*using Common;\s*public class MyFightStatusDamageReducePer\s*:\s*'
        r'MyComponent<FightStatusDamageReducePerComponent,\s*FightStatusDamageReducePerComponentInfo>\s*\{\s*\}\s*', text)
    marker = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities' / (name + '.cs')
    write_changed(marker, 'using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityViews;\n'
        'namespace Zjcs.Official.BattleGridItemEntities\n{\n'
        'public class MyFightStatusDamageReducePer : MyComponent<IBattleGridComponentViewData>\n{\n}\n}\n')
    meta(marker)
    family_root = ROOT / 'unity/Assets/Official/Combat/ProtectiveRunesGraphs'
    for sidecar in [family_root.with_suffix('.meta'), *family_root.rglob('*.meta')]:
        if sidecar.is_file() and sidecar.with_suffix('').is_dir():
            original_meta = sidecar.read_bytes().decode('utf-8')
            normalized = '\n'.join(line.rstrip() for line in original_meta.splitlines()) + '\n'
            if original_meta != normalized:
                sidecar.write_bytes(normalized.encode('utf-8'))
    mapped = dict(MAPPED)
    shared = shared_bindings()
    marker_id = struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
        -109006141: ('EntityActions', 'MyAction'),
        -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
        marker_id: ('BattleGridItemEntities', name),
        1640720687: shared['MAPPED'][1640720687]})
    build(DEST=family_root / 'Resources/Combat/ProtectiveRunesGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/protective-runes-current-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/protective-runes-current-graphs-source-manifest.json',
        ROOTS={}, graph_roots={24201: 'Prefabs/EcEntity/Fight/FightSkill/24201/24201.prefab'},
        MAPPED=mapped, family='ProtectiveRunesGraphs', shader_prefix='ProtectiveRunesGraph_',
        generator=Path(__file__), native_manifest=source.version,
        native_cache=ROOT / '.tools/source-packages/current' / source.version / 'destroyer-protective-runes-status',
        native_export_name='native-export',
        limits=['The owning skill verifies cultivated conditional reduction, direct and other damage, shields, expiry, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
