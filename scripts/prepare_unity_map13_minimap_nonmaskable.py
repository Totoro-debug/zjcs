"""Port the original non-maskable minimap layer and its complete native prefab."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_minimap_inputs import ROOT_BUNDLE
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_map13_prop_parts import meta as code_meta
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_water_entity_export import native_io_path

CODE = ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapNonMaskable'
DEST = ROOT/'unity/Assets/Official/World/Map/13/NativeMiniMapNonMaskable'
GUID = re.compile(r'guid:\s*([a-f0-9]{32})')
PREFAB = 'Prefabs/Map/Common/MiniMap/MinimapNonMaskable.prefab'


def build(cache, package_cache):
    cache = cache.resolve()
    if not cache.is_relative_to(ROOT/'output/validation'):
        raise ValueError('Owned validation cache required')
    inputs = json.loads((cache/'source-manifest.json').read_bytes())
    proof = json.loads((cache/'export-proof.json').read_bytes())
    if inputs['manifest'] != '871_156598' or inputs['roots'] != [ROOT_BUNDLE] or proof['counts']['registries'] != 12:
        raise ValueError('Verified original minimap export required')
    for entry in inputs['inputs']:
        if sha(native_io_path(cache/'inputs'/entry['output']).read_bytes()) != entry['converter_input_sha256']:
            raise ValueError('Converter input changed')
    source = ROOT/'.tools/source-packages/current/871_156598/official-code/client-source/MiniMapNonMaskableManager.cs'
    text = source.read_text(encoding='utf-8-sig').replace('\r\n', '\n')
    text = text.replace('public class MiniMapNonMaskableManager', 'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.World.NativeLabelUI;\npublic class MiniMapNonMaskableManager')+'\n}\n'
    script = CODE/'MiniMapNonMaskableManager.cs'
    write(script, text.encode()); code_meta(script, 'MonoImporter')
    assembly = CODE/'Zjcs.Official.World.Map13MiniMapNonMaskable.asmdef'
    write(assembly, encode(dict(name='Zjcs.Official.World.Map13MiniMapNonMaskable', references=['Zjcs.Official.World.Map13MiniMapCamera', 'Zjcs.Official.World.NativeLabelUI'], autoReferenced=True)))
    code_meta(assembly, 'AssemblyDefinitionImporter'); code_meta(CODE, folder=True)
    base = cache/'export/ExportedProject/Assets'
    index = {}
    for p in base.rglob('*.meta'):
        match = GUID.search(native_io_path(p).read_text(encoding='utf-8-sig'))
        if match:
            if match[1] in index: raise ValueError('Duplicate export GUID')
            index[match[1]] = Path(str(p)[:-5])
    pending = [base/PREFAB]; found = set()
    while pending:
        p = pending.pop()
        if p in found: continue
        if not native_io_path(p).is_file(): raise FileNotFoundError(p)
        found.add(p)
        if p.suffix == '.cs': continue
        for item in [p, Path(str(p)+'.meta')]:
            if item.suffix not in ['.prefab', '.mat', '.asset', '.meta']: continue
            for guid in GUID.findall(native_io_path(item).read_text(encoding='utf-8-sig')):
                if guid in index: pending.append(index[guid])
                elif guid not in ['0'*32, '0000000000000000e000000000000000', '0000000000000000f000000000000000']:
                    raise ValueError('Unresolved original non-maskable reference '+guid)
    counts = {suffix: sum(p.suffix == suffix for p in found) for suffix in {p.suffix for p in found}}
    if counts != {'.prefab': 1, '.mat': 1, '.asset': 1, '.png': 1, '.cs': 2}: raise ValueError(counts)
    remapping = {}; bindings = []
    for p in found:
        original_guid = GUID.search(native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig'))[1]
        if p.suffix == '.cs':
            if p.stem == 'MiniMapNonMaskableManager':
                guid = GUID.search(Path(str(script)+'.meta').read_text(encoding='utf-8'))[1]
                bindings.append(dict(original=p.relative_to(base).as_posix(), **record(script), guid=guid))
            elif p.stem == 'UniversalAdditionalCameraData':
                candidates = list((ROOT/'unity/Packages').glob('com.unity.render-pipelines.universal*/Runtime/UniversalAdditionalCameraData.cs.meta'))
                if not candidates:
                    candidates = list(package_cache.glob('com.unity.render-pipelines.universal*/Runtime/UniversalAdditionalCameraData.cs.meta'))
                if len(candidates) != 1: raise ValueError('Actual installed URP camera script required')
                guid = GUID.search(candidates[0].read_text(encoding='utf-8-sig'))[1]
                bindings.append(dict(original=p.relative_to(base).as_posix(), **record(candidates[0]), guid=guid))
            else: raise ValueError('Unported script '+p.stem)
        else: guid = hashlib.md5(('zjcs-map13-minimap-nonmaskable:'+p.relative_to(base).as_posix()).encode()).hexdigest()
        remapping[original_guid] = guid
    def remap(text):
        return GUID.sub(lambda m: 'guid: '+remapping.get(m[1], m[1]), text)
    apk = Apk.current(); _, missing = collect(apk, [ROOT_BUNDLE])
    if missing: raise ValueError(missing)
    textures = []
    for bundle in apk.loaded.values():
        for f in serialized_files(bundle):
            for obj in f.objects.values():
                if obj.type.name == 'Texture2D':
                    t = obj.read()
                    if t.m_Name == 'Square' and t.m_Width == 256 and t.m_Height == 256: textures.append((obj, t))
    if len(textures) != 1: raise ValueError('Unique original Square texture required')
    obj, texture = textures[0]; rgba, mips, profile = decode_mips(texture)
    template = ROOT/'unity/Assets/Official/World/Map/13/NativeScene/Textures/Lava.texture.asset'
    texture_text = native_texture(template.read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n', '--- !u!28 &1\n'), texture, rgba)
    exported = []; outputs = [record(script), record(assembly)]
    for p in sorted(found):
        if p.suffix == '.cs': continue
        relative = p.relative_to(base); target = DEST/relative
        raw = native_io_path(p).read_bytes(); metadata = native_io_path(Path(str(p)+'.meta')).read_text(encoding='utf-8-sig').replace('\r\n', '\n')
        exported.append(dict(relative=relative.as_posix(), sha256=sha(raw), meta_sha256=sha(native_io_path(Path(str(p)+'.meta')).read_bytes())))
        if p.suffix == '.png':
            target = target.with_name(target.stem+'.texture.asset'); raw = texture_text
            metadata = 'fileFormatVersion: 2\nguid: '+remapping[GUID.search(metadata)[1]]+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        else:
            text = remap(raw.decode('utf-8-sig').replace('\r\n', '\n'))
            if p.suffix == '.asset':
                text = re.sub(r'(\{fileID: 2800000, guid: [a-f0-9]{32}, type: )3(\})', r'\g<1>2\2', text)
            raw = text.encode()
        write(target, raw); write(Path(str(target)+'.meta'), remap(re.sub(r'(?m)^timeCreated: \d+\n', '', metadata)).encode())
        outputs.append(dict(**record(target), meta_sha256=sha(Path(str(target)+'.meta').read_bytes())))
    for folder in [DEST, *sorted(p for p in DEST.rglob('*') if p.is_dir())]: meta(folder, folder=True)
    receipt = dict(format=1, mapClassId=13, manifest=apk.version, root=source_path(DEST/PREFAB), inputs=[record(source)], bundle_inputs=inputs['inputs'], converter_input_receipt_sha256=sha((cache/'source-manifest.json').read_bytes()), export_proof_sha256=sha((cache/'export-proof.json').read_bytes()), exported_inputs=exported, script_bindings=bindings, texture=dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), compressed_sha256=sha(texture.get_image_data()), rgba_sha256=sha(rgba), mips=mips, profile=profile), outputs=outputs, generator=record(Path(__file__).resolve()), operation='Copy the complete original MiniMapNonMaskableManager; change only namespace/type qualification. Restore every original prefab component, transform, renderer, camera, material and Sprite field. Bind the actual migrated manager and installed URP camera script. Preserve the original built-in Quad mesh, material and shader identities; decode the exact original ASTC Square texture mip data to native RGBA32 without resampling.', limitations=['Formal minimap root/input/save and Windows Player remain required.'])
    write(ROOT/'assets/reference/official-map/map13-minimap-nonmaskable-source-manifest.json', encode(receipt))
    print('Original minimap non-maskable layer restored:', len(outputs), 'outputs', flush=True)
    apk.archive.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--package-cache', type=Path, default=ROOT/'unity/Library/PackageCache')
    args = parser.parse_args(); build(args.cache, args.package_cache.resolve())
