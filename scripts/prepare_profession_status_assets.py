"""Convert verified original profession status graphs without changing their source identity."""
import json
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_battle_status_orbit_assets import build, MAPPED


def main():
    mapped = dict(MAPPED)
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   -812331650: ('BattleGridItemEntities', 'MyFightStatusLogic')})
    export = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
    original = json.loads((export / 'native-export-all-source-manifest.json').read_bytes())
    # Reuse a hash-verified conversion only after checking the newly acquired
    # official client still has each of the mapped implementation types.
    old_code = export.parent / 'official-code'
    with Apk.current() as apk:
        current_code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        for _, name in mapped.values():
            relatives = [path.relative_to(old_code) for path in (
                old_code / 'client-source' / (name + '.cs'),
                old_code / 'game-framework-source/GameFramework' / (name + '.cs')) if path.is_file()]
            assert len(relatives) == 1, (name, relatives)
            relative, = relatives
            assert (old_code / relative).read_bytes() == (current_code / relative).read_bytes(), (
                'Current status presentation code changed', relative)
    build(DEST=ROOT / 'unity/Assets/Official/Combat/ProfessionStatuses/Resources/Combat/ProfessionStatuses',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/profession-statuses.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/profession-status-assets-source-manifest.json',
          ROOTS={}, graph_roots={24378: 'Prefabs/EcEntity/Fight/FightSkill/24378/24378.prefab'},
          MAPPED=mapped, family='ProfessionStatuses', shader_prefix='ProfessionStatus_',
          generator=Path(__file__), native_manifest=original['manifest'],
          limits=['Current official bundle, object and dependency equivalence is verified by the owning skill source receipt.'])


if __name__ == '__main__':
    main()
