"""Verify the reused native invitation hierarchy against the selected official APK."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from current_component_schemas import CurrentSchemas
from UnityPy.classes import PPtr


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(path):
    return {'path': path.relative_to(ROOT).as_posix(), 'sha256': digest(path)}


def compare(before, after, obj, references, apk, changes, path=''):
    if isinstance(before, dict) and isinstance(after, dict):
        if set(before) != set(after):
            raise ValueError('Original field set differs: ' + path + ' before=' + str(set(before)-set(after)) + ' after=' + str(set(after)-set(before)))
        if set(before) == {'m_FileID', 'm_PathID'} and before != after:
            if before['m_PathID'] != after['m_PathID'] or not after['m_FileID']:
                raise ValueError('Original pointer changed: ' + path)
            target = PPtr(m_FileID=after['m_FileID'], m_PathID=after['m_PathID'], assetsfile=obj.assets_file).deref()
            identity = target.assets_file.name + ':' + str(target.path_id)
            previous = references[identity]
            if hashlib.sha256(target.get_raw_data()).hexdigest() != previous['payload_sha256']:
                raise ValueError('Original dependency payload changed: ' + identity)
            changes.append(dict(field=path, before=before, after=after, identity=identity,
                                sha256=previous['payload_sha256'], source=apk.provenance(target)))
            return
        for key in before:
            compare(before[key], after[key], obj, references, apk, changes, path + '.' + key)
    elif isinstance(before, list) and isinstance(after, list):
        if len(before) != len(after):
            raise ValueError('Original array length differs: ' + path)
        for index, (left, right) in enumerate(zip(before, after)):
            compare(left, right, obj, references, apk, changes, path + '[' + str(index) + ']')
    elif before != after:
        raise ValueError('Original value changed: ' + path)


def main():
    apk, schemas = Apk.current(), CurrentSchemas()
    results, inputs = [], []
    for filename, name in [('team-ui-prefabs.json', 'TeamInvitePanel'),
                           ('team-invite-extra-prefabs.json', 'TeamInvitePanelRobotItem')]:
        source = ROOT / 'unity/Assets/Official/World/Map/12/Source' / filename
        inputs.append(record(source))
        data = json.loads(source.read_bytes())
        panel = next(value for value in data['panels'] if value['name'] == name)
        root = apk.asset('prefabs_ui_team', '/' + name + '.prefab')
        assert panel['root'] == root.assets_file.name + ':' + str(root.path_id)
        originals = {value['identity']: value for value in data['objects']}
        references = {value['identity']: value for value in data['references']}
        identities = {identity for node in panel['nodes'] for identity in [node['gameObject'], *node['components']]}
        exact, differences, excluded = 0, [], []
        for identity in sorted(identities):
            previous = originals[identity]
            obj = root.assets_file.objects[int(identity.rsplit(':', 1)[1])]
            current_hash = hashlib.sha256(obj.get_raw_data()).hexdigest()
            if current_hash == previous['payload_sha256']:
                exact += 1
                continue
            if previous.get('scriptType') in {'AppearanceSystem.Runtime.AppearanceUI', 'AppearanceSystem.Runtime.AppearanceUIPartHandler', 'AppearanceSystem.Runtime.AppearanceSkinClip'}:
                excluded.append(dict(identity=identity, type=previous['scriptType'], original_sha256=previous['payload_sha256'], sha256=current_hash, reason='Removed online appearance logic; not consumed by the shared selector.'))
                continue
            fields = schemas.read(obj) if obj.type.name == 'MonoBehaviour' else obj.read_typetree()
            changes = []
            try:
                compare(previous['fields'], fields, obj, references, apk, changes)
            except ValueError as error:
                raise ValueError(identity + ' ' + previous['type'] + ': ' + str(error)) from error
            if not changes:
                raise ValueError('Unexplained binary difference: ' + identity)
            differences.append(dict(identity=identity, type=previous['type'],
                                    original_sha256=previous['payload_sha256'], sha256=current_hash, changes=changes))
        native = ROOT / 'unity/Assets/Game/Resources/Map12/Team' / (name + '.prefab')
        results.append(dict(name=name, historical_manifest=data['manifest'], root=panel['root'],
                            nodes=len(panel['nodes']), objects=len(identities), exact_objects=exact,
                            pointer_table_changes=differences, excluded_online_components=excluded, selected_source=apk.provenance(root),
                            native=record(native), native_meta=record(Path(str(native) + '.meta'))))
    code = ['scripts/record_battle_assist_selection.py',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAssistSelectionView.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAssistSelectionView.cs.meta']
    receipt = dict(manifest=apk.version, historical_inputs=inputs, panels=results,
                   bindings=[record(ROOT / path) for path in code],
                   conversion='Reuse existing native Map12 prefabs without changing layout or asset payloads. '
                   'Retained hierarchy objects match the selected source; differing external pointer indices '
                   'resolve to byte-identical original dependencies. Historical input identities remain separate.')
    target = ROOT / 'assets/reference/battle-hud/assist-selection-source-manifest.json'
    target.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    print([(value['name'], value['objects'], value['exact_objects'], len(value['pointer_table_changes'])) for value in results])


if __name__ == '__main__':
    main()
