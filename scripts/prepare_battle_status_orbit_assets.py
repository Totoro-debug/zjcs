"""Restore complete original orbit graphs and their native Windows dependencies."""
import json
import os
import re
import struct
import shutil
import subprocess
import uuid
from functools import lru_cache
from pathlib import Path
from hashlib import sha256
from PIL import Image, ImageChops
from Crypto.Hash import MD4

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_ground_effect_assets import CACHE, EXPORTED, GUID, BUILTINS
from prepare_battle_movement_effects import SCRIPT, SCRIPTS, runtime_equivalence
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_playable_runtime import CODE
from prepare_unity_bundle_inputs import serialized_files

DEST = ROOT / 'unity/Assets/Official/Combat/StatusOrbits/Resources/Combat/StatusOrbits'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/status-orbits.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/status-orbit-assets-source-manifest.json'
ROOTS = {24221: 'playable_24221', 24241: 'playable24241', 24477: 'playable_24477'}
MAPPED = dict(SCRIPTS)
MAPPED.update({-413707684: ('BattleStatusPlayables', 'MyPlayableOrbitMovement'),
               -270686477: ('BattleStatusPlayables', 'ApplyByStatusStackCount')})


def sha(raw):
    return sha256(raw).hexdigest()


def build(DEST=DEST, DATA=DATA, RECEIPT=RECEIPT, ROOTS=ROOTS, MAPPED=MAPPED,
          family='StatusOrbits', shader_prefix='StatusOrbit_', runtime_receipts=None,
          generator=None, limits=None, graph_roots=None, reference_scripts=None,
          external_scripts=None, source_scripts=None, native_transform=None,
          external_assets=None, native_scripts=None, operation=None, native_preprocess=None,
          graph_originals=None):
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == (ROOT/'output/validation').resolve() and (work/'.zjcs-validation.json').is_file()
    export_receipt = CACHE/'native-export-all-source-manifest.json'
    proof = json.loads(export_receipt.read_bytes())
    assert proof['manifest'] == '884_156692'
    assert proof['code_manifest_sha256'] == sha((CACHE.parent/'code-manifest.json').read_bytes())
    verified = {r['path']: r['sha256'] for r in proof['outputs']}
    del proof  # Keep the digest lookup, release the full exporter receipt rows.
    inputs, index = {}, {}

    def read(path):
        raw = path.read_bytes()
        assert sha(raw) == verified[path.relative_to(CACHE/'native-export-all').as_posix()], path
        inputs[path.as_posix()] = identity(path)
        return raw

    index_path = work/'status-orbit-guid-index.json'
    if index_path.exists(): index = json.loads(index_path.read_bytes())
    complete_index_built = False

    def resolve(guid):
        nonlocal complete_index_built
        if guid not in index:
            for category in ('Effect', 'Shaders', 'Material', 'Materials', 'Mesh', 'Meshes', 'Packages', 'AnimatorController', 'AnimationClip',
                             'Texture2D', 'Textures', 'Shader', 'effect_shader', 'Light', 'Prefabs',
                             'AppearanceAssets', 'Wwise', 'UIEffect', 'Sprite'):
                flag = '@' + category
                if flag in index: continue
                for path in (EXPORTED/category).rglob('*.meta'):
                    match = GUID.search(path.read_text(encoding='utf-8-sig'))
                    if match: index[match[1]] = path.relative_to(EXPORTED).as_posix()[:-5]
                index[flag] = True
                index_path.write_bytes(json.dumps(index).encode())
                if guid in index: break
        if guid not in index and not complete_index_built:
            # The complete exporter has hundreds of thousands of sidecars. Let
            # rg stream them once instead of opening every tiny file in Python.
            # Selected assets are still individually hash-verified by read().
            if shutil.which('rg'):
                scan = subprocess.run(['rg', '--hidden', '--no-ignore', '--with-filename',
                    '--no-heading', '--color', 'never', '--glob', '*.meta',
                    r'^guid: [a-f0-9]{32}\r?$', str(EXPORTED)],
                    capture_output=True, text=True, encoding='utf-8', check=True)
                for line in scan.stdout.splitlines():
                    filename, value = line.rsplit(':guid: ', 1)
                    index[value.strip()] = Path(filename).relative_to(EXPORTED).as_posix()[:-5]
            else:
                for path in EXPORTED.rglob('*.meta'):
                    match = GUID.search(path.read_text(encoding='utf-8-sig'))
                    if match: index[match[1]] = path.relative_to(EXPORTED).as_posix()[:-5]
            index_path.write_bytes(json.dumps(index).encode())
            complete_index_built = True
        assert guid in index, ('Unresolved native orbit reference', guid)
        return EXPORTED/index[guid]

    script_guids, script_records = {}, []
    for file_id, (folder, name) in MAPPED.items():
        target = ROOT/'unity/Assets/Game/Runtime/Official'/folder/(name+'.cs')
        script_guids[file_id] = GUID.search(target.with_name(target.name+'.meta').read_text(encoding='utf-8-sig'))[1]
        originals = [path for path in (CODE/'client-source'/(name+'.cs'),
            CODE/'game-framework-source/GameFramework'/(name+'.cs')) if path.is_file()]
        assert len(originals) == 1, (name, originals)
        original_text = originals[0].read_text(encoding='utf-8-sig')
        namespace = re.search(r'(?m)^namespace ([\w.]+)', original_text)
        assert struct.unpack('<i', MD4.new(('s\0\0\0'+(namespace[1] if namespace else '')+name).encode()).digest()[:4])[0] == file_id
        script_records.append(dict(file_id=file_id, original=identity(originals[0]), migrated=identity(target),
            migrated_meta=identity(Path(str(target)+'.meta')),
            verified_current_runtime_methods=runtime_equivalence(original_text,
                target.read_text(encoding='utf-8-sig'), name)))
    for file_id, binding in (reference_scripts or {}).items():
        # SDK reference assets have no managed client method body. Verify the
        # original serialized type and retain the existing typed field bridge.
        original = EXPORTED/binding['native_asset']
        text = read(original).decode('utf-8-sig')
        read(Path(str(original)+'.meta'))
        assert any(int(match[1]) == file_id for match in SCRIPT.finditer(text))
        assert struct.unpack('<i', MD4.new(('s\0\0\0'+binding.get('namespace', '')+binding['name']).encode()).digest()[:4])[0] == file_id
        target = ROOT/binding['runtime']
        script_guids[file_id] = GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
        script_records.append(dict(file_id=file_id, original_native_reference=identity(original),
            migrated=identity(target), migrated_meta=identity(Path(str(target)+'.meta')),
            field_bridge_receipt=identity(ROOT/binding['receipt'])))
    for file_id, binding in (external_scripts or {}).items():
        original = EXPORTED/binding['native_meta']
        read(original)
        target = ROOT/binding['runtime']
        target_meta = Path(str(target)+'.meta')
        original_text = target.read_text(encoding='utf-8-sig')
        namespace = re.search(r'(?m)^namespace ([\w.]+)', original_text)
        actual_namespace = namespace[1] if namespace else ''
        assert actual_namespace == binding['namespace'] and target.stem == binding['name']
        assert re.search(r'\bclass\s+'+re.escape(target.stem)+r'\b', original_text)
        if binding.get('verify_file_id_hash'):
            assert struct.unpack('<i', MD4.new(('s\0\0\0'+actual_namespace+target.stem).encode()).digest()[:4])[0] == file_id
        source = ROOT/binding['source']
        assert source.is_file()
        script_guids[file_id] = GUID.search(target_meta.read_text(encoding='utf-8-sig'))[1]
        script_records.append(dict(file_id=file_id, original_native_reference=identity(original),
            migrated=identity(source), migrated_script=identity(target),
            migrated_meta=identity(target_meta),
            verification='namespace, type name, original native plugin reference'+
                (' and source script ID hash' if binding.get('verify_file_id_hash') else '')))
    for file_id, binding in (source_scripts or {}).items():
        original = CODE/binding.get('source_root', 'client-source')/binding['source']
        original_text = original.read_text(encoding='utf-8-sig')
        namespace = re.search(r'(?m)^namespace ([\w.]+)', original_text)
        actual_namespace = namespace[1] if namespace else ''
        assert actual_namespace == binding['namespace']
        assert re.search(r'\bclass\s+'+re.escape(binding['name'])+r'\b', original_text)
        target = ROOT/binding['runtime']
        target_text = target.read_text(encoding='utf-8-sig')
        target_namespace = re.search(r'(?m)^namespace ([\w.]+)', target_text)
        assert (target_namespace[1] if target_namespace else '') == binding.get('target_namespace', actual_namespace)
        assert re.search(r'\bclass\s+'+re.escape(binding['name'])+r'\b', target_text)
        target_meta = Path(str(target)+'.meta')
        script_guids[file_id] = GUID.search(target_meta.read_text(encoding='utf-8-sig'))[1]
        script_records.append(dict(file_id=file_id, original=identity(original),
            migrated=identity(target), migrated_meta=identity(target_meta),
            verification='official source type and namespace; ID bound by active-version prefab reference'))
    local_scripts = set(script_guids.values())
    native_script_records = []
    for relative in native_scripts or ():
        target = ROOT/relative
        sidecar = Path(str(target)+'.meta')
        local_scripts.add(GUID.search(sidecar.read_text(encoding='utf-8-sig'))[1])
        native_script_records.append(dict(script=identity(target), meta=identity(sidecar)))
    external_replacements, external_records = {}, []
    for original_guid, binding in (external_assets or {}).items():
        original, target = EXPORTED/binding['source'], ROOT/binding['target']
        read(original)
        source_meta = Path(str(original)+'.meta')
        assert GUID.search(read(source_meta).decode('utf-8-sig'))[1] == original_guid
        target_meta = Path(str(target)+'.meta')
        external_replacements[original_guid] = GUID.search(target_meta.read_text(encoding='utf-8-sig'))[1]
        external_records.append(dict(source=identity(original), source_meta=identity(source_meta),
            target=identity(target), target_meta=identity(target_meta)))
    unmapped_scripts = {}
    unmapped_script_samples = {}

    def adapt(text, path):
        if native_preprocess:
            text = native_preprocess(text, path)
        def script(match):
            file_id = int(match[1])
            if file_id not in script_guids:
                unmapped_scripts[file_id] = unmapped_scripts.get(file_id, 0) + 1
                unmapped_script_samples.setdefault(file_id, set()).add(path.relative_to(EXPORTED).as_posix())
                return match[0]
            return 'm_Script: {fileID: 11500000, guid: '+script_guids[file_id]+', type: 3}'
        text = SCRIPT.sub(script, text)
        return native_transform(text, path) if native_transform else text

    graph_paths = {key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{name}.prefab' for key, name in ROOTS.items()}
    graph_paths.update(graph_roots or {})
    selected = [EXPORTED/path for path in graph_paths.values()]
    found, pending = set(), list(selected)
    while pending:
        path = pending.pop()
        if path in found: continue
        found.add(path)
        raw = read(path); read(Path(str(path)+'.meta'))
        if path.suffix in ('.png', '.bytes'): continue
        try:
            text = adapt(raw.decode('utf-8-sig'), path)
        except UnicodeDecodeError as error:
            raise ValueError(('Non-text asset in skill graph closure', path, error.start)) from error
        original_script_guids = {match[2] for match in SCRIPT.finditer(text)}
        pending += [resolve(guid) for guid in set(GUID.findall(text))-BUILTINS-local_scripts-original_script_guids-set(external_replacements)]
    if unmapped_scripts:
        raise AssertionError(('Unmigrated skill graph scripts (occurrences, sample paths)',
            {file_id: dict(occurrences=count, paths=sorted(unmapped_script_samples[file_id])[:3])
             for file_id, count in sorted(unmapped_scripts.items())}))

    print(f"{family}: dependency closure {len(found)} assets", flush=True)
    apk = Apk.current()
    try:
        assert apk.version == '884_156692'
        # Shared graph roots resolve through verified native metadata, so they
        # do not need the full address manifest alongside the exporter lookup.
        manifest = (read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
                    if any(key not in (graph_roots or {}) for key in graph_paths) else ())
        originals = []
        for root_index, (key, path) in enumerate(graph_paths.items(), 1):
            if root_index == 1 or root_index % 128 == 0:
                print(f"{family}: verifying root {root_index}/{len(graph_paths)} {path}", flush=True)
            if key in (graph_roots or {}):
                # Shared dependency prefabs have no independent address entry.
                # The verified native exporter preserves their original bundle.
                source_meta = Path(str(EXPORTED/path)+'.meta')
                original_binding = (graph_originals or {}).get(key)
                bundle = (original_binding['source']['bundle'] if original_binding else
                    re.search(r'(?m)^  assetBundleName: (.+)$', read(source_meta).decode())[1]+'.bundle')
                read(source_meta)
                assert bundle in apk.index, bundle
                object_type = 'GameObject' if Path(path).suffix == '.prefab' else 'MonoBehaviour'
                candidates = [obj for obj in apk.objects(bundle) if obj.type.name == object_type
                              and obj.read(check_read=False).m_Name == Path(path).stem]
                assert len(candidates) == 1, (path, len(candidates))
                go = candidates[0]
                if original_binding:
                    assert sha(go.get_raw_data()) == original_binding['object_sha256'], path
                originals.append(dict(id=key, shared_dependency_bundle=bundle,
                    native_export_meta=identity(source_meta), original_game_object_name=go.read(check_read=False).m_Name,
                    original_game_object_sha256=sha(go.get_raw_data()), source=apk.provenance(go)))
            else:
                source_path = f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab'
                asset = next(row for row in manifest if row['path'].endswith('/'+source_path))
                go = apk.asset(asset['bundle'], '/'+Path(source_path).name)
                originals.append(dict(id=key, asset=asset, source=apk.provenance(go)))
            for obj in go.assets_file.objects.values():
                if obj.type.name != 'ParticleSystemRenderer': continue
                for ptr in obj.read().m_Materials:
                    if not ptr.m_PathID: continue
                    material = ptr.deref().read(); material.m_Shader.deref()
                    for _, env in material.m_SavedProperties.m_TexEnvs:
                        if env.m_Texture.m_PathID: env.m_Texture.deref()
        # A selected child graph can reference an asset that the numeric root
        # does not load. Resolve the verified exporter metadata's exact source
        # bundle before looking up its original texture/shader object.
        print(f"{family}: verified {len(originals)} roots", flush=True)
        source_bundles = set()
        source_paths = [path for path in sorted(found)
                        if path.suffix == '.png' or '/Shaders/' in path.as_posix()]
        for source_index, path in enumerate(source_paths, 1):
            if source_index == 1 or source_index % 256 == 0:
                print(f"{family}: loading original textures/shaders {source_index}/{len(source_paths)}", flush=True)
            source_meta = Path(str(path)+'.meta')
            bundle_name = re.search(r'(?m)^  assetBundleName: ([^\s\r\n]+)[ \t]*$', read(source_meta).decode())
            if bundle_name is None:
                # Built-in/exported shader metadata may omit a bundle. Its
                # real original object must still resolve uniquely below.
                continue
            bundle = bundle_name[1]+'.bundle'
            assert bundle in apk.index, (path,bundle)
            source_bundles.add(bundle)
            for _ in apk.objects(bundle):
                pass
        native = {}
        for bundle_name in sorted(source_bundles):
            bundle = apk.load(bundle_name)
            for file in serialized_files(bundle):
                for obj in file.objects.values():
                    if obj.type.name not in ('Shader', 'Texture2D'): continue
                    value = obj.read()
                    name = value.m_ParsedForm.m_Name if obj.type.name == 'Shader' else value.m_Name
                    native.setdefault((obj.type.name, name), {})[sha(obj.get_raw_data())] = (obj, value)
        texture_receipt = CACHE/'all-assets/windows-textures/source-manifest.json'
        texture_proof = json.loads(texture_receipt.read_bytes())
        assert texture_proof['manifest'] == apk.version and texture_proof['completed']
        texture_records = {row['source']['object_sha256']: row for row in texture_proof['textures']}
        texture_records_by_name = {}
        texture_records_by_base = {}
        for object_sha, record in texture_records.items():
            source_name = record['source'].get('name')
            if not source_name:
                continue
            texture_records_by_name.setdefault(source_name, []).append(object_sha)
            texture_records_by_base.setdefault(re.sub(r'_\d+$', '', source_name), []).append(object_sha)
        @lru_cache(maxsize=4)
        def converted_pixels(object_sha):
            record = texture_records[object_sha]
            if record['native_format'] not in (3, 4): return None
            converted = CACHE/'all-assets/windows-textures'/next(
                row['path'] for row in record['outputs'] if row['path'].endswith('.asset'))
            raw = converted.read_bytes()
            pixels = bytes.fromhex(re.search(rb'(?m)^  _typelessdata: ([a-fA-F0-9]+)', raw)[1].decode())
            width, height = record['source']['width'], record['source']['height']
            stride = 3 if record['native_format'] == 3 else 4
            return Image.frombytes('RGB' if stride == 3 else 'RGBA', (width, height),
                                   pixels[:width*height*stride]).convert('RGBA')
        @lru_cache(maxsize=1)
        def exported_pixels(path):
            with Image.open(path) as image:
                return image.convert('RGBA')
        def texture_candidates(name, related=False):
            candidates = dict(native.get(('Texture2D', name), {}))
            base = re.sub(r'_\d+$', '', name)
            object_shas = list(texture_records_by_name.get(name, ()))
            object_shas.extend(object_sha for object_sha in texture_records_by_name.get(base, ())
                               if object_sha not in object_shas)
            if related:
                object_shas.extend(object_sha for object_sha in texture_records_by_base.get(base, ())
                                   if object_sha not in object_shas
                                   and texture_records[object_sha]['source'].get('name', '').startswith(base+'_'))
            wanted_shas = set(object_shas)
            seen_bundles = set()
            for object_sha in object_shas:
                record = texture_records[object_sha]
                source = record['source']
                bundle_name = Path(source['input']).name
                if bundle_name in seen_bundles: continue
                seen_bundles.add(bundle_name)
                try:
                    objects = apk.objects(bundle_name)
                except KeyError:
                    continue
                for obj in objects:
                    if obj.type.name != 'Texture2D': continue
                    object_sha = sha(obj.get_raw_data())
                    if object_sha not in wanted_shas: continue
                    value = obj.read()
                    if value.m_Name in (name, base) or (related and value.m_Name.startswith(base+'_')):
                        candidates[object_sha] = (obj, value)
            return candidates
        shader_cache = CACHE/'all-assets/windows-shaders'
        shader_records = {}
        for path in (shader_cache/'Records').glob('*.json'):
            row = json.loads(path.read_bytes())
            shader_records[row['source']['object_sha256']] = (path, row)
        def export_png_delta(path, object_sha):
            record = texture_records[object_sha]
            exported = exported_pixels(path)
            if (record['source']['width'], record['source']['height']) != exported.size: return 256
            image = converted_pixels(object_sha)
            if image is None: return 256
            if image.size != exported.size: return 256
            differences = [max(hi for lo, hi in ImageChops.difference(exported, sample).getextrema())
                           for sample in (image, image.transpose(Image.Transpose.FLIP_TOP_BOTTOM))]
            return min(differences)
        targets, replacements = {}, dict(external_replacements)
        for path in sorted(found):
            target = DEST/path.relative_to(EXPORTED)
            if path.suffix == '.png': target = target.with_name(target.stem+'.texture.asset')
            elif '/Shaders/' in path.as_posix():
                target = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/Shaders'/(shader_prefix+path.stem+'.shader')
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.is_relative_to(DEST):
                sidecar = Path(str(target)+'.meta')
                if sidecar.exists():
                    expected = uuid.uuid5(uuid.NAMESPACE_URL,
                        'zjcs/profession/character-resource/' + target.relative_to(ROOT).as_posix()).hex
                    if GUID.search(sidecar.read_text(encoding='utf-8-sig'))[1] != expected:
                        sidecar.unlink()
            meta(target)
            old = GUID.search(read(Path(str(path)+'.meta')).decode('utf-8-sig'))[1]
            new = GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
            targets[path] = target; replacements[old] = new
        texture_guids = {replacements[GUID.search(read(Path(str(path)+'.meta')).decode())[1]]
                         for path in found if path.suffix == '.png'}
        shader_guids = {GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
                        for target in targets.values() if target.suffix == '.shader'}
        textures, shaders, binary_assets, converted_inputs = [], [], [], []
        print(f"{family}: converting {len(targets)} assets", flush=True)
        for ordinal, (path, target) in enumerate(targets.items(), 1):
            if ordinal % 500 == 0:
                print(f"{family}: {ordinal}/{len(targets)} {path.name}", flush=True)
            raw = read(path)
            if target.suffix == '.shader':
                name = re.search(r'(?m)^  m_Name: (.+)$', raw.decode('utf-8-sig'))[1]
                candidates = native['Shader', name]
                source_bundle = re.search(r'(?m)^  assetBundleName: ([^\s\r\n]+)[ \t]*$',
                    read(Path(str(path)+'.meta')).decode('utf-8-sig'))
                if source_bundle is not None and len(candidates) > 1:
                    bundle_path = '/'+source_bundle[1]+'.bundle'
                    candidates = {object_sha: candidate for object_sha, candidate in candidates.items()
                        if shader_records[object_sha][1]['source']['input'].endswith(bundle_path)}
                assert len(candidates) == 1, (path, len(candidates))
                object_sha, (obj, _) = next(iter(candidates.items()))
                record_path, record = shader_records[object_sha]
                assert not record['stripped_passes']
                converted = shader_cache/record['path']
                assert sha(converted.read_bytes()) == record['sha256']
                text = converted.read_text(encoding='utf-8-sig')
                raw = text.replace('Shader "'+name+'"', 'Shader "Zjcs/Official/Combat/'+family+'/'+path.stem+'"', 1).encode()
                shaders.append(dict(source=apk.provenance(obj), object_sha256=object_sha,
                                    conversion=identity(record_path), converted=identity(converted)))
            elif path.suffix == '.png':
                name = path.stem
                candidates = texture_candidates(name)
                source_meta = Path(str(path)+'.meta')
                bundle_name = re.search(r'(?m)^  assetBundleName: ([^\s\r\n]+)[ \t]*$',
                                        read(source_meta).decode('utf-8-sig'))
                source_bundle = bundle_name[1] + '.bundle' if bundle_name is not None else None
                if bundle_name is not None and len(candidates) > 1:
                    candidates = {object_sha: value for object_sha, value in candidates.items()
                        if texture_records[object_sha]['source']['input'].endswith('/'+source_bundle)}
                if not candidates:
                    candidates = texture_candidates(name)
                if len(candidates) > 1:
                    deltas = {object_sha: export_png_delta(path, object_sha)
                              for object_sha in candidates}
                    best = min(deltas.values())
                    candidates = {object_sha: candidates[object_sha] for object_sha, delta in deltas.items()
                                  if delta == best and delta <= 1}
                exact_records = texture_records_by_name.get(path.stem, [])
                exact_matches = [object_sha for object_sha in exact_records
                                 if object_sha in candidates and export_png_delta(path, object_sha) <= 1]
                if len(exact_matches) == 1:
                    object_sha = exact_matches[0]
                    candidates = {object_sha: candidates[object_sha]}
                if not candidates or (len(candidates) == 1
                                      and export_png_delta(path, next(iter(candidates))) > 1):
                    recovered = texture_candidates(name, related=True)
                    if bundle_name is not None:
                        scoped = {object_sha: value for object_sha, value in recovered.items()
                            if texture_records[object_sha]['source']['input'].endswith('/'+source_bundle)}
                        if scoped: recovered = scoped
                    deltas = {object_sha: export_png_delta(path, object_sha) for object_sha in recovered}
                    best = min(deltas.values(), default=256)
                    candidates = {object_sha: recovered[object_sha] for object_sha, delta in deltas.items()
                                  if delta == best and delta <= 1}
                assert len(candidates) == 1, (path, len(candidates))
                object_sha, (obj, value) = next(iter(candidates.items()))
                record = texture_records[object_sha]
                image_data = bytes(value.get_image_data())
                for mip in record['mips']:
                    if 'source_offset' in mip:
                        start, size, expected_mip = mip['source_offset'], mip['source_size'], mip['source_sha256']
                    else:
                        start = mip['source_face_offset'] + mip['astc_offset']
                        size, expected_mip = mip['astc_size'], mip['astc_sha256']
                    assert sha(image_data[start:start+size]) == expected_mip
                converted = CACHE/'all-assets/windows-textures'/next(row['path'] for row in record['outputs'] if row['path'].endswith('.asset'))
                expected = next(row['sha256'] for row in record['outputs'] if row['path'].endswith('.asset'))
                raw = converted.read_bytes(); assert sha(raw) == expected
                rgba = bytes.fromhex(re.search(rb'(?m)^  _typelessdata: ([a-fA-F0-9]+)', raw)[1].decode())
                assert sha(rgba) == record['pixels_sha256']
                assert record['native_format'] in (3, 4), record['native_format']
                mode, stride = ('RGB', 3) if record['native_format'] == 3 else ('RGBA', 4)
                first = Image.frombytes(mode, (value.m_Width, value.m_Height),
                                        rgba[:value.m_Width*value.m_Height*stride]).convert('RGBA')
                png = exported_pixels(path); assert first.size == png.size
                comparison = [max(hi for lo, hi in ImageChops.difference(png, image).getextrema())
                              for image in (first, first.transpose(Image.Transpose.FLIP_TOP_BOTTOM))]
                assert min(comparison) <= 1, (path, comparison)
                converted_inputs.append(identity(converted))
                textures.append(dict(source=apk.provenance(obj), object_sha256=object_sha,
                    compressed_sha256=sha(image_data), native_format=record['native_format'],
                    pixel_bytes=record['pixel_bytes'], pixels_sha256=record['pixels_sha256'],
                    mips=record['mips'], exported_png_channel_max_delta=min(comparison), converted=identity(converted)))
            elif path.suffix == '.bytes':
                binary_assets.append(dict(source=identity(path), source_sha256=sha(raw)))
            else:
                text = adapt(raw.decode('utf-8-sig'), path)
                text = GUID.sub(lambda m: 'guid: '+(m[1] if m[1] in BUILTINS|local_scripts else replacements[m[1]]), text)
                text = re.sub(r'(fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3',
                    lambda m: m[1]+m[2]+m[3]+('2' if m[2] in texture_guids else '3'), text)
                text = re.sub(r'(fileID: 4800000, guid: )([a-f0-9]{32})(, type: )2',
                    lambda m: m[1]+m[2]+m[3]+('3' if m[2] in shader_guids else '2'), text)
                raw = text.encode()
            if target.suffix == '.bytes':
                if not target.exists() or target.read_bytes() != raw:
                    target.write_bytes(raw)
            else:
                write_changed(target, raw.decode('utf-8'))
            # Concurrent asset conversion can leave an existing output without
            # its sidecar; restore the Unity GUID before recording the receipt.
            sidecar = Path(str(target)+'.meta')
            if not sidecar.exists():
                meta(target)
            guid = GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
            importer = ('ShaderImporter' if target.suffix == '.shader' else
                'PrefabImporter' if target.suffix == '.prefab' else
                'TextScriptImporter' if target.suffix == '.bytes' else 'NativeFormatImporter')
            write_changed(Path(str(target)+'.meta'), 'fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n')
        rows = [dict(id=key, resource='Combat/'+family+'/'+str(Path(path).with_suffix('')).replace('\\', '/'))
                for key, path in graph_paths.items()]
        DATA.parent.mkdir(parents=True, exist_ok=True)
        write_changed(DATA, json.dumps(dict(manifest=apk.version, graphs=rows), indent=2)+'\n'); meta(DATA)
        outputs = [DATA, Path(str(DATA)+'.meta')]
        for target in targets.values():
            outputs += [target, Path(str(target)+'.meta')]
            parent = target.parent
            assets_root = ROOT/'unity/Assets'
            while parent != assets_root and parent.is_relative_to(assets_root):
                if not Path(str(parent)+'.meta').exists(): meta(parent, folder=True)
                if parent.is_relative_to(DEST.parents[2]): outputs.append(Path(str(parent)+'.meta'))
                parent = parent.parent
        write_changed(RECEIPT, json.dumps(dict(manifest=apk.version, originals=originals,
            native_export_receipt=identity(export_receipt), native_inputs=[inputs[key] for key in sorted(inputs)],
            texture_conversion_receipt=identity(texture_receipt), converted_inputs=converted_inputs,
            textures=textures, shaders=shaders, binary_assets=binary_assets, application=script_records,
            external_assets=external_records, native_scripts=native_script_records,
            runtime_receipts=[identity(ROOT/'assets/reference/battle-animation'/name) for name in (
                runtime_receipts or ('status-orbit-runtime-source-manifest.json', 'status-view-runtime-source-manifest.json'))],
            generator=identity(generator or Path(__file__)), graph_asset_adapter=identity(Path(__file__)),
            outputs=[identity(path) for path in sorted(set(outputs))],
            operation=operation or 'Full original graphs: no components, sequence leaves or local objects removed. Preserve native local fileIDs, transforms, particles, orbit configuration and dependencies. Remap migrated application scripts/GUIDs, decode all original texture mips through hash-matched conversion cache and use verified full Windows shaders.',
            limits=limits or ['These three graph resources do not install all status lifecycle roles or formal battle actor/timeline binding.']),
            ensure_ascii=False, indent=2)+'\n')
        print('Original orbit graphs:', len(rows), 'assets:', len(targets), 'textures:', len(textures), 'shaders:', len(shaders))
    finally:
        apk.archive.close()


if __name__ == '__main__':
    build()
