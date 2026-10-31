"""Restore original zero-sized TMP atlas objects skipped by bitmap export."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

from official_apk import ROOT, UnityPy
from official_source import OfficialSource
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from prepare_unity_map13_native_scene import native_texture


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def restore(cache, export_name='native-export'):
    cache = cache.resolve()
    if not re.fullmatch(r'native-export(?:-[a-z0-9]+)?', export_name):
        raise ValueError('Use a native-export cache name')
    exported = cache/export_name/'ExportedProject/Assets'
    receipt_path = cache/(export_name+'-source-manifest.json')
    receipt = json.loads(receipt_path.read_bytes())
    allowed = (ROOT/'.tools/source-packages/current'/OfficialSource().version/'native-conversion').resolve()
    if cache != allowed or not receipt.get('completed'):
        raise ValueError('Restore only a completed current native conversion cache')
    source = cache/'inputs/DefaultPackage/defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle'
    env = UnityPy.load(str(source))
    textures = [o for o in env.objects if o.type.name == 'Texture2D' and
                o.read().m_Width == 0 and o.read().m_Height == 0]
    reference = 'unity/Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic-Atlas.texture.asset'
    template = subprocess.check_output(['git', '-c', 'safe.directory='+str(ROOT), 'show', 'HEAD:'+reference], cwd=ROOT).decode('utf8')
    template = template.replace('--- !u!28 &2800000', '--- !u!28 &1').replace('  m_TextureFormat: 1\n', '  m_TextureFormat: 4\n')
    texts = {p: p.read_text('utf8') for p in exported.rglob('*.asset')}
    schemas = ScriptSchemas.__new__(ScriptSchemas)
    schemas.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    schemas.generator.load_local_dll_folder(str(cache/'changed-inputs/Managed'))
    schemas.cache, schemas.records = {}, {}
    font_tree = schemas.tree('Unity.TextMeshPro.dll', 'TMPro.TMP_FontAsset')
    fonts_by_atlas = {}
    for original in env.objects:
        if original.type.name != 'MonoBehaviour':
            continue
        base = original.read(check_read=False)
        if not any('m_AtlasTextures:' in text and re.search(r'(?m)^  m_Name: '+re.escape(base.m_Name)+r'\s*$', text) for text in texts.values()):
            continue
        fields = original.read_typetree(nodes=font_tree)
        for pointer in fields['m_AtlasTextures']:
            if pointer['m_FileID'] != 0:
                raise ValueError('Expected original font atlas in the same serialized file')
            fonts_by_atlas.setdefault(pointer['m_PathID'], []).append(fields['m_Name'])
    results = []
    outputs = {r['path']: r for r in receipt['outputs']}
    removed = set()
    def remember(path):
        relative = path.relative_to(cache/export_name).as_posix()
        outputs[relative] = dict(path=relative, sha256=sha(path.read_bytes()), bytes=path.stat().st_size)
    for obj in textures:
        texture = obj.read()
        if texture.m_TextureFormat != 1 or texture.image_data or texture.m_StreamData.size or texture.m_StreamData.path:
            raise ValueError('Expected an original empty Alpha8 atlas: ' + texture.m_Name)
        names = fonts_by_atlas.get(obj.path_id, [])
        if len(names) != 1:
            raise ValueError('Original empty atlas has no unique font pointer: '+texture.m_Name)
        name = names[0]
        matches = [(path, text) for path, text in texts.items() if
                   'm_AtlasTextures:' in text and re.search(r'(?m)^  m_Name: '+re.escape(name)+r'\s*$', text)]
        if len(matches) != 1:
            raise ValueError('Original empty atlas font is absent or ambiguous: ' + name)
        font_path, text = matches[0]
        pointer = re.search(r'm_AtlasTextures:\s*\n\s*- \{fileID: 2800000, guid: ([a-f0-9]{32}), type: [23]\}', text)
        if not pointer:
            raise ValueError('Original font atlas reference differs: ' + str(font_path))
        guid = pointer[1]
        orphaned = []
        candidates = [cache/export_name/r['path'] for r in receipt['outputs'] if Path(r['path']).name == texture.m_Name+'.png.meta']
        for metadata in candidates:
            if not metadata.exists():
                continue
            if re.search(r'(?m)^guid: '+guid+'$', metadata.read_text('utf8')):
                if Path(str(metadata).removesuffix('.meta')).exists():
                    raise ValueError('An original empty atlas unexpectedly has exported pixels')
                orphaned.append(metadata)
        if len(orphaned) > 1:
            raise ValueError('Original empty atlas GUID is ambiguous')
        payload = native_texture(template, texture, b'').decode().replace('  m_TextureFormat: 4\n', '  m_TextureFormat: 1\n')
        path = exported/'Texture2D'/(texture.m_Name+'.texture.asset')
        path.parent.mkdir(exist_ok=True)
        if path.exists() and path.read_text('utf8') != payload:
            raise ValueError('Previously restored native atlas bytes differ: ' + str(path))
        if not path.exists():
            path.write_text(payload, 'utf8')
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8')
        remember(path)
        remember(Path(str(path)+'.meta'))
        for metadata in orphaned:
            metadata.unlink()  # Replace this failed bitmap's metadata, retaining its GUID.
            removed.add(metadata.relative_to(cache/export_name).as_posix())
        results.append(dict(source_bundle=source.name, path_id=str(obj.path_id), raw_sha256=sha(obj.get_raw_data()),
                            name=texture.m_Name, width=0, height=0, format='Alpha8', guid=guid,
                            path=path.relative_to(exported).as_posix(), sha256=sha(path.read_bytes()), references=[]))
    for relative, previous in list(outputs.items()):
        candidate = cache/export_name/relative
        if candidate.suffix not in ('.asset', '.mat', '.prefab', '.unity'):
            continue
        raw = texts[candidate].encode('utf8') if candidate in texts else candidate.read_bytes()
        updated = raw
        touched = []
        for entry in results:
            prefix = b'guid: '+entry['guid'].encode()+b', type: '
            if prefix+b'3' in updated or prefix+b'2' in updated:
                updated = updated.replace(prefix+b'3', prefix+b'2')
                touched.append(entry)
        if not touched:
            continue
        # Resume only our exact original-GUID type correction, never an
        # unrelated modified exported file.
        original = updated
        for entry in touched:
            prefix = b'guid: '+entry['guid'].encode()+b', type: '
            original = original.replace(prefix+b'2', prefix+b'3')
        if sha(original) != previous['sha256']:
            raise ValueError('Exported atlas reference differs from its source receipt: '+relative)
        if updated != raw:
            candidate.write_bytes(updated)
        outputs[relative] = dict(path=relative, sha256=sha(updated), bytes=len(updated))
        for entry in touched:
            entry['references'].append(dict(path=candidate.relative_to(exported).as_posix(), previous_sha256=previous['sha256'], sha256=sha(updated)))
    result = dict(manifest=receipt['manifest'], source_sha256=sha(source.read_bytes()),
                  generator_sha256=sha(Path(__file__).read_bytes()), template_sha256=sha(template.encode()),
                  operation='Restore original empty Alpha8 Texture2D objects and their existing exported GUID references. No replacement pixels.', outputs=results)
    (cache/'empty-atlas-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    receipt['empty_atlas_restoration_sha256'] = sha((cache/'empty-atlas-source-manifest.json').read_bytes())
    receipt['outputs'] = [outputs[key] for key in sorted(outputs) if key not in removed]
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Restored original empty native atlases:', len(results))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--export-name', default='native-export')
    args = parser.parse_args()
    restore(args.cache, args.export_name)
