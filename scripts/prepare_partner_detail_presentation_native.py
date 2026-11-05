"""Recover official detail backgrounds, region images and adventure tip inputs."""
from pathlib import Path
import argparse
import copy
import hashlib
import json
import re
import yaml

from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_current_partner_native import identity, documents
from prepare_partner_card_native import guid, sha, script_id, Dumper
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_detail_window_native import PartnerDetailWindowBinding
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory

NAME = 'partner-detail-presentation-native-source-manifest.json'


class PresentationBinding(PartnerDetailWindowBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('NpcAdventureSettings', 'PopTipNpcAdventure'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Detail' / (name + '.cs')
            pointer = dict(fileID=11500000, guid=guid(metadata(path)), type=3)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(metadata(path).read_bytes()))

    def sprite(self, row):
        sprites = [pointer.deref() for path, pointer in self.recovery.apk.container(row['bundle'])
                   if path.casefold() == row['path'].casefold() and pointer.deref().type.name == 'Sprite']
        obj, = sprites
        source_key, source_id = self.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(source_key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != row['guid']:
            raise ValueError('Original presentation sprite render identity differs: ' + row['path'])
        name = self.name_key(obj.read(check_read=False).m_Name)
        matches = []
        for key in [name] + [name + str(i) for i in range(100)]:
            for candidate in self.by_name.get(key, []):
                if candidate.suffix == '.asset':
                    docs = documents(candidate.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: source_id}:
                        matches.append(candidate)
        matches = sorted(set(matches))
        exact = (self.native / row['path'].removeprefix('Assets/')).with_suffix('.asset')
        if exact in matches: matches = [exact]
        if len(matches) != 1:
            raise ValueError('Resolve exact original presentation sprite export: ' + row['path'] + ' ' + repr(matches))
        candidate, = matches
        source_guid = guid(metadata(candidate))
        previous = self.objects.get(source_guid)
        if previous is not None and identity(previous) != identity(obj):
            raise ValueError('Original presentation sprite GUID collision')
        self.objects[source_guid] = obj
        pointer = self.load(source_guid)
        return dict(asset=pointer, official_asset=row, original=identity(obj),
                    original_sha256=sha(obj.get_raw_data()), source=self.recovery.apk.provenance(obj))

    def regions(self):
        apk = self.recovery.apk
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors = Constructors(code)
        obj, = [obj for obj in apk.objects('defaultpackage_assets_config_luban.bundle')
                if obj.type.name == 'TextAsset' and obj.read().m_Name == 'npc_region_image.g']
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        kind, = re.findall(r'new (Tb\w+)\(loader\("npc_region_image"\)\)', constructors.source('Tables'))
        rows = constructors.table(raw, kind)
        for name, text in constructors.sources.items():
            if text:
                path = code / 'common-source/Common' / (name + '.cs')
                if not path.is_file(): path = code / 'common-source/Common.LubanGen' / (name + '.cs')
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for relative in ('global-metadata.dat', 'common-source/Common/NpcInfo.cs',
                         'launch-source/Common/RegionCodeEnum.cs'):
            path = code / relative
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for name in ('official_tables.py', 'official_metadata.py'):
            path = ROOT / 'scripts' / name
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.region_proof = dict(source=apk.provenance(obj), original_sha256=sha(obj.get_raw_data()),
            payload_sha256=sha(raw), bytes=len(raw), constructor=kind, rows=len(rows), region='CN', region_value=0)
        return rows

    def friendship_templates(self):
        table = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        tip, = [row for row in table if row['path'].endswith('/PopTipNpcAdventure.prefab')]
        self.recovery.recover('PopTipNpcAdventure', self.work / 'partner-native', bundle=tip['bundle'],
                              native_relative=tip['path'].removeprefix('Assets/'))
        self.recovery.finalize()
        native, _, data, source = self.recovery.outputs[-1]
        data = copy.deepcopy(data)
        self.restore(data)
        for path in (native, metadata(native)):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        settings, = [row for row in table if row['path'].endswith('/NpcAdventureSettings.asset')]
        obj = self.recovery.apk.asset(settings['bundle'], settings['path'])
        path = self.native / settings['path'].removeprefix('Assets/')
        source_guid = guid(metadata(path)); self.objects[source_guid] = obj
        self.settings = dict(asset=self.load(source_guid), manifest_asset=settings,
            original=identity(obj), original_sha256=sha(obj.get_raw_data()), source=self.recovery.apk.provenance(obj))
        fields = self.recovery.fields(obj)
        self.icons = []
        for category, group, key in ((0, 'MapGroupDatas', 'MapGroupId'), (1, 'ProfessionDatas', 'Profession'), (2, 'PreferenceDatas', 'NpcGiftType')):
            for item in fields[group]:
                asset_guid = item['IconSpriteProxy']['mAssetGuid']
                row, = [row for row in table if row['guid'] == asset_guid]
                self.icons.append(dict(category=category, value=item[key], **self.sprite(row)))
        rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
        self.inputs[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
        rules = json.loads(rules_path.read_bytes()); assert rules['manifest'] == self.recovery.apk.version
        region_rows = self.regions()
        addresses = set()
        for npc in rules['tables']['npc']:
            addresses.update(npc[field] for field in ('BigBgImage', 'FullBodyImage') if npc[field])
        for row in region_rows:
            addresses.update((row['RegionFullBodyRImages'] or {}).values())
        self.sprites = []
        self.unreachable_images = []
        self.unresolved_images = []
        for address in sorted(addresses):
            path = 'Assets/Image/' + address
            matches = [row for row in table if row['address'] == path or
                       str(Path(row['path']).with_suffix('')).replace('\\', '/') == path]
            if len(matches) != 1:
                consumers = [npc for npc in rules['tables']['npc'] if address in
                             (npc['BigBgImage'], npc['FullBodyImage'])]
                if not matches and consumers and all(11015 <= npc['Id'] <= 11022 for npc in consumers):
                    self.unreachable_images.append(dict(address=address,
                        consumers=[npc['Id'] for npc in consumers],
                        reason='Confirmed closed collaboration; no substitute asset.'))
                    continue
                if not matches:
                    self.unresolved_images.append(dict(address=address, consumers=[npc['Id'] for npc in consumers],
                        reason='Not indexed in selected resource manifest; original bundle inspection and user decision required.'))
                    continue
                raise ValueError('Locate exact selected original detail sprite: ' + address)
            row, = matches
            self.sprites.append(dict(address=address, **self.sprite(row)))
            print('Original detail sprite:', address, flush=True)
        raw = (json.dumps(dict(manifest=self.recovery.apk.version, rows=region_rows), ensure_ascii=False, separators=(',', ':')) + '\n').encode()
        self.region_asset = dict(fileID=4900000, guid=hashlib.md5(b'zjcs:partner-detail-region-images').hexdigest(), type=3)
        self.queue.append((Path('partner-detail-region-images.json'), self.region_asset['guid'], raw))
        self.script_guids.add(self.region_asset['guid'])
        self.template_selection = dict(source=source, adaptation='Complete original adventure tip; fields and layout retained.')
        return [('PopTipNpcAdventure', data)]


def install(work):
    proof = json.loads((work / NAME).read_bytes())
    if proof['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild staged presentation after generator changes')
    for group in ('inputs', 'runtime_bindings'):
        for path, expected in proof[group].items():
            if sha((ROOT / path).read_bytes()) != expected:
                raise ValueError('Presentation input changed: ' + path)
    base = ROOT / 'unity/Assets/Official/Partners'
    own_root = base / 'UI/DetailPresentation'
    existing = {}
    for meta in base.rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing: raise ValueError('Duplicate partner asset GUID: ' + key)
            existing[key] = path
    staged = {}
    for relative, expected in proof['outputs'].items():
        path = work / 'gift-assets' / relative
        if sha(path.read_bytes()) != expected: raise ValueError('Staged presentation changed: ' + relative)
        if not relative.endswith('.meta'):
            meta = metadata(path)
            staged[guid(meta)] = (relative, path.read_bytes(), meta.read_bytes())
    receipt = ROOT / 'assets/reference' / NAME
    previous = json.loads(receipt.read_bytes()) if receipt.exists() else {}
    aliases = dict(previous.get('isolated_serialization_guids', {}))
    def owned(path):
        return path.is_relative_to(own_root) and previous.get('installed_output_sha256', {}).get(
            path.relative_to(ROOT).as_posix()) == sha(path.read_bytes())
    def remap(raw):
        if not raw.startswith(b'%YAML'): return raw
        return re.sub(rb'(?<=guid: )[0-9a-f]{32}', lambda m: aliases.get(m[0].decode(), m[0].decode()).encode(), raw)
    while True:
        changed = False
        for key, (_, raw, _) in staged.items():
            if key in aliases or key not in existing or owned(existing[key]): continue
            if remap(raw) != existing[key].read_bytes():
                aliases[key] = hashlib.md5(('zjcs:partner-detail-presentation:' + key).encode()).hexdigest()
                changed = True
        if not changed: break
    installed, planned = {}, []
    for key, (relative, raw, meta_raw) in staged.items():
        mapped = aliases.get(key, key)
        target = existing.get(mapped, own_root / relative)
        raw = remap(raw)
        meta_raw = re.sub(rb'(?m)^guid: [0-9a-f]{32}$', ('guid: ' + mapped).encode(), meta_raw)
        for path, content in ((target, raw), (Path(str(target) + '.meta'), meta_raw)):
            if path.exists():
                old = path.read_bytes()
                if path.suffix == '.meta' and yaml.safe_load(old) == yaml.safe_load(content): content = old
                if old != content and not owned(path): raise ValueError('Preserve existing presentation asset: ' + str(path))
            planned.append((path, content)); installed[path.relative_to(ROOT).as_posix()] = sha(content)
    def mapped(pointer): return dict(pointer, guid=aliases.get(pointer['guid'], pointer['guid']))
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeDetailPresentationAssets.cs'
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(metadata(script)), type=3), m_Name='PartnerNativeDetailPresentationAssets',
        m_EditorClassIdentifier='', adventureTip=mapped(proof['roots']['PopTipNpcAdventure']),
        settings=mapped(proof['settings']['asset']), regions=mapped(proof['region_asset']),
        sprites=[dict(Path=row['address'], Sprite=mapped(row['asset'])) for row in proof['sprites']],
        icons=[dict(Guid=row['official_asset']['guid'], Sprite=mapped(row['asset'])) for row in proof['adventure_icons']],
        texts=[dict(Key=row['key'], Text=row['text']) for row in proof['texts']])
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/PartnerNativeDetailPresentationAssets.asset'
    content = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
        yaml.dump(dict(MonoBehaviour=body), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode()
    if catalog.exists() and sha(catalog.read_bytes()) != previous.get('installed_output_sha256', {}).get(catalog.relative_to(ROOT).as_posix()):
        raise ValueError('Preserve existing presentation catalog')
    planned.append((catalog, content))
    for path, content in planned:
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != content: path.write_bytes(content)
    for folder in (own_root, *[p for p in own_root.rglob('*') if p.is_dir()]):
        path = metadata(folder, 'DefaultImporter', True)
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for path in (catalog, metadata(catalog, 'NativeFormatImporter')):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for name in ('PartnerNativeDetailPresentationAssets', 'OfflinePartnerAdventureTipServices'):
        path = script.parent / (name + '.cs')
        for item in (path, metadata(path)):
            proof['runtime_bindings'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    proof.update(installed_output_sha256=installed, isolated_serialization_guids=aliases)
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original detail presentation:', len(installed), 'paths')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install:
        install(work)
        return
    binding = PresentationBinding(work)
    try:
        PartnerGiftBinding.build(binding)
        proof = json.loads((work / 'partner-gift-native-source-manifest.json').read_bytes())
        from partner_panel_language import panel_language
        texts, inputs = panel_language(work / 'gift-assets/PopTipNpcAdventure.prefab')
        inputs.pop((work / 'gift-assets/PopTipNpcAdventure.prefab').relative_to(ROOT).as_posix())
        proof['inputs'].update(inputs)
        from npc_travel_language import language
        enum_path = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Profession.cs'
        names = re.findall(r'^\s*(\w+),?\s*$', enum_path.read_text('utf8').split('public enum Profession', 1)[1], re.M)
        gifts = ['None', 'Book', 'Valuables', 'Commodity', 'Flower']
        keys = set()
        for row in binding.icons:
            category, value = row['category'], row['value']
            if category == 0: keys.add('ui_npc_adventure_condition_mapGroup_' + str(value))
            elif category == 1: keys.add('ui_npc_adventure_condition_profession_' + names[value])
            elif category == 2: keys.add('ui_npc_adventure_condition_gift_' + gifts[value])
        dynamic_texts, inputs = language(keys)
        if keys != dynamic_texts.keys(): raise ValueError('Original adventure language missing: ' + repr(keys - dynamic_texts.keys()))
        texts += [dict(key=key, text=value) for key, value in sorted(dynamic_texts.items())]
        proof['inputs'].update(inputs)
        proof['inputs'][enum_path.relative_to(ROOT).as_posix()] = sha(enum_path.read_bytes())
        proof['runtime_bindings'] = {item[key]: item[hash_key]
            for item in proof['script_bindings'].values()
            for key, hash_key in (('path', 'sha256'),)}
        proof['runtime_bindings'].update({item['path'] + '.meta': item['meta_sha256']
            for item in proof['script_bindings'].values()})
        proof.update(generator_sha256=sha(Path(__file__).read_bytes()), settings=binding.settings,
            adventure_icons=binding.icons, sprites=binding.sprites, region_table=binding.region_proof,
            unreachable_images=binding.unreachable_images,
            unresolved_images=binding.unresolved_images,
            region_asset=binding.region_asset, texts=texts,
            scope='Original presentation sprites, region table and adventure tip staged; local services and interaction validation pending.')
        (work / NAME).write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    finally:
        binding.recovery.apk.archive.close()


if __name__ == '__main__':
    main()
