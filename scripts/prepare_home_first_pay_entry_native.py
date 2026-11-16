"""Stage the selected official first-pay home entry and its dependencies."""
import hashlib
import json
from pathlib import Path
import re

from npc_travel_language import language
from prepare_current_partner_native import identity
from prepare_home_activity_entry_native import ActivityEntryBinding, install_staged
from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_card_native import ROOT, guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory


class FirstPayEntryBinding(ActivityEntryBinding):
    def register_scripts(self):
        super().register_scripts()
        path = ROOT / 'unity/Assets/Game/Runtime/Official/HomeHudInfrastructureUI/FirstPayIconEntry.cs'
        meta = Path(str(path) + '.meta')
        ref = dict(fileID=11500000, guid=guid(meta), type=3)
        self.scripts['FirstPayIconEntry'] = ref
        self.class_ids[script_id('', 'FirstPayIconEntry')] = ref
        self.script_guids.add(ref['guid'])
        self.script_sources['FirstPayIconEntry'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def tip_address(self, obj, original_type):
        key = identity(obj)
        if key in self.original_refs:
            return self.original_refs[key]
        result = HomeBottomBarBinding.tip_address(self, obj, original_type)
        previous = result['guid']
        unique = hashlib.md5(('zjcs:home-first-pay-tip-address:' + key).encode()).hexdigest()
        result['guid'] = unique
        self.script_guids.add(unique)
        payload = hashlib.md5(b'zjcs:home-first-pay-tip-source-objects').hexdigest()
        self.script_guids.add(payload)
        for index, (path, asset_guid, data) in enumerate(self.queue):
            if asset_guid == previous:
                self.queue[index] = (path, unique, data)
                for doc in data.values():
                    doc['MonoBehaviour']['sourceGraphData']['guid'] = payload
        self.proof[key]['guid'] = unique
        return result

    def friendship_templates(self):
        self.recovery.recover('FirstPayBtn', self.work / 'first-pay-entry-native',
            bundle='defaultpackage_assets_prefabs_ui_activityentry.bundle',
            native_relative='Prefabs/UI/ActivityEntry/FirstPayBtn.prefab')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[0]
        missing = {'.'.join(filter(None, (row['namespace'], row['type'])))
                   for row in panel['scripts']} - self.scripts.keys()
        if missing:
            raise ValueError('Original first-pay entry scripts are not bound: ' + repr(sorted(missing)))
        for path in (native, Path(str(native) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.entry_text_keys = {doc['MonoBehaviour']['myKey'] for doc in panel['documents'].values()
                                if doc.get('MonoBehaviour', {}).get('myKey')}
        self.template_selection = dict(source=record['originalRoot'], mapped_objects=len(panel['objectMap']),
            scope='Complete selected original FirstPayBtn hierarchy and serialized components.')
        return [('FirstPayBtn', panel)]

    def build(self):
        PartnerGiftBinding.build(self)
        receipt = json.loads((self.work / 'partner-gift-native-source-manifest.json').read_bytes())
        folder = self.work / 'gift-assets'
        texts, sources = language(self.entry_text_keys)
        missing = self.entry_text_keys - texts.keys()
        if missing - {'{0}'}:
            raise ValueError('Original first-pay entry localization is missing: ' + repr(sorted(missing)))
        receipt['inputs'].update(sources)
        for name in ('partner-gift-texts.json', 'partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name)
            (folder / name).unlink()
        for name, payload, key in (
            ('home-first-pay-tip-source-objects.json', dict(manifest=self.recovery.apk.version,
                scope='Original tip-graph objects and selector addresses; graph evaluation is not supplied here.',
                objects=getattr(self, 'tip_source_objects', {})), b'zjcs:home-first-pay-tip-source-objects'),
            ('home-first-pay-entry-texts.json', dict(manifest=self.recovery.apk.version,
                texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]), b'zjcs:home-first-pay-entry-texts'),
        ):
            path = folder / name
            path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
            meta = Path(str(path) + '.meta')
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(key).hexdigest()
                + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            for item in (path, meta):
                receipt['outputs'][item.name] = sha(item.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            item = folder / relative
            if item.suffix != '.meta' and item.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', item.read_text('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items()
                                      if row['pointer']['guid'] in used}
        for name in ('prepare_home_activity_entry_native.py', 'prepare_home_bottom_bar_native.py',
                     'prepare_home_repair_native.py', 'prepare_partner_gift_native.py'):
            helper = ROOT / 'scripts' / name
            receipt['inputs'][helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
        original = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/client-source/FirstPayIconEntry.cs'
        receipt['inputs'][original.relative_to(ROOT).as_posix()] = sha(original.read_bytes())
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),
            tip_source_object_count=len(getattr(self, 'tip_source_objects', {})),
            template_selection=self.template_selection,
            scope='Selected original first-pay entry and dependencies; local services and formal home binding pending.')
        (self.work / 'home-first-pay-entry-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original first-pay entry:', len(receipt['outputs']), 'outputs', flush=True)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if not args.install:
        FirstPayEntryBinding(work).build()
    else:
        install_staged(work, receipt_name='home-first-pay-entry-native-source-manifest.json',
            generator_path=Path(__file__), destination_relative='unity/Assets/Official/Home/UI/FirstPayEntry',
            folder_prefix='zjcs:home-first-pay-entry-folder:', bindings=(
                'unity/Assets/Game/Runtime/Official/HomeHudInfrastructureUI/FirstPayIconEntry.cs',
                'unity/Assets/Game/Runtime/Official/HomeHudInfrastructureUI/FirstPayIconEntry.cs.meta',
                'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeFirstPayEntryAssets.cs',
                'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeFirstPayEntryAssets.cs.meta',
                'unity/Assets/Game/Resources/Home/OfflineHomeFirstPayEntryAssets.asset',
                'unity/Assets/Game/Resources/Home/OfflineHomeFirstPayEntryAssets.asset.meta',
                'scripts/fixtures/home_first_pay_controller_validation.cs',
                'scripts/fixtures/home_first_pay_prefab_validation.cs',
                'scripts/prepare_home_activity_entry_validation.py',
            ), loading='Resources/Home/OfflineHomeFirstPayEntryAssets binds the original prefab, localization and tip source objects; formal home host is not connected.')
