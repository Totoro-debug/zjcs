"""Restore the original serialized team font and its exact native dependencies."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

from official_apk import Apk, ROOT
from UnityPy.classes.PPtr import PPtr
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map13_native_scene import native_texture
from validation_workspace import validation_directory
from map13_native_schemas import NativeSchemas
from map12_team_native_schemas import install
from prepare_unity_map12_team_native_export import ConversionPath

DEST = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-font-source-manifest.json'
GUID = re.compile(r'(?m)guid: ([0-9a-f]{32})')


def build(stage, write=False, chinese=False):
    workspace = validation_directory()
    stage = stage.resolve()
    if workspace == ROOT / 'output/validation' or not stage.is_relative_to(workspace):
        raise ValueError('Use this invocation\'s original team conversion stage')
    stage = ConversionPath(stage)
    apk = Apk.current()
    if apk.version != '871_156598':
        raise ValueError('Team font source version changed')
    font_name = 'FZY4K SDF - dynamic' if chinese else 'MPLUSRounded1c-Medium SDF'
    source_name = 'FZY4K' if chinese else 'MPLUSRounded1c-Medium'
    stem = 'FZY4K-Dynamic' if chinese else 'MPLUSRounded1c-Medium'
    receipt_path = ROOT / 'assets/reference/official-map/map12-team-chinese-font-source-manifest.json' if chinese else RECEIPT
    bundle = 'textmesh_pro_assets_fonts_&_materials'
    font = apk.asset(bundle, '/' + font_name + '.asset')
    schemas = NativeSchemas(ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
    install(schemas, ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
    font_fields, _ = schemas.read(font)
    mat = PPtr(**font_fields['material'], assetsfile=font.assets_file).deref()
    atlas = next(v.m_Texture.deref() for k, v in mat.read().m_SavedProperties.m_TexEnvs if k == '_MainTex')
    font_atlas, = [PPtr(**p, assetsfile=font.assets_file).deref() for p in font_fields['m_AtlasTextures']]
    if (font_atlas.assets_file.name, font_atlas.path_id) != (atlas.assets_file.name, atlas.path_id):
        raise ValueError('Original team font and material atlas pointers differ')
    shader_obj = mat.read().m_Shader.deref()
    if shader_obj.read().m_ParsedForm.m_Name != 'Hidden/JSZX/TextMeshPro':
        raise ValueError('Restore the selected original team font shader explicitly')
    original_font = apk.asset('textmesh_pro_fonts_' + source_name.lower(), '/' + source_name + ('.TTF' if chinese else '.ttf'))
    raw_font = apk.asset('textmesh_pro_rawfonts_' + source_name.lower(), '/' + source_name + '.bytes')
    config = apk.asset('textmesh_pro_rawfonts_fontassetfontconfig', '/FontAssetFontConfig.json')
    item = json.loads(config.read().m_Script)['FontAssetInfoDict'][font_name]
    data = bytes(original_font.read().m_FontData)
    raw_data = raw_font.read().m_Script.encode('utf8', errors='surrogateescape')
    if data != raw_data or len(data) != item['Length'] or hashlib.md5(data).hexdigest() != item['Hash']:
        raise ValueError('Original team TTF/raw font/config bytes differ')
    exported = stage / 'export/ExportedProject/Assets'
    font_path = exported / ('TextMesh Pro/Assets/Fonts & Materials/' + font_name + '.asset')
    font_text = font_path.read_text(encoding='utf8')
    mat_guid = re.search(r'(?m)^  material: \{fileID: 2100000, guid: (\w+)', font_text)[1]
    atlas_guid = re.search(r'(?m)^  - \{fileID: 2800000, guid: (\w+)', font_text)[1]
    index = {}
    for meta in exported.rglob('*.meta'):
        match = GUID.search(meta.read_text(encoding='utf8'))
        if match and match[1] in (mat_guid, atlas_guid):
            index[match[1]] = meta.with_suffix('')
    mat_path = index[mat_guid]
    mat_text = mat_path.read_text(encoding='utf8')
    shader_guid = re.search(r'm_Shader: \{fileID: 4800000, guid: (\w+)', mat_text)[1]
    script_guid = re.search(r'm_Script: \{fileID: 11500000, guid: (\w+)', font_text)[1]
    package_meta = ROOT / 'unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TMP_FontAsset.cs.meta'
    shader = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders/WorldText.shader'
    source_shader_receipt = ROOT / 'assets/reference/official-map/map13-text-shader-source-manifest.json'
    shader_receipt = json.loads(source_shader_receipt.read_bytes())
    if not any(row['object_sha256'] == sha(shader_obj.get_raw_data()) for row in shader_receipt['inputs']):
        raise ValueError('Original team and shared native TMP shader objects differ')
    shared_shader, = [r for r in shader_receipt['outputs'] if r['path'] == source_path(shader)]
    if shared_shader['sha256'] != sha(shader.read_bytes()):
        raise ValueError('Shared original TMP shader Windows port changed')
    paths = dict(font=DEST / (stem + '.asset'), material=DEST / (stem + '.mat'),
                 atlas=DEST / (stem + '-Atlas.texture.asset'), source=DEST / (source_name + '.ttf'))
    ids = {k: hashlib.md5(('zjcs-map12-team-font:' + source_path(p)).encode()).hexdigest() for k, p in paths.items()}
    redirects = {mat_guid: ids['material'], atlas_guid: ids['atlas'],
                 shader_guid: GUID.search(shader.with_name(shader.name + '.meta').read_text())[1],
                 script_guid: GUID.search(package_meta.read_text())[1]}

    def remap(text):
        return GUID.sub(lambda m: 'guid: ' + redirects.get(m[1], m[1]), text)

    font_text = remap(font_text)
    if font_text.count('  m_SourceFontFileGUID:\n') != 1 or font_text.count('  m_SourceFontFile: {fileID: 0}') != 1:
        raise ValueError('Original deferred team font binding differs')
    font_text = font_text.replace('  m_SourceFontFileGUID:\n', '  m_SourceFontFileGUID: ' + ids['source'] + '\n')
    font_text = font_text.replace('  m_SourceFontFile: {fileID: 0}',
                                '  m_SourceFontFile: {fileID: 12800000, guid: ' + ids['source'] + ', type: 3}')
    font_text = font_text.replace('guid: ' + ids['atlas'] + ', type: 3', 'guid: ' + ids['atlas'] + ', type: 2')
    texture = atlas.read()
    # TMP clears dynamic atlas data before the Android build. UnityPy's generic
    # reader attempts to open the empty stream path; the original 0x0 texture
    # actually has no pixel/stream bytes and is populated by TMP at runtime.
    empty_atlas = texture.m_Width == 0 and texture.m_Height == 0
    if empty_atlas:
        if texture.image_data or texture.m_StreamData.size or texture.m_StreamData.path:
            raise ValueError('Original empty team atlas contains unexpected storage')
        pixels = b''
    else:
        pixels = texture.get_image_data()
    if texture.m_TextureFormat != 1 or texture.m_MipCount != 1 or len(pixels) != texture.m_Width * texture.m_Height:
        raise ValueError('Restore original team Alpha8 atlas format explicitly')
    # Reuse the already restored Unity-native Alpha8 serialization layout. The
    # previous converter scratch template is not a runtime/source prerequisite.
    template = ROOT / 'unity/Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic-Atlas.texture.asset'
    # Dynamic glyph generation can dirty a shared atlas in another editor. Read
    # its committed native serialization without changing that session's file.
    template_ref = 'HEAD:' + source_path(template)
    template_raw = subprocess.check_output(['git', 'show', template_ref], cwd=ROOT)
    template_blob = subprocess.check_output(['git', 'rev-parse', template_ref], cwd=ROOT, text=True).strip()
    template_text = template_raw.decode('utf8')
    template_text, count = re.subn(r'(?m)^--- !u!28 &2800000$', '--- !u!28 &1', template_text)
    if count != 1:
        raise ValueError('Shared native Alpha8 texture serialization differs')
    template_text, count = re.subn(r'(?m)^  m_TextureFormat: 1$', '  m_TextureFormat: 4', template_text)
    if count != 1:
        raise ValueError('Shared native font atlas format differs')
    atlas_text = native_texture(template_text, texture, pixels).decode()
    atlas_text, count = re.subn(r'(?m)^  m_TextureFormat: 4$', '  m_TextureFormat: 1', atlas_text)
    if count != 1:
        raise ValueError('Native original team atlas format field differs')
    payloads = dict(font=font_text.encode(),
                    material=remap(mat_text).replace('guid: ' + ids['atlas'] + ', type: 3',
                                                   'guid: ' + ids['atlas'] + ', type: 2').encode(),
                    atlas=atlas_text.encode(), source=data)
    outputs = {}
    for key, path in paths.items():
        outputs[path] = payloads[key]
        meta = 'fileFormatVersion: 2\nguid: ' + ids[key] + '\n'
        if key == 'source':
            meta += ('TrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n'
                     '  forceTextureCase: -2\n  characterSpacing: 0\n  characterPadding: 1\n  includeFontData: 1\n'
                     '  fontNames:\n  - ' + ('FZCuYuan-M03' if chinese else 'Rounded Mplus 1c') + '\n  fallbackFontReferences: []\n  customCharacters:\n'
                     '  fontRenderingMode: 0\n  ascentCalculationMode: 1\n  useLegacyBoundsCalculation: 0\n'
                     '  shouldRoundAdvanceValue: 1\n')
        else:
            meta += ('NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' +
                     str(dict(font=11400000, material=2100000, atlas=2800000)[key]) + '\n')
        outputs[path.with_name(path.name + '.meta')] = (meta + '  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    folder_meta = ('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-map12-team-font:' + source_path(DEST)).encode()).hexdigest() +
                   '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    outputs[DEST.with_name(DEST.name + '.meta')] = folder_meta.encode()

    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))

    receipt = dict(format=1, manifest=apk.version, mapClassId=12, fontName=font_name,
        inputs=[dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
                for obj in (font, mat, atlas, original_font, raw_font, config, shader_obj)],
        outputs=[dict(path=source_path(p), sha256=sha(raw)) for p, raw in outputs.items()],
        exportedInputs=[dict(file=p.relative_to(exported).as_posix(), sha256=sha(p.read_bytes()))
                        for p in (font_path, mat_path)],
        sourceFont=dict(bytes=len(data), md5=hashlib.md5(data).hexdigest(), sha256=sha(data)),
        atlas=dict(width=texture.m_Width, height=texture.m_Height, format=1, mipCount=1, sha256=sha(pixels)),
        dependencies=[record(p) for p in (shader, source_shader_receipt, package_meta)],
        serializationTemplate=dict(path=source_path(template), gitBlob=template_blob, sha256=sha(template_raw)),
        adapters=[record(Path(__file__).resolve()), record(ROOT / 'scripts/prepare_unity_map13_native_scene.py')],
        operation='Retain original team serialized font, metrics, style/spacing, empty initial glyph tables, '
                  'Alpha8 atlas bytes and original custom TMP material. Recover identical TTF/raw bytes and verify '
                  'the official MD5/length. Bind the original source Font and the shared mechanically translated '
                  'Windows TMP shader. This is the original serialized font; Chinese language selection remains '
                  'the original separate FZY4K font service rule.',
        limitations=['Actual team window binding, complete custom TMP compatibility and runtime rendering remain pending.'])
    outputs[receipt_path] = encode(receipt)
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original team font source/native drift: ' + str(path))
    print('Original serialized team font restored: exact TTF/raw bytes, Alpha8 atlas and native material.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage-dir', type=Path, required=True)
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--chinese', action='store_true', help='Restore the original config-selected Chinese team font')
    args = parser.parse_args()
    build(args.stage_dir, args.write, args.chinese)
