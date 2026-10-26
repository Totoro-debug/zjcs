"""Recover the original ordered GameSkillIcon references and StateSwitcher bindings."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from refresh_current_ui_graphics import object_path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build(output_root=ROOT):
    source_path = ROOT / 'assets/reference/battle-hud/source-manifest.json'
    runtime_path = ROOT / 'unity/Assets/Official/Combat/HUD/panels-runtime.json'
    prefabs = json.loads(source_path.read_bytes())['prefabs']
    trees = {p['key']: {n['path'] for n in p['nodes']}
             for p in json.loads(runtime_path.read_bytes())['prefabs']}
    apk, schemas = Apk(), ScriptSchemas()
    inputs, cards, skill_ui = {}, {}, None

    def record(obj):
        data = apk.provenance(obj)
        data['object_sha256'] = sha(obj.get_raw_data())
        inputs[(data['bundle'], data['path_id'])] = data
        return obj

    def reference(owner, data):
        if data['m_FileID'] != 0 or not data['m_PathID']:
            raise ValueError('Expected a non-null local prefab reference')
        return record(owner.assets_file.objects[data['m_PathID']])

    def path(obj, key):
        go = obj if obj.type.name == 'GameObject' else obj.read(check_read=False).m_GameObject.deref()
        value, root_id = object_path(record(go))
        if root_id != prefabs[key]['path_id'] or value not in trees[key]:
            raise ValueError('Skill reference escaped its verified prefab: ' + value)
        return value

    for key in ('main', 'skill'):
        for obj in apk.objects(prefabs[key]['bundle']):
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            if not basic.m_Script.path_id:
                continue
            class_name = basic.m_Script.read().m_ClassName
            if class_name not in ('GameSkillIcon', 'FightMainPanelSkillUI'):
                continue
            record(obj)
            data = schemas.read(obj)
            owner = path(obj, key)
            if class_name == 'FightMainPanelSkillUI':
                if key != 'main' or skill_ui is not None:
                    raise ValueError('Unexpected skill-list controller')
                skill_ui = {'owner': owner,
                            'rect': path(reference(obj, data['mRect']), key),
                            'pet': path(reference(obj, data['mGamePetSkillIcon']), key),
                            'noUnlockPetOffset': data['mNoUnlockPetOffset'],
                            'slotIds': [str(reference(obj, p).path_id) for p in data['SkillIcons']]}
                continue
            configs = data['StateSwitcher']['mStateConfigs']
            if len(configs) != 1:
                raise ValueError('Expected the original single StateGameObjectConfig')
            config = next(r for r in data['references']['RefIds'] if r['rid'] == configs[0]['rid'])
            if config['type']['class'] != 'StateGameObjectConfig':
                raise ValueError('Unsupported skill state configuration')
            states = [{'state': row['State']['mValue'],
                       'objects': [path(reference(obj, p), key) for p in row['GameObjects']]}
                      for row in config['data']['mStateGameObjectData']['GoConfig']]
            icon = reference(obj, data['Icon'])
            icon_data = schemas.read(icon)
            card = {'prefab': key, 'owner': owner,
                    'icon': path(reference(icon, icon_data['mIconProxy']['mImage']), key),
                    'quality': path(reference(obj, data['QualityImg']), key),
                    'cooldown': path(reference(obj, data['CdText']), key), 'states': states}
            cards[str(obj.path_id)] = card
    if skill_ui is None or len(skill_ui['slotIds']) != 4 or len(cards) != 5:
        raise ValueError('Incomplete original skill-slot references')
    slots = [cards[identity] for identity in skill_ui.pop('slotIds')]
    if any(c['prefab'] != 'main' or [s['state'] for s in c['states']] != list(range(7)) for c in slots):
        raise ValueError('Unexpected inline skill-slot state set')
    standalone = next(c for c in cards.values() if c['prefab'] == 'skill')
    if [s['state'] for s in standalone['states']] != list(range(8)):
        raise ValueError('Unexpected standalone skill-icon states')
    rules_path = ROOT / 'assets/official/rules.json'
    unlock_rows = [r for r in json.loads(rules_path.read_bytes())['function_limit']
                   if r['Type'] == 62 and r['Key0'] == 0 and r['Key1'] == 0]
    if len(unlock_rows) != 1 or unlock_rows[0]['Condition'] != {'Jumpable': None, '_type': 'HasAnyPetCondition'}:
        raise ValueError('Unsupported official pet function unlock condition')
    output = output_root / 'unity/Assets/Game/Resources/Combat/battle-skill-slots.json'
    receipt_path = output_root / 'assets/reference/battle-hud/skill-slots-source-manifest.json'
    payload = (json.dumps({'manifest': '854_156383', 'skillUI': skill_ui, 'slots': slots,
                           'standalone': standalone, 'petUnlockCondition': 'HasAnyPetCondition'},
                          ensure_ascii=False, separators=(',', ':')) + '\n').encode()
    if output.exists() and sha(output.read_bytes()) != json.loads(receipt_path.read_bytes())['output_sha256']:
        raise ValueError('Refusing to overwrite modified skill-slot bindings')
    output.write_bytes(payload)
    code = ['client-source/GameSkillIcon.cs', 'client-source/FightMainPanelSkillUI.cs',
            'client-source/ItemIconSkill.cs', 'client-source/FightSkillListDataEntityProvider.cs',
            'framework-source/StateGameObjectConfig.cs', 'framework-source/StateGameObjectData.cs',
            'common-source/Common/HasAnyPetCondition.cs', 'common-source/Common/FunctionType.cs']
    receipt = {'manifest': '854_156383', 'output': output.relative_to(output_root).as_posix(),
               'output_sha256': sha(payload), 'generator_sha256': sha(Path(__file__).read_bytes()),
               'runtime_prefab_sha256': sha(runtime_path.read_bytes()),
               'pet_unlock_input': {'path': rules_path.relative_to(ROOT).as_posix(),
                                    'sha256': sha(rules_path.read_bytes()), 'rows': unlock_rows},
               'inputs': list(inputs.values()),
               'official_code': {p: sha((ROOT / '.tools/source-packages/official-code' / p).read_bytes()) for p in code},
               'runtime': {p: sha((output_root / p).read_bytes()) for p in (
                   'unity/Assets/Game/Runtime/Official/WorldUI/WorldGameSkillIcon.cs',
                   'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillSlotBindings.cs',
                   'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudView.cs',
                   'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs')},
               'operation': 'Restore the original four serialized slots in order and apply each original StateGameObjectConfig. Preserve absent state 7 in the inline prefab; do not append replacement cards or invent a disabled overlay binding.',
               'remaining': 'Item quality, pet icons/energy, skill detail input, and skill action lifecycle callbacks are not completed by these bindings.'}
    receipt_path.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    sidecar = output.with_name('battle-skill-slots-source-manifest.json')
    sidecar.write_bytes((json.dumps({'manifest': receipt['manifest'], 'output_sha256': receipt['output_sha256']})+'\n').encode())
    print('Recovered four original ordered skill slots and five state configurations.')


if __name__ == '__main__':
    build()
