"""Stage the selected official activity-entry button and its dependencies."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

from npc_travel_language import language
from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_card_native import ROOT, guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_current_partner_native import documents, identity
from validation_workspace import validation_directory


def validate_dynamic_atlas(original, current):
    """Accept only runtime glyph pixels and Unity's known empty-field serialization."""
    before = documents(original.decode('utf8'))
    after = documents(current.decode('utf8'))
    if before.keys() != after.keys():
        raise ValueError('Dynamic font atlas object identities differ')
    for key in before:
        if set(before[key]) != {'Texture2D'} or set(after[key]) != {'Texture2D'}:
            raise ValueError('Unexpected dynamic font atlas object type')
        left, right = dict(before[key]['Texture2D']), dict(after[key]['Texture2D'])
        original_pixels, current_pixels = left.pop('_typelessdata', None), right.pop('_typelessdata', None)
        if not isinstance(original_pixels, str) or not isinstance(current_pixels, str) or len(original_pixels) != len(current_pixels):
            raise ValueError('Dynamic font atlas pixel layout differs')
        for fields in (left, right):
            for name in ('m_AlphaIsTransparency', 'm_VTOnly'):
                if fields.pop(name, 0) != 0:
                    raise ValueError('Unexpected dynamic font atlas flag: ' + name)
            image_hash = fields.pop('m_ImageContentsHash', None)
            if image_hash not in (None, {'serializedVersion': 2, 'Hash': 0}):
                raise ValueError('Unexpected dynamic font atlas content hash')
            for name in ('m_MipmapLimitGroupName', 'm_PlatformBlob'):
                if fields.get(name) is None:
                    fields[name] = ''
            if isinstance(fields.get('m_StreamData'), dict) and fields['m_StreamData'].get('path') is None:
                fields['m_StreamData'] = dict(fields['m_StreamData'], path='')
        if left != right:
            raise ValueError('Dynamic font atlas metadata differs from the selected original')


class ActivityEntryBinding(HomeBottomBarBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {
            'ActivityEntryBtn': runtime / 'HomeHudInfrastructureUI/ActivityEntryBtn.cs',
            'LayoutElementClamper': runtime / 'WorldUI/WorldLayoutElementClamper.cs',
        }
        for name, path in paths.items():
            meta = Path(str(path) + '.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = ref
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def tip_address(self, obj, original_type):
        key = identity(obj)
        if key in self.original_refs:
            return self.original_refs[key]
        result = super().tip_address(obj, original_type)
        previous = result['guid']
        unique = hashlib.md5(('zjcs:home-activity-tip-address:' + key).encode()).hexdigest()
        result['guid'] = unique
        self.script_guids.add(unique)
        payload = hashlib.md5(b'zjcs:home-activity-tip-source-objects').hexdigest()
        self.script_guids.add(payload)
        for index, (path, asset_guid, data) in enumerate(self.queue):
            if asset_guid == previous:
                self.queue[index] = (path, unique, data)
                for doc in data.values():
                    doc['MonoBehaviour']['sourceGraphData']['guid'] = payload
        self.proof[key]['guid'] = unique
        return result

    def friendship_templates(self):
        self.recovery.recover('ActivityEntryBtn', self.work / 'activity-entry-native',
            bundle='defaultpackage_assets_prefabs_ui_activityentry.bundle',
            native_relative='Prefabs/UI/ActivityEntry/ActivityEntryBtn.prefab')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[0]
        missing = {'.'.join(filter(None, (row['namespace'], row['type'])))
                   for row in panel['scripts']} - self.scripts.keys()
        if missing:
            raise ValueError('Original activity entry scripts are not bound: ' + repr(sorted(missing)))
        for path in (native, Path(str(native) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.entry_text_keys = {doc['MonoBehaviour']['myKey'] for doc in panel['documents'].values()
                                if doc.get('MonoBehaviour', {}).get('myKey')}
        self.template_selection = dict(source=record['originalRoot'], mapped_objects=len(panel['objectMap']),
            scope='Complete selected original ActivityEntryBtn hierarchy and serialized components.')
        return [('ActivityEntryBtn', panel)]

    def build(self):
        PartnerGiftBinding.build(self)
        receipt_path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        folder = self.work / 'gift-assets'
        texts, sources = language(self.entry_text_keys)
        missing = self.entry_text_keys - texts.keys()
        if missing - {'{0}'}:
            raise ValueError('Original activity entry localization is missing: ' + repr(sorted(missing)))
        receipt['inputs'].update(sources)
        tip_data = folder / 'home-activity-tip-source-objects.json'
        tip_data.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            scope='Original tip-graph objects and selector addresses; graph evaluation is not supplied here.',
            objects=getattr(self, 'tip_source_objects', {})), ensure_ascii=False, indent=2) + '\n',
            'utf8', newline='\n')
        tip_meta = Path(str(tip_data) + '.meta')
        tip_meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-activity-tip-source-objects').hexdigest()
            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (tip_data, tip_meta):
            receipt['outputs'][item.name] = sha(item.read_bytes())
        for name in ('partner-gift-texts.json', 'partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name)
            (folder / name).unlink()
        path = folder / 'home-activity-entry-texts.json'
        path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key, text=value) for key, value in sorted(texts.items())]),
            ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        meta = Path(str(path) + '.meta')
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-activity-entry-texts').hexdigest()
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
        original = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/client-source/ActivityEntryBtn.cs'
        receipt['inputs'][original.relative_to(ROOT).as_posix()] = sha(original.read_bytes())
        for name in ('prepare_home_bottom_bar_native.py', 'prepare_home_repair_native.py', 'prepare_partner_gift_native.py'):
            helper = ROOT / 'scripts' / name
            receipt['inputs'][helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
        receipt['tip_source_object_count'] = len(getattr(self, 'tip_source_objects', {}))
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),
            template_selection=self.template_selection,
            scope='Selected original activity-entry button and dependencies; host and production HUD binding pending.')
        (self.work / 'home-activity-entry-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original activity entry:', len(receipt['outputs']), 'outputs', flush=True)


def install_staged(work, *, receipt_name='home-activity-entry-native-source-manifest.json',
                   generator_path=None, destination_relative='unity/Assets/Official/Home/UI/ActivityEntry',
                   folder_prefix='zjcs:home-activity-entry-folder:', bindings=None, loading=None):
    receipt = json.loads((work / receipt_name).read_bytes())
    generator_path = Path(__file__) if generator_path is None else Path(generator_path)
    if receipt['generator_sha256'] != sha(generator_path.read_bytes()):
        raise ValueError('Rebuild activity entry after the generator changed')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Activity entry input changed: ' + relative)
    for binding in receipt['script_bindings'].values():
        source = ROOT / binding['path']
        if sha(source.read_bytes()) != binding['sha256'] or sha(Path(str(source) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Activity entry script changed: ' + str(source))
    stage = work / 'gift-assets'
    for relative, expected in receipt['outputs'].items():
        if sha((stage / relative).read_bytes()) != expected:
            raise ValueError('Activity entry output changed: ' + relative)
    ids = {guid(stage / relative): relative[:-5] for relative in receipt['outputs'] if relative.endswith('.meta')}
    if len(ids) * 2 != len(receipt['outputs']):
        raise ValueError('Activity entry outputs have duplicate or incomplete GUID metadata')
    wanted = work / 'activity-entry-guids.txt'
    wanted.write_text(''.join('guid: ' + key + '\n' for key in ids), 'utf8', newline='\n')
    result = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), str(ROOT / 'unity/Assets'), '-g', '*.meta'],
        capture_output=True, text=True)
    if result.returncode not in (0, 1):
        raise RuntimeError(result.stderr)
    existing = {}
    for name in result.stdout.splitlines():
        metadata = Path(name)
        key = guid(metadata)
        if key in existing:
            raise ValueError('Existing asset GUID is ambiguous: ' + key)
        existing[key] = Path(str(metadata)[:-5])
    destination = ROOT / destination_relative
    plan, reused, owned = [], {}, {}
    dynamic_atlas = 'unity/Assets/Official/Partners/UI/Card/Converted/ac573a40fda4c096.asset'
    for key, relative in ids.items():
        original = stage / relative
        target = existing.get(key, destination / relative)
        if target.exists():
            if not target.is_file():
                raise ValueError('Existing GUID does not point to a file: ' + str(target))
            current = target.read_bytes()
            if target.is_relative_to(destination):
                if current != original.read_bytes() or Path(str(target) + '.meta').read_bytes() != Path(str(original) + '.meta').read_bytes():
                    raise ValueError('Installed activity entry asset differs: ' + str(target))
                owned[target.relative_to(ROOT).as_posix()] = sha(current)
                owned[Path(str(target) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(target) + '.meta').read_bytes())
                continue
            if current != original.read_bytes():
                name = target.relative_to(ROOT).as_posix()
                if name != dynamic_atlas:
                    raise ValueError('Existing original asset differs: ' + name)
                baseline = subprocess.check_output(['git', 'show', 'HEAD:' + name], cwd=ROOT)
                if baseline != original.read_bytes():
                    raise ValueError('Dynamic font atlas baseline differs: ' + name)
                validate_dynamic_atlas(baseline, current)
                reused[name] = dict(staged_sha256=sha(original.read_bytes()),
                    working_sha256=sha(current), reason='Existing Unity dynamic font atlas; preserve working data.')
            else:
                reused[target.relative_to(ROOT).as_posix()] = dict(staged_sha256=sha(current), working_sha256=sha(current))
        else:
            plan.append((original, target))
    installed, folders = dict(owned), {}
    for original, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(original.read_bytes())
        metadata = Path(str(target) + '.meta')
        metadata.write_bytes(Path(str(original) + '.meta').read_bytes())
        for item in (target, metadata):
            installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    for relative in installed:
        target = ROOT / relative
        if target.suffix == '.meta':
            continue
        parent = target.parent
        while parent.is_relative_to(destination):
            folder_meta = Path(str(parent) + '.meta')
            if not folder_meta.exists():
                folder_meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(
                    (folder_prefix + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                    + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            folders[folder_meta.relative_to(ROOT).as_posix()] = sha(folder_meta.read_bytes())
            parent = parent.parent
    receipt['installed_output_sha256'] = installed
    receipt['folder_meta_sha256'] = folders
    receipt['reused_assets'] = reused
    if bindings is None:
        bindings = (
        'unity/Assets/Game/Runtime/Official/HomeHudInfrastructureUI/ActivityEntryBtn.cs',
        'unity/Assets/Game/Runtime/Official/HomeHudInfrastructureUI/ActivityEntryBtn.cs.meta',
        'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeActivityEntryAssets.cs',
        'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeActivityEntryAssets.cs.meta',
        'unity/Assets/Game/Resources/Home/OfflineHomeActivityEntryAssets.asset',
        'unity/Assets/Game/Resources/Home/OfflineHomeActivityEntryAssets.asset.meta',
        'scripts/fixtures/home_activity_entry_controller_validation.cs',
        'scripts/fixtures/home_activity_entry_prefab_validation.cs',
        'scripts/prepare_home_activity_entry_validation.py',
        )
    receipt['runtime_bindings'] = {path: sha((ROOT / path).read_bytes()) for path in bindings}
    receipt['loading'] = loading or 'Resources/Home/OfflineHomeActivityEntryAssets binds the original prefab, localization and tip source objects; formal home host is not connected.'
    (ROOT / 'assets/reference' / receipt_name).write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original home entry assets:', len(plan), 'reused:', len(reused), flush=True)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    if args.install:
        install_staged(validation_directory())
    else:
        ActivityEntryBinding(validation_directory()).build()
