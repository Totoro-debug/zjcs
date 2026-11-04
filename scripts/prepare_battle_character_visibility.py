"""Decode original SerializeReference role views; YAML export omits this registry."""
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/character-art-python/Lib/site-packages'))
sys.path.insert(0, str(ROOT / '.tools/python-packages'))
from UnityPy.classes import PPtr

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

DATA = ROOT / 'unity/Assets/Game/Resources/Combat/character-visibility-bindings.json'
RECEIPT = ROOT / 'assets/reference/battle-scenes/character-visibility-source-manifest.json'


def main():
    apk = Apk.current()
    if apk.version != '884_156692':
        raise ValueError('Review character visibility bindings for the newly selected version.')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    schemas = ScriptSchemas(code_root=code)
    inputs = {}

    def record(obj):
        source = apk.provenance(obj)
        source['object_sha256'] = hashlib.sha256(obj.get_raw_data()).hexdigest()
        inputs[(source['bundle'], source['path_id'])] = source

    def path_of(part):
        names = []
        while True:
            record(part.object_reader)
            names.insert(0, part.m_Name)
            transform = part.m_Component[0].component.read()
            record(transform.object_reader)
            if not hasattr(transform, 'm_Father'):
                raise ValueError('Role visibility target does not have an original Transform.')
            if not transform.m_Father.path_id:
                return '/'.join(names)
            part = transform.m_Father.read().m_GameObject.read()

    bundles = sorted(name for name in apk.index
        if 'prefabs_ecentity_fight_fightcharacter_' in name or 'prefabs_ecentity_fight_fightmonster_' in name)
    roles = []
    for bundle in bundles:
        found = []
        for obj in apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            if basic.m_Script.read().m_ClassName != 'ECViewDriver':
                continue
            value = schemas.read(obj)
            references = {reference['rid']: reference for reference in value['references']['RefIds']}
            # Only registered, ordered mECViews entries are live views. Do not
            # mistake an unrelated/stale managed-reference record for a role.
            for pointer in value['mECViews']:
                reference = references.get(pointer['rid'])
                if not reference or reference['type']['class'] != 'ECFightRoleCharacterView':
                    continue
                record(obj)
                paths = []
                for part in reference['data']['mDelayVisibilityParts']:
                    paths.append(None if not part['m_PathID'] else
                        path_of(PPtr(assetsfile=obj.assets_file, **part).read()))
                root_name = basic.m_GameObject.read().m_Name
                key = re.fullmatch(r'defaultpackage_assets_prefabs_ecentity_fight_fight(?:character|monster)_(.+)\.bundle', bundle)
                if not key:
                    raise ValueError('Unexpected official role bundle identity: ' + bundle)
                found.append(dict(prefab=key[1], root=root_name, parts=paths))
        if len(found) != 1:
            raise ValueError(f'Expected one original role view in {bundle}: {found}')
        roles.extend(found)
    if len({role['prefab'] for role in roles}) != len(roles):
        raise ValueError('Duplicate original role prefab identity.')
    write_changed(DATA, json.dumps(dict(manifest=apk.version, roles=roles), ensure_ascii=False, indent=2) + '\n')
    meta(DATA)
    receipt = dict(manifest=apk.version, generator=identity(Path(__file__)),
        decoder=identity(ROOT / 'scripts/official_unity_schema.py'),
        schema=identity(ROOT / 'scripts/unity_schema_types.cs'),
        code_inputs=[identity(code / 'client-source' / (name + '.cs')) for name in
            ('ECViewDriver', 'ECFightRoleCharacterView', 'MyPlayableCharacterPartVisibility')],
        inputs=list(inputs.values()), outputs=[identity(DATA), identity(DATA.with_name(DATA.name + '.meta'))],
        operation='APK-first verified selected-version bundles. Strictly decode the ECViewDriver managed-reference '
            'registry and follow its ordered mECViews references to ECFightRoleCharacterView. Preserve each '
            'mDelayVisibilityParts object path, array order, null pointer and empty array; record all referenced '
            'GameObject/Transform raw hashes. This catalog recovers configuration only; it does not establish '
            'runtime HUD/body/shadow bindings or imply that missing shadow geometry has been migrated.')
    write_changed(RECEIPT, json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
    print('Original character visibility:', len(roles), 'role prefabs,',
          sum(bool(role['parts']) for role in roles), 'with configured parts')


if __name__ == '__main__':
    main()
