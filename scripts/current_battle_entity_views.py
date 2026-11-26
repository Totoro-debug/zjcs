"""Restore selected current managed battle views from verified serialized objects."""
import copy
import hashlib
import json
from pathlib import Path
import re

import yaml
import UnityPy
from UnityPy.classes.PPtr import PPtr

from official_apk import Apk, ROOT
from official_source import OfficialSource
from current_component_schemas import CurrentSchemas
from prepare_battle_scene_appearance_runtime import identity
from prepare_unity_bundle_inputs import serialized_files


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
    TYPES = {'ECFightSkillView', 'ECFightHitView', 'ECFightDamageView', 'ECFightStatusPlayableView',
             'ECFightActionSkillMoveView'}
    EVENT_TYPES = {'ECStatusInvokedEventSourceView', 'PlayPlayableWithSourceAndTargetHandler',
                   'StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter'}

    @classmethod
    def primary_record(cls, records):
        primary = [row for row in records if row['type']['class'] in cls.TYPES]
        if len(primary) != 1:
            raise ValueError('A current battle entity requires one primary playable view')
        return primary[0]

    @classmethod
    def validate_registry(cls, original):
        records = original['references']['RefIds']
        if original['references']['version'] != 2:
            raise ValueError('Unsupported current managed-reference registry version')
        by_id = {row['rid']: row for row in records}
        if len(by_id) != len(records):
            raise ValueError('Duplicate current managed-reference identifier')
        view_ids = [reference['rid'] for reference in original['mECViews']]
        if len(view_ids) != len(set(view_ids)):
            raise ValueError('Duplicate current view registration')
        for row in records:
            name = row['type']['class']
            if row['type'] != dict(asm='Assembly-CSharp', ns='', **{'class': name}) or name not in cls.TYPES | cls.EVENT_TYPES:
                raise ValueError(('Unmigrated current battle view registry', row['type']))
        reached = set()

        def visit(value):
            if isinstance(value, dict) and set(value) == {'rid'}:
                rid = value['rid']
                if rid not in by_id:
                    raise ValueError(('Unresolved current managed reference', rid))
                if rid not in reached:
                    reached.add(rid)
                    visit(by_id[rid]['data'])
            elif isinstance(value, dict):
                for child in value.values():
                    visit(child)
            elif isinstance(value, list):
                for child in value:
                    visit(child)

        for reference in original['mECViews']:
            if reference.get('rid') not in by_id or by_id[reference['rid']]['type']['class'] not in cls.TYPES | {'ECStatusInvokedEventSourceView'}:
                raise ValueError('A current view driver references a non-view record')
            visit(reference)
        if reached != by_id.keys():
            raise ValueError('Unreferenced current managed records require review')
        cls.primary_record(records)
        return records
    ALIASES = {
        'mBlockPlayablePrefab': 'BlockPlayablePrefab',
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
        'mStackedPlayable': 'StackedPlayablePrefab', 'mStackPlayableMap': 'StackPlayableMap',
        'mStackPlayable': 'StackPlayable', 'mMasterCastPlayable': 'MasterCastPlayable',
    }

    def __init__(self, exported, paths):
        self.exported = exported
        self.paths = {int(Path(path).stem): exported / path for path in paths}
        self.restored, self.fields, self.counts = {}, {}, {}
        self.registrations, self.pointer_bindings = [], []
        schemas, self.schema_inputs, self.schema_tools = current_battle_schemas()
        native, unbundled, original_roots = {}, {}, {}
        cache = exported.parents[2]
        for sidecar in exported.rglob('*.prefab.meta'):
            text = sidecar.read_text('utf-8-sig')
            bundle = re.search(r'(?m)^  assetBundleName: (.+)$', text)
            if bundle:
                key = (bundle[1] + '.bundle', sidecar.name.removesuffix('.prefab.meta'))
                native.setdefault(key, []).append(sidecar.with_suffix(''))
            else:
                prefab = sidecar.with_suffix('')
                block = re.search(r'(?ms)^--- !u!1 &-?\d+\nGameObject:\n(.*?)(?=^--- !u!|\Z)',
                                  prefab.read_text('utf-8-sig'))
                if block:
                    name = str(yaml.safe_load('GameObject:\n' + block[1])['GameObject']['m_Name'])
                    unbundled.setdefault(name, []).append(prefab)

        def file_key(file):
            return str(file.name).replace('\\', '/').rsplit('/', 1)[-1].casefold()

        def unique_original_root(target):
            name = target.read().m_Name
            if name not in original_roots:
                matches = {}
                input_path = cache / 'source-manifest.json'
                for row in json.loads(input_path.read_bytes())['inputs']:
                    if row['format'] not in ('UnityFS', 'builtin'):
                        continue
                    raw = (cache / row['output']).read_bytes()
                    if hashlib.sha256(raw).hexdigest() != row['decoded_sha256']:
                        raise ValueError('Changed native input while resolving an unbundled prefab')
                    env = UnityPy.load(raw)
                    for bundle in env.files.values():
                        for file in serialized_files(bundle):
                            for obj in file.objects.values():
                                if obj.type.name != 'GameObject' or obj.read().m_Name != name:
                                    continue
                                transforms = [ref.component.deref() for ref in obj.read().m_Component
                                              if ref.component.deref().type.name in ('Transform', 'RectTransform')]
                                if len(transforms) != 1 or transforms[0].read().m_Father.m_PathID:
                                    continue
                                key = file_key(file), obj.path_id
                                digest = hashlib.sha256(obj.get_raw_data()).hexdigest()
                                if key in matches and matches[key]['object_sha256'] != digest:
                                    raise ValueError('Conflicting unbundled original prefab identity')
                                matches[key] = dict(input=row['output'], object_sha256=digest)
                original_roots[name] = matches
            matches = original_roots[name]
            key = file_key(target.assets_file), target.path_id
            if len(matches) != 1 or key not in matches or matches[key]['object_sha256'] != hashlib.sha256(target.get_raw_data()).hexdigest():
                raise ValueError(('Unbundled prefab is not unique in the verified native inputs', name))
            return dict(input_manifest=identity(cache / 'source-manifest.json'),
                        cab=key[0], path_id=str(key[1]), **matches[key])
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
                records = self.validate_registry(original)

                def convert(value):
                    if isinstance(value, dict) and set(value) == {'m_FileID', 'm_PathID'}:
                        if not value['m_PathID']:
                            return dict(fileID=0)
                        target = PPtr(**value, assetsfile=component.assets_file).deref()
                        if target.type.name != 'GameObject':
                            raise ValueError('Review the actual current view object type')
                        provenance = apk.provenance(target)
                        matches = native.get((provenance['bundle'], target.read().m_Name), [])
                        orphan = None
                        if not matches and len(unbundled.get(target.read().m_Name, [])) == 1:
                            orphan = unique_original_root(target)
                            matches = unbundled[target.read().m_Name]
                        if len(matches) != 1:
                            raise ValueError(('Current view pointer has no unique native object', provenance, matches))
                        target_path = matches[0]
                        target_meta = Path(str(target_path) + '.meta')
                        target_text = target_path.read_text('utf-8-sig')
                        object_id = int(re.search(r'(?m)^--- !u!1 &(-?\d+)$', target_text)[1])
                        guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', target_meta.read_text('utf-8-sig'))[1]
                        binding = dict(entity_id=entity, source=provenance,
                            object_sha256=hashlib.sha256(target.get_raw_data()).hexdigest(),
                            native=identity(target_path), native_meta=identity(target_meta), guid=guid, file_id=object_id)
                        if orphan is not None:
                            binding['unbundled_original'] = orphan
                        self.pointer_bindings.append(binding)
                        return dict(fileID=object_id, guid=guid, type=3)
                    if isinstance(value, dict):
                        return {key: convert(child) for key, child in value.items()}
                    if isinstance(value, list):
                        return [convert(child) for child in value]
                    return value

                registry = convert(copy.deepcopy(original['references']))
                for row in registry['RefIds']:
                    namespace = 'Zjcs.Official.BattleStatusPlayables' if row['type']['class'] in {
                        'ECFightStatusPlayableView', 'ECStatusInvokedEventSourceView',
                        'StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter'} else 'Zjcs.Official.BattleSkillPlayables'
                    row['type']['ns'] = row['type']['asm'] = namespace
                restored = dict(mECViews=original['mECViews'], references=registry)
                self.restored[entity] = yaml.safe_dump(restored, sort_keys=False, allow_unicode=True, width=160)
                self.fields[entity] = {self.ALIASES[key]: value for key, value in self.primary_record(registry['RefIds'])['data'].items()}
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
        record = self.primary_record(driver['references']['RefIds'])
        return {self.ALIASES[key]: value for key, value in record['data'].items()}
