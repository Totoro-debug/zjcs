"""Bind the selected original shared minimap HUD, its fonts, graphics and sound."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_mainline_minimap_native_root import MiniMapRootBinding, integration_sources
from prepare_mainline_map15_native_ground import GroundSource
from prepare_partner_card_native import PartnerCardBinding, guid, script_id, sha
from validation_workspace import validation_directory


class MiniMapHudBinding(MiniMapRootBinding):
    def __init__(self, work):
        super().__init__(work, 13)
        self.source = GroundSource(13, 'Assets/Prefabs/UI/Map/MiniMap/MiniMapMaskedUI.prefab',
            'defaultpackage_assets_prefabs_ui_map_minimap.bundle')
        self.destination = ROOT / 'unity/Assets/Official/World/Shared/NativeMiniMapHudCurrent'
        self.receipt_path = ROOT / 'assets/reference/official-map/mainline-current-minimap-hud-source-manifest.json'

    def native_guid(self, name):
        return hashlib.md5(('zjcs:mainline-minimap-hud:' + name).encode()).hexdigest()

    def sound(self, payload, owner):
        result = super().sound(payload, owner)
        if not result.get('fileID'):
            return result
        key, proof = next((key, proof) for key, proof in self.audio_proof.items() if proof['pointer'] is result)
        target = Path(proof['output'])
        result['guid'] = self.native_guid('audio:' + key)
        self.script_guids.add(result['guid'])
        self.audio_outputs[target] = (result['guid'], self.audio_outputs[target][1])
        return result

    def register_scripts(self):
        PartnerCardBinding.register_scripts(self)
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = {name: runtime / f'Official/World/MainlineMiniMapHud/{name}.cs'
            for name in ('MiniMapMaskedUI', 'AreaInfoUI', 'PublicMapBranchLineBtn',
                'MultiLanguageTMPControler', 'LanguageGroupConfig')}
        paths.update(ButtonSound=runtime / 'Official/WorldUI/WorldButtonSound.cs',
            NoDrawingRayCast=runtime / 'Official/WorldUI/NoDrawingRayCast.cs')
        for name, path in paths.items():
            ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = ref
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def build_hud(self):
        root, record = self.bind_prefab(self.source,
            Path('Resources/World/Native/mini-map-hud.prefab'))
        card = self.recovery.outputs[-1][2]
        if len(card['documents']) != 79 or card['unresolved']:
            raise ValueError('Review the complete selected HUD hierarchy and pending references')
        for script in card['scripts']:
            if script['type'] == 'ButtonSound':
                owner = self.original(script['original'])
                card['documents'][script['nativeFileID']]['MonoBehaviour']['ClickEvent'] = self.sound(
                    self.recovery.fields(owner)['ClickEvent'], owner)
        manifest = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        config, = [row for row in manifest if Path(row['path']).name == 'LanguageGroupConfig.asset']
        objects = [p.deref() for path,p in self.recovery.apk.container(config['bundle'])
            if path.casefold() == config['path'].casefold()]
        obj, = objects
        language_config = PartnerCardBinding.original_asset(self, obj)
        # Resolve all dependencies before writing the closure or audio payloads.
        index = 0
        while index < len(self.queue):
            target, identity, data = self.queue[index]
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear(); data.update(resolved)
            index += 1
        for target, (identity, raw) in self.audio_outputs.items():
            self.queue.append((target, identity, raw))
            meta = Path(str(self.destination / target) + '.meta')
            meta.parent.mkdir(parents=True, exist_ok=True)
            if not meta.exists():
                meta.write_text('fileFormatVersion: 2\nguid: ' + identity +
                    '\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n'
                    '    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 0\n'
                    '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                    '  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n',
                    encoding='utf8', newline='\n')
        self.inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
        for name in ('prepare_mainline_minimap_native_root.py','prepare_mainline_minimap_regions.py'):
            path = ROOT / 'scripts' / name
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        code_receipt = ROOT / 'assets/reference/official-map/mainline-minimap-hud-code-source-manifest.json'
        self.inputs[code_receipt.relative_to(ROOT).as_posix()] = sha(code_receipt.read_bytes())
        equivalence = []
        for name, folder, previous in (
            ('MultiLanguageText','framework-firstpass-source','map13-label-core-code'),
            ('NoDrawingRayCast','framework-firstpass-source','map14-handin-ui'),
            ('ButtonSound','framework-firstpass-source','map14-handin-ui'),
            ('MapMeCharacterUIBase','client-source','map13-minimap-region-manager-code'),
            ('MapUIBase','client-source','map13-label-components-code'),
            ('UIBase','framework-firstpass-source','map13-label-components-code'),
            ('StateSwitcher','framework-firstpass-source','map13-label-core-code'),
            ('MonoDictionary','framework-firstpass-source','map13-label-components-code')):
            selected = self.cache.parent / 'official-code' / folder / (name + '.cs')
            if not selected.is_file():
                selected, = (self.cache.parent / 'official-code').glob('*/' + name + '.cs')
            previous = ROOT / 'assets/reference/official-map' / (previous + '-source-manifest.json')
            selected_hash = sha(selected.read_bytes())
            if selected_hash not in previous.read_text('utf8'):
                raise ValueError('Review original helper before reuse: ' + name)
            self.inputs[selected.relative_to(ROOT).as_posix()] = selected_hash
            self.inputs[previous.relative_to(ROOT).as_posix()] = sha(previous.read_bytes())
            equivalence.append(dict(selected=selected.relative_to(ROOT).as_posix(),sha256=selected_hash,
                reused_receipt=previous.relative_to(ROOT).as_posix()))
        self.write_outputs(record, root, language_group_config=language_config,
            audio=self.audio_proof, original_code_equivalence=equivalence, serialized_object_count=len(card['documents']),
            integration=integration_sources(),
            scope='Shared original HUD prefab only. Preserve every original layout field and component. Replace Wwise posting with the decoded original event clip and an instance sound port. Production world-canvas placement and state wiring remain pending.')
        receipt = json.loads(self.receipt_path.read_bytes())
        receipt.pop('mapClassId')
        shared_meta = Path(str(self.destination.parent) + '.meta')
        if not shared_meta.exists():
            shared_meta.write_text('fileFormatVersion: 2\nguid: ' + self.native_guid('folder:shared-world') +
                '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n',encoding='utf8',newline='\n')
        shared_meta.write_bytes(b'\n'.join(line.rstrip(b' \t\r') for line in shared_meta.read_bytes().split(b'\n')))
        receipt['outputs'][shared_meta.relative_to(ROOT).as_posix()] = sha(shared_meta.read_bytes())
        receipt['audio_guid_namespace'] = 'zjcs:mainline-minimap-hud:audio:<original CAB/PPtr identity>; independent from other copied event clips.'
        self.receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')


if __name__ == '__main__':
    MiniMapHudBinding(validation_directory()).build_hud()
