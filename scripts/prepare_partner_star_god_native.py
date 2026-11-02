"""Restore the selected star-god page and its exact external grouped template."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import yaml

from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_current_partner_native import identity, documents
from prepare_partner_card_native import guid, sha, script_id
from prepare_partner_dialogue_entry_native import PartnerDialogueEntryBinding
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory

NAME = 'partner-star-god-native-source-manifest.json'
SCRIPTS = ('NpcPanelStarGodContent', 'StarGodNpcItem', 'StarGodNpcItemGroupItem', 'StarGodNpcItemContent')


class PartnerStarGodBinding(PartnerDialogueEntryBinding):
    def replace(self, value):
        if isinstance(value, dict) and value.get('guid') and value.get('fileID'):
            original = self.original_pointers.get((value['guid'], value['fileID']))
            if original is not None:
                is_prefab_object = original.type.name == 'GameObject' or (
                    original.type.name in ('MonoBehaviour', 'RectTransform', 'Transform') and
                    original.read(check_read=False).m_GameObject.path_id != 0)
                if is_prefab_object:
                    key = identity(original)
                    if key not in self.prefab_object_pointers: self.load(value['guid'])
                    return self.prefab_object_pointers[key]
            queued = [data for _, key, data in self.queue if key == value['guid']]
            if queued:
                if any(isinstance(data, dict) and value['fileID'] not in data for data in queued):
                    raise ValueError('Generated native pointer references an absent object: ' + repr(value))
                return value
        if value in (dict(**{'class': 'WorldStateGameObjectConfig', 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'}),
                     dict(**{'class': 'WorldStateUIColorConfig', 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'}),
                     dict(**{'class': 'StateAnimatorConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnersUI'})):
            return value
        if isinstance(value, dict) and set(value) == {'originalCAB', 'originalPathID'}:
            key = value['originalCAB'].lower() + ':' + str(value['originalPathID'])
            obj = self.original(key)
            if obj.type.name == 'MonoBehaviour' and not obj.read(check_read=False).m_GameObject.path_id:
                return self.original_asset(obj)
        return super().replace(value)

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: runtime / 'PartnersUI/Roster' / (name + '.cs') for name in SCRIPTS}
        paths.update({name: runtime / 'ProfessionUI' / (name + '.cs') for name in
            ('ItemIcon', 'ItemIconAttachment', 'UIClickSound', 'ItemIconAttachmentCell',
             'ItemIconAttachmentHint', 'ItemIconAttachmentConfig', 'ItemIconConfig')})
        paths['ItemProgress'] = runtime / 'WorldUI/WorldItemProgress.cs'
        paths['AnimationSoundPlayer'] = runtime / 'WorldUI/WorldAnimationSoundPlayer.cs'
        paths['UIMesh'] = runtime / 'WorldUI/WorldUiMesh.cs'
        paths['RotateAnimation'] = runtime / 'WorldUI/WorldRotateAnimation.cs'
        paths['ImageLocator'] = runtime / 'ProfessionUI/ImageLocator.cs'
        for name, path in paths.items():
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])

    def restore(self, data):
        def empty(source, target):
            if isinstance(source, dict) and isinstance(target, dict):
                for key in source.keys() & target.keys():
                    if target[key] is None and source[key] in ('', [], {}): target[key] = source[key]
                    else: empty(source[key], target[key])
            elif isinstance(source, list) and isinstance(target, list) and len(source) == len(target):
                for left, right in zip(source, target): empty(left, right)
        for key, file_id in data['objectMap'].items():
            owner = self.original(key)
            fields = next(iter(data['documents'][file_id].values()))
            original = self.recovery.fields(owner)
            empty(original, fields)
            self.learn(original, fields, owner)
        for key, ref in data['external'].items():
            obj = self.original(key)
            self.objects.setdefault(ref['guid'], obj)
            self.original_pointers[(ref['guid'], ref['fileID'])] = obj
            self.original_objects_by_guid.setdefault(ref['guid'], {})[key] = obj
        for row in data['scripts']:
            if row['type'] == 'StarGodNpcItem':
                owner = self.original(row['original'])
                data['documents'][row['nativeFileID']]['MonoBehaviour']['MeetSound'] = self.sound(self.recovery.fields(owner)['MeetSound'], owner)
            elif row['type'] == 'AnimationSoundPlayer':
                owner = self.original(row['original'])
                data['documents'][row['nativeFileID']]['MonoBehaviour']['mEvents'] = [
                    self.sound(value, owner) for value in self.recovery.fields(owner)['mEvents']]

    def load(self, source_guid):
        if source_guid in self.redirects: return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        root = obj
        if obj is not None and obj.type.name in ('MonoBehaviour', 'Transform', 'RectTransform'):
            fields = obj.read(check_read=False)
            if fields.m_GameObject.path_id: root = fields.m_GameObject.deref()
        if root is not None and root.type.name == 'GameObject' and root.read(check_read=False).m_Name != 'NpcPanelNpcItem':
            # A component pointer into a prefab must retain its exact object ID.
            transform = root.read(check_read=False).m_Component[0].component.deref().read(check_read=False)
            while transform.m_Father.path_id:
                transform = transform.m_Father.deref().read(check_read=False)
            root = transform.m_GameObject.deref()
            prefab_path = self.native_path(source_guid, root)
            self.recovery.recover(root.read(check_read=False).m_Name, self.work / 'partner-native',
                native_relative=prefab_path.relative_to(self.native).as_posix())
            native, _, data, record = self.recovery.outputs[-1]
            original_root, = [key for key, value in data['objectMap'].items() if value == data['root']]
            if identity(root) != original_root or guid(Path(str(native) + '.meta')) != source_guid:
                raise ValueError('External star-god prefab identity differs: ' + identity(root))
            target_guid = hashlib.md5(('zjcs:partner-current-prefab:' + source_guid).encode()).hexdigest()
            target = Path('NativePrefabs') / (sha(source_guid.encode())[:16] + '.prefab')
            self.prefab_references[source_guid] = target_guid
            self.script_guids.add(target_guid)
            self.prefab_object_pointers.update({key: dict(fileID=value, guid=target_guid, type=3) for key, value in data['objectMap'].items()})
            self.redirects[source_guid] = self.prefab_object_pointers[identity(obj)]
            self.restore(data)
            print('External original template types:', sorted({row['type'] for row in data['scripts']}), flush=True)
            for row in data['scripts']:
                full = '.'.join(filter(None, [row['namespace'], row['type']]))
                data['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[full]
            resolved = self.replace(data['documents'])
            self.queue.append((target, target_guid, resolved))
            for path in (native, Path(str(native) + '.meta')):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            self.proof[identity(root)] = dict(source=record['originalSource'], original_sha256=sha(root.get_raw_data()),
                output=target.as_posix(), guid=target_guid, mapped_objects=len(data['objectMap']),
                nativeSource=record['nativeSource'], native_sha256=record['native_sha256'])
            return self.redirects[source_guid]
        return super().load(source_guid)

    def friendship_templates(self):
        self.recovery.recover('NpcPanel', self.work / 'partner-native')
        self.recovery.finalize()
        native, _, panel, _ = self.recovery.outputs[-1]
        consumer, = [row for row in panel['scripts'] if row['type'] == 'NpcPanelStarGodContent']
        root = panel['documents'][consumer['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
        selected = set()

        def visit(go):
            selected.add(go)
            components = panel['documents'][go]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(panel['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(panel['documents'][child['fileID']].values()))['m_GameObject']['fileID'])
        visit(root)
        data = copy.deepcopy(panel)
        data['root'] = root
        data['documents'] = {key: value for key, value in data['documents'].items() if key in selected}
        data['scripts'] = [row for row in data['scripts'] if row['nativeFileID'] in selected]
        data['objectMap'] = {key: value for key, value in data['objectMap'].items() if value in selected}
        transform = next(iter(data['documents'][data['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']].values()))
        parent = transform['m_Father']
        transform['m_Father'] = dict(fileID=0)
        self.restore(data)
        self.template_selection = dict(consumer=consumer['original'], native_root=root,
            mapped_objects=len(selected), original_parent=parent,
            adaptation='Exact original star-god content subtree; detach root parent only. Preserve external template component identity.')
        for path in (native, Path(str(native) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.star_god_images = self.static_images()
        return [('NpcPanelStarGodContent', data)]

    def static_images(self):
        rules = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
        source = json.loads(rules.read_bytes())
        if source['manifest'] != self.recovery.apk.version: raise ValueError('Selected NPC rules differ')
        self.inputs[rules.relative_to(ROOT).as_posix()] = sha(rules.read_bytes())
        decoder = Constructors(ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code')
        tables = {}
        for name, table in (('npc_star_god.g', 'TbNpcStarGodInfo'), ('npc_region_image.g', 'TbNpcRegionImageInfo')):
            obj, = [value for value in self.recovery.apk.objects('defaultpackage_assets_config_luban.bundle')
                if value.type.name == 'TextAsset' and value.read().m_Name.rsplit('/', 1)[-1] == name]
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = dict(rows=decoder.table(raw, table), source=self.recovery.apk.provenance(obj), sha256=sha(raw))
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        bindings, definitions, keys = [], [], set()
        def sprite(address):
            record, = [value for value in records if value['address'] == 'Assets/Image/' + address
                or value['path'] == 'Assets/Image/' + address + '.png']
            obj, = [ref.deref() for path, ref in self.recovery.apk.container(record['bundle'])
                if path.casefold() == record['path'].casefold() and ref.deref().type.name == 'Sprite']
            source_key, source_id = self.recovery.fields(obj)['m_RenderDataKey']
            render_guid = ''.join(format(source_key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
            if render_guid != record['guid']: raise ValueError('Original star-god image identity differs')
            name = self.name_key(obj.read(check_read=False).m_Name)
            matches = []
            for key in [name] + [name + str(i) for i in range(100)]:
                for path in self.by_name.get(key, []):
                    if path.suffix != '.asset': continue
                    docs = documents(path.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: source_id}:
                        matches.append(path)
            native, = matches
            source_guid = guid(Path(str(native) + '.meta'))
            self.objects[source_guid] = obj
            pointer = self.load(source_guid)
            bindings.append(dict(address=address, pointer=pointer, source=self.recovery.apk.provenance(obj),
                original=identity(obj), original_sha256=sha(obj.get_raw_data()), manifest_asset=record))

        meet_items = set()
        conditions = {row['ClassId']: row['Condition'] for row in source['tables']['conditions']}
        for row in source['tables']['npc']:
            if row['NpcRoleType'] != 2 or row['NpcType'] == 2: continue
            region = next((value for value in tables['npc_region_image.g']['rows'] if value['NpcId'] == row['Id']), None)
            regional = region.get('RegionFullBodyRImages') if region else None
            # Common.RegionCodeEnum CN=0; current selected source is checked below.
            address = (regional.get('0') or regional.get(0) if regional else None) or row['FullBodyImage']
            sprite(address)
            condition = row['CanMeetCondition']
            direct = [conditions[value] for value in condition['Ids']] if condition['_type'] in (
                'CompositeAndCondition', 'CompositeOrCondition') else [condition]
            meet_items.update(value['ClassId'] for value in direct if value['_type'] == 'HasItemCondition')
            star = next((value for value in tables['npc_star_god.g']['rows'] if value['NpcClassId'] == row['Id']), None)
            location_key = 'npc_star_god_location_' + str(row['Id']) if star else None
            keys.add(row['NameKey'])
            if location_key: keys.add(location_key)
            definitions.append(dict(Id=row['Id'], IsStarGodNpc=True, IsMapExploreBoss=False,
                NameKey=row['NameKey'], LocationDescKey=location_key, StaticFullBodyImagePath=address))
        item_rows = {row['ClassId']: row for row in source['tables']['item']}
        for item in sorted(meet_items): sprite(item_rows[item]['RawIcon'])
        from npc_travel_language import language
        texts, inputs = language(keys)
        if set(texts) != keys: raise ValueError('Selected star-god language is incomplete')
        self.inputs.update(inputs)
        for relative in ('common-source/Common/NpcStarGodInfo.cs', 'common-source/Common/NpcRegionImageInfo.cs',
                         'framework-firstpass-source/ResourceLoaderExtensions.cs', 'launch-source/AnimationSoundPlayer.cs',
                         'launch-source/UIMesh.cs', 'launch-source/RotateAnimation.cs', 'launch-source/Common/RegionCodeEnum.cs',
                         'common-source/NpcHelper.cs'):
            path = ROOT / '.tools/source-packages/official-code' / relative
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if path.read_bytes() != selected.read_bytes(): raise ValueError('Selected source differs: ' + relative)
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return dict(sprites=bindings, definitions=definitions, tables=tables, meet_item_ids=sorted(meet_items),
            texts=[dict(key=key, text=texts[key]) for key in sorted(texts)])

    def build(self):
        super().build()
        receipt = json.loads((self.work / 'partner-dialogue-entry-native-source-manifest.json').read_bytes())
        repairs = []
        for name in receipt['outputs']:
            if name.endswith('.meta'): continue
            path = self.work / 'gift-assets' / name
            if path.suffix != '.asset': continue
            content = path.read_text('utf8')
            if '\nMesh:\n' not in content: continue
            # Unity's binary readers do not treat YAML's literal `null` as an
            # empty buffer. It corrupts the imported vertex data and crashes UI
            # geometry jobs. Preserve the bytes and use Unity's empty scalar.
            fixed = content.replace(': null', ': ')
            if fixed == content: continue
            before = sha(path.read_bytes())
            path.write_text(fixed, 'utf8', newline='\n')
            receipt['outputs'][name] = sha(path.read_bytes())
            repairs.append(dict(path=name, before_sha256=before, after_sha256=receipt['outputs'][name],
                adaptation='Serialize empty Mesh buffers/strings as Unity empty scalars; retain all non-empty vertex/index/channel bytes and fields.'))
        receipt['serialization_repairs'] = repairs
        for name in ('prepare_partner_dialogue_entry_native.py',):
            path = ROOT / 'scripts' / name
            receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['star_god_images'] = self.star_god_images
        receipt['scope'] = 'Original star-god page and grouped card template; full host/detail not yet installed.'
        (self.work / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def install(work):
    receipt = json.loads((work / NAME).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Rebuild star-god assets')
    for name, expected in receipt['inputs'].items():
        if sha((ROOT / name).read_bytes()) != expected: raise ValueError('Input changed: ' + name)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Script binding changed: ' + row['path'])
    base = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for meta in base.rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing: raise ValueError('Duplicate partner GUID: ' + key)
            existing[key] = path
    destination = base / 'UI/StarGod'
    previous_path = ROOT / 'assets/reference' / NAME
    previous = json.loads(previous_path.read_bytes()).get('installed_output_sha256', {}) if previous_path.exists() else {}
    planned, installed, reused = [], {}, []
    for name, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged original asset changed: ' + name)
        if name.endswith('.meta'): continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, destination / name)
        if target.exists():
            if source.read_bytes() != target.read_bytes():
                relative = target.relative_to(ROOT).as_posix()
                if not target.is_relative_to(destination) or sha(target.read_bytes()) != previous.get(relative):
                    raise ValueError('Preserve differing asset: ' + str(target))
                planned.append((source, target))
            else: reused.append(target.relative_to(ROOT).as_posix())
        else:
            planned.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
        for left, right in ((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))):
            installed[right.relative_to(ROOT).as_posix()] = sha((right if right.exists() else left).read_bytes())
    for source, target in planned:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
    for folder in [destination, *[path for path in destination.rglob('*') if path.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerStarGodAssetCatalog.cs'
    script_meta = metadata(script)
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerStarGodAssetCatalog.asset'
    data = receipt['star_god_images']
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3), m_Name='OfficialPartnerStarGodAssetCatalog',
        m_EditorClassIdentifier='', pagePrefab=receipt['roots']['NpcPanelStarGodContent'],
        definitions=[{key: int(value) if isinstance(value, bool) else value for key, value in row.items()}
                     for row in data['definitions']],
        images=[dict(path=row['address'], assetGuid=row['manifest_asset']['guid'], sprite=row['pointer'])
                for row in data['sprites']], texts=data['texts'])
    from prepare_partner_card_native import Dumper
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
        yaml.dump(dict(MonoBehaviour=body), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000), 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter'), script, script_meta):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused_assets=reused)
    (ROOT / 'assets/reference' / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed star-god native files:', len(installed), 'reused assets:', len(reused), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    work = validation_directory()
    if parser.parse_args().install: install(work)
    else: PartnerStarGodBinding(work).build()
