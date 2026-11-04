"""Bind partner avatars to the selected application ports and original enum lists."""
import copy
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import PartnerNativeRecovery, documents, digest
from prepare_battle_scene_appearance_runtime import write_changed
from validation_workspace import validation_directory

RECEIPT = ROOT / 'assets/reference/partner-panel-actors-source-manifest.json'
BLOCK = re.compile(r'(?ms)^--- !u!114 &(-?\d+)\n(.*?)(?=^--- !u!|\Z)')
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official'
NAMES = ('AppearanceCharacter', 'AppearanceUI', 'PlayerAnimationAppearance')
TARGETS = (
    ('UI/Panel/NpcPanel.prefab', 'partner-panel-native-source-manifest.json', 'NpcPanel'),
    ('UI/Panel/NativePrefabs/39af9cbc1cf8b837.prefab', 'partner-panel-native-source-manifest.json', 'NpcPanelDialogueItem'),
    ('UI/DialoguePlayback/NativePrefabs/9e4e500e3555f2fa.prefab', 'partner-dialogue-playback-native-source-manifest.json', 'NpcDialoguePanelItem'),
)


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path.read_bytes()))


def guid(path):
    return re.search(r'(?m)^guid: ([0-9a-f]{32})$', Path(str(path) + '.meta').read_text('utf8'))[1]


def main():
    work = validation_directory()
    assert (work / '.zjcs-validation.json').is_file(), 'Owned validation workspace required'
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    recovery = PartnerNativeRecovery()
    assert recovery.apk.version == '884_156692'
    inputs, outputs, adaptations = {}, [], []
    planned = []
    try:
        for relative, receipt_name, name in TARGETS:
            path = ROOT / 'unity/Assets/Official/Partners' / relative
            base_path = ROOT / 'assets/reference' / receipt_name
            base = json.loads(base_path.read_bytes())
            assert base['manifest'] == recovery.apk.version
            key = path.relative_to(ROOT).as_posix()
            expected = next((row['sha256'] for row in previous.get('outputs', []) if row['path'] == key),
                            base['installed_output_sha256'][key])
            assert digest(path.read_bytes()) == expected, 'Preserve edited partner prefab: ' + key
            inputs[base_path.relative_to(ROOT).as_posix()] = digest(base_path.read_bytes())
            recovery.recover(name, work / 'partner-actor-source')
            _, _, source, evidence = recovery.outputs[-1]
            text = path.read_text('utf8')
            before = documents(text)
            wanted = copy.deepcopy(before)
            components = []
            for row in source['scripts']:
                script_name = row['type']
                if script_name not in (*NAMES, 'AppearanceUIPartHandler'):
                    continue
                file_id = row['nativeFileID']
                assert file_id in before, (key, file_id)
                # Recovery already decoded and checked the component's complete
                # current APK fields and its exact native hierarchy identity.
                fields = source['documents'][file_id]['MonoBehaviour']
                target = wanted[file_id]['MonoBehaviour']
                binding = dict(nativeFileID=file_id, original=row['original'], type=script_name)
                binding['payload_sha256'] = next(entry['payload_sha256'] for entry in source['bindings']
                    if entry['nativeFileID'] == file_id)
                if script_name in NAMES:
                    old = RUNTIME / 'ProfessionUI' / (script_name + '.cs')
                    current = RUNTIME / 'CombatAppearance' / (script_name + '.cs')
                    assert target['m_Script']['guid'] in (guid(old), guid(current))
                    target['m_Script'] = dict(fileID=11500000, guid=guid(current), type=3)
                    binding['runtime'] = record(current)
                    binding['runtime_meta'] = record(Path(str(current) + '.meta'))
                else:
                    binding['original_fields'] = {}
                    for field in ('IgnoreSkinTypes', 'IgnoreEffectTypes'):
                        value = fields[field]
                        assert isinstance(value, list) and all(type(item) is int for item in value)
                        target[field] = value
                        binding['original_fields'][field] = value
                components.append(binding)

            def replace(match):
                file_id = int(match[1])
                binding = next((row for row in components if row['nativeFileID'] == file_id), None)
                if binding is None:
                    return match[0]
                raw = match[0]
                fields = wanted[file_id]['MonoBehaviour']
                if binding['type'] in NAMES:
                    raw, count = re.subn(r'(?m)^  m_Script: .*$',
                        '  m_Script: {fileID: 11500000, guid: ' + fields['m_Script']['guid'] + ', type: 3}', raw)
                    assert count == 1
                else:
                    for field, values in binding['original_fields'].items():
                        encoded = '  ' + field + ':\n' + '\n'.join('  - ' + str(item) for item in values)
                        raw, count = re.subn(r'(?m)^  ' + field + r':[^\n]*(?:\n  -[^\n]*)*', encoded, raw)
                        assert count == 1
                return raw

            result = BLOCK.sub(replace, text)
            assert components and documents(result) == wanted, 'Only actor bindings and source enum arrays may change'
            planned.append((path, result))
            adaptations.append(dict(path=key, baseline_sha256=base['installed_output_sha256'][key],
                source=evidence['originalSource'], native_source=evidence['nativeSource'],
                native_sha256=evidence['native_sha256'], components=components))
        for path, text in planned:
            write_changed(path, text)
            outputs.extend((record(path), record(Path(str(path) + '.meta'))))
        proof = dict(manifest=recovery.apk.version, inputs=inputs, adaptations=adaptations, outputs=outputs,
            component_cache=recovery.proof['component_cache'], generator=record(Path(__file__).resolve()),
            window_lifecycle=dict(
                original=record(ROOT / '.tools/source-packages/current/884_156692/official-code/framework-firstpass-source/PopupWindow.cs'),
                methods=['OnWindowDestory', 'OnDestroy'],
                migrated=record(RUNTIME / 'WorldUI/WorldPopupWindow.cs'),
                service=record(ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerWindowService.cs'),
                operation='Notify the original destruction action after releasing the playable graph; subscribe/unsubscribe through the window port.'),
            part_loader=record(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAppearancePartBindings.cs'),
            window_skin_clip_bindings=[record(ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI' / name)
                for name in ('PartnerPanelView.cs', 'PartnerDialoguePlaybackView.cs')],
            operation='Retain the original prefab object IDs, hierarchy and all other fields byte-for-byte. '
                'Bind the selected current shared application appearance ports. Restore each original ignored skin/effect list '
                'from verified APK fields as a Unity enum sequence; never interpret packed enum hex as a YAML number.')
        write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
        print('Bound original partner actors:', len(outputs) // 2, 'prefabs;',
              sum(len(row['components']) for row in adaptations), 'components')
    finally:
        recovery.apk.archive.close()


if __name__ == '__main__':
    main()
