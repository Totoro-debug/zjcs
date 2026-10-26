"""Restore battle report graphics from verified original objects and native Sprite exports."""
from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import re
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'unity/Assets/Official/Combat/BattleReport/Graphics'
RECEIPT = ROOT / 'assets/reference/battle-hud/report-graphics-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    temp.write_bytes(raw)
    temp.replace(path)


def guid(path):
    return re.search(r'(?m)^guid: (\w+)$', path.with_name(path.name + '.meta').read_text(encoding='utf8'))[1]


def meta(path, kind='DefaultImporter', file_id=None, folder=False):
    p = path.with_name(path.name + '.meta')
    if not p.exists():
        value = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/report-graphics/' + path.relative_to(OUT).as_posix()).hex
        text = 'fileFormatVersion: 2\nguid: ' + value + '\n'
        if folder:
            text += 'folderAsset: yes\n'
        text += kind + ':\n  externalObjects: {}\n'
        if file_id:
            text += '  mainObjectFileID: ' + str(file_id) + '\n'
        text += '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        write(p, text.encode())


def build(source_root, bind=False):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    from UnityPy.classes.PPtr import PPtr
    from PIL import Image
    import numpy as np
    import yaml

    class Dumper(yaml.SafeDumper):
        pass

    def represent(dumper, value):
        return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(),
                                       flow_style=bool(value) and all(not isinstance(v, (dict, list)) for v in value.values()))
    Dumper.add_representer(dict, represent)

    def load_native(path):
        text = path.read_text(encoding='utf8')
        text = re.sub(r'(?m)^(\s*(?:m_IndexBuffer|_typelessdata):) ([0-9a-fA-F]+)$', r'\1 "\2"', text)
        return next(iter(yaml.safe_load(re.sub(r'(?m)^--- !u!\d+ &-?\d+', '---',
                                             re.sub(r'(?m)^%.*\n', '', text))).values()))

    apk = Apk(manifest='854_156383', reuse_current_cache=True)
    ugui = ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll'
    net = Path('D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll')
    schemas = ScriptSchemas(assembly_overrides={'UnityEngine.UI': ugui})
    schemas.generator.load_dll(net.read_bytes())
    tmp = ScriptSchemas()
    tmp.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    tmp.generator.load_local_dll_folder(str(net.parents[3] / 'Managed/UnityEngine'))
    tmp.generator.load_local_dll_folder(str(ugui.parent))
    tmp.generator.load_dll(net.read_bytes())
    tmp.generator.load_dll((net.parents[3] / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll').read_bytes())
    export = source_root / '.tools/assetripper-1.3.14/temp/battle-report-ui-ripper/ExportedProject/Assets'
    export_by_name, export_by_guid = {}, {}
    for p in export.rglob('*.asset'):
        export_by_name.setdefault(p.stem, []).append(p)
    for p in export.rglob('*.meta'):
        if p.with_suffix('').is_file():
            export_by_guid[guid(p.with_suffix(''))] = p.with_suffix('')
    inputs, textures, sprites, fonts, materials, bindings = {}, {}, {}, {}, {}, []
    native_exports, verified_textures, reused = {}, {}, {}
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    tabs = json.loads((ROOT / 'assets/reference/battle-hud/report-tabs-source-manifest.json').read_bytes())

    def record(obj):
        key = obj.assets_file.name + ':' + str(obj.path_id)
        inputs[key] = dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), type=obj.type.name)
        return obj

    def pointer(value, owner):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref()) if value['m_PathID'] else None

    def texture(obj):
        record(obj)
        key = 'Texture_' + sha(obj.get_raw_data())[:16]
        if key not in textures:
            data = obj.read()
            stream = io.BytesIO()
            if data.m_Width == 0 and data.m_Height == 0 and not data.image_data and not data.m_StreamData.size:
                pass  # The original empty dynamic font atlas is allocated by TMP at initialization.
            else:
                data.image.save(stream, format='PNG')
                write(OUT / (key + '.png'), stream.getvalue())
            settings = data.m_TextureSettings
            textures[key] = dict(key=key, width=data.m_Width, height=data.m_Height,
                                 filter=settings.m_FilterMode, wrapU=settings.m_WrapU, wrapV=settings.m_WrapV,
                                 aniso=settings.m_Aniso, bias=settings.m_MipBias, sRGB=data.m_ColorSpace == 1)
        return key

    def reference(path, file_id):
        # Unity's serialized native assets use type 2; imported textures/scripts use type 3.
        return dict(fileID=file_id, guid=guid(path), type=2 if path.suffix in ('.asset', '.mat') else 3)

    def shared(obj, key, suffix, file_id):
        # These two objects were already restored from these exact original raw identities.
        path = ROOT / 'unity/Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback' / (key + suffix)
        source_receipt = json.loads((ROOT / 'assets/reference/battle-hud/element-feedback-source-manifest.json').read_bytes())
        if not any(x.get('sha256') == sha(obj.get_raw_data()) for x in source_receipt['inputs']):
            raise ValueError('Shared font/material original identity is absent from its source receipt')
        reused[key] = dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()),
                           meta_sha256=sha(path.with_name(path.name + '.meta').read_bytes()))
        return reference(path, file_id)

    def sprite(obj):
        record(obj)
        key = 'Sprite_' + sha(obj.get_raw_data())[:16]
        if key not in sprites:
            original = obj.read()
            raw = obj.read_typetree()
            if original.m_SpriteAtlas.path_id:
                atlas = record(original.m_SpriteAtlas.deref()).read()
                packed = next(v for k, v in atlas.m_RenderDataMap if k == original.m_RenderDataKey)
            else:
                packed = original.m_RD
            candidates = []
            for source in export_by_name.get(original.m_Name, []):
                value = load_native(source)
                if (all(value.get(k) == raw[k] for k in ('m_Border', 'm_PixelsToUnits'))
                        and value['m_RD']['m_VertexData']['m_VertexCount'] == raw['m_RD']['m_VertexData']['m_VertexCount']
                        and value['m_RD']['m_IndexBuffer'] == bytes(raw['m_RD']['m_IndexBuffer']).hex()
                        and all(abs(value['m_RD']['textureRect'][axis] - getattr(packed.textureRect, axis)) <= 0.0003
                                for axis in ('x', 'y', 'width', 'height'))):
                    candidates.append((source, value))
            direct = not candidates and not export_by_name.get(original.m_Name) and not original.m_SpriteAtlas.path_id
            if direct:
                # Built-in Knob is present in the APK's unity default resources but has no native export.
                # Serialize its complete original native typetree; no geometry is reconstructed by hand.
                native = copy.deepcopy(raw)
                def native_fields(value):
                    if isinstance(value, list):
                        return [native_fields(v) for v in value]
                    if isinstance(value, dict):
                        if set(value) == {'m_FileID', 'm_PathID'}:
                            if value['m_PathID'] and value != raw['m_RD']['texture']:
                                raise ValueError('Unmapped direct native Sprite reference')
                            return {'fileID': 0}
                        return {k: native_fields(v) for k, v in value.items()}
                    return value
                native = native_fields(native)
                native['m_RD']['m_IndexBuffer'] = bytes(raw['m_RD']['m_IndexBuffer']).hex()
                vertex_data = bytes(raw['m_RD']['m_VertexData']['m_DataSize'])
                native['m_RD']['m_VertexData']['m_DataSize'] = len(vertex_data)
                native['m_RD']['m_VertexData']['_typelessdata'] = vertex_data.hex()
                native['m_RenderDataKey'] = {uuid.uuid5(uuid.NAMESPACE_URL, key).hex: 21300000}
                source = None
            elif len(candidates) != 1:
                raise ValueError('Original Sprite export is not uniquely verified: ' + original.m_Name + ' candidates=' + str(export_by_name.get(original.m_Name, [])) + ' selected=' + str(candidates))
            else:
                source, native = candidates[0]
            rd = raw['m_RD']
            if native['m_RD']['m_IndexBuffer'] != bytes(rd['m_IndexBuffer']).hex():
                raise ValueError('Sprite triangle indices differ: ' + original.m_Name)
            vertices = native['m_RD']['m_VertexData']
            count = rd['m_VertexData']['m_VertexCount']
            if count != vertices['m_VertexCount'] or vertices['m_Channels'][0] != rd['m_VertexData']['m_Channels'][0]:
                raise ValueError('Sprite vertex layout differs: ' + original.m_Name)
            if bytes.fromhex(vertices['_typelessdata'])[:count * 12] != bytes(rd['m_VertexData']['m_DataSize'])[:count * 12]:
                raise ValueError('Sprite geometry differs: ' + original.m_Name)
            for k, axes in [('textureRect', ('x', 'y', 'width', 'height'))]:
                if any(abs(native['m_RD'][k][axis] - getattr(getattr(packed, k), axis)) > 0.0003 for axis in axes):
                    raise ValueError('Sprite atlas rectangle differs: ' + original.m_Name)
            original_texture = record(packed.texture.deref())
            texture_key = texture(original_texture)
            # AssetRipper and UnityPy ASTC decoders differ by at most one channel unit.
            # Use the exact UnityPy decoding of the verified original texture for the final asset.
            exported_texture = export_by_guid[native['m_RD']['texture']['guid']] if not direct else None
            if not direct and texture_key not in verified_textures:
                expected_pixels = np.array(original_texture.read().image.convert('RGBA'))
                actual_pixels = np.array(Image.open(exported_texture).convert('RGBA'))
                if expected_pixels.shape != actual_pixels.shape:
                    raise ValueError('Native atlas dimensions differ: ' + original.m_Name)
                difference = int(np.abs(expected_pixels.astype('int16') - actual_pixels.astype('int16')).max())
                if difference > 1:
                    raise ValueError('Native atlas pixels differ from the original compressed texture: ' + original.m_Name)
                verified_textures[texture_key] = dict(export=exported_texture.relative_to(export).as_posix(), max_channel_difference=difference,
                                                     output_pixels_sha256=sha(expected_pixels.tobytes()))
            if not direct:
                native_exports[source.relative_to(export).as_posix()] = sha(source.read_bytes())
                native_exports[exported_texture.relative_to(export).as_posix()] = sha(exported_texture.read_bytes())
            target_texture = OUT / (texture_key + '.png')
            if bind:
                native['m_RD']['texture'] = reference(target_texture, 2800000)
                # The old exporter shrank tight sprites to their texture rect, changing UI padding.
                # Restore the original full rect and pivot within the verified packed texture.
                native['m_Rect'] = dict(serializedVersion=2,
                    x=packed.textureRect.x - packed.textureRectOffset.x,
                    y=packed.textureRect.y - packed.textureRectOffset.y,
                    width=raw['m_Rect']['width'], height=raw['m_Rect']['height'])
                native['m_Offset'] = raw['m_Offset']
                native['m_Pivot'] = raw['m_Pivot']
                native['m_RD']['textureRectOffset'] = dict(x=packed.textureRectOffset.x, y=packed.textureRectOffset.y)
                native_text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!213 &21300000\n'
                native_text += yaml.dump({'Sprite': native}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
                target = OUT / (key + '.asset')
                write(target, native_text.encode())
                meta(target, 'NativeFormatImporter', 21300000)
            sprites[key] = dict(key=key, name=original.m_Name, texture=texture_key, source=source.relative_to(export).as_posix() if source else 'original-native-typetree',
                                source_sha256=sha(source.read_bytes()) if source else sha(obj.get_raw_data()), original_rect=raw['m_Rect'],
                                pivot=raw['m_Pivot'], border=raw['m_Border'], vertex_count=count)
        return reference(OUT / (key + '.asset'), 21300000) if bind else {'asset': key}

    config_obj = record(apk.asset('textmesh_pro_rawfonts_fontassetfontconfig', '/FontAssetFontConfig.json'))
    config = json.loads(config_obj.read().m_Script)['FontAssetInfoDict']

    def font(obj):
        record(obj)
        key = 'Font_' + sha(obj.get_raw_data())[:16]
        if sha(obj.get_raw_data()) == '993fe9f48bba80bc3f729c6cfabf269fdf7b91ac69fb2d013625ee8ac8a68031':
            return shared(obj, key, '.asset', 11400000)
        if key not in fonts:
            data = tmp.read(obj)
            info = config[data['m_Name']]
            raw_obj = record(apk.asset('textmesh_pro_rawfonts_' + Path(info['RawFontPath']).stem.lower(), info['RawFontPath']))
            payload = raw_obj.read().m_Script.encode('utf8', 'surrogateescape')
            if len(payload) != info['Length'] or hashlib.md5(payload).hexdigest() != info['Hash']:
                raise ValueError('Original font configuration hash differs: ' + data['m_Name'])
            source_path = OUT / (Path(info['FontPath']).name)
            write(source_path, payload)
            material_obj = pointer(data['material'], obj)
            material(material_obj)
            material_key = 'Material_' + sha(material_obj.get_raw_data())[:16]
            retained = ['m_FaceInfo', 'm_GlyphTable', 'm_CharacterTable', 'm_UsedGlyphRects', 'm_FreeGlyphRects',
                        'm_FontFeatureTable', 'normalStyle', 'normalSpacingOffset', 'boldStyle', 'boldSpacing', 'italicStyle', 'tabSize']
            fonts[key] = dict(key=key, name=data['m_Name'], sourcePath=source_path.relative_to(ROOT / 'unity').as_posix(),
                              sourceSha256=sha(payload), pointSize=data['m_FaceInfo']['m_PointSize'],
                              width=data['m_AtlasWidth'], height=data['m_AtlasHeight'], padding=data['m_AtlasPadding'],
                              renderMode=data['m_AtlasRenderMode'], population=data['m_AtlasPopulationMode'],
                              multiAtlas=bool(data['m_IsMultiAtlasTexturesEnabled']), clearOnBuild=bool(data['m_ClearDynamicDataOnBuild']),
                              atlasTextures=[texture(pointer(p, obj)) for p in data['m_AtlasTextures']],
                              fontJson=json.dumps({k: data[k] for k in retained}, ensure_ascii=True, separators=(',', ':')),
                              material=material_key, warmCharacters='')
        return reference(OUT / (key + '.asset'), 11400000) if bind else {'asset': key}

    def material(obj):
        record(obj)
        key = 'Material_' + sha(obj.get_raw_data())[:16]
        if sha(obj.get_raw_data()) == '57b7f2adeb457a8f4f3b3842127ac1bdf497f87992d883bbfee812a4525383be':
            return shared(obj, key, '.mat', 2100000)
        if key not in materials:
            data = obj.read()
            shader = record(data.m_Shader.deref())
            original_shader = shader.read()
            if original_shader.m_ParsedForm.m_Name != 'Hidden/JSZX/TextMeshPro':
                raise ValueError('Unrestored report shader: ' + original_shader.m_ParsedForm.m_Name)
            from prepare_battle_world_shaders import shader_text
            translated, _ = shader_text(original_shader, 'TextMeshPro', ('UNITY_UI_CLIP_RECT', 'UNITY_UI_ALPHACLIP', 'OUTLINE_ON'))
            shader_path = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders/WorldTextMeshPro.shader'
            if translated != shader_path.read_text(encoding='utf8'):
                raise ValueError('Report font shader differs from the verified existing original translation')
            materials[key] = dict(key=key, name=data.m_Name, shader='Zjcs/Combat/WorldTextMeshPro',
                                 queue=data.m_CustomRenderQueue, keywords=data.m_ValidKeywords,
                                 floats=[dict(name=k, value=v) for k, v in data.m_SavedProperties.m_Floats],
                                 colors=[dict(name=k, rgba=[v.r, v.g, v.b, v.a]) for k, v in data.m_SavedProperties.m_Colors],
                                 textures=[dict(name=k, texture=texture(record(v.m_Texture.deref())) if v.m_Texture.path_id else '',
                                                scale=[v.m_Scale.x, v.m_Scale.y], offset=[v.m_Offset.x, v.m_Offset.y])
                                           for k, v in data.m_SavedProperties.m_TexEnvs])
        return reference(OUT / (key + '.mat'), 2100000) if bind else {'asset': key}

    # The tab receipt already proves hierarchy/component indices against every original prefab.
    outputs = []
    for panel in tabs['outputs']:
        path = ROOT / panel['path']
        raw = path.read_bytes()
        expected = {panel['sha256']}
        if prior:
            expected.update(x['sha256'] for x in prior['outputs'] if x['path'] == panel['path'])
        if sha(raw.replace(b'\r\n', b'\n')) not in expected:
            raise ValueError('Changed report prefab: ' + str(path))
        blocks = re.split(r'(?m)^--- ', raw.decode())
        docs = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            docs[int(match[2])] = (index, next(iter(yaml.safe_load(block[match.end():]).values())))
        bundle = 'defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_' + path.stem.lower() + '.bundle'
        objects = {str(o.path_id): o for o in apk.objects(bundle)}
        for binding in tabs['bindings']:
            if binding['prefab'] != panel['path'] or binding['class'] not in ('Image', 'MirrorImage', 'TextMeshProUGUI'):
                continue
            obj = record(objects[binding['original_path_id']])
            if sha(obj.get_raw_data()) != binding['object_sha256']:
                raise ValueError('Original graphic object differs')
            data = (tmp if binding['class'] == 'TextMeshProUGUI' else schemas).read(obj)
            def convert(v):
                if isinstance(v, list):
                    return [convert(x) for x in v]
                if isinstance(v, dict):
                    if set(v) == {'m_FileID', 'm_PathID'}:
                        target = pointer(v, obj)
                        if target is None:
                            return {'fileID': 0}
                        if target.type.name == 'Sprite':
                            return sprite(target)
                        if target.type.name == 'Material':
                            return material(target)
                        if target.type.name == 'MonoBehaviour' and target.read(check_read=False).m_Script.read().m_ClassName == 'TMP_FontAsset':
                            return font(target)
                        raise ValueError('Restore original graphic reference explicitly: ' + target.type.name)
                    return {k: convert(x) for k, x in v.items()}
                return v
            fields = {k: convert(v) for k, v in data.items() if k not in ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name')}
            if binding['class'] == 'TextMeshProUGUI':
                key = 'Font_' + sha(pointer(data['m_fontAsset'], obj).get_raw_data())[:16]
                if key in fonts:
                    fonts[key]['warmCharacters'] = ''.join(sorted(set(fonts[key]['warmCharacters'] + data['m_text'])))
            index, native = docs[int(binding['file_id'])]
            if bind:
                if binding['class'] == 'MirrorImage':
                    native['m_Script'] = reference(ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/MirrorImage.cs', 11500000)
                # Rebuild in the original inherited field order after the earlier hit-test-only phase.
                native = {k: v for k, v in native.items() if k not in fields}
                native.update(fields)
                blocks[index] = '!u!114 &' + binding['file_id'] + '\n' + yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
            bindings.append(dict(prefab=panel['path'], path=binding['path'], file_id=binding['file_id'],
                                 original_path_id=binding['original_path_id'], object_sha256=sha(obj.get_raw_data()),
                                 kind=binding['class'], fields=fields))
        if bind:
            result = '--- '.join(blocks).encode()
            write(path, result)
            outputs.append(dict(path=panel['path'], sha256=sha(result), bytes=len(result)))
    data = dict(manifest=apk.version, textures=list(textures.values()), fonts=list(fonts.values()), materials=list(materials.values()))
    write(OUT / 'report-graphics.json', (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())
    for directory in [OUT]:
        meta(directory, folder=True)
    meta(OUT / 'report-graphics.json')
    receipt = dict(format=1, manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
                   inputs=list(inputs.values()), native_exports=native_exports, reused=reused, texture_verification=verified_textures, sprites=list(sprites.values()),
                   bindings=bindings, outputs=outputs if bind else (prior['outputs'] if prior else []), pending=['Report custom components, localization refresh, data rows and production entry remain unfinished.'])
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    print(json.dumps(dict(phase='bind' if bind else 'extract', sprites=len(sprites), textures=len(textures), fonts=len(fonts), graphics=len(bindings))))


def record_native(source_root):
    receipt = json.loads(RECEIPT.read_bytes())
    for entry in receipt['outputs']:
        if sha((ROOT / entry['path']).read_bytes()) != entry['sha256']:
            raise ValueError('Report prefab changed after source binding: ' + entry['path'])
    receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
    receipt['native_assets'] = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes()), bytes=p.stat().st_size)
                                for p in sorted([p for p in OUT.rglob('*') if p.is_file()] + [OUT.with_name(OUT.name + '.meta')])]
    paths = ['unity/Assets/Game/Editor/Combat/OfficialBattleReportGraphicsAssetTools.cs',
             'unity/Assets/Game/Editor/Combat/OfficialBattleReportGraphicsAcceptance.cs',
             'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']
    receipt['runtime'] = {p: sha((ROOT / p).read_bytes()) for p in paths}
    original = source_root / '.tools/source-packages/official-code/framework-firstpass-source/MirrorImage.cs'
    migrated = 'unity/Assets/Game/Runtime/Official/WorldUI/MirrorImage.cs'
    receipt['mirror_code'] = dict(original=original.relative_to(source_root).as_posix(), original_sha256=sha(original.read_bytes()),
                                  path=migrated, canonical_lf_sha256=sha((ROOT / migrated).read_bytes().replace(b'\r\n', b'\n')))
    receipt['source_scripts'] = {p: sha((source_root / p).read_bytes()) for p in
                                ['scripts/official_apk.py', 'scripts/official_unity_schema.py', 'scripts/prepare_battle_world_shaders.py']}
    receipt['adaptations'] = [
        'Read original UGUI/TMP fields with strict byte-consumption schemas; restore references by verified object identity.',
        'Native .asset/.mat PPtrs use Unity type 2, imported textures/scripts type 3; preserve original inherited field order.',
        'Verify exported Sprite vertices/triangles and packed texture rectangles against original objects. Restore full Sprite rectangles, pivots and trimming offsets omitted by the old native export.',
        'Compare exported atlas pixels with the original ASTC decoding (maximum channel difference 1); final PNGs use the exact UnityPy decoding of the original textures.',
        'Serialize the APK built-in Knob directly from its complete native typetree when no prior export exists.',
        'Reuse the already restored FZY4K font/material with identical original raw object hashes. Restore the MPLUS raw font by its official configuration hash; allocate its originally empty dynamic atlas from original TMP settings.',
        'The graphics receipt supersedes the earlier tab receipt runtime prefab hashes; tab code and original hierarchy/Animator dependencies remain intact.'
    ]
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    print('Recorded native report graphics assets:', len(receipt['native_assets']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--bind', action='store_true')
    parser.add_argument('--record-native', action='store_true')
    args = parser.parse_args()
    if args.record_native:
        record_native(args.source_root)
    else:
        build(args.source_root, args.bind)
