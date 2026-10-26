"""Restore APK Boot/Launch prefab fields and their artwork as native Windows assets.

No Android bundles are loaded at runtime. The APK is checked before extraction;
all PPtrs are resolved against its CAB files and written as explicit bindings.
"""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import re
import struct
import zipfile

from official_apk import Apk, ROOT, UnityPy
from official_source import OfficialSource
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

ART = ROOT / 'unity/Assets/Official/Startup'
REF = ROOT / 'assets/reference/startup'
SUPPORTED = set(('Canvas CanvasScaler GraphicRaycaster RectMask2D CanvasGroup '
                 'Image SlicedFilledImage MirrorImage NoDrawingRayCast TextMeshProUGUI '
                 'ContentSizeFitter AspectRatioFitter Slider Button LayoutElement '
                 'HorizontalLayoutGroup VerticalLayoutGroup ScrollRect Mask '
                 'LaunchPanel LaunchUIRoot AgeTipGo AgeTipsDialog').split())


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def build():
    source = OfficialSource('854_156383')
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256:
            raise ValueError('Pinned APK SHA256 differs')
    archive = zipfile.ZipFile(source.apk)
    env = UnityPy.Environment()
    provenance, inputs, containers = {}, [], {}
    # APK lookup precedes any cloud lookup. All required Boot/Launch payloads
    # and their CAB dependency closure are packaged in this APK.
    for package in ['Boot', 'Launch']:
        for bundle in source.manifest(package, archive)['bundles']:
            entry = 'assets/yoo/' + package + '/' + bundle['file']
            raw = archive.read(entry)
            if len(raw) != bundle['size'] or hashlib.md5(raw).hexdigest() != bundle['hash']:
                raise ValueError('APK manifest mismatch: ' + entry)
            loaded = env.load_file(Apk.decode(raw), name=bundle['name'])
            record = dict(package=package, bundle=bundle['name'], apk_entry=entry,
                          bytes=len(raw), md5=bundle['hash'], sha256=digest(raw))
            for asset in loaded.files.values():
                if hasattr(asset, 'objects'):
                    provenance[asset.name] = record
                    containers.update(asset.container)
    used = set()
    def record(obj):
        row = provenance[obj.assets_file.name]
        used.add(row['bundle'])
        return dict(cab=obj.assets_file.name, path_id=str(obj.path_id), bundle=row['bundle'],
                    object_sha256=digest(obj.get_raw_data()))

    # Use the actual Unity/UGUI/TMP reference assemblies, rather than partial
    # schema stubs, so every serialized UGUI field survives conversion.
    editor = Path(json.loads((ROOT / 'unity-tools.json').read_text())['editorInstallPath']) / 'Editor/Data'
    schemas = ScriptSchemas.__new__(ScriptSchemas)
    schemas.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    schemas.cache, schemas.records = {}, {}
    loaded_dlls = set()
    for folder in [ROOT / '.tools/source-packages/official-code/managed/decoded',
                   editor / 'Managed/UnityEngine', ROOT / 'unity/Library/Bee/PlayerScriptAssemblies',
                   editor / 'NetStandard/ref/2.1.0', editor / 'MonoBleedingEdge/lib/mono/4.5']:
        for dll in folder.glob('*.dll'):
            if dll.name in loaded_dlls:
                continue
            if folder.name == 'PlayerScriptAssemblies' and dll.name not in ['UnityEngine.UI.dll', 'Unity.TextMeshPro.dll']:
                continue
            schemas.generator.load_dll(dll.read_bytes())
            loaded_dlls.add(dll.name)
    # Reuse the verified native TMP schema from element-feedback migration.
    font_schemas = ScriptSchemas()
    font_schemas.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    font_schemas.generator.load_local_dll_folder(str(editor / 'Managed/UnityEngine'))
    font_schemas.generator.load_local_dll_folder(str(ROOT / 'unity/Library/ScriptAssemblies'))
    font_schemas.generator.load_dll((editor / 'NetStandard/ref/2.1.0/netstandard.dll').read_bytes())
    font_schemas.generator.load_dll((editor / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll').read_bytes())
    # The APK's TMP fork serializes three PSD import settings before m_Version,
    # and no editor-only source Font PPtr. Verify the entire payload with this
    # layout; using the stock TMP layout silently misreads the version string.
    font_schemas.tree('Unity.TextMeshPro', 'TMPro.TMP_FontAsset')
    font_key = 'Unity.TextMeshPro:TMPro.TMP_FontAsset'
    font_nodes, skip = [], False
    for item in font_schemas.cache[font_key]:
        if item['level'] == 1:
            skip = item['name'] == 'm_SourceFontFile_EditorRef'
        if skip:
            continue
        if item['level'] == 1 and item['name'] == 'm_Version':
            font_nodes.extend(dict(level=1, type='int', name='psd_import_setting_' + str(i), flags=0) for i in range(3))
        font_nodes.append(item)
    font_schemas.cache[font_key] = font_nodes
    effect_schemas = ScriptSchemas()

    def asset(suffix):
        return next(ptr.deref() for path, ptr in containers.items() if path.endswith(suffix))

    default_manifest = {b['name']: b for b in source.manifest('DefaultPackage', archive)['bundles']}
    def default_asset(bundle_name, asset_path):
        bundle = default_manifest[bundle_name]
        entry = 'assets/yoo/DefaultPackage/' + bundle['file']
        raw = archive.read(entry)
        if len(raw) != bundle['size'] or hashlib.md5(raw).hexdigest() != bundle['hash']:
            raise ValueError('Original streaming font bundle checksum differs')
        loaded = env.load_file(Apk.decode(raw), name=bundle_name)
        for file in loaded.files.values():
            if hasattr(file, 'objects'):
                provenance[file.name] = dict(package='DefaultPackage', bundle=bundle_name, apk_entry=entry,
                                            bytes=len(raw), md5=bundle['hash'], sha256=digest(raw))
                for path, pointer in file.container.items():
                    if path == asset_path:
                        result = pointer.deref()
                        record(result)
                        return result
        raise ValueError('Original streaming font asset missing: ' + asset_path)

    font_config = json.loads(default_asset('defaultpackage_assets_textmesh_pro_rawfonts_fontassetfontconfig.bundle',
                                          'Assets/TextMesh Pro/RawFonts/FontAssetFontConfig.json').read().m_Script)
    font_info = font_config['FontAssetInfoDict']['FZY4K SDF - dynamic']
    font_obj = default_asset('defaultpackage_assets_textmesh_pro_rawfonts_' + Path(font_info['RawFontPath']).stem.lower() + '.bundle',
                             font_info['RawFontPath'])
    font_bytes = font_obj.read().m_Script.encode('utf8', 'surrogateescape')
    font_path = ROOT / 'unity/Assets/Official/Commerce/Art/fonts/FZY4K.ttf'
    if len(font_bytes) != font_info['Length'] or hashlib.md5(font_bytes).hexdigest() != font_info['Hash'] or font_bytes != font_path.read_bytes():
        raise ValueError('Explicit local startup TTF differs from the original streaming font')

    refs, reference_ids, objects, components = [], {}, {}, []
    sprites, animations, localized = [], [], set()
    def ref(obj):
        if obj is None:
            return {'instanceID': 0}
        if obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Script.read().m_ClassName == 'MultiLanguageText':
            record(obj)
            obj = next(c.component.deref() for c in obj.read(check_read=False).m_GameObject.read().m_Component
                       if c.component.deref().type.name == 'MonoBehaviour'
                       and c.component.deref().read(check_read=False).m_Script.read().m_ClassName == 'TextMeshProUGUI')
        key = obj.assets_file.name + ':' + str(obj.path_id)
        if key not in reference_ids:
            index = len(refs) + 1
            reference_ids[key] = index
            objects[index] = obj
            refs.append(dict(id=index, kind=obj.type.name, source=record(obj)))
        return {'instanceID': reference_ids[key]}

    def convert(value, owner):
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                return ref(PPtr(**value, assetsfile=owner.assets_file).deref()) if value['m_PathID'] else ref(None)
            return {k: convert(v, owner) for k, v in value.items()}
        if isinstance(value, list):
            return [convert(v, owner) for v in value]
        return value

    roots, nodes = [], []
    def walk(obj, parent=-1):
        go = obj.read()
        parts = [c.component.deref() for c in go.m_Component]
        tr = next(c for c in parts if c.type.name in ['Transform', 'RectTransform'])
        index = len(nodes)
        node = dict(name=go.m_Name, active=bool(go.m_IsActive), layer=go.m_Layer, parent=parent,
                    id=ref(obj)['instanceID'], transform_id=ref(tr)['instanceID'], rect=tr.type.name == 'RectTransform',
                    transform_json='', components=[], source=record(obj))
        nodes.append(node)
        data = tr.read_typetree()
        node['transform_json'] = json.dumps({k: v for k, v in data.items()
                                            if k not in ['m_GameObject', 'm_Father', 'm_Children']})
        for c in parts:
            if c.type.name in ['Transform', 'RectTransform', 'CanvasRenderer']:
                continue
            cls = c.read(check_read=False).m_Script.read().m_ClassName if c.type.name == 'MonoBehaviour' else c.type.name
            record(c)
            if cls not in SUPPORTED:
                # Kept as provenance; online service/DI components are bound by
                # the offline host after native prefab restoration.
                if cls in ['Animation', 'Animator']:
                    components.append((node, c, cls))
                elif cls in ['LanguageRegionImage', 'MultiLanguageText', 'TMPEffect', 'SafeArea', 'CanvasWidthAdjuster']:
                    components.append((node, c, cls))
                continue
            data = schemas.read(c) if c.type.name == 'MonoBehaviour' else c.read_typetree()
            if c.type.name == 'MonoBehaviour':
                script = c.read(check_read=False).m_Script.read()
                schema_key = script.m_AssemblyName + ':' + (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
                booleans = {n['name'] for n in schemas.cache[schema_key] if n['type'] == 'UInt8'}
                def boolean_fields(value):
                    if isinstance(value, dict):
                        return {k: bool(v) if k in booleans and isinstance(v, int) else boolean_fields(v) for k, v in value.items()}
                    if isinstance(value, list):
                        return [boolean_fields(v) for v in value]
                    return value
                data = boolean_fields(data)
            data = {k: v for k, v in data.items() if k not in ['m_GameObject', 'm_Script', 'm_Name']}
            if 'm_Enabled' in data:
                data['m_Enabled'] = bool(data['m_Enabled'])
            cfg = dict(id=ref(c)['instanceID'], type=cls, json='', source=record(c))
            cfg['json'] = json.dumps(convert(data, c), ensure_ascii=False)
            node['components'].append(cfg)
        for child in tr.read().m_Children:
            walk(child.read().m_GameObject.deref(), index)
        return index

    for name in ['BootSplash', 'ThemeCanvas', 'LaunchRoot', 'AgeTipsDialog']:
        obj = asset('/' + name + '.prefab')
        if name == 'LaunchRoot':
            obj = next(p.read().m_GameObject.deref() for p in obj.read().m_Component[0].component.read().m_Children
                       if p.read().m_GameObject.read().m_Name == 'LaunchCanvas')
        roots.append(dict(name=name, node=walk(obj)))

    for node, obj, cls in components:
        if cls == 'MultiLanguageText':
            cfg = schemas.read(obj)
            node['text_key'] = cfg['myKey']
            if cfg['m_Enabled'] and '{' not in cfg['myKey']:
                localized.add(cfg['myKey'])
        elif cls == 'LanguageRegionImage':
            cfg = schemas.read(obj)
            config = PPtr(**cfg['mConfig'], assetsfile=obj.assets_file).deref()
            data = schemas.read(config)
            record(config)
            # LanguageType.zh_CN == 0, with the original config fallback.
            region = next(p['Value'] for p in data['mImageDict']['Pairs'] if p['Key'] == 0)
            pointer = next(p['Value'] for p in region['Pairs'] if p['Key'] == 0)
            node['language_sprite_id'] = ref(PPtr(**pointer, assetsfile=config.assets_file).deref())['instanceID']
        elif cls == 'TMPEffect':
            cfg = effect_schemas.read(obj)
            node['text_effect'] = json.dumps({k: v for k, v in cfg.items() if not k.startswith('m_')})
        elif cls in ['SafeArea', 'CanvasWidthAdjuster']:
            node[cls] = json.dumps(convert({k: v for k, v in schemas.read(obj).items()
                                           if k not in ['m_GameObject', 'm_Script', 'm_Name']}, obj))
        elif cls in ['Animation', 'Animator']:
            if cls == 'Animation':
                clips = obj.read().m_Animations
            else:
                controller = obj.read().m_Controller
                if not controller.path_id:
                    continue
                record(controller.deref())
                from commerce_animation import animator
                class ControllerReader:
                    input = staticmethod(record)
                    @staticmethod
                    def clip(value, root_path, root):
                        return value.read().m_Name
                graph = animator(ControllerReader(), controller, dict(path=node['name']))
                original_controller = controller.deref().read_typetree()
                names = dict(original_controller['m_TOS'])
                parameters = original_controller['m_Controller']['m_Values']['data']['m_ValueArray']
                if any(p['m_Type'] != 9 for p in parameters):
                    raise ValueError('Unexpected original startup animator parameter type')
                graph['parameters'] = [names[p['m_ID']] for p in parameters]
                layer = original_controller['m_Controller']['m_LayerArray'][0]['data']
                graph['layer'] = names[layer['m_Binding']]
                graph['weight'] = layer['m_DefaultWeight']
                node['animator'] = json.dumps(graph)
                clips = controller.read().m_AnimationClips
            def tree(i):
                n = nodes[i]
                return dict(name=n['name'], path=n['name'] if n['parent'] < 0 else path(i),
                            children=[tree(j) for j, x in enumerate(nodes) if x['parent'] == i])
            def path(i):
                n = nodes[i]
                return (path(n['parent']) + '/' if n['parent'] >= 0 else '') + n['name']
            for ptr in clips:
                if not ptr.path_id:
                    continue
                clip = ptr.read()
                tracks = []
                for curve in clip.m_FloatCurves:
                    tracks.append(dict(path=curve.path, property=curve.attribute,
                                       keys=[[k.time, k.value, k.inSlope, k.outSlope] for k in curve.curve.m_Curve]))
                for field, prop in [('m_ScaleCurves', 'm_LocalScale'), ('m_PositionCurves', 'm_LocalPosition')]:
                    for curve in getattr(clip, field):
                        for axis in 'xyz':
                            tracks.append(dict(path=curve.path, property=prop + '.' + axis,
                                               keys=[[k.time, getattr(k.value, axis), getattr(k.inSlope, axis), getattr(k.outSlope, axis)]
                                                     for k in curve.curve.m_Curve]))
                if not tracks:
                    from official_animation import mecanim_tracks
                    decoded, duration, loop, unknown = mecanim_tracks(ptr.deref(), tree(nodes.index(node)))
                    if unknown:
                        raise ValueError('Unconverted startup animation bindings: ' + repr(unknown))
                    for track in decoded:
                        relative = track['path'].removeprefix(path(nodes.index(node))).removeprefix('/')
                        prop = track['property'].replace('scale.', 'm_LocalScale.').replace('position.', 'm_LocalPosition.')
                        tracks.append(dict(path=relative, property=prop, keys=track['keys']))
                for track in tracks:
                    track['keys'] = [dict(time=k[0], value=k[1], inSlope=k[2] if math.isfinite(k[2]) else 0,
                                          outSlope=k[3] if math.isfinite(k[3]) else 0,
                                          step=not math.isfinite(k[2]) or not math.isfinite(k[3])) for k in track['keys']]
                animations.append(dict(node=nodes.index(node), name=clip.m_Name, tracks=tracks, source=record(ptr.deref())))

    localized.update(['Game_Initializing', 'YooAsset_Init'])
    from npc_travel_language import language
    texts, language_sources = language(localized)
    missing = localized - texts.keys()
    if missing:
        raise ValueError('Official Chinese text missing: ' + repr(missing))
    for node in nodes:
        key = node.get('text_key')
        if key in texts:
            for component in node['components']:
                if component['type'] == 'TextMeshProUGUI':
                    cfg = json.loads(component['json']); cfg['m_text'] = texts[key]
                    component['json'] = json.dumps(cfg, ensure_ascii=False)

    # Resolve every referenced external object, including language config PPtrs.
    for row in refs:
        obj = objects[row['id']]
        if obj.type.name == 'Sprite':
            sprite = obj.read()
            filename = re.sub(r'[^a-zA-Z0-9_-]', '_', sprite.m_Name) + '-' + row['source']['object_sha256'][:10] + '.png'
            target = ART / 'Sprites' / filename
            target.parent.mkdir(parents=True, exist_ok=True)
            sprite.image.save(target)
            rd = obj.read_typetree()['m_RD']
            vertex = rd['m_VertexData']
            if vertex['m_Channels'][0] != dict(stream=0, offset=0, format=0, dimension=3):
                raise ValueError('Unexpected original startup sprite vertex stream')
            geometry = [dict(zip(('x', 'y'), struct.unpack_from('<2f', bytes(vertex['m_DataSize']), i * 12)))
                        for i in range(vertex['m_VertexCount'])]
            triangles = list(struct.unpack('<' + 'H' * (len(rd['m_IndexBuffer']) // 2), bytes(rd['m_IndexBuffer'])))
            row.update(path=target.relative_to(ROOT / 'unity').as_posix(),
                       vertices=geometry, triangles=triangles,
                       border=[getattr(sprite.m_Border, a) for a in 'xyzw'],
                       pivot=[sprite.m_Pivot.x, sprite.m_Pivot.y], ppu=sprite.m_PixelsToUnits,
                       sha256=digest(target.read_bytes()))
            sprites.append(row)
        elif obj.type.name == 'MonoBehaviour':
            script = obj.read(check_read=False).m_Script.read()
            if script.m_ClassName == 'TMP_FontAsset':
                cfg = font_schemas.read(obj)
                retained = ['m_FaceInfo', 'm_GlyphTable', 'm_CharacterTable', 'm_UsedGlyphRects', 'm_FreeGlyphRects',
                            'm_FontFeatureTable', 'normalStyle', 'normalSpacingOffset', 'boldStyle', 'boldSpacing', 'italicStyle', 'tabSize']
                if cfg['m_Version'] != '1.1.0':
                    raise ValueError('Unexpected original TMP font version')
                row.update(kind='Font', name=cfg['m_Name'], json=json.dumps({k: cfg[k] for k in retained}),
                           width=cfg['m_AtlasWidth'], height=cfg['m_AtlasHeight'], padding=cfg['m_AtlasPadding'],
                           renderMode=cfg['m_AtlasRenderMode'], population=cfg['m_AtlasPopulationMode'],
                           multiAtlas=bool(cfg['m_IsMultiAtlasTexturesEnabled']),
                           clearOnBuild=False,
                           atlas_ids=[ref(PPtr(**p, assetsfile=obj.assets_file).deref())['instanceID'] for p in cfg['m_AtlasTextures']],
                           material_id=ref(PPtr(**cfg['material'], assetsfile=obj.assets_file).deref())['instanceID'])
        elif obj.type.name == 'Material':
            mat = obj.read()
            shader = mat.m_Shader.read()
            if shader.m_ParsedForm.m_Name != 'Hidden/JSZX/TextMeshPro':
                raise ValueError('Unexpected startup material shader: ' + shader.m_ParsedForm.m_Name)
            from prepare_battle_world_shaders import shader_text
            code, variants = shader_text(shader, 'TextMeshPro', ('UNITY_UI_CLIP_RECT', 'UNITY_UI_ALPHACLIP', 'OUTLINE_ON'))
            code = code.replace('Zjcs/Combat/WorldTextMeshPro', 'Zjcs/Startup/TextMeshPro')
            shader_path = ROOT / 'unity/Assets/Game/Runtime/Official/Startup/StartupTextMeshPro.shader'
            shader_path.write_text(code, encoding='utf8')
            row.update(name=mat.m_Name, shader=mat.m_Shader.read().m_ParsedForm.m_Name,
                       native_shader='Zjcs/Startup/TextMeshPro', shader_variants=variants,
                       texture_id=next((ref(t.m_Texture.deref())['instanceID'] for k, t in mat.m_SavedProperties.m_TexEnvs
                                        if k == '_MainTex' and t.m_Texture.path_id), 0),
                       keywords=mat.m_ValidKeywords,
                       floats=[dict(key=k, value=v) for k, v in mat.m_SavedProperties.m_Floats],
                       colors=[dict(key=k, value=dict(r=v.r, g=v.g, b=v.b, a=v.a)) for k, v in mat.m_SavedProperties.m_Colors])
            record(mat.m_Shader.deref())
        elif obj.type.name == 'Texture2D':
            texture = obj.read()
            target = ART / 'Fonts' / (str(row['id']) + '.png')
            target.parent.mkdir(parents=True, exist_ok=True)
            texture.image.save(target)
            settings = texture.m_TextureSettings
            row.update(path=target.relative_to(ROOT / 'unity').as_posix(), sha256=digest(target.read_bytes()),
                       sRGB=texture.m_ColorSpace == 1, filter=settings.m_FilterMode,
                       wrapU=settings.m_WrapU, wrapV=settings.m_WrapV, aniso=settings.m_Aniso,
                       bias=settings.m_MipBias)

    result = dict(manifest=source.version, roots=roots, nodes=nodes, refs=refs, animations=animations,
                  texts=[dict(key=k, value=v) for k, v in sorted(texts.items())])
    write(ART / 'startup-ui.json', result)
    inputs = [row for row in provenance.values() if row['bundle'] in used]
    inputs = list({row['bundle']: row for row in inputs}.values())
    effect_evidence = json.loads((REF.parent / 'battle-hud/element-feedback-source-manifest.json').read_text(encoding='utf8'))
    receipt = dict(manifest=source.version, apk_sha256=source.apk_sha256,
                   source_unity='2022.3.57f1', target_unity='2022.3.57f1c2',
                   operation='Deserialize original BootSplash, ThemeCanvas, LaunchRoot/LaunchCanvas and AgeTipsDialog; restore native components with exact PPtr bindings; decode original sprites and animation curves.',
                   inputs=inputs, language_sources=language_sources,
                   code_sources=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in [
                       ROOT / '.tools/source-packages/official-code/launch-source/LaunchPanel.cs',
                       ROOT / '.tools/source-packages/official-code/launch-source/LaunchUIRoot.cs',
                       ROOT / '.tools/source-packages/official-code/launch-source/ThemeCanvas.cs',
                       ROOT / '.tools/source-packages/official-code/launch-source/Launch/AgeTipGo.cs',
                       ROOT / '.tools/source-packages/official-code/launch-source/AgeTipsDialog.cs',
                       ROOT / '.tools/source-packages/official-code/framework-firstpass-source/CanvasWidthAdjuster.cs',
                       ROOT / '.tools/source-packages/official-code/framework-firstpass-source/PopupWindow.cs']],
                   native_text_effect={k: effect_evidence[k] for k in ['native_methods', 'native_image_sha256', 'metadata_sha256', 'native_outline']},
                   source_font=dict(path=font_path.relative_to(ROOT).as_posix(), sha256=digest(font_bytes), bytes=len(font_bytes)),
                   converters=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in [
                       Path(__file__), ROOT / 'scripts/prepare_battle_world_shaders.py', ROOT / 'scripts/commerce_animation.py',
                       ROOT / 'unity/Assets/Game/Editor/Startup/OfficialStartupTools.cs']],
                   outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes()))
                            for p in sorted(ART.rglob('*')) if p.is_file() and p.suffix != '.meta' and p.name != 'source-manifest.json'])
    write(REF / 'source-manifest.json', receipt)
    write(ART / 'source-manifest.json', receipt)
    print('Restored', len(nodes), 'nodes,', len(sprites), 'sprites,', len(animations), 'animations')
    print('Fonts:', [(r['id'], r.get('name')) for r in refs if r['kind'] == 'Font'])


if __name__ == '__main__':
    build()
