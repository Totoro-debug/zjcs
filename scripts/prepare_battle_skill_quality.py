"""Restore original skill-item quality bindings, pixels, trim and fractional atlas UVs."""
from __future__ import annotations

import hashlib
import io
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from prepare_character_creation_opening import SpriteCache


def sha(data):
    return hashlib.sha256(data).hexdigest()


def guid(path):
    return re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(encoding='utf-8'), re.M)[1]


def meta(path):
    p = Path(str(path) + '.meta')
    if not p.exists():
        p.write_bytes(('fileFormatVersion: 2\nguid: ' + sha(path.relative_to(path.parents[4]).as_posix().encode())[:32] + '\n').encode())
    return p


def write_verified(path, raw):
    if path.exists() and path.read_bytes() != raw:
        raise ValueError('Refusing to overwrite a modified quality asset: ' + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(raw)


def build(output_root=ROOT):
    receipt_path = output_root / 'assets/reference/battle-hud/skill-quality-source-manifest.json'
    if receipt_path.exists():
        previous = json.loads(receipt_path.read_bytes())
        for relative, expected in previous['outputs'].items():
            path = output_root / relative
            if not path.is_file() or sha(path.read_bytes()) != expected:
                raise ValueError('Refusing to overwrite modified quality output: ' + relative)
    apk = Apk()
    cache = SpriteCache(apk)
    names = {1: 'White', 2: 'Blue', 3: 'Purple', 4: 'Orange', 5: 'Gold', 6: 'Red'}
    bundle = 'defaultpackage_assets_image_ui_itemquality.bundle'
    native_root = ROOT / 'unity/Assets/Official/Commerce/Art/sprites'
    bindings, inputs, native_outputs = [], [], []
    native_destination = output_root / 'unity/Assets/Official/Combat/SkillQualities/Sprites'
    for folder in (native_destination.parent, native_destination):
        folder.mkdir(parents=True, exist_ok=True)
        folder_meta = Path(str(folder) + '.meta')
        write_verified(folder_meta, ('fileFormatVersion: 2\nguid: ' + sha(folder.relative_to(output_root).as_posix().encode())[:32]
                       + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode())
        native_outputs.append(folder_meta)
    for obj in apk.objects(bundle):
        if obj.type.name != 'Sprite':
            continue
        sprite = obj.read()
        matches = [q for q, name in names.items() if sprite.m_Name == 'ImgQuality_' + name]
        if not matches:
            continue
        cache.record_source(obj, 'Sprite')
        atlas_obj = sprite.m_SpriteAtlas.deref()
        cache.record_source(atlas_obj, 'SpriteAtlas')
        packed = dict(atlas_obj.read().m_RenderDataMap)[sprite.m_RenderDataKey]
        texture_obj = packed.texture.deref()
        cache.record_source(texture_obj, 'Texture2D')
        texture = texture_obj.read()
        if (packed.settingsRaw >> 2) & 15 or packed.alphaTexture.path_id:
            raise ValueError('Required quality Sprite has unsupported atlas rotation or split alpha')
        decoded_image = sprite.image
        png = io.BytesIO()
        decoded_image.save(png, format='PNG', optimize=False)
        digest = sha(png.getvalue())
        candidates = [p for p in native_root.glob(sprite.m_Name + '-*.png') if sha(p.read_bytes()) == digest]
        if len(candidates) != 1:
            raise ValueError('No unique verified native quality sprite: ' + sprite.m_Name)
        native = candidates[0]
        native_meta = Path(str(native) + '.meta')
        imported = native_meta.read_text(encoding='utf-8')
        if not re.search(r'^  textureType: 8$', imported, re.M) or not re.search(r'^  spriteMode: 1$', imported, re.M):
            raise ValueError('Expected an already imported single Sprite: ' + str(native))
        if (sprite.m_Pivot.x, sprite.m_Pivot.y, sprite.m_PixelsToUnits) != (0.5, 0.5, 100):
            raise ValueError('Unexpected official quality sprite geometry')
        if any(getattr(sprite.m_Border, axis) for axis in 'xyzw'):
            raise ValueError('Quality Simple Image unexpectedly has a border')
        for expected in ('  spritePivot: {x: 0.5, y: 0.5}', '  spritePixelsToUnits: 100'):
            if expected not in imported:
                raise ValueError('Native quality geometry differs: ' + str(native))
        settings = texture.m_TextureSettings
        if (settings.m_FilterMode, settings.m_WrapU, settings.m_WrapV) != (1, 1, 1):
            raise ValueError('Unexpected original quality atlas sampler')
        for expected in ('  sRGBTexture: 1', '    filterMode: 1', '    wrapU: 1', '    wrapV: 1'):
            if expected not in imported:
                raise ValueError('Native quality sampler differs: ' + str(native))
        rect = packed.textureRect
        crop_x, crop_y = round(rect.x), round(rect.y)
        if decoded_image.size != (round(rect.x + rect.width) - crop_x, round(rect.y + rect.height) - crop_y):
            raise ValueError('Decoded quality pixels are not the original rectangular atlas crop')
        geometry = {'sourceSize': [sprite.m_Rect.width, sprite.m_Rect.height],
                    'trimOffset': [packed.textureRectOffset.x, packed.textureRectOffset.y],
                    'trimSize': [rect.width, rect.height],
                    'uvMin': [(rect.x - crop_x) / decoded_image.width, (rect.y - crop_y) / decoded_image.height],
                    'uvMax': [(rect.x + rect.width - crop_x) / decoded_image.width,
                              (rect.y + rect.height - crop_y) / decoded_image.height]}
        source_native = native
        native = native_destination / native.name
        write_verified(native, png.getvalue())
        native_meta = Path(str(native) + '.meta')
        # Explicit FullRect/RGBA32 imports preserve the decoded pixels and prevent Unity's
        # automatic tight mesh from applying a second, unrelated trim to the original UVs.
        imported = re.sub(r'^guid: [0-9a-f]{32}$', 'guid: ' + sha(native.relative_to(output_root).as_posix().encode())[:32], imported, flags=re.M)
        imported = imported.replace('  spriteMeshType: 1', '  spriteMeshType: 0')
        imported = re.sub(r'^    spriteID: [0-9a-f]{32}$',
                          '    spriteID: ' + sha((native.relative_to(output_root).as_posix() + ':Sprite').encode())[:32], imported, flags=re.M)
        imported = imported.replace('  alignment: 0', '  alignment: 9')
        pivot = [(sprite.m_Rect.width * sprite.m_Pivot.x - packed.textureRectOffset.x + rect.x - crop_x) / decoded_image.width,
                 (sprite.m_Rect.height * sprite.m_Pivot.y - packed.textureRectOffset.y + rect.y - crop_y) / decoded_image.height]
        imported = imported.replace('  spritePivot: {x: 0.5, y: 0.5}', '  spritePivot: {x: '+repr(pivot[0])+', y: '+repr(pivot[1])+'}')
        imported = re.sub(r'^    textureCompression: \d+$', '    textureCompression: 0', imported, flags=re.M)
        imported = re.sub(r'^    textureFormat: -1$', '    textureFormat: 4', imported, flags=re.M)
        # alphaIsTransparency's color dilation would alter already decoded atlas pixels.
        imported = imported.replace('  alphaIsTransparency: 1', '  alphaIsTransparency: 0')
        write_verified(native_meta, imported.encode())
        native_outputs += [native, native_meta]
        bindings.append({'quality': matches[0], 'name': sprite.m_Name,
                         'path': native.relative_to(output_root).as_posix(), 'guid': guid(native_meta),
                         'png_sha256': digest, 'meta_sha256': sha(native_meta.read_bytes()),
                         'verified_existing_png': source_native.relative_to(ROOT).as_posix(),
                         'width': decoded_image.width, 'height': decoded_image.height,
                         'pivot': [sprite.m_Pivot.x, sprite.m_Pivot.y], 'ppu': sprite.m_PixelsToUnits,
                         'geometry': geometry})
        inputs.append(dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))
    if sorted(b['quality'] for b in bindings) != list(range(1, 7)):
        raise ValueError('Incomplete required quality sprites')
    rules_path = ROOT / 'assets/official/rules.json'
    rules = json.loads(rules_path.read_bytes())
    skill_ids = {r['ClassId'] for r in rules['skill']} | {r['ClassId'] for r in rules['skill_skin']}
    items = [{'itemId': r['ClassId'], 'quality': r['Quality']} for r in rules['item'] if r['ClassId'] in skill_ids]
    if len(items) != len(skill_ids) or len({r['itemId'] for r in items}) != len(items):
        raise ValueError('Official skill/skin item quality rows are incomplete or duplicated')
    if any(r['Quality'] not in names for r in rules['item']
           if r['ClassId'] in skill_ids and r['ItemType'] in (100, 425)):
        raise ValueError('A player active skill or skin requires an unbound quality')
    dest = output_root / 'unity/Assets/Game/Resources/Combat'
    output = dest / 'battle-skill-qualities.json'
    output.write_bytes((json.dumps({'manifest': '854_156383', 'items': items}, separators=(',', ':')) + '\n').encode())
    receipt_asset = dest / 'battle-skill-qualities-source-manifest.json'
    receipt_asset.write_bytes((json.dumps({'manifest': '854_156383', 'output_sha256': sha(output.read_bytes())}) + '\n').encode())
    runtime = output_root / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillQualityCatalog.cs'
    lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:',
             '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}',
             '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}',
             '  m_Enabled: 1', '  m_EditorHideFlags: 0',
             '  m_Script: {fileID: 11500000, guid: ' + guid(Path(str(runtime)+'.meta')) + ', type: 3}',
             '  m_Name: OfficialBattleSkillQualityCatalog', '  m_EditorClassIdentifier: ',
             '  itemQualities: {fileID: 4900000, guid: ' + guid(meta(output)) + ', type: 3}',
             '  sourceManifest: {fileID: 4900000, guid: ' + guid(meta(receipt_asset)) + ', type: 3}',
             '  qualities:']
    for b in sorted(bindings, key=lambda b: b['quality']):
        lines += ['  - quality: ' + str(b['quality']),
                  '    sprite: {fileID: 21300000, guid: ' + b['guid'] + ', type: 3}']
        for field, values in b['geometry'].items():
            lines.append('    ' + field + ': {x: ' + repr(values[0]) + ', y: ' + repr(values[1]) + '}')
    asset = dest / 'OfficialBattleSkillQualityCatalog.asset'
    asset.write_bytes(('\n'.join(lines)+'\n').encode())
    meta(asset)
    code = ['client-source/GameSkillIcon.cs', 'common-source/Common/LanguageUtils.cs',
            'common-source/Common/Quality.cs', 'common-source/Common/ItemType.cs']
    receipt = {'manifest': '854_156383', 'generator_sha256': sha(Path(__file__).read_bytes()),
               'rules_sha256': sha(rules_path.read_bytes()), 'items': len(items), 'sprites': bindings,
               'inputs': inputs, 'dependencies': list(cache.sources.values()),
               'official_code': {p: sha((ROOT/'.tools/source-packages/official-code'/p).read_bytes()) for p in code},
               'runtime': {p: sha((output_root/p).read_bytes()) for p in (
                   runtime.relative_to(output_root).as_posix(),
                   'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillQualityGeometry.cs',
                   'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudView.cs')},
               'outputs': {p.relative_to(output_root).as_posix(): sha(p.read_bytes()) for p in
                           (output, meta(output), receipt_asset, meta(receipt_asset), asset, meta(asset), *native_outputs)},
               'operation': 'Decode the pinned APK Sprite/atlas objects through Sprite.image, verify exact PNG bytes against existing native sprites, and import independent FullRect RGBA32 Sprites with the original sampler, no secondary compression or alpha dilation. Restore the original Simple Image padding and fractional UVs from the atlas textureRect/offset and original m_Rect; do not truncate floats or stretch the trimmed image. No new or substitute artwork.',
               'remaining': 'Quality 7 (Rainbow) has no direct ImgQuality_Rainbow Sprite in this bundle; it is not mapped to Multicolour by inference. No current player active skill or skill skin requires quality 7.'}
    receipt_path = output_root / 'assets/reference/battle-hud/skill-quality-source-manifest.json'
    receipt_path.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2)+'\n').encode())
    print('Bound six original quality Sprites and', len(items), 'official skill item qualities.')


if __name__ == '__main__':
    build()
