"""Bind the three original team panel prefabs to native Windows components and assets."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import re
import struct
from pathlib import Path

from PIL import Image
import xxhash
from official_apk import Apk, ROOT
from official_native_yaml_verification import documents
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_sources import BUNDLE, OUTPUT as ORIGINAL
from prepare_unity_map12_team_toggle import guid
from map13_native_schemas import NativeSchemas
from map12_team_native_schemas import install
from prepare_unity_map12_team_panel_animation import build as animations
from prepare_unity_map12_team_panel_sprites import NativeTeamSprites

NATIVE = ROOT / 'unity/Assets/Official/World/Map/12/Source/native-team-ui-prefabs.json'
DEST = ROOT / 'unity/Assets/Game/Resources/Map12/Team'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-panel-prefabs-source-manifest.json'
NAMES = ('TeamPanel', 'TeamPanelItem', 'TeamPanelDialogButtonGroup')


def output_guid(name):
    return hashlib.md5(('zjcs:map12:team-target:' + name).encode()).hexdigest()


def build(write=False):
    original, native = json.loads(ORIGINAL.read_bytes()), json.loads(NATIVE.read_bytes())
    if original['manifest'] != '871_156598' or native['manifest'] != original['manifest']:
        raise ValueError('Original team target version differs')
    rows = {row['identity']: row for row in original['objects']}
    references = {row['identity']: row for row in native['externalReferences']}
    apk = Apk.current()
    _, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError('Resolve original team target dependencies: ' + str(missing))
    objects = {f.name + ':' + str(o.path_id): o for b in apk.loaded.values()
               for f in serialized_files(b) for o in f.objects.values()}

    def external(pointer):
        if pointer['m_PathID'] == 0:
            return None, None
        candidates = [key for key in references if key.rsplit(':', 1)[1] == str(pointer['m_PathID'])]
        if len(candidates) != 1:
            raise ValueError('Ambiguous original team target dependency')
        key = candidates[0]
        obj, reference = objects[key], references[key]
        if sha(obj.get_raw_data()) != reference['payload_sha256']:
            raise ValueError('Original target dependency changed: ' + key)
        return obj, reference

    ui_path = ROOT / 'unity/Assets/Official/World/Map/12/UI/map12-ui.json'
    ui = json.loads(ui_path.read_bytes())
    font_paths = [ROOT / ('assets/reference/official-map/' + name) for name in
                  ('map12-team-font-source-manifest.json', 'map12-team-chinese-font-source-manifest.json')]
    fonts = [json.loads(path.read_bytes()) for path in font_paths]
    for receipt in fonts:
        for row in receipt['outputs']:
            if sha((ROOT / row['path']).read_bytes()) != row['sha256']:
                raise ValueError('Original target font changed')
    font_names = ('MPLUSRounded1c-Medium', 'FZY4K-Dynamic')
    world = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI'
    team = ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI'
    ugui = ROOT / 'unity/Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core'
    scripts = {name: team / (name + '.cs') for name in ('TeamPanel', 'TeamPanelItem', 'TeamPanelItemButton', 'TeamMemberIcon', 'TeamPanelTarget')}
    scripts.update({name: team / (target + '.cs') for name, target in {
        'PlayerIcon': 'TeamPlayerIcon', 'PlayerIconOther': 'TeamPlayerIconOther',
        'HeadCharacterPortrait': 'TeamHeadCharacterPortrait', 'PlayerIconMeHpSliderNormal': 'TeamPlayerIconMeHpSliderNormal',
        'UIToggleObject': 'TeamToggleObject'}.items()})
    profession = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
    scripts.update({name: profession / (name + '.cs') for name in ('PlayerIconMe', 'PlayerInfoUI', 'StoryNodeRegister')})
    scripts.update({name: world / (target + '.cs') for name, target in {
        'ButtonSound': 'WorldButtonSound', 'MultiLanguageText': 'WorldMultiLanguageText',
        'LayoutElementClamper': 'WorldLayoutElementClamper', 'PreviewTool': 'WorldPreviewTool',
        'NoDrawingRayCast': 'NoDrawingRayCast', 'MirrorImage': 'MirrorImage',
        'PopupWindow': 'WorldPopupWindow', 'PopupWindowSound': 'WorldPopupWindowSound',
        'BgDialogElement': 'WorldBgDialogElement', 'UIBloomGroup': 'WorldUIBloomGroup',
        'AllowMemberInviteToggle': 'WorldAllowMemberInviteToggle', 'UIClickSound': 'WorldUiClickSound',
        'SlicedFilledImage': 'SlicedFilledImage',
        'UGUIJszx.VerticalLayoutGroupJSZX': 'VerticalLayoutGroupJSZX',
        'UGUIJszx.HorizontalLayoutGroupJSZX': 'HorizontalLayoutGroupJSZX'}.items()})
    for name in ('Image', 'Button', 'GraphicRaycaster', 'ScrollRect', 'RectMask2D',
                 'ContentSizeFitter', 'HorizontalLayoutGroup', 'VerticalLayoutGroup', 'LayoutElement', 'Toggle',
                 'GridLayoutGroup', 'Slider', 'Mask', 'RawImage', 'Shadow'):
        paths = list(ugui.rglob(name + '.cs'))
        if len(paths) != 1:
            raise ValueError('Inspect actual UGUI dependency: ' + name)
        scripts['UnityEngine.UI.' + name] = paths[0]
    scripts['TMPro.TextMeshProUGUI'] = ROOT / 'unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TextMeshProUGUI.cs'
    audio_path = ROOT / 'assets/reference/official-map/map12-ui-audio-source-manifest.json'
    audios = json.loads(audio_path.read_bytes())['events']
    target_audio_path = ROOT / 'assets/reference/official-map/map12-team-target-audio-source-manifest.json'
    target_audio = json.loads(target_audio_path.read_bytes())
    audio_output, = [row for row in target_audio['outputs'] if row['path'].endswith('.wav')]
    audios.append(dict(name=target_audio['name'], id=target_audio['id'], **audio_output))
    schemas = NativeSchemas(ROOT / '.tools/source-packages/current/871_156598/official-code')
    install(schemas, ROOT / '.tools/source-packages/current/871_156598/official-code')
    redirects = {row['nativeGuid']: output_guid(row['name']) for row in native['panels'] if row['name'] in (*NAMES, 'TeamPanelTarget', 'TeamPanelTargetItem', 'TeamPanelTargetSubItem')}
    outputs, dependencies, animation_source_path = animations(apk, objects, references)
    coverage = []
    native_sprites = NativeTeamSprites(apk)
    wanted_texts = {'TeamTargetCatalog.None', 'TeamTargetCatalog.BattleStele'}
    for name in NAMES:
        panel, = [row for row in native['panels'] if row['name'] == name]
        if sha(panel['yaml'].encode()) != panel['sha256']:
            raise ValueError('Original target YAML changed')
        docs = documents(panel['yaml'])
        bindings = {int(row['fileId']): rows[row['original']] for row in native['bindings'] if row['panel'] == name}
        event_bindings = {}
        for key, (kind, fields) in docs.items():
            if kind == 'Animator':
                source = bindings[key]
                obj, reference = external(source['fields']['m_Controller'])
                if obj is not None:
                    redirects[fields['m_Controller']['guid']] = dependencies[reference['identity']]['nativeGuid']
                continue
            if kind != 'MonoBehaviour':
                continue
            source = bindings[key]
            script = scripts[source['scriptType']]
            redirects[fields['m_Script']['guid']] = guid(script.with_name(script.name + '.meta'))
            if source['scriptType'] == 'MultiLanguageText' and source['fields']['myKey']:
                wanted_texts.add(source['fields']['myKey'])
            if source['scriptType'] in ('UnityEngine.UI.Image', 'MirrorImage', 'SlicedFilledImage'):
                obj, reference = external(source['fields']['m_Sprite'])
                if obj is None:
                    continue
                sprite_row = native_sprites.sprite(obj)
                redirects[fields['m_Sprite']['guid']] = sprite_row['guid']
                dependencies[reference['identity']] = dict(path=sprite_row['path'], sha256=sprite_row['sha256'],
                    nativeSpritePath=sprite_row['path'], nativeGuid=sprite_row['guid'],
                    name=sprite_row['expected']['name'], spriteGeometry=sprite_row['expected'])
            elif source['scriptType'] == 'UnityEngine.UI.RawImage':
                obj, reference = external(source['fields']['m_Texture'])
                if obj is not None:
                    texture = obj.read()
                    image = texture.image.convert('RGBA')
                    if (image.width != reference['decodedPixels']['width']
                            or image.height != reference['decodedPixels']['height']
                            or sha(image.tobytes()) != reference['decodedPixels']['rgba_sha256']):
                        raise ValueError('Original default portrait pixels changed')
                    folder = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/Sprites'
                    path = folder / ('DefaultPortrait-' + sha(reference['identity'].encode())[:10] + '.png')
                    stream = io.BytesIO(); image.save(stream, format='PNG')
                    outputs[path] = stream.getvalue()
                    target_guid = output_guid('texture:' + reference['identity'])
                    template = ROOT / 'unity/Assets/Official/World/Map/12/UI/Sprites/BtnCheck_Nor-2bb9ad0ab5.png.meta'
                    meta = template.read_text(encoding='utf-8')
                    for key, value in (('guid', target_guid), ('textureType', 0), ('spriteMode', 0),
                        ('sRGBTexture', int(texture.m_ColorSpace == 1)), ('enableMipMap', int(texture.m_MipCount > 1)),
                        ('filterMode', texture.m_TextureSettings.m_FilterMode), ('aniso', texture.m_TextureSettings.m_Aniso),
                        ('mipBias', texture.m_TextureSettings.m_MipBias), ('wrapU', texture.m_TextureSettings.m_WrapU),
                        ('wrapV', texture.m_TextureSettings.m_WrapV), ('wrapW', texture.m_TextureSettings.m_WrapW)):
                        meta, count = re.subn(r'(?m)^(\s*' + key + r':) .*$', r'\g<1> ' + str(value), meta)
                        if count != 1:
                            raise ValueError('Original default portrait texture setting field differs: ' + key)
                    outputs[path.with_name(path.name + '.meta')] = meta.encode()
                    redirects[fields['m_Texture']['guid']] = target_guid
                    dependencies[reference['identity']] = dict(path=source_path(path), sha256=sha(outputs[path]),
                        decodedPixels=reference['decodedPixels'], textureSampler=dict(
                            filter=texture.m_TextureSettings.m_FilterMode, aniso=texture.m_TextureSettings.m_Aniso,
                            mipBias=texture.m_TextureSettings.m_MipBias, wrapU=texture.m_TextureSettings.m_WrapU,
                            wrapV=texture.m_TextureSettings.m_WrapV, wrapW=texture.m_TextureSettings.m_WrapW))
            elif source['scriptType'] == 'TMPro.TextMeshProUGUI':
                for field, index, suffix in (('m_fontAsset', 0, '.asset'), ('m_sharedMaterial', 1, '.mat')):
                    obj, reference = external(source['fields'][field])
                    choices = [(receipt, font_name) for receipt, font_name in zip(fonts, font_names)
                               if receipt['inputs'][index]['object_sha256'] == sha(obj.get_raw_data())]
                    if len(choices) != 1:
                        raise ValueError('Find exact original target font/material')
                    _, font_name = choices[0]
                    path = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team' / (font_name + suffix)
                    redirects[fields[field]['guid']] = guid(path.with_name(path.name + '.meta'))
            elif source['scriptType'] == 'PopupWindow':
                for field in ('mShowClip', 'mHideClip', 'mLoopClip'):
                    obj, reference = external(source['fields'][field])
                    if obj is not None:
                        redirects[fields[field]['guid']] = dependencies[reference['identity']]['nativeGuid']
            elif source['scriptType'] in ('ButtonSound', 'UIClickSound', 'PopupWindowSound'):
                event_bindings[key] = {}
                event_fields = {'ButtonSound': ('ClickEvent',), 'UIClickSound': ('AkEvent',),
                                'PopupWindowSound': ('ShowSound', 'HideSound')}[source['scriptType']]
                for field in event_fields:
                    payload = source['fields'][field]
                    if payload['idInternal'] or payload['valueGuidInternal']:
                        raise ValueError('Resolve original inline panel sound event')
                    obj, reference = external(payload['WwiseObjectReference'])
                    if obj is None:
                        event_bindings[key][field] = None
                        continue
                    event, _ = schemas.read(obj)
                    event_rows = [row for row in audios if row['id'] == event['id'] and row['name'] == event['objectName']]
                    if len(event_rows) != 1:
                        raise ValueError('Locate original panel PCM: ' + str(event))
                    event_row = event_rows[0]
                    path = ROOT / event_row['path']
                    if sha(path.read_bytes()) != event_row['sha256']:
                        raise ValueError('Original panel PCM changed')
                    event_bindings[key][field] = guid(path.with_name(path.name + '.meta'))
                    dependencies[reference['identity']] = dict(path=source_path(path), sha256=event_row['sha256'])
        blocks = re.split(r'(?m)(?=^--- !u!\d+ &-?\d+\n)', panel['yaml'])
        result = [blocks[0]]
        for block in blocks[1:]:
            key = int(re.match(r'--- !u!\d+ &(-?\d+)\n', block)[1])
            block = re.sub(r'guid: ([a-f0-9]{32})', lambda match: 'guid: ' + redirects.get(match[1], match[1]), block)
            if key in event_bindings:
                for field, audio_guid in event_bindings[key].items():
                    replacement = '{fileID: 8300000, guid: ' + audio_guid + ', type: 3}' if audio_guid else '{fileID: 0}'
                    block, count = re.subn(r'(?m)^  ' + field + r':\n(?:    .*\n)+',
                        '  ' + field + ': ' + replacement + '\n', block)
                    if count != 1:
                        raise ValueError('Original panel sound field changed: ' + field)
            prefab_guids = {output_guid(target_name) for target_name in (*NAMES, 'TeamPanelTarget', 'TeamPanelTargetItem', 'TeamPanelTargetSubItem')}
            block = re.sub(r'(\{fileID: -?\d+, guid: ([a-f0-9]{32}), )type: 2\}',
                lambda match: match[1] + 'type: 3}' if match[2] in prefab_guids else match[0], block)
            for original_type, translated in (('StateGameObjectConfig', 'WorldStateGameObjectConfig'),
                                               ('StateUIColorConfig', 'WorldStateUIColorConfig')):
                block = block.replace('class: ' + original_type + '\n        ns:\n        asm: Framework',
                    'class: ' + translated + '\n        ns: Zjcs.Official.WorldUI\n        asm: Zjcs.Official.WorldUI')
            result.append(block)
        raw = ''.join(result).encode()
        known = set(redirects.values()) | {value for fields in event_bindings.values() for value in fields.values() if value}
        unbound = set(re.findall(r'guid: ([a-f0-9]{32})', raw.decode())) - known
        if unbound:
            raise ValueError('Resolve all original target native dependencies: ' + str(unbound))
        if set(documents(raw.decode())) != set(docs):
            raise ValueError('Original target node/component coverage changed')
        path = DEST / (name + '.prefab')
        outputs[path] = raw
        outputs[path.with_suffix('.prefab.meta')] = ('fileFormatVersion: 2\nguid: ' + output_guid(name) + '\nPrefabImporter:\n  externalObjects: {}\n').encode()
        coverage.append(dict(name=name, nodes=sum(kind == 'GameObject' for kind, _ in docs.values()), objects=len(docs)))
    language_path = ROOT / 'assets/reference/official-map/localization-source-manifest.json'
    language_rows = json.loads(language_path.read_bytes())['inputs']
    hashes = {xxhash.xxh64_intdigest(key.encode()): key for key in wanted_texts}
    texts = {}
    for row in language_rows.values():
        raw = apk.archive.read(row['source']['path'])
        if sha(raw) != row['sha256']:
            raise ValueError('Original target localization input changed')
        count, = struct.unpack_from('<i', raw)
        start = 4 + count * 12
        for number in range(count):
            identity, offset = struct.unpack_from('<Qi', raw, 4 + number * 12)
            key = hashes.get(identity)
            if key is not None and key not in texts:
                length, = struct.unpack_from('<H', raw, start + offset)
                texts[key] = raw[start + offset + 2:start + offset + 2 + length].decode('utf-8')
    # LocalizationTextService.TryGetOriginalText returns the unchanged key when
    # it is absent. The original count label deliberately uses the literal {0}.
    literal_fallbacks = sorted(wanted_texts - set(texts))
    for key in literal_fallbacks:
        texts[key] = key
    text_path = DEST / 'panel-texts.json'
    outputs[text_path] = encode(dict(manifest=apk.version, texts=[dict(key=key, value=texts[key]) for key in sorted(texts)]))
    outputs[text_path.with_name(text_path.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + output_guid('panel-texts') + '\nTextScriptImporter:\n  externalObjects: {}\n').encode()
    geometry_path = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-panel-sprite-geometry.json'
    outputs.update(native_sprites.outputs)
    outputs[geometry_path] = encode(dict(manifest=apk.version, sprites=[row['expected'] for key, row in sorted(native_sprites.sprites.items())],
        textures=[dict(path=row['path'].removeprefix('unity/'), **{key: row[key] for key in ('original_fields', 'pixels_sha256', 'mips', 'format')}) for key, row in sorted(native_sprites.textures.items())]))
    outputs[geometry_path.with_name(geometry_path.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + output_guid('panel-sprite-geometry') + '\nTextScriptImporter:\n  externalObjects: {}\n').encode()
    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))
    value = dict(format=1, manifest=apk.version, mapClassId=12,
        inputs=[record(path) for path in (ORIGINAL, NATIVE, ui_path, audio_path, target_audio_path, language_path, animation_source_path, *font_paths)],
        languageInputs=language_rows,
        unchangedKeyFallbacks=literal_fallbacks,
        localizationImplementation=record(ROOT / '.tools/source-packages/current/871_156598/official-code/framework-firstpass-source/Framework/LocalizationTextService.cs'),
        dependencies=[dict(identity=key, **row) for key, row in sorted(dependencies.items())],
        scriptBindings=[dict(original=name, **record(path), meta_sha256=sha(path.with_name(path.name + '.meta').read_bytes()))
                        for name, path in sorted(scripts.items())],
        coverage=coverage, outputs=[dict(path=source_path(path), sha256=sha(raw)) for path, raw in outputs.items()],
        nativeSprites=list(native_sprites.sprites.values()), nativeTextures=list(native_sprites.textures.values()),
        nativeSpriteImporter=record(ROOT / 'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.TeamPanelImport.cs'),
        adapters=[record(Path(__file__).resolve()), record(ROOT / 'scripts/prepare_unity_map12_team_panel_sprites.py'),
                  record(ROOT / 'scripts/prepare_unity_map12_team_panel_animation.py'), record(ROOT / 'scripts/official_animation.py'),
                  record(ROOT / 'scripts/prepare_unity_map13_native_scene.py')],
        operation='Keep the full original three native prefab hierarchies, transforms, component order, fields '
        'and managed state references. Remap real Windows scripts, serialized fonts/materials, original '
        'Wwise click PCM and cross-prefab references. Resolve the original SpriteAtlas render-data entry '
        'into each source Sprite, bake its original atlas UV transform into the UV stream and preserve '
        'source position/index bytes, logical rect, pivot, border, PPU and fractional trim offsets. '
        'Decode complete original ASTC atlas mip payloads to native RGBA32 Texture2D assets, preserving '
        'samplers and color space. GPU readback verifies every decoded mip and native Sprite fields; '
        'no integer-cropped sprite image substitutes the atlas. Native Sprite references remain type 2. '
        'Original popup clips and button controller retain the verified AssetRipper native data; '
        'derived incoming tangents allow only the documented one-float32-ULP decoder rounding difference.',
        limitations=['Formal full TeamPanel/local NPC/battle/save integration remains pending.',
                     'Runtime fixtures inject isolated data services and the original loading portrait; '
                     'they do not establish formal player/NPC portraits, equipped skills, save or battle integration.'])
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original target native output drift: ' + path.name)
    raw = encode(value)
    if write:
        RECEIPT.write_bytes(raw)
    elif not RECEIPT.is_file() or RECEIPT.read_bytes() != raw:
        raise ValueError('Original target native receipt drift')
    print('Original team panel native prefabs verified: ' + str(coverage), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
