"""Bind the selected official portrait studio to the native Windows character backend."""
import hashlib
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re
import subprocess
import sys
import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from prepare_partner_card_native import PartnerCardBinding, Dumper, guid, sha
from prepare_current_partner_native import documents, identity
from validation_workspace import validation_directory


def metadata(key, importer):
    return ('fileFormatVersion: 2\nguid: ' + key + '\n' + importer + ':\n  externalObjects: {}\n').encode()


def stable(name):
    return hashlib.md5(('zjcs:partner-player-portraits:' + name).encode()).hexdigest()


def serialize(payload):
    if not isinstance(payload, dict):
        return payload
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for file_id, content in payload.items():
        kind, = content
        text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
        text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    return text.encode()


def binding_sources(receipt):
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    paths = [runtime / 'Official/ProfessionUI' / (name + suffix) for name in
        ('HeadCharacterPortraitBuilder', 'PreviewTool') for suffix in ('.cs', '.cs.meta')]
    paths += [runtime / 'Offline/ProfessionUI' / (name + suffix) for name in
        ('ProfessionNativePortraitAppearance', 'ProfessionNativePortraitStudioResources',
         'OfflineProfessionPortraitSkinClipBindings', 'OfflineProfessionPortraitStudio') for suffix in ('.cs', '.cs.meta')]
    paths += [runtime / 'Offline/ProfessionUI' / (name + '.cs') for name in
        ('ProfessionNativeSceneRenderer', 'ProfessionNativeSceneRendererSettings', 'ProfessionNativeMeshBatches')]
    paths += [ROOT / 'scripts/prepare_partner_player_portrait_validation.py',
        ROOT / 'scripts/fixtures/partner_player_portrait_validation.cs']
    receipt['binding_source_sha256'] = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in paths}
    original = ROOT / '.tools/source-packages/current/884_156692/official-code'
    original_files = [original / 'client-source' / (n + '.cs') for n in
        ('HeadCharacterPortraitBuilder', 'CharacterPortraitUtils', 'CharacterPortraitPhotographer', 'AppearancePortraitUtils')]
    original_files += [original / 'client-source/AppearanceSystem.Runtime' / (n + '.cs') for n in
        ('AppearanceAnimation', 'AppearanceAnimUtils')]
    original_files += [original / 'framework-source/PreviewTool.cs']
    receipt['original_source_sha256'] = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in original_files}
    dependencies = ['assets/reference/battle-animation/scene-player-appearance-source-manifest.json',
        'assets/reference/battle-animation/scene-texture-source-manifest.json',
        'assets/reference/battle-animation/scene-native-renderer-source-manifest.json']
    receipt['current_character_dependencies'] = {p: sha((ROOT / p).read_bytes()) for p in dependencies}
    receipt['adaptations'] = [
        'Original Head builder and exclusion clone/order; explicit per-studio configuration replaces DI.',
        'Original 24-object studio hierarchy/fields, Camera, Animator and SortingGroup retained; nine script bindings are explicit.',
        'SkeletonAnimation/AppearanceAnimation draw through the independent native character backend and an added MeshPart; source slot zSpacing is applied after clipping.',
        'Original synchronous frame-zero Head capture, current skin/material/Low texture/clip services and original release path. No elapsed animation or physics simulation is claimed.',
        'Head and BlindBox share the original builder; other portrait kinds require their own original builder. Full wardrobe effects/independent parts and the production UI manager remain separate integration work.']
    receipt['generator_sha256'] = sha(Path(__file__).read_bytes())


def main():
    work = validation_directory()
    proof_path = ROOT / 'assets/reference/partner-player-portraits-source-manifest.json'
    if '--refresh-binding-sources' in sys.argv:
        receipt = json.loads(proof_path.read_bytes())
        for field in ('installed_output_sha256', 'reused_output_sha256'):
            for relative, expected in receipt[field].items():
                if sha((ROOT / relative).read_bytes()) != expected:
                    raise ValueError('Installed original portrait changed: ' + relative)
        # Recovered art inputs remain immutable. Explicit runtime bindings may
        # advance, with one current hash in both source sections.
        binding_sources(receipt)
        for relative, expected in receipt['inputs'].items():
            if relative in receipt['binding_source_sha256']:
                receipt['inputs'][relative] = receipt['binding_source_sha256'][relative]
            elif sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Recovered portrait input changed: ' + relative)
        proof_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Verified original portrait payloads and refreshed application bindings', flush=True)
        return
    binding = PartnerCardBinding(work)
    recovery = binding.recovery
    if recovery.apk.version != '884_156692':
        raise ValueError('Review the selected portrait source version')
    recovery.recover('CharacterPortraitStudio', work / 'source',
        bundle='defaultpackage_assets_prefabs_appearance_portrait.bundle',
        native_relative='Prefabs/Appearance/Portrait/CharacterPortraitStudio.prefab')
    recovery.finalize()
    _, _, studio, record = recovery.outputs[-1]
    if studio['unresolved']:
        raise ValueError('Resolve the complete original studio before binding')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    scripts = {
        'CharacterPortraitPhotographer': runtime / 'Official/ProfessionUI/CharacterPortraitPhotographer.cs',
        'UniversalAdditionalCameraData': ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/UniversalAdditionalCameraData.cs',
        'SkeletonAnimation': runtime / 'Offline/ProfessionUI/ProfessionNativeSceneRenderer.cs',
        'AppearanceAnimation': runtime / 'Offline/ProfessionUI/ProfessionNativePortraitAppearance.cs',
        'AppearanceCharacter': runtime / 'Official/CombatAppearance/AppearanceCharacter.cs',
        'AppearanceRendererPartHandler': runtime / 'Official/ProfessionUI/AppearanceRendererPartHandler.cs',
        'AppearanceSkinClip': runtime / 'Official/ProfessionUI/AppearanceSkinClip.cs',
        'PreviewTool': runtime / 'Official/ProfessionUI/PreviewTool.cs',
        'PlayerAppearance': runtime / 'Official/CombatAppearance/PlayerAppearance.cs',
        'PlayerPortraitSettings': runtime / 'Official/ProfessionUI/PlayerPortraitSettings.cs',
        'AppearanceConfig': runtime / 'Official/ProfessionUI/AppearanceConfig.cs',
    }
    pointers = {}
    for name, path in scripts.items():
        pointers[name] = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
        binding.script_guids.add(pointers[name]['guid'])
        binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        binding.inputs[path.relative_to(ROOT).as_posix() + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
    for key, pointer in studio['external'].items():
        binding.objects[pointer['guid']] = binding.original(key)
    nodes = []
    by_id = {int(k): v for k, v in studio['documents'].items()}
    source_by_native = {}
    for key, file_id in studio['objectMap'].items():
        owner = binding.original(key)
        source = recovery.fields(owner)
        source_by_native[file_id] = (owner, source)
        binding.learn(source, next(iter(by_id[file_id].values())), owner)
    source_types = {s['nativeFileID']: '.'.join(filter(None, (s['namespace'], s['type']))) for s in studio['scripts']}
    for file_id, doc in by_id.items():
        if 'GameObject' not in doc:
            continue
        go = doc['GameObject']
        components = []
        for p in go['m_Component']:
            cid = p['component']['fileID']
            owner, fields = source_by_native[cid]
            components.append(dict(type=source_types.get(cid, owner.type.name), fields=fields,
                original=identity(owner), original_sha256=sha(owner.get_raw_data())))
        nodes.append(dict(name=go['m_Name'], components=components))
    for script in studio['scripts']:
        fields = by_id[script['nativeFileID']]['MonoBehaviour']
        fields['m_Script'] = pointers[script['type']]
    player_id, = [i for i, d in by_id.items() if d.get('GameObject', {}).get('m_Name') == 'PlayerModel']
    mesh_part_id = 114000000000000001
    if mesh_part_id in by_id:
        raise ValueError('Native backend component ID collision')
    mesh_script = runtime / 'Offline/ProfessionUI/ProfessionNativeSceneMeshPart.cs'
    mesh_pointer = dict(fileID=11500000, guid=guid(Path(str(mesh_script) + '.meta')), type=3)
    binding.script_guids.add(mesh_pointer['guid'])
    by_id[player_id]['GameObject']['m_Component'].append(dict(component=dict(fileID=mesh_part_id)))
    by_id[mesh_part_id] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
        m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0),
        m_GameObject=dict(fileID=player_id), m_Enabled=1, m_EditorHideFlags=0, m_Script=mesh_pointer,
        m_Name='', m_EditorClassIdentifier=''))
    by_id = binding.replace(by_id)

    configurations = {}
    for name, bundle, relative in (
        ('PlayerPortraitSettings', 'defaultpackage_assets_scriptableconfig_modules_playerportrait.bundle',
         'ScriptableConfig/Modules/PlayerPortrait/PlayerPortraitSettings.asset'),
        ('AppearanceConfig', 'defaultpackage_assets_scriptableconfig_modules_appearance.bundle',
         'ScriptableConfig/Modules/Appearance/AppearanceConfig.asset')):
        candidates = [o for o in recovery.apk.objects(bundle) if o.type.name == 'MonoBehaviour'
            and recovery.fields(o).get('m_Name') == name]
        owner, = candidates
        path = recovery.native / relative
        raw = path.read_bytes()
        if sha(raw) != binding.checked[path.relative_to(binding.cache / 'native-export-all').as_posix()]:
            raise ValueError('Current original portrait configuration changed: ' + name)
        binding.inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        docs = documents(raw.decode())
        file_id, = docs
        fields = docs[file_id]['MonoBehaviour']
        original_fields = recovery.fields(owner)
        binding.learn(original_fields, fields, owner)
        # The converter encodes some enum arrays as compact byte strings.
        # Restore every field from the active assembly descriptor, retaining
        # original sequence values/order and resolving exact original PPtrs.
        def restore(value, field=''):
            if field == 'm_Script': return pointers[name]
            if isinstance(value, dict):
                if 'm_FileID' in value and 'm_PathID' in value:
                    if not value['m_PathID']: return dict(fileID=0)
                    from UnityPy.classes.PPtr import PPtr
                    linked = PPtr(**value, assetsfile=owner.assets_file).deref()
                    candidates = [key for key, obj in binding.objects.items() if identity(obj) == identity(linked)]
                    source_guid, = candidates
                    return binding.load(source_guid)
                return {k: restore(v, k) for k, v in value.items()}
            if isinstance(value, list): return [restore(v) for v in value]
            return value
        fields = dict(fields, **restore(original_fields))
        fields['m_EditorClassIdentifier'] = ''
        docs = {file_id: dict(MonoBehaviour=fields)}
        binding.script_guids.update(p['guid'] for p in binding.redirects.values() if p.get('guid'))
        configurations[name] = dict(fileID=file_id, guid=stable(name), type=2)
        binding.queue.append((Path(name + '.asset'), stable(name), binding.replace(docs)))
        binding.proof[identity(owner)] = dict(source=recovery.apk.provenance(owner),
            original_sha256=sha(owner.get_raw_data()), native_path=path.relative_to(ROOT).as_posix(), original_fields=original_fields)
    for _, _, payload in binding.queue:
        if isinstance(payload, dict):
            binding.script_guids.update(p['guid'] for p in binding.redirects.values() if p.get('guid'))
            resolved = binding.replace(payload)
            payload.clear(); payload.update(resolved)
    hierarchy = dict(manifest=recovery.apk.version, nodes=nodes)
    binding.queue += [(Path('CharacterPortraitStudio.prefab'), stable('studio'), by_id),
        (Path('portrait-hierarchy.json'), stable('hierarchy'),
         (json.dumps(hierarchy, ensure_ascii=False, indent=2) + '\n').encode())]
    source_handler, = [source_by_native[s['nativeFileID']][1] for s in studio['scripts']
        if s['type'] == 'AppearanceRendererPartHandler']
    # The original handler's slot is a Transform; its owning GameObject supplies
    # the exact path used by the production binding, never a replacement layout.
    from UnityPy.classes.PPtr import PPtr
    handler_owner, = [source_by_native[s['nativeFileID']][0] for s in studio['scripts']
        if s['type'] == 'AppearanceRendererPartHandler']
    slot = PPtr(**source_handler['mSlot'], assetsfile=handler_owner.assets_file).deref()
    slot_name = slot.read().m_GameObject.read().m_Name
    character_asset = ROOT / 'unity/Assets/Game/Resources/Combat/SceneCharacters/appearance-resources.asset'
    catalog_script = runtime / 'Offline/ProfessionUI/ProfessionNativePortraitStudioResources.cs'
    catalog = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(catalog_script) + '.meta')), type=3),
        m_Name='OfficialPartnerPlayerPortraitResources', m_EditorClassIdentifier='',
        StudioPrefab=dict(fileID=studio['root'], guid=stable('studio'), type=3),
        Hierarchy=dict(fileID=4900000, guid=stable('hierarchy'), type=3),
        Settings=configurations['PlayerPortraitSettings'], AppearanceConfig=configurations['AppearanceConfig'],
        Characters=dict(fileID=11400000, guid=guid(Path(str(character_asset) + '.meta')), type=2),
        PartSlot=slot_name, SortingLayer=source_handler['mSortingLayer'], SortingOrder=source_handler['mDefaultSortingOrder']))
    binding.inputs[character_asset.relative_to(ROOT).as_posix()] = sha(character_asset.read_bytes())
    previous = json.loads(proof_path.read_bytes()).get('installed_output_sha256', {}) if proof_path.exists() else {}
    outputs, reused = {}, {}
    existing = {}
    tracked = subprocess.run(['git', 'ls-files', '-z', 'unity/Assets/*.meta'], cwd=ROOT, check=True, capture_output=True).stdout
    def metadata_entry(relative):
        meta = ROOT / relative
        if not meta.is_file(): return None
        asset = Path(str(meta)[:-5])
        return (guid(meta), asset) if asset.is_file() else None
    with ThreadPoolExecutor(max_workers=16) as executor:
        for entry in executor.map(metadata_entry, [p.decode() for p in tracked.split(b'\0') if p]
                + [p for p in previous if p.endswith('.meta')]):
            if entry: existing.setdefault(entry[0], set()).add(entry[1])

    def install(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve changed local portrait file: ' + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs[relative] = sha(raw)

    base = ROOT / 'unity/Assets/Official/Partners/PlayerPortraits'
    folders = {base}
    for target, key, payload in binding.queue:
        raw = serialize(payload)
        path = base / target
        candidates = existing.get(key, set())
        if candidates:
            candidate, = candidates
            if candidate != path:
                if candidate.read_bytes().replace(b'\r\n', b'\n') != raw:
                    raise ValueError('Existing portrait dependency differs: ' + str(candidate))
                for p in (candidate, Path(str(candidate) + '.meta')):
                    reused[p.relative_to(ROOT).as_posix()] = sha(p.read_bytes())
                continue
        install(path, raw)
        importer = 'PrefabImporter' if path.suffix == '.prefab' else 'TextScriptImporter' if path.suffix == '.json' else 'ShaderImporter' if path.suffix == '.shader' else 'NativeFormatImporter'
        install(Path(str(path) + '.meta'), metadata(key, importer))
        parent = path.parent
        while parent != base:
            folders.add(parent); parent = parent.parent
    for folder in folders:
        install(Path(str(folder) + '.meta'), ('fileFormatVersion: 2\nguid: ' + stable(folder.relative_to(base).as_posix())
            + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
    catalog_path = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerPlayerPortraitResources.asset'
    install(catalog_path, serialize({11400000: catalog}))
    install(Path(str(catalog_path) + '.meta'), metadata(stable('catalog'), 'NativeFormatImporter'))
    receipt = dict(manifest=recovery.apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
        inputs=binding.inputs, studio=record, script_bindings={k: v.relative_to(ROOT).as_posix() for k,v in scripts.items()},
        native_assets=binding.proof,
        installed_output_sha256=outputs, reused_output_sha256=reused)
    binding_sources(receipt)
    proof_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed selected original portrait studio:', len(outputs), 'files;', len(reused), 'verified reused dependencies', flush=True)


if __name__ == '__main__':
    main()
