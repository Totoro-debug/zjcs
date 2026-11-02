"""Bind four original movement terrain effects as native Windows particle prefabs.

Reuses verified current-version AssetRipper YAML and GLES-to-HLSL conversion;
decodes every original texture mip without resampling or recompression.
"""
import json
import os
import re
from pathlib import Path
import astc_encoder

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_unity_map13_native_scene import native_texture
from prepare_unity_map14_native_scene import decode_mips
from prepare_unity_bundle_inputs import serialized_files
from hashlib import sha256

CACHE = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
EXPORTED = CACHE / 'native-export-all/ExportedProject/Assets'
DEST = ROOT / 'unity/Assets/Official/Combat/GroundEffects/Resources/Combat/GroundEffects'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/ground-effects.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/ground-effects-assets-source-manifest.json'
GUID = re.compile(r'guid: ([a-f0-9]{32})')
BUILTINS = {'00000000000000000000000000000000', '0000000000000000e000000000000000', '0000000000000000f000000000000000'}


def sha(raw):
    return sha256(raw).hexdigest()


def build():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == (ROOT / 'output/validation').resolve() and (work / '.zjcs-validation.json').is_file()
    # This disposable lookup is not source evidence: every selected file and
    # meta is checked below against the retained converter output receipt.
    index_path = work / 'asset-guid-index.json'
    if not index_path.exists():
        index = {}
        for category in ('Effect', 'Shaders', 'Prefabs/EcEntity/Fight/FightSkill/Move'):
            for p in (EXPORTED / category).rglob('*.meta'):
                found = GUID.search(p.read_text(encoding='utf-8-sig'))
                if found: index[found[1]] = p.relative_to(EXPORTED).as_posix()[:-5]
        index_path.write_bytes(json.dumps(index).encode())
    index = json.loads(index_path.read_bytes())
    export_receipt = CACHE / 'native-export-all-source-manifest.json'
    exported = json.loads(export_receipt.read_bytes())
    assert exported['manifest'] == '884_156692'
    verified = {v['path']: v['sha256'] for v in exported['outputs']}
    inputs, originals, textures, outputs = [], [], {}, []
    fade_script = ROOT / 'unity/Assets/Game/Runtime/Official/StoryFollowBone/MyEffectFadeOut.cs'
    fade_receipt = ROOT / 'assets/reference/official-map/map14-story-follow-bone-runtime-source-manifest.json'
    fade_proof = json.loads(fade_receipt.read_bytes())
    old_fade = next(v for v in fade_proof['inputs'] if v['path'].endswith('/MyEffectFadeOut.cs'))
    assert sha((ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/MyEffectFadeOut.cs').read_bytes()) == old_fade['sha256']
    migrated_fade = next(v for v in fade_proof['outputs'] if v['path'] == fade_script.relative_to(ROOT).as_posix())
    assert sha(fade_script.read_bytes().replace(b'\r\n', b'\n')) == migrated_fade['sha256']
    fade_guid = GUID.search(fade_script.with_name(fade_script.name + '.meta').read_text(encoding='utf-8-sig'))[1]
    fade_pointer = 'm_Script: {fileID: -194148604, guid: 055811b6aa1e2185dc556870e6de0c64, type: 3}'

    def read(p):
        raw = p.read_bytes()
        key = p.relative_to(CACHE / 'native-export-all').as_posix()
        assert sha(raw) == verified[key], 'Converted native asset changed: ' + key
        inputs.append(identity(p))
        return raw

    apk = Apk.current()
    try:
        assert apk.version == '884_156692'
        assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        selected = [row for row in assets if '/FightSkill/Move/Effect/' in row['path']]
        assert len(selected) == 4
        for asset in selected:
            go = apk.asset(asset['bundle'], '/' + Path(asset['path']).name)
            originals.append(dict(asset=asset, source=apk.provenance(go)))
            # Explicitly hydrate renderer -> material -> shader/texture CABs;
            # loading a root bundle alone does not load its dependencies.
            for obj in go.assets_file.objects.values():
                if obj.type.name != 'ParticleSystemRenderer': continue
                for ref in obj.read().m_Materials:
                    if not ref.m_PathID: continue
                    material = ref.deref().read()
                    material.m_Shader.deref()
                    for _, environment in material.m_SavedProperties.m_TexEnvs:
                        if environment.m_Texture.m_PathID: environment.m_Texture.deref()
        native = {}
        for bundle in list(apk.loaded.values()):
            for file in serialized_files(bundle):
                for obj in file.objects.values():
                    if obj.type.name in ('Material', 'Texture2D', 'Shader', 'Mesh'):
                        value = obj.read()
                        name = value.m_ParsedForm.m_Name if obj.type.name == 'Shader' else value.m_Name
                        native.setdefault((obj.type.name, name), []).append((obj, value))
        root_paths = [EXPORTED / Path(row['path']).relative_to('Assets') for row in selected]
        found, pending = set(), list(root_paths)
        while pending:
            p = pending.pop()
            if p in found: continue
            found.add(p)
            raw, metadata = read(p), read(p.with_name(p.name + '.meta'))
            if p.suffix == '.png': continue
            text = raw.decode('utf-8-sig')
            if fade_pointer in text: text = text.replace(fade_pointer, 'm_Script: {fileID: 0}')
            for g in set(GUID.findall(text)):
                if g in BUILTINS: continue
                assert g in index, 'Unresolved original ground effect reference: ' + g
                pending.append(EXPORTED / index[g])
        shader_path = EXPORTED / 'Shaders/Effect/Noise_Mask_Pro_0.asset'
        assert shader_path in found
        shader_candidates = native['Shader', 'Effect/Noise_Mask_Pro']
        assert len(shader_candidates) == 1
        shader_obj, _ = shader_candidates[0]
        converted_root = CACHE / 'all-assets/windows-shaders'
        converted_record = converted_root / 'Records/59de19d261370c02044c882aeda72c54bf496caf9dc73bed0a9d6e9339fd6b95.json'
        shader_record = json.loads(converted_record.read_bytes())
        assert sha(shader_obj.get_raw_data()) == shader_record['source']['object_sha256']
        shader_source = converted_root / shader_record['path']
        assert sha(shader_source.read_bytes()) == shader_record['sha256']
        shader_target = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders/GroundEffectNoiseMaskPro.shader'
        shader_text = shader_source.read_text(encoding='utf-8-sig')
        assert shader_text.startswith('Shader "Effect/Noise_Mask_Pro"')
        shader_target.parent.mkdir(parents=True, exist_ok=True)
        write_changed(shader_target, shader_text.replace('Shader "Effect/Noise_Mask_Pro"', 'Shader "Zjcs/Official/Combat/GroundEffects/NoiseMaskPro"', 1))
        meta(shader_target)
        shader_guid = GUID.search(shader_target.with_name(shader_target.name + '.meta').read_text(encoding='utf-8-sig'))[1]
        shader_target.with_name(shader_target.name + '.meta').write_bytes(('fileFormatVersion: 2\nguid: ' + shader_guid + '\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n').encode())
        destinations, replacements = {}, {}
        for p in sorted(found):
            relative = p.relative_to(EXPORTED)
            target = DEST / relative
            if p == shader_path: target = shader_target
            elif p.suffix == '.png': target = target.with_name(target.stem + '.texture.asset')
            destinations[p] = target
            target.parent.mkdir(parents=True, exist_ok=True)
            meta(target)
            new = GUID.search(target.with_name(target.name + '.meta').read_text(encoding='utf-8-sig'))[1]
            old = GUID.search(read(p.with_name(p.name + '.meta')).decode('utf-8-sig'))[1]
            replacements[old] = new
        texture_guids = {replacements[GUID.search(read(p.with_name(p.name + '.meta')).decode('utf-8-sig'))[1]]
                         for p in found if p.suffix == '.png'}
        # Reuse an already verified native RGBA32 serialization schema. Every
        # source-dependent field and all mip bytes are replaced by native_texture.
        template_receipt = ROOT / 'assets/reference/official-map/map13-native-scene-source-manifest.json'
        template_proof = json.loads(template_receipt.read_bytes())
        template_path = ROOT / 'unity/Assets/Official/World/Map/13/NativeScene/Effect/CustomWater/Textures/default_water_mask.texture.asset'
        template_record = next(v for v in template_proof['outputs'] if v['path'] == template_path.relative_to(ROOT).as_posix())
        assert sha(template_path.read_bytes()) == template_record['sha256']
        template = template_path.read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n', '--- !u!28 &1\n')
        for p, target in destinations.items():
            if p == shader_path: continue
            raw = read(p)
            if p.suffix == '.png':
                candidates = native['Texture2D', p.stem]
                assert len(candidates) == 1, 'Non-unique original ground effect texture: ' + p.stem
                obj, value = candidates[0]
                rgba, mips, profile = decode_mips(value)
                raw = native_texture(template, value, rgba).replace(b': \n', b':\n')
                textures[p.stem] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                    compressed_sha256=sha(value.get_image_data()), rgba_sha256=sha(rgba), profile=profile,
                    mip_count=value.m_MipCount, width=value.m_Width, height=value.m_Height, mips=mips,
                    output=target.relative_to(ROOT).as_posix())
            else:
                assert p.suffix in ('.prefab', '.mat', '.asset')
                text = raw.decode('utf-8-sig')
                if '--- !u!114 ' in text:
                    assert text.count('--- !u!114 ') == 1 and fade_pointer in text, 'Native particle closure contains an unbound MonoBehaviour'
                    text = text.replace(fade_pointer, 'm_Script: {fileID: 11500000, guid: ' + fade_guid + ', type: 3}')
                # Script GUIDs are explicitly bound above, never copied from
                # an Android DLL or converted to a placeholder behaviour.
                text = text.replace('guid: ' + fade_guid, 'groundFadeScript: ' + fade_guid)
                text = GUID.sub(lambda m: 'guid: ' + (m[1] if m[1] in BUILTINS else replacements[m[1]]), text)
                text = text.replace('groundFadeScript: ' + fade_guid, 'guid: ' + fade_guid)
                text = re.sub(r'(fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3',
                    lambda m: m[1] + m[2] + m[3] + ('2' if m[2] in texture_guids else '3'), text)
                text = text.replace('fileID: 4800000, guid: ' + shader_guid + ', type: 2',
                                    'fileID: 4800000, guid: ' + shader_guid + ', type: 3')
                raw = text.encode()
            target.write_bytes(raw)
            # All payloads are native YAML, including exact decoded Texture2D.
            importer = 'PrefabImporter' if target.suffix == '.prefab' else 'NativeFormatImporter'
            old_guid = GUID.search(target.with_name(target.name + '.meta').read_text(encoding='utf-8-sig'))[1]
            target.with_name(target.name + '.meta').write_bytes(('fileFormatVersion: 2\nguid: ' + old_guid + '\n' + importer + ':\n  externalObjects: {}\n').encode())
        entries = [dict(guid=row['guid'], resource='Combat/GroundEffects/' + Path(row['path']).relative_to('Assets').with_suffix('').as_posix()) for row in selected]
        write_changed(DATA, json.dumps(dict(manifest=apk.version, effects=entries), ensure_ascii=False, indent=2) + '\n')
        meta(DATA)
        for parent in [DEST, *sorted(p for p in DEST.rglob('*') if p.is_dir())]:
            while not parent.with_name(parent.name + '.meta').exists():
                meta(parent, folder=True); parent = parent.parent
        paths = [DATA, DATA.with_name(DATA.name + '.meta'), shader_target, shader_target.with_name(shader_target.name + '.meta')]
        paths += [p for target in destinations.values() if target != shader_target for p in (target, target.with_name(target.name + '.meta'))]
        parent = DEST
        while parent != ROOT / 'unity/Assets/Official/Combat':
            paths.append(parent.with_name(parent.name + '.meta')); parent = parent.parent
        paths += [p.with_name(p.name + '.meta') for p in DEST.rglob('*') if p.is_dir()]
        library = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGroundEffectLibrary.cs'
        editor = ROOT / 'unity/Assets/Game/Editor/BattleGroundEffects'
        bound = [library, *[p for p in editor.iterdir() if p.is_file() and p.suffix != '.meta']]
        paths += [p for value in bound for p in (value, value.with_name(value.name + '.meta'))]
        paths += [editor.with_name(editor.name + '.meta'), ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Zjcs.Presentation.Combat.asmdef']
        proof = dict(manifest=apk.version, roots=originals, native_export_receipt=identity(export_receipt),
            native_converter_inputs=identity(CACHE / 'source-manifest.json'),
            native_export_inputs=sorted({v['path']:v for v in inputs}.values(), key=lambda v:v['path']),
            shader=dict(source=apk.provenance(shader_obj), object_sha256=sha(shader_obj.get_raw_data()),
                retained_conversion=identity(converted_record), converted=identity(shader_source),
                programs=shader_record['programs'], stripped_passes=shader_record['stripped_passes']),
            script_binding=dict(original_pointer=fade_pointer, migrated=identity(fade_script), guid=fade_guid,
                current_original=identity(ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/MyEffectFadeOut.cs'),
                reused_receipt=identity(fade_receipt), original_source_byte_identical=True),
            textures=textures, serialization_template=identity(template_path), generators=[identity(p) for p in
                [Path(__file__), ROOT / 'scripts/prepare_unity_map14_native_scene.py', ROOT / 'scripts/prepare_unity_map13_native_scene.py']],
            decoder=dict(package='astc_encoder', binaries=[identity(p) for p in sorted(Path(astc_encoder.__file__).parent.rglob('*.pyd'))]),
            outputs=sorted({p.as_posix():identity(p) for p in paths}.values(), key=lambda v:v['path']),
            operation='Copy four verified original native particle hierarchies/curves/modules/renderers/meshes/materials with reference GUID adaptation only. Decode all original texture mips to native RGBA32 without flip/resampling/recompression. Reuse byte-verified current884 full Windows shader conversion; only shader identity is renamed.',
            limitations=['The 17 movement follow-bone node graphs and complete battle-host binding remain separate work.'])
        write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
        print('Native movement ground effects:', len(entries), 'assets=',len(found), 'textures=',len(textures))
    finally:
        apk.archive.close()


if __name__ == '__main__':
    build()
