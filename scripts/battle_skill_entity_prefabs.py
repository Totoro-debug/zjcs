"""Read and restore the original additional skill entity component bindings."""
import hashlib
import re
import struct
from pathlib import Path

import yaml
from Crypto.Hash import MD4
from UnityPy.classes.PPtr import PPtr

from official_apk import Apk, ROOT
from current_component_schemas import CurrentSchemas
from prepare_battle_ground_effect_assets import EXPORTED, GUID
from prepare_battle_scene_appearance_runtime import identity


def skill_entity_scripts():
    groups = {
        'EntityLifecycle': ('MyEntity', 'MyGridTransform', 'MyTransform'),
        'BattleGridItemEntities': ('MyFightGameplayTag',),
        'EntityActions': ('MyAction',),
        'EntityViews': ('ECViewDriver',),
        'BattleSkillPlayables': (
            'MyFightSkill', 'MyFightSkillExtSkill', 'MyFightSkillHitCfgOverrideByStatusStack',
            'MyFightSkillAIRandomTargetFallbackSpawner', 'MyFightSkillBehAttachOperate',
            'MyFightSkillBehAutoRemove', 'MyFightSkillCombo', 'MyFightSkillReplay',
            'MyFightSkillLogic', 'MyPlayableOverrider', 'FightSkillPlayableOverrideByStatusStack',
            'MyFightHitExtDelayInfoOverriderByStatusStack')}
    return {struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]:
        dict(name=name, namespace='', target_namespace='Zjcs.Official.' + folder,
             source=name + '.cs',
             runtime='unity/Assets/Game/Runtime/Official/' + folder + '/' + name + '.cs')
        for folder, entries in groups.items() for name in entries}


class SkillEntityManagedViews:
    """Own the original registry and pointer conversion behind one preprocess hook."""

    def __init__(self, entity_paths):
        self.entity_paths = {int(key): EXPORTED / path for key, path in entity_paths.items()}
        self.registrations = []
        self.pointer_bindings = []
        self.schemas = CurrentSchemas()
        self.restored = {}
        self.restore_count = {}
        if 54383 not in self.entity_paths:
            raise ValueError('The original special skill entity must remain in the complete catalog.')
        self._read_original()

    def _read_original(self):
        path = self.entity_paths[54383]
        native_meta = Path(str(path) + '.meta')
        source_bundle = re.search(r'(?m)^  assetBundleName: (.+)$',
                                  native_meta.read_text(encoding='utf-8-sig'))[1] + '.bundle'
        with Apk.current() as apk:
            if apk.version != '884_156692':
                raise ValueError('Review original skill entity view source version.')
            root = apk.asset(source_bundle, '/54383.prefab')
            drivers = []

            def walk(go):
                for reference in go.read().m_Component:
                    obj = reference.component.deref()
                    if obj.type.name in ('Transform', 'RectTransform'):
                        for child in obj.read().m_Children:
                            walk(child.read().m_GameObject.deref())
                    elif obj.type.name == 'MonoBehaviour':
                        script = obj.read(check_read=False).m_Script.read()
                        if script.m_ClassName == 'ECViewDriver':
                            drivers.append(obj)
            walk(root)
            if len(drivers) != 1:
                raise ValueError('Review original special skill view driver count.')
            component = drivers[0]
            fields = self.schemas.read(component)
            records = fields['references']['RefIds']
            if (fields['references']['version'] != 2 or len(records) != 1
                    or records[0]['type'] != dict(asm='Assembly-CSharp', ns='',
                                                  **{'class': 'ECPlayableSupporterView'})
                    or fields['mECViews'] != [dict(rid=records[0]['rid'])]):
                raise ValueError('Review additional original skill managed view branches.')

            def convert(value):
                if isinstance(value, dict) and set(value) == {'m_FileID', 'm_PathID'}:
                    if not value['m_PathID']:
                        return dict(fileID=0)
                    target = PPtr(**value, assetsfile=component.assets_file).deref()
                    if target.type.name != 'GameObject':
                        raise ValueError('Bind an additional original skill view object type.')
                    source = apk.provenance(target)
                    family = re.fullmatch(
                        r'defaultpackage_assets_prefabs_ecentity_fight_fightskill_(\d+)\.bundle',
                        source['bundle'])
                    if family is None:
                        raise ValueError('Resolve the actual original skill view graph family.')
                    name = target.read().m_Name
                    native = EXPORTED / 'Prefabs/EcEntity/Fight/FightSkill' / family[1] / (name + '.prefab')
                    sidecar = Path(str(native) + '.meta')
                    metadata = sidecar.read_text(encoding='utf-8-sig')
                    if re.search(r'(?m)^  assetBundleName: (.+)$', metadata)[1] + '.bundle' != source['bundle']:
                        raise ValueError('Original skill view pointer bundle differs from native export.')
                    guid = GUID.search(metadata)[1]
                    file_id = int(re.search(r'(?m)^--- !u!1 &(-?\d+)$',
                                             native.read_text(encoding='utf-8-sig'))[1])
                    self.pointer_bindings.append(dict(source=source,
                        object_sha256=hashlib.sha256(target.get_raw_data()).hexdigest(),
                        native=identity(native), native_meta=identity(sidecar), guid=guid, file_id=file_id))
                    return dict(fileID=file_id, guid=guid, type=3)
                if isinstance(value, dict):
                    return {key: convert(child) for key, child in value.items()}
                if isinstance(value, list):
                    return [convert(child) for child in value]
                return value

            registry = convert(fields['references'])
            for record in registry['RefIds']:
                record['type']['ns'] = 'Zjcs.Official.EntityViewPlayables'
                record['type']['asm'] = 'Zjcs.Official.EntityViewPlayables'
            restored = dict(mECViews=fields['mECViews'], references=registry)
            self.restored[54383] = yaml.safe_dump(restored, sort_keys=False,
                                                allow_unicode=True, width=160)
            self.restore_count[54383] = 0
            self.registrations.append(dict(entity_id=54383,
                source=apk.provenance(component),
                object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest(),
                fields=fields, native=identity(path), native_meta=identity(native_meta),
                conversion='Original ordered RIDs and typed fields; actual original graph objects resolve before native GUID remapping.'))

    def preprocess(self, text, path):
        key = next((key for key in self.restored if self.entity_paths[key] == path), None)
        if key is None:
            return text
        blocks = re.split(r'(?m)(?=^--- !u!)', text)
        count = 0
        for index, block in enumerate(blocks):
            if not re.search(r'(?m)^  m_Script: \{fileID: 401787210,', block):
                continue
            if 'mECViews:' in block or 'references:' in block:
                raise ValueError('Original exported skill view fields changed; review before restoring.')
            blocks[index] = block.rstrip() + '\n' + ''.join(
                '  ' + line + '\n' for line in self.restored[key].splitlines())
            count += 1
        if count != 1:
            raise ValueError('Original exported skill driver must be restored exactly once.')
        self.restore_count[key] += 1
        return ''.join(blocks)
