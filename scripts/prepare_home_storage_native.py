"""Bind the entire original storage prefab, components and resource closure."""
import hashlib
import json
from pathlib import Path
import uuid

from home_native_assets import install_staged_assets, write_staged_assets
from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity
from prepare_partner_card_native import PartnerCardBinding, guid, script_id, sha
from prepare_selected_wwise_inputs import prepare as prepare_audio
from prepare_bag_tip_dots import NODES as BAG_NODES


DEST = ROOT / 'unity/Assets/Official/Home/StorageBox/Native'
RECEIPT = ROOT / 'assets/reference/home-storage-native-source-manifest.json'
BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_furniture_storagebox.bundle'
PREFAB = 'Prefabs/Home/Dynamic/Furniture/StorageBox/StorageBox.prefab'


class StorageBinding(PartnerCardBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = {name: runtime / 'Official/HomeFurniture' / (name + '.cs') for name in (
            'EntityClickSound', 'StoreBoxFurniture', 'HomeNotice', 'HomeWeaponRack', 'HomeViewModeControl', 'HomeSwitcherActionEventHandler')}
        paths.update({
            'Home.ActorBehaviour.HomeBindingAuthoring': runtime / 'Official/HomeEntityBindings/HomeBindingAuthoring.cs',
            'HomeEntityDataProvider': runtime / 'Official/HomeEntityBindings/HomeEntityDataProvider.cs',
            'Home.Furniture.HomeEntityNodeRegister': runtime / 'Official/HomeEntityBindings/HomeEntityNodeRegister.cs',
            'HomePlayable.HomePlayableClipBind': runtime / 'Official/HomeEntityBindings/HomePlayableClipBind.cs',
            'StoryNodeRegister': runtime / 'Official/StoryNodes/StoryNodeRegister.cs',
            'MaintainGlobalScale': runtime / 'Official/WorldUI/WorldMaintainGlobalScale.cs',
            'TextMeshProColorProxy': runtime / 'Official/World/NativeLabelUI/TextMeshProColorProxy.cs',
            'MultiLanguageTMPControler': runtime / 'Official/World/MainlineMiniMapHud/MultiLanguageTMPControler.cs',
            'PreviewTool': runtime / 'Official/WorldUI/WorldPreviewTool.cs',
            'UnityEngine.Rendering.Universal.ShadowCaster2D': ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/2D/Shadows/ShadowCaster2D.cs',
        })
        paths.update({name: runtime / 'Official/BagTipDots' / (name + '.cs') for name in BAG_NODES})
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            if not path.is_file():
                raise ValueError('Migrate original native storage component: ' + full_name)
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[full_name] = pointer
            self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def sound(self, payload, owner):
        if not hasattr(self, 'audio_cache'):
            self.audio_cache = prepare_audio(self.cache.parent / 'home-storage-audio',
                ['wwisebnk_assets_wwisedata_android_minimum_pck.rawfile'])
            receipt = self.audio_cache / 'raw-resources-source-manifest.json'
            self.inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt.read_bytes())
        native_cache = self.cache
        try:
            self.cache = self.audio_cache
            result = super().sound(payload, owner)
            if result.get('fileID') and not getattr(self, 'sound_namespaced', False):
                old = result['guid']
                result['guid'] = hashlib.md5(('zjcs:home-storage-native-audio:' + old).encode()).hexdigest()
                self.audio_outputs = {path: (result['guid'] if key == old else key, raw)
                    for path, (key, raw) in self.audio_outputs.items()}
                self.script_guids.add(result['guid'])
                self.sound_namespaced = True
            return result
        finally:
            self.cache = native_cache

    def load(self, source_guid):
        result = super().load(source_guid)
        if result['guid'].startswith('0000000000000000') or result['guid'] in self.script_guids:
            return result
        old = result['guid']
        token = hashlib.md5(('zjcs:home-storage-native:' + old).encode()).hexdigest()
        result['guid'] = token
        self.queue = [(target, token if key == old else key, value) for target, key, value in self.queue]
        for proof in self.proof.values():
            if proof.get('guid') == old:
                proof['guid'] = token
        self.script_guids.add(token)
        return result

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'} and value['class'] in BAG_NODES:
            return dict(value, ns='Zjcs.Official.BagTipDots', asm='Zjcs.Official.BagTipDots')
        return super().replace(value)

    def build(self):
        root, = [pointer.deref() for path, pointer in self.recovery.apk.container(BUNDLE)
            if path.casefold() == ('Assets/' + PREFAB).casefold() and pointer.deref().type.name == 'GameObject']
        native = documents((self.native / PREFAB).read_text('utf8'))
        native_root, = [key for key, value in native.items() if value.get('GameObject', {}).get('m_Name') == root.read().m_Name]
        self.recovery.recover('StorageBox', self.work / 'recovered', bundle=BUNDLE,
            native_relative=PREFAB, source_root=root, native_root_id=native_root)
        self.recovery.finalize()
        _, _, panel, _ = self.recovery.outputs[-1]
        if any(value['type'] != 'MonoBehaviour' for value in panel['unresolved'].values()):
            raise ValueError('Resolve additional original storage prefab references')
        for key, pointer in panel['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        for script in panel['scripts']:
            name = '.'.join(filter(None, [script['namespace'], script['type']]))
            if name not in self.scripts:
                raise ValueError('Bind the actual original storage controller: ' + name)
            fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
            fields['m_Script'] = self.scripts[name]
            if name == 'EntityClickSound':
                owner = self.original(script['original'])
                fields['Sound'] = self.sound(self.recovery.fields(owner)['Sound'], owner)
        resolved = self.replace(panel['documents'])
        index = 0
        while index < len(self.queue):
            _, _, data = self.queue[index]
            if isinstance(data, dict):
                updated = self.replace(data)
                data.clear(); data.update(updated)
            index += 1
        pointer = dict(fileID=panel['root'], guid=hashlib.md5(('zjcs:home-storage-native-prefab:' + identity(root)).encode()).hexdigest(), type=3)
        self.queue.append((Path('StorageBox.prefab'), pointer['guid'], resolved))
        self.queue.extend((path, token, raw) for path, (token, raw) in self.audio_outputs.items())
        staged = self.work / 'assets'
        outputs = write_staged_assets(self.queue, staged)
        return outputs, staged, pointer, dict(root=identity(root), objects=panel['objectMap'], scripts=panel['scripts'],
            resolved_graph_references=panel['unresolved'])


def build():
    source = OfficialSource()
    work = ROOT / 'output/validation' / ('home-storage-native-' + uuid.uuid4().hex)
    work.mkdir(parents=True)
    (work / '.zjcs-validation.json').write_text(json.dumps(dict(format=1, repository=str(ROOT))), 'utf8')
    recovery = PartnerNativeRecovery(native_cache=ROOT / '.tools/source-packages/current' / source.version / 'home-storage-native',
        export_name='native-export', bundle=BUNDLE)
    binding = StorageBinding(work, recovery=recovery)
    outputs, staged, prefab, selection = binding.build()
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    installed = install_staged_assets(work, staged, outputs, DEST, 'native-storage-building', previous_outputs=previous)
    for name in ('prepare_home_storage_native.py', 'prepare_selected_wwise_inputs.py'):
        path = ROOT / 'scripts' / name
        binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt = dict(manifest=source.version, inputs=binding.inputs, selection=selection, recovery=recovery.proof,
        native_scripts=binding.script_sources, resources=binding.proof, audio=binding.audio_proof,
        prefab=prefab, outputs=installed,
        validation_inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
            ROOT / 'scripts/fixtures/bag_tip_dot_validation.cs', ROOT / 'scripts/test_bag_tip_dots.ps1')},
        scope='Complete original storage hierarchy with migrated component/resource identities. Host ownership, gameplay and Player verification remain separate.')
    stage_receipt = work / 'bound-source-manifest.json'
    stage_receipt.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    stage_receipt.replace(RECEIPT)
    print('Bound complete original storage prefab:', len(installed), 'files; work', work)


if __name__ == '__main__':
    build()
