"""Recover the original dialogue character group and its animation clips."""
import hashlib
import json
from pathlib import Path

from home_native_assets import write_staged_assets
from prepare_current_partner_native import documents, identity
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory
from official_apk import ROOT


class StoryCharacterBinding(PartnerGiftBinding):
    def load(self, source_guid):
        pointer = super().load(source_guid)
        self.script_guids.add(pointer['guid'])
        return pointer

    def register_scripts(self):
        super().register_scripts()
        path = self.work / 'home-story-character-code/TalkDialogCharacter.cs'
        meta = Path(str(path) + '.meta')
        pointer = dict(fileID=11500000, guid=guid(meta), type=3)
        self.scripts['TalkDialogCharacter'] = pointer
        self.class_ids[script_id('', 'TalkDialogCharacter')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['TalkDialogCharacter'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def build(self):
        bundle = 'defaultpackage_assets_prefabs_ui_story.bundle'
        relative = 'Prefabs/UI/Story/OptionTalkDialog.prefab'
        native = self.native / relative
        root = self.recovery.apk.asset(bundle, 'OptionTalkDialog.prefab')
        def descendants(go):
            yield go
            for child in go.read().m_Component[0].component.read().m_Children:
                yield from descendants(child.read().m_GameObject.deref())
        source, = [go for go in descendants(root) if go.read().m_Name == 'CharacterGroup']
        native_docs = documents(native.read_text('utf8'))
        native_root, = [key for key, value in native_docs.items()
                        if value.get('GameObject', {}).get('m_Name') == 'CharacterGroup']
        self.recovery.recover('CharacterGroup', self.work / 'home-story-character-recovery', bundle=bundle,
            native_relative=relative, source_root=source, native_root_id=native_root)
        self.recovery.finalize()
        _, _, panel, _ = self.recovery.outputs[-1]
        if panel['unresolved']: raise ValueError('Resolve original dialogue character dependencies: ' + repr(panel['unresolved']))
        def restore_empty(source_value, target):
            if isinstance(source_value, dict) and isinstance(target, dict):
                for key in source_value.keys() & target.keys():
                    if target[key] is None and source_value[key] in ('', [], {}): target[key] = source_value[key]
                    else: restore_empty(source_value[key], target[key])
            elif isinstance(source_value, list) and isinstance(target, list) and len(source_value) == len(target):
                for left, right in zip(source_value, target): restore_empty(left, right)
        for key, pointer in panel['external'].items(): self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            target = next(iter(panel['documents'][file_id].values()))
            fields = self.recovery.fields(owner)
            restore_empty(fields, target)
            self.learn(fields, target, owner)
        for script in panel['scripts']:
            full = '.'.join(filter(None, (script['namespace'], script['type'])))
            if full not in self.scripts: raise ValueError('Bind original character component: ' + full)
            panel['documents'][script['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[full]
        key = hashlib.md5(b'zjcs:home-story-character:CharacterGroup').hexdigest()
        self.queue.append((Path('CharacterGroup.prefab'), key, self.replace(panel['documents'])))
        for _, _, data in self.queue:
            if isinstance(data, dict):
                result = self.replace(data)
                data.clear()
                data.update(result)
        remap = {value: hashlib.md5(('zjcs:home-story-character-asset:' + value).encode()).hexdigest()
            for path, value, _ in self.queue if path.suffix in ('.asset', '.mat', '.controller', '.anim')}
        def remap_guids(value):
            if isinstance(value, dict):
                return {name: remap.get(item, item) if name == 'guid' and isinstance(item, str)
                        else remap_guids(item) for name, item in value.items()}
            if isinstance(value, list): return [remap_guids(item) for item in value]
            return value
        self.queue = [(path, remap.get(value, value), remap_guids(data)) for path, value, data in self.queue]
        outputs = write_staged_assets(self.queue, self.work / 'home-story-character-assets')
        for path in (native, Path(str(native) + '.meta'), Path(__file__), ROOT / 'scripts/prepare_home_story_character_code.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, source_root=identity(source),
            source_parent=identity(root), root=dict(fileID=panel['root'], guid=key, type=3),
            objects=self.proof, script_bindings=self.script_sources, recovery=self.recovery.proof,
            native_guid_remap=remap, outputs=outputs,
            scope='Original character group, controller and clips; complete dialogue panel remains separate.')
        (self.work / 'home-story-character-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original story character group:', len(panel['objectMap']), 'objects', flush=True)


if __name__ == '__main__':
    StoryCharacterBinding(validation_directory()).build()
