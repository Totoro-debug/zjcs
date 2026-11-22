"""Convert the current original blinding status and its native playable bindings."""
from pathlib import Path
from official_apk import ROOT
from official_source import OfficialSource
from battle_native_script_bindings import shared_bindings
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    family_root = ROOT / 'unity/Assets/Official/Combat/StarBurstGraphs'
    # Unity can create folder sidecars before conversion. Keep their identities
    # and importer fields while emitting the same whitespace as generated files.
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
                   -265745592: ('BattleGridItemEntities', 'MyFightStatusHitDmgAddPer'),
                   1640720687: shared['MAPPED'][1640720687]})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/StarBurstGraphs/Resources/Combat/StarBurstGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/star-burst-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/star-burst-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={24234: 'Prefabs/EcEntity/Fight/FightSkill/24234/24234.prefab'},
          MAPPED=mapped, reference_scripts={-210918009: shared['reference_scripts'][-210918009]},
          family='StarBurstGraphs', shader_prefix='StarBurstGraph_',
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'destroyer-statuses',
          native_export_name='native-export',
          limits=['The owning skill verifies point selection, light damage, blinding, cultivation, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
