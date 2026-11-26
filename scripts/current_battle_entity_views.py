"""Restore selected current managed battle views from verified serialized objects."""
import copy
import hashlib
import json
from pathlib import Path
import re

import yaml
from UnityPy.classes.PPtr import PPtr

from official_apk import Apk, ROOT
from official_source import OfficialSource
from current_component_schemas import CurrentSchemas
from prepare_battle_scene_appearance_runtime import identity


def current_battle_schemas():
    source = OfficialSource()
    code = source.prepared_path / 'official-code'
    proof = json.loads((code.parent / 'code-manifest.json').read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    assert identity(assembly)['sha256'] == proof['assemblies'][assembly.name]['decoded_sha256']
    schemas = CurrentSchemas(assembly.parent)
    tools = json.loads((ROOT / 'unity-tools.json').read_bytes())
    editor = Path(tools['editorInstallPath']) / 'Editor/Data'
    dependencies = [editor / 'MonoBleedingEdge/lib/mono/unityjit-win32/mscorlib.dll',
                    editor / 'Managed/UnityEngine/UnityEngine.CoreModule.dll']
    for dependency in dependencies:
        schemas.generator.load_dll(dependency.read_bytes())
    return schemas, [identity(assembly), identity(ROOT / 'unity-tools.json')], [
        dict(path=path.as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest()) for path in dependencies]


def battle_view_drivers(root):
    drivers = []
    def walk(go):
        for reference in go.read().m_Component:
            obj = reference.component.deref()
            if obj.type.name in ('Transform', 'RectTransform'):
                for child in obj.read().m_Children:
                    walk(child.read().m_GameObject.deref())
            elif obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Script.read().m_ClassName == 'ECViewDriver':
                drivers.append(obj)
    walk(root)
    return drivers


class CurrentBattleEntityViews:
    TYPES = {'ECFightSkillView', 'ECFightHitView', 'ECFightDamageView'}
    ALIASES = {
        'mPlayablePrefab': 'myPlayablePrefab', 'mUpPlayablePrefab': 'UpPlayablePrefab',
        'mDownPlayablePrefab': 'DownPlayablePrefab', 'mLeftPlayablePrefab': 'LeftPlayablePrefab',
        'mRightPlayablePrefab': 'RightPlayablePrefab', 'mShowSkillReplay': 'ShowSkillReplay',
        'mPreferredPosToPlayable': 'PreferredPosToPlayable', 'mPlayable': 'myPlayable',
        'mScopePlayable': 'myScopePlayable', 'mScopePlayableFollowDelay': 'ScopePlayableFollowDelay',
        'mScopeZFollowHitZ': 'ScopeZFollowHitZ', 'mSourceToMePlayable': 'SourceToMePlayable',
        'mShouldSetPlayableDirectinParamToSouceToMePlayable': 'ShouldSetPlayableDirectinParamToSouceToMePlayable',
        'mSourceToMeAnchor': 'SourceToMeAnchor', 'mIsApplyOffsetToPlayable': 'IsApplyOffsetToPlayable',
        'mCommonPlayable': 'myCommonPlayable', 'mDamagePlayable': 'myDamagePlayable',
        'mBlockPlayable': 'myBlockPlayable', 'mCritPlayable': 'myCritPlayable',
        'mTriggerStatusPlayable': 'myTriggerStatusPlayable', 'mShowZeroDamage': 'myShowZeroDamage',
        'mHideDamage': 'myHideDamage',
    }

    def __init__(self, exported, paths):
        self.exported = exported
        self.paths = {int(Path(path).stem): exported / path for path in paths}
        self.restored, self.fields, self.counts = {}, {}, {}
        self.registrations, self.pointer_bindings = [], []
        schemas, self.schema_inputs, self.schema_tools = current_battle_schemas()
        native = {}
        for sidecar in exported.rglob('*.prefab.meta'):
            text = sidecar.read_text('utf-8-sig')
            bundle = re.search(r'(?m)^  assetBundleName: (.+)$', text)
            if bundle:
                key = (bundle[1] + '.bundle', sidecar.name.removesuffix('.prefab.meta'))
                native.setdefault(key, []).append(sidecar.with_suffix(''))
        with Apk.current() as apk:
            for entity, path in self.paths.items():
                sidecar = Path(str(path) + '.meta')
                bundle = re.search(r'(?m)^  assetBundleName: (.+)$', sidecar.read_text('utf-8-sig'))[1] + '.bundle'
                root = apk.asset(bundle, '/' + str(entity) + '.prefab')
                drivers = battle_view_drivers(root)
                if len(drivers) != 1:
                    raise ValueError(('Current battle entity requires one reviewed view driver', entity))
                component = drivers[0]
                original = schemas.read(component)
                records = original['references']['RefIds']
                assert original['references']['version'] == 2
                assert original['mECViews'] == [dict(rid=row['rid']) for row in records]
                if len(records) != 1 or any(row['type'] != dict(asm='Assembly-CSharp', ns='',
                        **{'class': row['type']['class']}) or row['type']['class'] not in self.TYPES for row in records):
                    raise ValueError(('Unmigrated current battle view registry', entity, records))

                def convert(value):
                    if isinstance(value, dict) and set(value) == {'m_FileID', 'm_PathID'}:
                        if not value['m_PathID']:
                            return dict(fileID=0)
                        target = PPtr(**value, assetsfile=component.assets_file).deref()
                        if target.type.name != 'GameObject':
                            raise ValueError('Review the actual current view object type')
                        provenance = apk.provenance(target)
                        matches = native.get((provenance['bundle'], target.read().m_Name), [])
                        if len(matches) != 1:
                            raise ValueError(('Current view pointer has no unique native object', provenance, matches))
                        target_path = matches[0]
                        target_meta = Path(str(target_path) + '.meta')
                        target_text = target_path.read_text('utf-8-sig')
                        object_id = int(re.search(r'(?m)^--- !u!1 &(-?\d+)$', target_text)[1])
                        guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', target_meta.read_text('utf-8-sig'))[1]
                        self.pointer_bindings.append(dict(entity_id=entity, source=provenance,
                            object_sha256=hashlib.sha256(target.get_raw_data()).hexdigest(),
                            native=identity(target_path), native_meta=identity(target_meta), guid=guid, file_id=object_id))
                        return dict(fileID=object_id, guid=guid, type=3)
                    if isinstance(value, dict):
                        return {key: convert(child) for key, child in value.items()}
                    if isinstance(value, list):
                        return [convert(child) for child in value]
                    return value

                registry = convert(copy.deepcopy(original['references']))
                for row in registry['RefIds']:
                    row['type']['ns'] = row['type']['asm'] = 'Zjcs.Official.BattleSkillPlayables'
                restored = dict(mECViews=original['mECViews'], references=registry)
                self.restored[entity] = yaml.safe_dump(restored, sort_keys=False, allow_unicode=True, width=160)
                self.fields[entity] = {self.ALIASES[key]: value for key, value in registry['RefIds'][0]['data'].items()}
                self.counts[entity] = 0
                self.registrations.append(dict(entity_id=entity, source=apk.provenance(component),
                    object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest(), fields=original,
                    native=identity(path), native_meta=identity(sidecar)))

    def preprocess(self, text, path):
        entity = next((key for key, value in self.paths.items() if path == value), None)
        if entity is None:
            return text
        blocks = re.split(r'(?m)(?=^--- !u!)', text)
        count = 0
        for index, block in enumerate(blocks):
            if not re.search(r'(?m)^  m_Script: \{fileID: 401787210,', block):
                continue
            if 'mECViews:' in block or 'references:' in block:
                raise ValueError('Exported current driver fields changed; review before restoring')
            blocks[index] = block.rstrip() + '\n' + ''.join('  ' + line + '\n' for line in self.restored[entity].splitlines())
            count += 1
        assert count == 1, ('Current driver must be restored exactly once', entity)
        self.counts[entity] += 1
        return ''.join(blocks)

    def converted_fields(self, text, entity):
        drivers = [yaml.safe_load('MonoBehaviour:\n' + match[1])['MonoBehaviour']
                   for match in re.finditer(r'(?ms)^--- !u!114 &-?\d+\nMonoBehaviour:\n(.*?)(?=^--- !u!|\Z)', text)
                   if 'mECViews:' in match[1]]
        driver, = drivers
        record, = driver['references']['RefIds']
        return {self.ALIASES[key]: value for key, value in record['data'].items()}
