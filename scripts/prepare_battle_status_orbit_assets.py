"""Restore complete original orbit graphs and their native Windows dependencies."""
import json
import os
import re
import struct
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


def build():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == (ROOT/'output/validation').resolve() and (work/'.zjcs-validation.json').is_file()
    export_receipt = CACHE/'native-export-all-source-manifest.json'
    proof = json.loads(export_receipt.read_bytes())
    assert proof['manifest'] == '884_156692'
    assert proof['code_manifest_sha256'] == sha((CACHE.parent/'code-manifest.json').read_bytes())
    verified = {r['path']: r['sha256'] for r in proof['outputs']}
    inputs, index = {}, {}

    def read(path):
        raw = path.read_bytes()
        assert sha(raw) == verified[path.relative_to(CACHE/'native-export-all').as_posix()], path
        inputs[path.as_posix()] = identity(path)
        return raw

    index_path = work/'status-orbit-guid-index.json'
    if index_path.exists(): index = json.loads(index_path.read_bytes())

    def resolve(guid):
        if guid not in index:
            for category in ('Effect', 'Shaders', 'Material', 'Materials', 'Mesh', 'Meshes',
                             'Texture2D', 'Textures', 'Shader', 'effect_shader', 'Light', 'Prefabs'):
                flag = '@' + category
                if flag in index: continue
                for path in (EXPORTED/category).rglob('*.meta'):
                    match = GUID.search(path.read_text(encoding='utf-8-sig'))
                    if match: index[match[1]] = path.relative_to(EXPORTED).as_posix()[:-5]
                index[flag] = True
                index_path.write_bytes(json.dumps(index).encode())
                if guid in index: break
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
    local_scripts = set(script_guids.values())

    def adapt(text):
        def script(match):
            file_id = int(match[1])
            assert file_id in script_guids, ('Unmigrated orbit node', file_id)
            return 'm_Script: {fileID: 11500000, guid: '+script_guids[file_id]+', type: 3}'
        return SCRIPT.sub(script, text)

    selected = [EXPORTED/f'Prefabs/EcEntity/Fight/FightSkill/{key}/{name}.prefab' for key, name in ROOTS.items()]
    found, pending = set(), list(selected)
    while pending:
        path = pending.pop()
        if path in found: continue
        found.add(path)
        raw = read(path); read(Path(str(path)+'.meta'))
        if path.suffix == '.png': continue
        text = adapt(raw.decode('utf-8-sig'))
        pending += [resolve(guid) for guid in set(GUID.findall(text))-BUILTINS-local_scripts]

    apk = Apk.current()
    try:
        assert apk.version == '884_156692'
        manifest = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        originals = []
        for key in ROOTS:
            asset = next(row for row in manifest if row['path'].endswith(f'/FightSkill/{key}/{key}.prefab'))
            go = apk.asset(asset['bundle'], '/'+str(key)+'.prefab')
            originals.append(dict(id=key, asset=asset, source=apk.provenance(go)))
            for obj in go.assets_file.objects.values():
                if obj.type.name != 'ParticleSystemRenderer': continue
                for ptr in obj.read().m_Materials:
                    if not ptr.m_PathID: continue
                    material = ptr.deref().read(); material.m_Shader.deref()
                    for _, env in material.m_SavedProperties.m_TexEnvs:
                        if env.m_Texture.m_PathID: env.m_Texture.deref()
        native = {}
        for bundle in list(apk.loaded.values()):
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
        shader_cache = CACHE/'all-assets/windows-shaders'
        shader_records = {}
        for path in (shader_cache/'Records').glob('*.json'):
            row = json.loads(path.read_bytes())
            shader_records[row['source']['object_sha256']] = (path, row)
        targets, replacements = {}, {}
        for path in sorted(found):
            target = DEST/path.relative_to(EXPORTED)
            if path.suffix == '.png': target = target.with_name(target.stem+'.texture.asset')
            elif '/Shaders/' in path.as_posix():
                target = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/Shaders'/('StatusOrbit_'+path.stem+'.shader')
            target.parent.mkdir(parents=True, exist_ok=True); meta(target)
            old = GUID.search(read(Path(str(path)+'.meta')).decode('utf-8-sig'))[1]
            new = GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
            targets[path] = target; replacements[old] = new
        texture_guids = {replacements[GUID.search(read(Path(str(path)+'.meta')).decode())[1]]
                         for path in found if path.suffix == '.png'}
        shader_guids = {GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
                        for target in targets.values() if target.suffix == '.shader'}
        textures, shaders, converted_inputs = [], [], []
        for path, target in targets.items():
            raw = read(path)
            if target.suffix == '.shader':
                name = re.search(r'(?m)^  m_Name: (.+)$', raw.decode('utf-8-sig'))[1]
                candidates = native['Shader', name]; assert len(candidates) == 1, name
                object_sha, (obj, _) = next(iter(candidates.items()))
                record_path, record = shader_records[object_sha]
                assert not record['stripped_passes']
                converted = shader_cache/record['path']
                assert sha(converted.read_bytes()) == record['sha256']
                text = converted.read_text(encoding='utf-8-sig')
                raw = text.replace('Shader "'+name+'"', 'Shader "Zjcs/Official/Combat/StatusOrbits/'+path.stem+'"', 1).encode()
                shaders.append(dict(source=apk.provenance(obj), object_sha256=object_sha,
                                    conversion=identity(record_path), converted=identity(converted)))
            elif path.suffix == '.png':
                name = path.stem
                if ('Texture2D', name) not in native: name = re.sub(r'_\d+$', '', name)
                candidates = native['Texture2D', name]; assert len(candidates) == 1, (path, len(candidates))
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
                png = Image.open(path).convert('RGBA'); assert first.size == png.size
                comparison = [max(hi for lo, hi in ImageChops.difference(png, image).getextrema())
                              for image in (first, first.transpose(Image.Transpose.FLIP_TOP_BOTTOM))]
                assert min(comparison) <= 1, (path, comparison)
                converted_inputs.append(identity(converted))
                textures.append(dict(source=apk.provenance(obj), object_sha256=object_sha,
                    compressed_sha256=sha(image_data), native_format=record['native_format'],
                    pixel_bytes=record['pixel_bytes'], pixels_sha256=record['pixels_sha256'],
                    mips=record['mips'], exported_png_channel_max_delta=min(comparison), converted=identity(converted)))
            else:
                text = adapt(raw.decode('utf-8-sig'))
                text = GUID.sub(lambda m: 'guid: '+(m[1] if m[1] in BUILTINS|local_scripts else replacements[m[1]]), text)
                text = re.sub(r'(fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3',
                    lambda m: m[1]+m[2]+m[3]+('2' if m[2] in texture_guids else '3'), text)
                text = re.sub(r'(fileID: 4800000, guid: )([a-f0-9]{32})(, type: )2',
                    lambda m: m[1]+m[2]+m[3]+('3' if m[2] in shader_guids else '2'), text)
                raw = text.encode()
            write_changed(target, raw.decode('utf-8'))
            guid = GUID.search(Path(str(target)+'.meta').read_text(encoding='utf-8-sig'))[1]
            importer = 'ShaderImporter' if target.suffix == '.shader' else 'PrefabImporter' if target.suffix == '.prefab' else 'NativeFormatImporter'
            write_changed(Path(str(target)+'.meta'), 'fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n')
        rows = [dict(id=key, resource='Combat/StatusOrbits/Prefabs/EcEntity/Fight/FightSkill/'+str(key)+'/'+name)
                for key, name in ROOTS.items()]
        DATA.parent.mkdir(parents=True, exist_ok=True)
        write_changed(DATA, json.dumps(dict(manifest=apk.version, graphs=rows), indent=2)+'\n'); meta(DATA)
        outputs = [DATA, Path(str(DATA)+'.meta')]
        for target in targets.values():
            outputs += [target, Path(str(target)+'.meta')]
            parent = target.parent
            while parent.name != 'Assets':
                if not Path(str(parent)+'.meta').exists(): meta(parent, folder=True)
                if parent.is_relative_to(DEST.parents[2]): outputs.append(Path(str(parent)+'.meta'))
                parent = parent.parent
        write_changed(RECEIPT, json.dumps(dict(manifest=apk.version, originals=originals,
            native_export_receipt=identity(export_receipt), native_inputs=list(inputs.values()),
            texture_conversion_receipt=identity(texture_receipt), converted_inputs=converted_inputs,
            textures=textures, shaders=shaders, application=script_records,
            runtime_receipts=[identity(ROOT/'assets/reference/battle-animation'/name) for name in (
                'status-orbit-runtime-source-manifest.json', 'status-view-runtime-source-manifest.json')],
            generator=identity(Path(__file__)), outputs=[identity(path) for path in sorted(set(outputs))],
            operation='Full original graphs: no components, sequence leaves or local objects removed. Preserve native local fileIDs, transforms, particles, orbit configuration and dependencies. Remap migrated application scripts/GUIDs, decode all original texture mips through hash-matched conversion cache and use verified full Windows shaders.',
            limits=['These three graph resources do not install all status lifecycle roles or formal battle actor/timeline binding.']),
            ensure_ascii=False, indent=2)+'\n')
        print('Original orbit graphs:', len(rows), 'assets:', len(targets), 'textures:', len(textures), 'shaders:', len(shaders))
    finally:
        apk.archive.close()


if __name__ == '__main__':
    build()
