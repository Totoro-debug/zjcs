"""Recover the selected original acceleration presentation, retaining its actor hierarchy."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import struct

import yaml
from prepare_home_fast_hangup_native import HomeFastHangUpBinding, equivalent
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT, guid, sha, script_id, Dumper
from prepare_partner_input_dialog import metadata
from prepare_current_partner_native import documents, identity, pointer
from prepare_partner_detail_actor_native import PartnerDetailActorBinding
from npc_travel_language import language
from validation_workspace import validation_directory
from prepare_character_creation_native_data import read_aligned_string
from prepare_native_character_geometry import atlas_regions
from prepare_partner_native_battle import package
from convert_official_skeletons import reader_type, WHEEL_SHA256

RECEIPT = ROOT/'assets/reference/home-fast-hangup-progress-native-source-manifest.json'
DESTINATION = ROOT/'unity/Assets/Official/Home/UI/FastHangUpProgress'


def decode_bubble(recovery, panel, work):
    """Follow the embedded graphic pointer: this UI asset has no Yoo address."""
    sources, outputs = {}, {}; base = work/'detail-actor-data'; base.mkdir(exist_ok=True)

    def record(obj):
        sources[identity(obj)] = dict(source=recovery.apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        return obj

    def resolve(key):
        cab, path_id = key.rsplit(':', 1)
        return record(recovery.apk.env.find_file(cab).objects[int(path_id)])

    def at(obj, offset):
        file_id, path_id = struct.unpack_from('<iq', obj.get_raw_data(), offset)
        return record(pointer(dict(m_FileID=file_id, m_PathID=path_id), obj))

    def text(obj): return record(obj).read(check_read=False).m_Script.encode('utf8', 'surrogateescape')

    def write(name, raw):
        path = base/name; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        outputs[name] = sha(raw); return name

    def document(name, value):
        return write(name, json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8'))

    graphics = [row for row in panel['scripts'] if row['type'] == 'SkeletonGraphic'
        and panel['documents'][row['nativeFileID']]['MonoBehaviour']['skeletonDataAsset']['fileID']]
    graphic, = graphics; owner = resolve(graphic['original'])
    skeleton = record(pointer(recovery.fields(owner)['skeletonDataAsset'], owner))
    _, offset = read_aligned_string(skeleton.get_raw_data(), 28)
    if struct.unpack_from('<i', skeleton.get_raw_data(), offset)[0] != 1: raise ValueError('Restore every original UI atlas')
    atlas = at(skeleton, offset+4); scale = struct.unpack_from('<f', skeleton.get_raw_data(), offset+16)[0]
    raw = text(at(skeleton, offset+20)); reader_class, _ = reader_type(); reader = reader_class(); data = reader.convert(raw)
    if reader.inp.index != len(raw): raise ValueError('Unconsumed original sleeping bubble skeleton')
    _, atlas_offset = read_aligned_string(atlas.get_raw_data(), 28)
    atlas_bytes = text(at(atlas, atlas_offset+16)); regions = atlas_regions(atlas_bytes.decode('utf8'))
    materials = []
    for i in range(struct.unpack_from('<i', atlas.get_raw_data(), atlas_offset+28)[0]):
        obj = at(atlas, atlas_offset+32+i*12); value = obj.read(); textures = []
        for prop, env in value.m_SavedProperties.m_TexEnvs:
            if not env.m_Texture.path_id: continue
            target = record(env.m_Texture.deref()); texture = target.read(); pixels = texture.image
            buffer = io.BytesIO(); pixels.save(buffer, format='PNG')
            textures.append(dict(property=prop, original=identity(target), name=texture.m_Name,
                width=texture.m_Width, height=texture.m_Height, rgba_sha256=sha(pixels.convert('RGBA').tobytes()),
                output=write('textures/'+sha(buffer.getvalue())+'.png', buffer.getvalue())))
        materials.append(dict(original=identity(obj), fields=obj.read_typetree(), textures=textures, runtimeTextures=[]))
    prefix = 'bubble_09/'
    native = package(0, scale, json.dumps(data, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8'),
        json.dumps(regions, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8'))
    row = dict(npcId=0, source=identity(skeleton), scale=scale, splitSource=None,
        metadata=document(prefix+'metadata.json', recovery.fields(skeleton)),
        skeleton=document(prefix+'skeleton.json', data), binary=write(prefix+'original.skel.bytes', raw),
        atlas=write(prefix+'original.atlas.txt', atlas_bytes), regions=document(prefix+'regions.json', regions),
        materials=document(prefix+'materials.json', materials), package=write(prefix+'native.bytes', b'ZJCSPNUI'+native[8:]),
        animationNames=list(data['animations']), counts={key: len(data.get(key, [])) for key in ('bones','slots','skins','animations')})
    helpers = ['official-source.json', 'scripts/prepare_home_fast_hangup_progress_native.py',
        'scripts/prepare_partner_native_battle.py', 'scripts/convert_official_skeletons.py', 'scripts/prepare_native_character_geometry.py']
    return dict(manifest=recovery.apk.version, sources=sources, outputs=outputs, actors=[row],
        inputs={name: sha((ROOT/name).read_bytes()) for name in helpers}, decoder=dict(wheel_sha256=WHEEL_SHA256))


class HomeFastHangUpProgressBinding(HomeFastHangUpBinding):
    # Reuse the established original skeleton/atlas conversion without importing
    # unrelated NPC prefabs or changing any serialized presentation geometry.
    native_fields = PartnerDetailActorBinding.native_fields
    local_asset = PartnerDetailActorBinding.local_asset
    load_original = PartnerDetailActorBinding.load_original
    actor_data = PartnerDetailActorBinding.actor_data
    script_pointer = PartnerDetailActorBinding.script_pointer

    def __init__(self, work):
        self.actor_proof = {}; self.actor_rows = {}
        self.native_actors = {}; self.native_atlases = {}; self.texture_objects = {}
        super().__init__(work)

    def load(self, source_guid):
        if source_guid in self.redirects: return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name in ('Texture2D', 'Cubemap'):
            key = identity(obj)
            if key not in self.texture_objects:
                self.texture_objects[key] = super().load(source_guid)
                self.script_guids.add(self.texture_objects[key]['guid'])
            self.redirects[source_guid] = self.texture_objects[key]
            return self.redirects[source_guid]
        if obj is not None and obj.type.name == 'MonoBehaviour':
            if identity(obj) in self.actor_rows:
                self.redirects[source_guid] = self.actor_data(source_guid, obj)
                return self.redirects[source_guid]
            script = obj.read(check_read=False).m_Script.deref().read()
            if script.m_ClassName == 'SpineAtlasAsset':
                path, fields = self.native_fields(source_guid, obj)
                # Learn native texture GUIDs before actor_data binds atlas pages;
                # otherwise two GUID aliases overwrite one converted texture path.
                for material in fields['materials']: self.load(material['guid'])
                ref = self.local_asset(identity(obj), fields['m_Name'],
                    dict(sourcePath=path.relative_to(self.native).as_posix(), sourceGuid=source_guid,
                        originalMetadata=dict(fileID=0), materials=fields['materials']),
                    ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeTextureAtlas.cs')
                self.redirects[source_guid] = ref
                return ref
        return super().load(source_guid)

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT/'unity/Assets/Game/Runtime'
        paths = {name: runtime/'Official/HomeFastHangUpProgressUI'/(name.rsplit('.', 1)[-1]+'.cs')
            for name in ('FastHangUpProgressBarDialog', 'TMPFloatEffect', 'AppearanceSystem.MyAppearance',
                'AppearanceSystem.PlayerAppearanceOverride')}
        for name in ('AppearanceUIPartHandler', 'OffScreenUISpinePlayer', 'UIBloomPlayerElement', 'UIBloomGraphicElement',
                'PreviewTool', 'AppearanceSystem.Runtime.AppearanceSkinClip'):
            paths[name] = runtime/'Official/ProfessionUI'/(name.rsplit('.', 1)[-1]+'.cs')
        for name in ('AppearanceSystem.PlayerAppearance', 'AppearanceSystem.Runtime.AppearanceCharacter',
                'AppearanceSystem.Runtime.AppearanceUI'):
            paths[name] = runtime/'Official/CombatAppearance'/(name.rsplit('.', 1)[-1]+'.cs')
        paths['Spine.Unity.SkeletonGraphic'] = runtime/'Offline/ProfessionUI/ProfessionNativeUiGraphic.cs'
        for name, path in paths.items():
            meta = Path(str(path)+'.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            namespace, _, leaf = name.rpartition('.')
            self.scripts[name] = self.class_ids[script_id(namespace, leaf)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def friendship_templates(self):
        self.recovery.recover('FastHangUpProgressBarDialog', self.work/'progress-native',
            bundle='defaultpackage_assets_prefabs_ui_furniture.bundle',
            native_relative='Prefabs/UI/Furniture/FastHangUpProgressBarDialog.prefab')
        self.recovery.finalize()
        result = []; self.template_selection = {}; self.home_template_pointers = {}; self.empty_restorations = []

        def restore(original, current, address):
            if isinstance(original, dict) and isinstance(current, dict):
                for key in original.keys() & current.keys():
                    if current[key] is None and original[key] in ('', [], {}):
                        current[key] = original[key]; self.empty_restorations.append(address+'/'+key)
                    else: restore(original[key], current[key], address+'/'+key)
            elif isinstance(original, list) and isinstance(current, list) and len(original) == len(current):
                for i, (left, right) in enumerate(zip(original, current)): restore(left, right, address+'/'+str(i))

        for native, _, panel, record in self.recovery.outputs:
            self.actor_proof = decode_bubble(self.recovery, panel, self.work)
            self.actor_rows = {row['source']: row for row in self.actor_proof['actors']}
            self.inputs.update(self.actor_proof['inputs'])
            name = native.stem
            template_guid = hashlib.md5(('zjcs:partner-current:'+name+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({key: dict(fileID=file_id, guid=template_guid, type=3)
                for key, file_id in panel['objectMap'].items()})
            self.script_guids.add(template_guid)
            for path in (native, Path(str(native)+'.meta')): self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            for key, ref in panel['external'].items(): self.objects[ref['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            for script in panel['scripts']:
                owner = self.original(script['original']); original = self.recovery.fields(owner)
                fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
                restore(original, fields, script['original'])
                if script['type'] == 'PopupWindowSound':
                    for field in ('ShowSound', 'HideSound'): fields[field] = self.sound(original[field], owner)
            missing = {'.'.join(filter(None, (row['namespace'], row['type']))) for row in panel['scripts']}-self.scripts.keys()
            if missing: raise ValueError('Migrate original presentation scripts: '+repr(missing))
            root_transform = panel['documents'][panel['root']]['GameObject']['m_Component'][0]['component']['fileID']
            for row in panel['scripts']:
                if row['type'] != 'SkeletonGraphic': continue
                fields = panel['documents'][row['nativeFileID']]['MonoBehaviour']
                if not fields['skeletonDataAsset']['fileID']: continue
                go = fields['m_GameObject']['fileID']; bootstrap_id = max(panel['documents'])+1
                panel['documents'][go]['GameObject']['m_Component'].insert(1, dict(component=dict(fileID=bootstrap_id)))
                panel['documents'][bootstrap_id] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
                    m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0),
                    m_GameObject=dict(fileID=go), m_Enabled=1, m_EditorHideFlags=0,
                    m_Script=self.script_pointer(ROOT/'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeUiActor.cs'),
                    m_Name='', m_EditorClassIdentifier='', graphic=dict(fileID=row['nativeFileID']), renderRoot=dict(fileID=root_transform)))
            self.template_selection[name] = dict(source=record['originalRoot'], mapped_objects=len(panel['objectMap']),
                adaptation='Complete original hierarchy, materials, animation and actor references. Native empty values restored; existing current UI actor endpoint bound.')
            result.append((name, panel))
        cache = self.recovery.proof['component_cache']; self.inputs[cache['path']] = cache['sha256']
        return result

    def build(self):
        PartnerGiftBinding.build(self)
        receipt = json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes())
        folder = self.work/'gift-assets'
        keys = {doc['MonoBehaviour']['myKey'] for relative in receipt['outputs'] if relative.endswith('.prefab')
            for doc in documents((folder/relative).read_text('utf8')).values() if doc.get('MonoBehaviour', {}).get('myKey')}
        texts, sources = language(keys)
        if keys-texts.keys(): raise ValueError('Missing original presentation text: '+repr(keys-texts.keys()))
        receipt['inputs'].update(sources)
        for name in ('partner-gift-texts.json', 'partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name); (folder/name).unlink()
        path = folder/'home-fast-hangup-progress-texts.json'
        path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]), ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-fast-hangup-progress-texts').hexdigest()
            +'\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (path, Path(str(path)+'.meta')): receipt['outputs'][item.name] = sha(item.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            raw = (folder/relative).read_bytes()
            if raw.startswith(b'%YAML'): used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', raw.decode('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        for name in ('prepare_home_fast_hangup_progress_native.py', 'prepare_home_fast_hangup_native.py',
                'prepare_home_currency_windows_native.py', 'prepare_home_repair_native.py'):
            path = ROOT/'scripts'/name
            if path.exists(): receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()), texts=texts,
            native_skeleton_inputs=self.actor_proof,
            original_empty_value_restorations=self.empty_restorations,
            scope='Complete original acceleration presentation prefab and dependencies. Local owner, actor appearance and graphical runtime verification are recorded by the home feature page.')
        (self.work/RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        print('Original acceleration presentation:', len(receipt['outputs']), 'outputs', flush=True)


def install(work):
    receipt = json.loads((work/RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Rebuild changed presentation generator')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT/relative).read_bytes()) != expected: raise ValueError('Presentation input changed: '+relative)
    for row in receipt['script_bindings'].values():
        if sha((ROOT/row['path']).read_bytes()) != row['sha256'] or sha((ROOT/(row['path']+'.meta')).read_bytes()) != row['meta_sha256']:
            raise ValueError('Presentation script binding changed: '+row['path'])
    existing = {}
    for meta in (ROOT/'unity/Assets').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if not path.is_file(): continue
        key = guid(meta)
        if key in existing: raise ValueError('Existing Unity GUID collision: '+key)
        existing[key] = path
    staged_guids = {guid(work/'gift-assets'/name) for name in receipt['outputs'] if name.endswith('.meta')}
    verified_script_guids = {row['pointer']['guid'] for row in receipt['script_bindings'].values()}
    def check_refs(value, relative):
        if isinstance(value, dict):
            if value.get('fileID') and value.get('guid'):
                key = value['guid']
                if key not in staged_guids and key not in existing and key not in verified_script_guids and not key.startswith('0000000000000000'):
                    raise ValueError('Unbound original presentation reference: '+relative+' / '+key)
            for item in value.values(): check_refs(item, relative)
        elif isinstance(value, list):
            for item in value: check_refs(item, relative)
    for name in receipt['outputs']:
        source = work/'gift-assets'/name
        raw = source.read_bytes()
        if raw.startswith(b'%YAML'): check_refs(documents(raw.decode('utf8')), name)
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.exists() else {}
    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work/'gift-assets'/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Staged presentation resource changed: '+relative)
        if relative.endswith('.meta'): continue
        key = guid(Path(str(source)+'.meta')); target = existing.get(key, DESTINATION/relative)
        same = target.exists() and equivalent(target.read_bytes(), source.read_bytes())
        if target.exists() and not same:
            if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different original asset: '+str(target))
        if same: reused.append(dict(path=target.relative_to(ROOT).as_posix(), guid=key, sha256=sha(target.read_bytes())))
        else: plan.extend(((source, target), (Path(str(source)+'.meta'), Path(str(target)+'.meta'))))
        if target.is_relative_to(DESTINATION):
            for item, staged in ((target, source), (Path(str(target)+'.meta'), Path(str(source)+'.meta'))):
                installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes()) if same else sha(staged.read_bytes())
        existing[key] = target
    catalog = ROOT/'unity/Assets/Game/Resources/Home/OfficialHomeFastHangUpProgressAssetCatalog.asset'
    script = ROOT/'unity/Assets/Game/Runtime/Official/HomeFastHangUpProgressUI/OfficialHomeFastHangUpProgressAssetCatalog.cs'
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script)+'.meta')), type=3),
        m_Name='OfficialHomeFastHangUpProgressAssetCatalog', m_EditorClassIdentifier='',
        progressPrefab=receipt['roots']['FastHangUpProgressBarDialog'],
        texts=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-fast-hangup-progress-texts').hexdigest(), type=3))
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump(dict(MonoBehaviour=fields), Dumper=Dumper, allow_unicode=True, sort_keys=False)
    if catalog.exists() and not equivalent(catalog.read_bytes(), raw.encode('utf8')) and sha(catalog.read_bytes()) != previous.get(catalog.relative_to(ROOT).as_posix()):
        raise ValueError('Preserve changed presentation catalog')
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(source.read_bytes())
    for folder in [DESTINATION, *[p for p in DESTINATION.rglob('*') if p.is_dir()]]:
        path = metadata(folder, 'DefaultImporter', True); installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    catalog.write_text(raw, 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter')): installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused=reused)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Installed original presentation:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--install', action='store_true')
    args = parser.parse_args(); work = validation_directory()
    if args.install: install(work)
    else: HomeFastHangUpProgressBinding(work).build()
