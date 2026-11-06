"""Restore the complete original 3706 RT hierarchy and its native dependencies."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import identity
from prepare_partner_card_native import PartnerCardBinding, guid, sha, script_id
from prepare_partner_detail_actor_native import PartnerDetailActorBinding
from prepare_partner_input_dialog import metadata
from prepare_activity_monster_native import yaml_bytes
from validation_workspace import validation_directory

NAME = 'activity-rt-prefab-source-manifest.json'


class ActivityRtPrefabBinding(PartnerDetailActorBinding):
    def replace(self, value):
        # This closure has no partner panel state or animation-script remaps.
        # Preserve the native empty strings before the common pointer resolver.
        if isinstance(value, dict):
            value = {key: '' if item is None and key in ('mAssetGuid', 'DyeTexturePath') else item
                     for key, item in value.items()}
            stream = value.get('m_StreamData')
            if isinstance(stream, dict) and stream.get('path') is None:
                value['m_StreamData'] = dict(stream, path='')
        return PartnerCardBinding.replace(self, value)

    def register_scripts(self):
        self.script_guids = set()
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        self.actor_scripts = {
            'Spine.Unity.SkeletonMecanim': runtime / 'Offline/ProfessionUI/BattleNativeMonsterRenderer.cs',
            'AppearanceSystem.AppearanceRendererLite': runtime / 'Official/ProfessionUI/AppearanceRendererLite.cs',
            'AttachmentFollower': runtime / 'Official/BattleSpinePlayables/AttachmentFollower.cs',
            'AppearanceSystem.AppearanceConfig': runtime / 'Official/ProfessionUI/AppearanceConfig.cs',
            'AppearanceSystem.YooAssetTextureLoader': runtime / 'Official/ProfessionUI/YooAssetTextureLoader.cs',
        }
        for name, path in self.actor_scripts.items():
            reference = self.script_pointer(path)
            self.scripts[name] = reference
            namespace, _, leaf = name.rpartition('.')
            self.class_ids[script_id(namespace, leaf)] = reference

    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'AnimatorController':
            if obj.read().m_Name != 'monster_3706_Controller':
                raise ValueError('Reinspect additional original RT Animator')
            catalog = json.loads((self.work / 'live-monsters/catalog.json').read_bytes())
            model, = catalog['models']
            target = ROOT / 'unity/Assets/Official/DailyChallenges/ActivityRtMonsters' / model['nativeController']
            pointer = dict(fileID=9100000, guid=guid(Path(str(target) + '.meta')), type=2)
            self.script_guids.add(pointer['guid'])
            for path in (target, Path(str(target) + '.meta')):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            return pointer
        return super().load(source_guid)

    def build(self):
        relative = 'Prefabs/Appearance/Monster/3706/MonsterModel_3706_RT.prefab'
        self.recovery.recover('MonsterModel_3706_RT', self.work / 'native-rt-prefab',
            bundle='defaultpackage_assets_prefabs_appearance_monster_3706.bundle', native_relative=relative)
        self.recovery.finalize()
        native, _, prefab, evidence = self.recovery.outputs[-1]
        for path in (native, Path(str(native) + '.meta')):
            expected = self.checked[path.relative_to(self.cache / 'native-export-all').as_posix()]
            if sha(path.read_bytes()) != expected: raise ValueError('Original RT prefab changed')
            self.inputs[path.relative_to(ROOT).as_posix()] = expected
        if len(prefab['documents']) != 373 or len(prefab['scripts']) != 22 or prefab['unresolved']:
            raise ValueError('Original complete RT hierarchy differs')
        for key, pointer in prefab['external'].items(): self.objects[pointer['guid']] = self.original(key)
        for key, file_id in prefab['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(prefab['documents'][file_id].values())), owner)
        data = prefab['documents']
        catalog = ROOT / 'unity/Assets/Official/DailyChallenges/ActivityRtMonsters/Resources/DailyChallenges/ActivityRtMonsters/catalog.asset'
        catalog_pointer = dict(fileID=11400000, guid=guid(metadata(catalog)), type=2)
        self.script_guids.add(catalog_pointer['guid'])
        for row in prefab['scripts']:
            fields = data[row['nativeFileID']]['MonoBehaviour']
            full_name = '.'.join(filter(None, (row['namespace'], row['type'])))
            fields['m_Script'] = self.scripts[full_name]
            if row['type'] == 'SkeletonMecanim':
                fields.pop('skeletonDataAsset')
                fields.update(nativeAssets=catalog_pointer, nativeEntityId=3706)
                go = fields['m_GameObject']['fileID']
                mesh_part_id = max(data) + 1
                data[go]['GameObject']['m_Component'].append(dict(component=dict(fileID=mesh_part_id)))
                data[mesh_part_id] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
                    m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0),
                    m_GameObject=dict(fileID=go), m_Enabled=1, m_EditorHideFlags=0,
                    m_Script=self.script_pointer(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneMeshPart.cs'),
                    m_Name='', m_EditorClassIdentifier=''))
        resolved = self.replace(data)
        # Original runtime-selected Low loader and appearance configuration.
        table = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        requests = ['Assets/AppearanceAssets/Monster/monster_3706/monster_3706_TextureLoader_Low.asset']
        requests += [row['path'] for row in table if row['path'].endswith('/AppearanceConfig.asset')]
        for path in requests:
            entry, = [row for row in table if row['path'] == path]
            obj = self.recovery.apk.asset(entry['bundle'], entry['path'])
            native = self.native / path.removeprefix('Assets/')
            source_guid = guid(Path(str(native) + '.meta'))
            self.objects[source_guid] = obj
            pointer = self.load(source_guid)
            self.dynamic_assets.append(dict(path=path, asset=pointer, source=self.recovery.apk.provenance(obj), original=identity(obj)))
        loader = self.dynamic_assets[0]
        obj = self.original(loader['original'])
        fields = self.recovery.fields(obj)
        # Resolve the original requested GUIDs, rather than the placeholder textures.
        by_guid = {row['guid']: row for row in table}
        for mapping in fields['placeholderMap']:
            for field in ('targetTextureReference', 'dyeTextureReference'):
                key = mapping[field]['AssetReference']['mAssetGuid']
                if not key: continue
                location = by_guid[key]
                texture = self.recovery.apk.asset(location['bundle'], location['path'])
                self.texture_addresses.append(dict(guid=key, path=location['path'], asset=self.load_original(identity(texture))))
        for _, _, payload in self.queue:
            if isinstance(payload, dict):
                replacement = self.replace(payload); payload.clear(); payload.update(replacement)
        target_guid = hashlib.md5(b'zjcs:activity-rt-prefab:3706').hexdigest()
        self.queue.append((Path('MonsterModel_3706_RT.prefab'), target_guid, resolved))
        outputs = {}
        stage = self.work / 'rt-prefab-assets'
        for relative, key, payload in self.queue:
            path = stage / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(yaml_bytes(payload) if isinstance(payload, dict) else payload)
            importer = ('PrefabImporter' if path.suffix == '.prefab' else 'ShaderImporter' if path.suffix == '.shader'
                else 'TextScriptImporter' if path.suffix in ('.json', '.bytes') else 'NativeFormatImporter')
            sidecar = Path(str(path) + '.meta')
            sidecar.write_text(f'fileFormatVersion: 2\nguid: {key}\n{importer}:\n  externalObjects: {{}}\n', 'utf8', newline='\n')
            for output in (path, sidecar): outputs[output.relative_to(stage).as_posix()] = sha(output.read_bytes())
        proof = dict(manifest=self.recovery.apk.version, original=evidence, recovery=self.recovery.proof,
            inputs=self.inputs, outputs=outputs, objects=self.proof, script_bindings=self.script_sources,
            dynamic_assets=self.dynamic_assets, texture_addresses=self.texture_addresses,
            native_atlases=list(self.native_atlases.values()),
            root=dict(fileID=prefab['root'], guid=target_guid, type=3),
            adaptations=['Replace the native SDK renderer with the shared battle pose and mesh module.',
                'Keep all 373 original objects and serialized application components; add one native mesh adapter.',
                'Reuse the verified original RT Animator closure and Low texture request mappings.'],
            generator_sha256=sha(Path(__file__).read_bytes()), scope='Complete original RT hierarchy staged; runtime validation and production binding pending.')
        (self.work / NAME).write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged complete original RT hierarchy:', len(data), 'objects;', len(outputs), 'outputs', flush=True)


def main():
    binding = ActivityRtPrefabBinding(validation_directory())
    try: binding.build()
    finally: binding.recovery.apk.archive.close()


if __name__ == '__main__': main()
