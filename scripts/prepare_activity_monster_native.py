#!/usr/bin/env python3
"""Bind original activity Spine icon prefabs using the existing native UI renderer."""
import argparse
import copy
import hashlib
import json
import re
from pathlib import Path

import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import identity, documents
from prepare_partner_card_native import Dumper, guid, sha, script_id
from prepare_activity_battle_team_native import extract
from prepare_partner_detail_actor_native import PartnerDetailActorBinding
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory

NAME = 'activity-monster-native-source-manifest.json'


def yaml_bytes(data):
    result = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for file_id, content in data.items():
        kind, = content
        result += f'--- !u!{int(ClassIDType[kind])} &{file_id}\n'
        result += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    return result.encode('utf8')


class ActivityMonsterBinding(PartnerDetailActorBinding):
    def register_scripts(self):
        super().register_scripts()
        path = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI/MonsterUI.cs'
        pointer = self.script_pointer(path)
        self.scripts['MonsterUI'] = pointer
        self.class_ids[script_id('', 'MonsterUI')] = pointer

    def friendship_templates(self):
        table = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        self.prefab_addresses = [dict(row, officialAssetGuid=next(item['guid'] for item in table if item['path'] == row['path']))
            for row in self.actor_proof['model_templates']]
        for row in self.actor_proof['model_templates']:
            self.recovery.recover(row['name'], self.work / 'native-monsters', bundle=row['bundle'],
                native_relative=row['path'].removeprefix('Assets/'))
        self.recovery.finalize()
        requests = [row['textureLoader']['path'] for row in self.actor_rows.values()]
        requests += [row['path'] for row in table if row['path'].endswith('/AppearanceConfig.asset')]
        for path in dict.fromkeys(requests):
            entry, = [row for row in table if row['path'] == path]
            obj = self.recovery.apk.asset(entry['bundle'], entry['path'])
            native = self.native / path.removeprefix('Assets/')
            native_guid = guid(Path(str(native) + '.meta'))
            self.objects[native_guid] = obj
            self.dynamic_assets.append(dict(path=path, officialAssetGuid=entry['guid'], nativeGuid=native_guid,
                original=identity(obj), source=self.recovery.apk.provenance(obj), original_sha256=sha(obj.get_raw_data()),
                asset=self.load(native_guid)))
        for row in self.actor_rows.values():
            for material in json.loads((self.work / 'detail-actor-data' / row['materials']).read_bytes()):
                for texture in material['runtimeTextures']:
                    if any(item['guid'] == texture['guid'] for item in self.texture_addresses):
                        continue
                    self.texture_addresses.append(dict(guid=texture['guid'], path=texture['path'],
                        asset=self.load_original(texture['original'])))
        templates = []
        for _, _, original, evidence in self.recovery.outputs:
            data = copy.deepcopy(original)
            root = data['documents'][data['root']]['GameObject']['m_Component'][0]['component']['fileID']
            graphic, = [item for item in data['scripts'] if item['type'] == 'SkeletonGraphic']
            go = data['documents'][graphic['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
            bootstrap = max(data['documents']) + 1
            data['documents'][go]['GameObject']['m_Component'].insert(1, dict(component=dict(fileID=bootstrap)))
            data['documents'][bootstrap] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
                m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0),
                m_GameObject=dict(fileID=go), m_Enabled=1, m_EditorHideFlags=0,
                m_Script=self.script_pointer(ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeUiActor.cs'),
                m_Name='', m_EditorClassIdentifier='', graphic=dict(fileID=graphic['nativeFileID']), renderRoot=dict(fileID=root)))
            templates.append((evidence['name'], data))
            self.actor_roots.append(dict(name=evidence['name'], source=evidence, bootstraps=[bootstrap],
                adaptation='Preserve original nodes, fields, order and geometry; insert the existing native renderer bootstrap.'))
        self.template_selection = self.actor_roots
        self.recovery.recover('ActivityDomainBossPanel', self.work / 'activity-monster-host',
            bundle='defaultpackage_assets_prefabs_ui_activitydomainboss.bundle',
            native_relative='Prefabs/UI/ActivityDomainBoss/ActivityDomainBossPanel.prefab')
        self.recovery.finalize()
        _, _, panel, _ = self.recovery.outputs[-1]
        host, selection = extract(panel, field='mMonsterUI')
        templates.append(('ActivityDomainBossMonsterUI', host))
        self.actor_roots.append(dict(name='ActivityDomainBossMonsterUI', selection=selection))
        return templates

    def build(self):
        templates = self.friendship_templates()
        for row in self.recovery.proof['prefabs']:
            native = ROOT / row['nativeSource']
            for path in (native, Path(str(native) + '.meta')):
                expected = self.checked[path.relative_to(self.cache / 'native-export-all').as_posix()]
                if sha(path.read_bytes()) != expected:
                    raise ValueError('Verified original monster prefab changed: ' + str(path))
                self.inputs[path.relative_to(ROOT).as_posix()] = expected
        for _, prefab in templates:
            for key, pointer in prefab['external'].items():
                self.objects[pointer['guid']] = self.original(key)
            for key, file_id in prefab['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(prefab['documents'][file_id].values())), owner)
        roots, prefabs = {}, []
        for name, prefab in templates:
            for script in prefab['scripts']:
                fields = prefab['documents'][script['nativeFileID']]['MonoBehaviour']
                full_name = '.'.join(filter(None, [script['namespace'], script['type']]))
                fields['m_Script'] = self.scripts[full_name]
            resolved = self.replace(prefab['documents'])
            target_guid = hashlib.md5(('zjcs:activity-monster:' + name).encode()).hexdigest()
            prefabs.append((Path(name + '.prefab'), target_guid, resolved))
            roots[name] = dict(fileID=prefab['root'], guid=target_guid, type=3)
        for _, _, data in self.queue:
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        self.queue.extend(prefabs)
        folder, outputs = self.work / 'monster-assets', {}
        for target, target_guid, data in self.queue:
            raw = yaml_bytes(data) if isinstance(data, dict) else data
            path = folder / target
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            importer = ('PrefabImporter' if path.suffix == '.prefab' else 'ShaderImporter' if path.suffix == '.shader'
                else 'TextScriptImporter' if path.suffix in ('.json', '.bytes') else 'NativeFormatImporter')
            meta = Path(str(path) + '.meta')
            meta.write_text(f'fileFormatVersion: 2\nguid: {target_guid}\n{importer}:\n  externalObjects: {{}}\n', 'utf8', newline='\n')
            for item in (path, meta): outputs[item.relative_to(folder).as_posix()] = sha(item.read_bytes())
        inputs = dict(self.inputs)
        for path, expected in self.actor_proof['inputs'].items():
            if sha((ROOT / path).read_bytes()) != expected:
                raise ValueError('Rebuild original monster data after input changes: ' + path)
            inputs[path] = expected
        for name in ('prepare_activity_monster_native.py', 'prepare_partner_card_native.py', 'prepare_partner_gift_native.py',
                'prepare_partner_detail_window_native.py', 'prepare_partner_detail_actor_native.py'):
            path = ROOT / 'scripts' / name
            inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        proof = dict(manifest=self.recovery.apk.version, inputs=inputs, outputs=outputs, roots=roots,
            objects=self.proof, native_recovery=self.recovery.proof, script_bindings=self.script_sources,
            actor_data=self.actor_proof, native_actors=self.native_actors, native_atlases=self.native_atlases,
            dynamic_assets=self.dynamic_assets, texture_addresses=self.texture_addresses, prefab_addresses=self.prefab_addresses,
            template_selection=self.template_selection,
            scope='Six original Spine activity icons; TempRTUI icon 3706 and full activity page remain pending.')
        (self.work / NAME).write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original activity monster assets:', len(outputs), flush=True)


def install(work):
    proof = json.loads((work / NAME).read_bytes())
    for path, expected in proof['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Activity monster input changed: ' + path)
    for row in proof['script_bindings'].values():
        for suffix, key in (('', 'sha256'), ('.meta', 'meta_sha256')):
            if sha((ROOT / (row['path'] + suffix)).read_bytes()) != row[key]:
                raise ValueError('Activity monster runtime binding changed: ' + row['path'])
    staged = work / 'monster-assets'
    for path, expected in proof['outputs'].items():
        if sha((staged / path).read_bytes()) != expected: raise ValueError('Activity monster output changed: ' + path)
    # Isolate native serialization identities so another consumer's converted
    # resources cannot overwrite this original closure, or be overwritten by it.
    assets = {guid(Path(str(staged / path) + '.meta')): path for path in proof['outputs'] if not path.endswith('.meta')}
    aliases = {key: hashlib.md5(('zjcs:activity-monster-closure:' + key).encode()).hexdigest() for key in assets}
    def remap(raw):
        return re.sub(rb'(?<=guid: )[0-9a-f]{32}', lambda m: aliases.get(m[0].decode(), m[0].decode()).encode(), raw)
    def mapped(pointer): return dict(pointer, guid=aliases[pointer['guid']])
    base = ROOT / 'unity/Assets/Official/DailyChallenges/Resources/ActivityMonsterIcons'
    planned = {}
    for key, relative in assets.items():
        path = staged / relative
        raw = path.read_bytes()
        planned[base / relative] = remap(raw) if raw.startswith(b'%YAML') else raw
        meta = Path(str(path) + '.meta').read_bytes()
        planned[Path(str(base / relative) + '.meta')] = remap(meta)
    config, = [row['asset'] for row in proof['dynamic_assets'] if row['path'].endswith('/AppearanceConfig.asset')]
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeDetailActorAssets.cs'
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(metadata(script)), type=3),
        m_Name='ActivityMonsterAssets', m_EditorClassIdentifier='', config=mapped(config),
        assets=[dict(Path=row['path'], Asset=mapped(row['asset']), Resource='') for row in proof['dynamic_assets']],
        prefabs=[dict(Path=row['path'].removeprefix('Assets/Prefabs/').removesuffix('.prefab'),
            Prefab=mapped(proof['roots'][row['name']]), Resource='') for row in proof['actor_data']['model_templates']],
        textures=[dict(Guid=row['guid'], Resource='ActivityMonsterIcons/' + str(Path(assets[row['asset']['guid']]).with_suffix('')).replace('\\', '/'))
            for row in proof['texture_addresses']],
        atlases=[mapped(row) for row in proof['native_atlases'].values()], atlasSourceGuids=[])
    catalog = ROOT / 'unity/Assets/Game/Resources/DailyChallenges/ActivityMonsterAssets.asset'
    planned[catalog] = yaml_bytes({11400000: dict(MonoBehaviour=body)})
    loader_script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
    loader_body = dict(body)
    for field in ('config', 'assets', 'prefabs', 'textures', 'atlases', 'atlasSourceGuids'): loader_body.pop(field)
    loader_body.update(m_Name='ActivityMonsterPrefabs', m_Script=dict(fileID=11500000, guid=guid(metadata(loader_script)), type=3),
        Entries=[dict(Path=row['path'].removeprefix('Assets/Prefabs/').removesuffix('.prefab'),
            AssetGuid=row['officialAssetGuid'], Asset=mapped(proof['roots'][row['name']]))
            for row in proof['prefab_addresses']])
    loader_catalog = catalog.with_name('ActivityMonsterPrefabs.asset')
    planned[loader_catalog] = yaml_bytes({11400000: dict(MonoBehaviour=loader_body)})
    previous = ROOT / 'assets/reference/daily-challenges' / NAME
    old = json.loads(previous.read_bytes()).get('installed_output_sha256', {}) if previous.exists() else {}
    for path, raw in planned.items():
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != old.get(path.relative_to(ROOT).as_posix()):
            raise ValueError('Preserve existing activity monster asset: ' + str(path))
    for path, raw in planned.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
    for asset in (catalog, loader_catalog):
        meta = metadata(asset, 'NativeFormatImporter')
        planned[meta] = meta.read_bytes()
    parent = base
    while parent.is_relative_to(ROOT / 'unity/Assets/Official/DailyChallenges'):
        meta = metadata(parent, 'DefaultImporter', True)
        planned[meta] = meta.read_bytes()
        parent = parent.parent
    for folder in (p for p in base.rglob('*') if p.is_dir()):
        meta = metadata(folder, 'DefaultImporter', True)
        planned[meta] = meta.read_bytes()
    proof['installed_output_sha256'] = {p.relative_to(ROOT).as_posix(): sha(raw) for p, raw in planned.items()}
    proof['isolated_serialization_guids'] = aliases
    previous.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original activity monster assets:', len(planned), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install: install(work)
    else:
        binding = ActivityMonsterBinding(work)
        try: binding.build()
        finally: binding.recovery.apk.archive.close()


if __name__ == '__main__': main()
