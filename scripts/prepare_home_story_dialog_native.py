"""Recover the complete original OptionTalkDialog prefab and its dependencies."""
import hashlib
import json
from pathlib import Path

from home_native_assets import write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import documents, identity
from prepare_home_story_option_native import StoryOptionBinding
from prepare_partner_card_native import guid, script_id, sha
from validation_workspace import validation_directory


class StoryDialogBinding(StoryOptionBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {
            'OptionTalkDialogPanel': self.work / 'home-story-dialog-code/OptionTalkDialogPanel.cs',
            'TalkDialogCharacter': runtime / 'HomeStoryCharacter/TalkDialogCharacter.cs',
            'TalkDialogCharacterController': runtime / 'HomeStoryCharacter/TalkDialogCharacterController.cs',
            'TalkDialogContentSwitch': runtime / 'HomeStoryCharacter/TalkDialogContentSwitch.cs',
            'PopupWindow': runtime / 'WorldUI/WorldPopupWindow.cs',
            'SortingOrderSetter': runtime / 'TeamUI/SortingOrderSetter.cs',
            'TMP_TypeWriterEffect': runtime / 'StoryBubbles/TMP_TypeWriterEffect.cs',
            'UIBloomGroup': runtime / 'StoryBubbles/UIBloomGroup.cs',
            'UGUIJszx.HorizontalLayoutGroupJSZX': runtime / 'WorldUI/HorizontalLayoutGroupJSZX.cs',
        }
        for full, path in paths.items():
            namespace, _, name = full.rpartition('.')
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[full] = pointer
            self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[full] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def build(self):
        bundle = 'defaultpackage_assets_prefabs_ui_story.bundle'
        relative = 'Prefabs/UI/Story/OptionTalkDialog.prefab'
        native = self.native / relative
        root = self.recovery.apk.asset(bundle, 'OptionTalkDialog.prefab')
        native_docs = documents(native.read_text('utf8'))
        native_root, = [key for key, value in native_docs.items()
                        if value.get('GameObject', {}).get('m_Name') == 'OptionTalkDialog']
        self.recovery.recover('OptionTalkDialog', self.work / 'home-story-dialog-recovery', bundle=bundle,
            native_relative=relative, source_root=root, native_root_id=native_root)
        self.recovery.finalize()
        _, _, panel, _ = self.recovery.outputs[-1]
        if panel['unresolved']: raise ValueError('Resolve original dialogue dependencies: ' + repr(panel['unresolved']))
        def restore_empty(source, target):
            if isinstance(source, dict) and isinstance(target, dict):
                for key in source.keys() & target.keys():
                    if target[key] is None and source[key] in ('', [], {}): target[key] = source[key]
                    else: restore_empty(source[key], target[key])
            elif isinstance(source, list) and isinstance(target, list) and len(source) == len(target):
                for left, right in zip(source, target): restore_empty(left, right)
        for key, pointer in panel['external'].items(): self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            target = next(iter(panel['documents'][file_id].values()))
            fields = self.recovery.fields(owner)
            restore_empty(fields, target)
            self.learn(fields, target, owner)
        for script in panel['scripts']:
            full = '.'.join(filter(None, (script['namespace'], script['type'])))
            if full not in self.scripts: raise ValueError('Bind original dialogue component: ' + full)
            fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
            fields['m_Script'] = self.scripts[full]
            if script['type'] == 'ButtonSound':
                owner = self.original(script['original'])
                fields['ClickEvent'] = self.sound(self.recovery.fields(owner)['ClickEvent'], owner)
        key = hashlib.md5(b'zjcs:home-story-dialog:OptionTalkDialog').hexdigest()
        self.queue.append((Path('OptionTalkDialog.prefab'), key, self.replace(panel['documents'])))
        for _, _, data in self.queue:
            if isinstance(data, dict):
                result = self.replace(data)
                data.clear()
                data.update(result)
        remap = {value: hashlib.md5(('zjcs:home-story-dialog-asset:' + value).encode()).hexdigest()
            for path, value, _ in self.queue if path.suffix in ('.asset', '.mat', '.controller', '.anim')}
        def remap_guids(value):
            if isinstance(value, dict):
                return {name: remap.get(item, item) if name == 'guid' and isinstance(item, str)
                        else remap_guids(item) for name, item in value.items()}
            if isinstance(value, list): return [remap_guids(item) for item in value]
            return value
        self.queue = [(path, remap.get(value, value), remap_guids(data)) for path, value, data in self.queue]
        staged = self.work / 'home-story-dialog-assets'
        outputs = write_staged_assets(self.queue, staged)
        for target, (asset_guid, raw) in self.audio_outputs.items():
            path = staged / target
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            metadata = Path(str(path) + '.meta')
            metadata.write_text('fileFormatVersion: 2\nguid: ' + asset_guid + '\nAudioImporter:\n'
                '  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n'
                '    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 0\n'
                '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                '  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n'
                '  ambisonic: 0\n  3D: 0\n', 'utf8', newline='\n')
            outputs[target.as_posix()] = sha(raw)
            outputs[target.as_posix() + '.meta'] = sha(metadata.read_bytes())
        for path in (native, Path(str(native) + '.meta'), Path(__file__),
                     ROOT / 'scripts/prepare_home_story_dialog_code.py', ROOT / 'scripts/prepare_home_story_option_native.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, source_root=identity(root),
            root=dict(fileID=panel['root'], guid=key, type=3), objects=self.proof,
            script_bindings=self.script_sources, recovery=self.recovery.proof,
            native_guid_remap=remap, audio=self.audio_proof, outputs=outputs,
            scope='Complete original dialog prefab and component bindings; local story host remains separate.')
        (self.work / 'home-story-dialog-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged complete original dialogue prefab:', len(panel['objectMap']), 'objects', flush=True)


if __name__ == '__main__':
    StoryDialogBinding(validation_directory()).build()
