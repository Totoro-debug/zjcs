"""Recover official activity battle UI fields before runtime/resource binding.

Intermediate prefabs stay in the owned validation workspace. This command does
not install incomplete UI into Assets or claim that its activities are playable.
"""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import PartnerNativeRecovery, digest, identity
from validation_workspace import validation_directory


PREFABS = (
    ('ActivityDomainBossPanel', 'activitydomainboss', 'ActivityDomainBoss/ActivityDomainBossPanel'),
    ('AnniversaryFunnyBossEntryPanel', 'activityfestivalanniversary',
     'ActivityFestivalAnniversary/FunnyBoss/AnniversaryFunnyBossEntryPanel'),
    ('GameEndPanelActivityDomainBoss', 'fight_gameendpanelactivitydomainboss', 'Fight/GameEndPanelActivityDomainBoss'),
    ('GameEndPanelActivityFunnyBoss', 'fight_gameendpanelactivityfunnyboss', 'Fight/GameEndPanelActivityFunnyBoss'),
    ('ActivityDomainBossNumCell', 'activitydomainboss', 'ActivityDomainBoss/ActivityDomainBossNumCell'),
)


def build():
    work = validation_directory()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation workspace for activity UI recovery')
    recovery = PartnerNativeRecovery()
    if recovery.apk.version != '884_156692':
        raise ValueError('Review the selected activity UI version before recovery')
    export_manifest = recovery.cache / 'native-export-all-source-manifest.json'
    export = json.loads(export_manifest.read_bytes())
    checked = {row['path']: row['sha256'] for row in export['outputs']}
    destination = work / 'activity-native'
    for name, bundle, path in PREFABS:
        native = recovery.native / ('Prefabs/UI/' + path + '.prefab')
        for payload in (native, Path(str(native) + '.meta')):
            key = payload.relative_to(recovery.cache / 'native-export-all').as_posix()
            if digest(payload.read_bytes()) != checked[key]:
                raise ValueError('Verified native conversion payload changed: ' + key)
        recovery.recover(name, destination, bundle='defaultpackage_assets_prefabs_ui_' + bundle + '.bundle',
                         native_relative='Prefabs/UI/' + path + '.prefab')
    recovery.finalize()
    # Complete source coverage is required even when a decoder cache is absent.
    # A decoded but omitted GameEndPanelBase used to silently lose all buttons.
    for source, output, data, record in recovery.outputs:
        expected = {row['original'] for row in data['scripts']}
        recovered = {row['original'] for row in data['bindings']}
        if expected != recovered:
            raise ValueError('Incomplete component field recovery: ' + record['name'])
        by_original = {row['original']: row for row in data['bindings']}
        for row in data['scripts']:
            cab, path_id = row['original'].rsplit(':', 1)
            original = recovery.apk.env.get_cab(cab).objects[int(path_id)]
            fields = recovery.fields(original)
            restored = data['documents'][row['nativeFileID']]['MonoBehaviour']
            keys = set(fields) - {'m_GameObject', 'm_Enabled', 'm_Script', 'm_Name'}
            if keys - restored.keys():
                raise ValueError('Original serialized fields omitted: ' + identity(original))
            if by_original[row['original']]['payload_sha256'] != digest(original.get_raw_data()):
                raise ValueError('Recovered component source bytes differ')
        record['script_components'] = len(expected)
        record['complete_field_coverage'] = True
        record['script_types'] = sorted({'.'.join(filter(None, [row['namespace'], row['type']]))
                                         for row in data['scripts']})
    proof = recovery.proof
    proof['native_conversion'] = dict(path=export_manifest.relative_to(ROOT).as_posix(),
                                      sha256=digest(export_manifest.read_bytes()))
    proof['generator'] = dict(path=Path(__file__).relative_to(ROOT).as_posix(),
                              sha256=digest(Path(__file__).read_bytes()))
    proof['mapped_objects'] = sum(row['mapped_objects'] for row in proof['prefabs'])
    proof['recovered_components'] = sum(row['recovered_components'] for row in proof['prefabs'])
    proof['runtime_binding_complete'] = False
    receipt = ROOT / 'assets/reference/daily-challenges/native-ui-source-manifest.json'
    receipt.parent.mkdir(parents=True, exist_ok=True)
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Verified', proof['mapped_objects'], 'objects and', proof['recovered_components'],
          'complete component payloads; runtime/resource binding remains pending.', flush=True)


if __name__ == '__main__':
    build()
