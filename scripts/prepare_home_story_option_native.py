"""Recover the exact original story dialogue option subtree and its dependencies."""
import hashlib
import json
from pathlib import Path

from home_native_assets import write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import documents, identity
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_panel_native import PartnerPanelBinding
from validation_workspace import validation_directory


class StoryOptionBinding(PartnerGiftBinding):
    def replace(self, value):
        if value in (dict(**{'class': 'WorldStateGameObjectConfig', 'ns': 'Zjcs.Official.WorldUI',
                            'asm': 'Zjcs.Official.WorldUI'}),
                     dict(**{'class': 'WorldStateUIColorConfig', 'ns': 'Zjcs.Official.WorldUI',
                            'asm': 'Zjcs.Official.WorldUI'})):
            return value
        return super().replace(value)

    def load(self, source_guid):
        try:
            obj = self.objects.get(source_guid)
            if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimatorController':
                result = PartnerPanelBinding.controller(self, source_guid, obj)
            else:
                result = super().load(source_guid)
            self.script_guids.add(result['guid'])
            return result
        except Exception as error:
            obj = self.objects.get(source_guid)
            name = getattr(obj.read(check_read=False), 'm_Name', '') if obj is not None else ''
            raise ValueError('Recover option dependency ' + source_guid + ' / '
                + (obj.type.name if obj is not None else '<unknown>') + ' / ' + name) from error

    def register_scripts(self):
        super().register_scripts()
        official = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI'
        paths = {'SafeArea': official / 'WorldSafeArea.cs',
                 'ShowRemainTimeInfo': official / 'WorldShowRemainTimeInfo.cs',
                 'CountRedDotImage': official / 'WorldCountRedDotImage.cs'}
        staged = self.work / 'home-story-option-code'
        for name in ('OptionSlot', 'OptionTalkDialogItem', 'OptionTalkItemNormalText',
                     'OptionTalkItemRemainTimeText', 'OptionTalkItemDot'):
            paths[name] = staged / (name + '.cs')
        for name, path in paths.items():
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def build(self):
        bundle = 'defaultpackage_assets_prefabs_ui_story.bundle'
        relative = 'Prefabs/UI/Story/OptionTalkDialog.prefab'
        native_path = self.native / relative
        objects = self.recovery.apk.objects(bundle)
        root, = [obj for obj in objects if obj.type.name == 'GameObject'
            and obj.read().m_Name == 'OptionTalkDialog'
            and not obj.read().m_Component[0].component.read().m_Father.path_id]

        def descendants(transform):
            yield transform.read().m_GameObject.deref()
            for child in transform.read().m_Children:
                yield from descendants(child.deref())

        selected, = [obj for obj in descendants(root.read().m_Component[0].component.deref())
                     if obj.read().m_Name == 'OptionSlots']
        native_docs = documents(native_path.read_text('utf8'))
        native_root, = [key for key, value in native_docs.items()
            if value.get('GameObject', {}).get('m_Name') == 'OptionSlots']
        self.recovery.recover('OptionSlots', self.work / 'home-story-option-recovery',
            bundle=bundle, native_relative=relative, source_root=selected, native_root_id=native_root)
        self.recovery.finalize()
        _, _, panel, record = self.recovery.outputs[-1]
        if panel['unresolved']:
            raise ValueError('Resolve original option subtree dependencies: ' + repr(panel['unresolved']))

        def restore_empty(source, target):
            if isinstance(source, dict) and isinstance(target, dict):
                for key in source.keys() & target.keys():
                    if target[key] is None and source[key] in ('', [], {}):
                        target[key] = source[key]
                    else:
                        restore_empty(source[key], target[key])
            elif isinstance(source, list) and isinstance(target, list) and len(source) == len(target):
                for left, right in zip(source, target):
                    restore_empty(left, right)

        for key, ref in panel['external'].items():
            self.objects[ref['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            target = next(iter(panel['documents'][file_id].values()))
            source = self.recovery.fields(owner)
            restore_empty(source, target)
            self.learn(source, target, owner)
        for script in panel['scripts']:
            full = '.'.join(filter(None, (script['namespace'], script['type'])))
            if full not in self.scripts:
                raise ValueError('Bind original option component: ' + full)
            fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
            fields['m_Script'] = self.scripts[full]
            if script['type'] == 'ButtonSound':
                owner = self.original(script['original'])
                fields['ClickEvent'] = self.sound(self.recovery.fields(owner)['ClickEvent'], owner)
        icon_bundle = 'defaultpackage_assets_image_ui_dialogue.bundle'
        icon, = [obj for obj in self.recovery.apk.objects(icon_bundle) if obj.type.name == 'Sprite'
                 and obj.read(check_read=False).m_Name == 'Iconarrow_1']
        native_icon = self.native / 'Image/UI/Dialogue/Dialogue_Atlas/Iconarrow_1.asset'
        icon_guid = guid(Path(str(native_icon) + '.meta'))
        self.objects[icon_guid] = icon
        icon_pointer = self.load(icon_guid)
        resolved = self.replace(panel['documents'])
        root_guid = hashlib.md5(b'zjcs:home-story-option:OptionSlots').hexdigest()
        self.queue.append((Path('OptionSlots.prefab'), root_guid, resolved))
        for _, _, data in self.queue:
            if isinstance(data, dict):
                value = self.replace(data)
                data.clear()
                data.update(value)
        # Fonts, sprites, controllers and materials can have source GUIDs already
        # used by other modules with different converted subasset bindings.
        remap = {key: hashlib.md5(('zjcs:home-story-option-asset:' + key).encode()).hexdigest()
                 for path, key, _ in self.queue if path.suffix in ('.asset', '.mat', '.controller', '.anim')}
        def remap_guids(value):
            if isinstance(value, dict):
                return {key: remap.get(item, item) if key == 'guid' and isinstance(item, str)
                        else remap_guids(item) for key, item in value.items()}
            if isinstance(value, list):
                return [remap_guids(item) for item in value]
            return value
        self.queue = [(path, remap.get(key, key), remap_guids(data)) for path, key, data in self.queue]
        icon_pointer = remap_guids(icon_pointer)
        staged = self.work / 'home-story-option-assets'
        outputs = write_staged_assets(self.queue, staged)
        for target, (key, raw) in self.audio_outputs.items():
            path = staged / target
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            metadata = Path(str(path) + '.meta')
            metadata.write_text('fileFormatVersion: 2\nguid: ' + key + '\nAudioImporter:\n'
                '  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n'
                '    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 0\n'
                '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                '  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n'
                '  ambisonic: 0\n  3D: 0\n', 'utf8', newline='\n')
            outputs[target.as_posix()] = sha(raw)
            outputs[target.as_posix() + '.meta'] = sha(metadata.read_bytes())
        for path in (native_path, Path(str(native_path) + '.meta'), Path(__file__),
                     ROOT / 'scripts/prepare_home_story_option_code.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs,
            source_root=identity(selected), source_parent=identity(root), recovery=self.recovery.proof,
            root=dict(fileID=panel['root'], guid=root_guid, type=3),
            dynamic_icons={'UI/Dialogue/Iconarrow_1': icon_pointer}, objects=self.proof,
            script_bindings=self.script_sources, native_guid_remap=remap,
            audio=self.audio_proof, outputs=outputs,
            scope='Original option subtree and components only; full story dialogue remains separate.')
        (self.work / 'home-story-option-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original story option subtree:', len(panel['objectMap']), 'objects,', len(outputs), 'files', flush=True)


if __name__ == '__main__':
    StoryOptionBinding(validation_directory()).build()
