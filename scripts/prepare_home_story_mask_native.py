"""Recover the original invisible window mask used by the story dialog."""
import hashlib
import json
from pathlib import Path

from home_native_assets import write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import documents, identity
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_input_dialog import source_pointer
from validation_workspace import validation_directory


class StoryMaskBinding(PartnerGiftBinding):
    def load(self, source_guid):
        pointer = super().load(source_guid)
        self.script_guids.add(pointer['guid'])
        return pointer

    def register_scripts(self):
        super().register_scripts()
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldWindowMask.cs'
        meta = Path(str(path) + '.meta')
        pointer = dict(fileID=11500000, guid=guid(meta), type=3)
        self.scripts['WindowMask'] = pointer
        self.class_ids[script_id('', 'WindowMask')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['WindowMask'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def build(self):
        bundle = 'defaultpackage_assets_prefabs_ui_main.bundle'
        containers = []
        for obj in self.recovery.apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour': continue
            script = obj.read(check_read=False).m_Script.read()
            if script.m_ClassName == 'WindowContainer' and self.recovery.fields(obj)['mContainerType'] == 0:
                containers.append((obj, script))
        if len(containers) != 1: raise ValueError('Inspect the original default window container.')
        container, script = containers[0]
        original = self.recovery.fields(container)
        masks = [(index, source_pointer(value, container)) for index, value in enumerate(original['MaskList'])]
        matches = [(index, mask) for index, mask in masks if self.recovery.fields(mask)['mMaskType'] == 2]
        if len(matches) != 1: raise ValueError('Inspect the original invisible window mask.')
        index, mask = matches[0]
        native = self.native / 'Prefabs/UI/Main/MainCanvas.prefab'
        native_docs = documents(native.read_text('utf8'))
        native_container, = [doc['MonoBehaviour'] for doc in native_docs.values() if 'MonoBehaviour' in doc
            and doc['MonoBehaviour']['m_Script']['fileID'] == script_id(script.m_Namespace, script.m_ClassName)
            and doc['MonoBehaviour'].get('mContainerType') == 0]
        native_mask = native_docs[native_container['MaskList'][index]['fileID']]['MonoBehaviour']
        if native_mask['mMaskType'] != 2: raise ValueError('Original/native invisible mask differs.')
        source_root = source_pointer(self.recovery.fields(mask)['m_GameObject'], mask)
        self.recovery.recover('HomeStoryInvisibleMask', self.work / 'home-story-mask-recovery', bundle=bundle,
            native_relative='Prefabs/UI/Main/MainCanvas.prefab', source_root=source_root,
            native_root_id=native_mask['m_GameObject']['fileID'])
        self.recovery.finalize()
        _, _, panel, _ = self.recovery.outputs[-1]
        if panel['unresolved']: raise ValueError('Resolve original invisible mask dependencies: ' + repr(panel['unresolved']))
        for key, pointer in panel['external'].items(): self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        for script_info in panel['scripts']:
            full = '.'.join(filter(None, (script_info['namespace'], script_info['type'])))
            if full not in self.scripts: raise ValueError('Bind original invisible mask component: ' + full)
            panel['documents'][script_info['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[full]
        key = hashlib.md5(b'zjcs:home-story-dialog:InvisibleMask').hexdigest()
        self.queue.append((Path('InvisibleMask.prefab'), key, self.replace(panel['documents'])))
        for _, _, data in self.queue:
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        outputs = write_staged_assets(self.queue, self.work / 'home-story-mask-assets')
        for path in (native, Path(str(native) + '.meta'), Path(__file__), ROOT / 'scripts/prepare_partner_input_dialog.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs,
            mask_selection=dict(container=identity(container), source=identity(mask), list_index=index,
                original_container_fields=original, native_source=native.relative_to(ROOT).as_posix()),
            root=dict(fileID=panel['root'], guid=key, type=3), objects=self.proof,
            script_bindings=self.script_sources, recovery=self.recovery.proof, outputs=outputs,
            scope='Original WindowMaskType.InvisibleMask selected by the dialogue prefab.')
        (self.work / 'home-story-mask-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original story invisible window mask:', len(panel['objectMap']), 'objects', flush=True)


if __name__ == '__main__':
    StoryMaskBinding(validation_directory()).build()
