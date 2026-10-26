"""Restore configured world-space damage-number prefabs and their exact bitmap fonts."""
import hashlib
import io
import json
import struct
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.classes.PPtr import PPtr
from prepare_unity_battle_assets import normalize_world_float_node, localize_hud_node, normalize_hud_animation
from official_animation import mecanim_tracks

OUT = ROOT/'unity/Assets/Official/Combat/Numbers/Resources/Combat/Numbers'


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def build():
    apk = Apk(manifest='854_156383')
    schemas = ScriptSchemas(assembly_overrides={'UnityEngine.TextRenderingModule':
        Path('D:/Tool/Unity/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll')})
    source_path = ROOT/'assets/official/battle-hud/world-ui.json'
    source = json.loads(source_path.read_bytes())
    rules_path = ROOT/'assets/official/battle-hud/rules.json'
    settings = json.loads(rules_path.read_bytes())['damage_settings']
    from refresh_current_localization import language
    import xxhash
    language_values, language_sources = language(ROOT/'.tools/source-packages/history/854_156383/WwiseBnk')
    inputs, fonts, prefabs, sorting_layers = {}, {}, [], []
    OUT.mkdir(parents=True, exist_ok=True)

    def record(obj):
        inputs[obj.assets_file.name+':'+str(obj.path_id)] = dict(apk.provenance(obj),
            sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return obj

    def ptr(value, owner):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    def bitmap(obj, material):
        data = record(obj).read_typetree()
        key = data['m_Name']+'_'+hashlib.sha256(obj.get_raw_data()).hexdigest()[:12]
        if key in fonts:
            return key
        mat = record(material).read()
        shader = record(mat.m_Shader.deref()).read().m_ParsedForm.m_Name
        if shader != 'UI/GameDamage':
            raise ValueError('Untranslated world number shader: '+shader)
        env = dict(mat.m_SavedProperties.m_TexEnvs)['_MainTex']
        texture = record(env.m_Texture.deref())
        buffer = io.BytesIO()
        texture.read().image.save(buffer, format='PNG')
        (OUT/(key+'.png')).write_bytes(buffer.getvalue())
        fonts[key] = dict(key=key, name=data['m_Name'], glyphs=data['m_CharacterRects'],
            size=data['m_FontSize'], lineSpacing=data['m_LineSpacing'], ascent=data['m_Ascent'],
            descent=data['m_Descent'], floats=[dict(name=k,value=v) for k,v in mat.m_SavedProperties.m_Floats],
            colors=[dict(name=k, rgba=[v.r,v.g,v.b,v.a]) for k,v in mat.m_SavedProperties.m_Colors],
            textureScale=[env.m_Scale.x,env.m_Scale.y], textureOffset=[env.m_Offset.x,env.m_Offset.y],
            shader=shader)
        return key

    for effect_type, pool in enumerate(settings['EffectUIPoolPresents']):
        guid = pool['EffectUIPrefabProxy']['mAssetGuid']
        prefab = source['prefabs'][guid]
        localize_hud_node(prefab['root'], lambda key: language_values.get(xxhash.xxh64_intdigest(key.encode('utf8'))))
        rows = []
        normalize_world_float_node(prefab['root'], -1, rows)
        by_path = {row['path']: row for row in rows}
        bundle = prefab['source']['bundle']
        root = next(p.deref() for path,p in apk.container(bundle)
            if p.type.name=='GameObject' and p.read().m_Name==prefab['root']['name'])
        paths, behaviours, texts, order_layers = {}, [], [], []

        def walk(obj, parent=''):
            go = record(obj).read()
            path = parent+'/'+go.m_Name
            paths[(obj.assets_file.name,obj.path_id)] = path
            components = [c.component.deref() for c in go.m_Component]
            tr = next(c for c in components if c.type.name in ('Transform','RectTransform'))
            transform = record(tr).read()
            row = by_path[path]
            row['local_position'] = [transform.m_LocalPosition.x,transform.m_LocalPosition.y,transform.m_LocalPosition.z]
            row['scale'] = [transform.m_LocalScale.x,transform.m_LocalScale.y,transform.m_LocalScale.z]
            if tr.type.name == 'RectTransform':
                row.update(uses_rect_transform=True,
                    anchor_min=[transform.m_AnchorMin.x,transform.m_AnchorMin.y],
                    anchor_max=[transform.m_AnchorMax.x,transform.m_AnchorMax.y],
                    anchored_position=[transform.m_AnchoredPosition.x,transform.m_AnchoredPosition.y],
                    size_delta=[transform.m_SizeDelta.x,transform.m_SizeDelta.y],
                    pivot=[transform.m_Pivot.x,transform.m_Pivot.y])
            for c in components:
                paths[(c.assets_file.name,c.path_id)] = path
                if c.type.name=='TextMesh':
                    text = record(c).read()
                    renderer = record(next(x for x in components if x.type.name=='MeshRenderer')).read()
                    key = bitmap(text.m_Font.deref(), renderer.m_Materials[0].deref())
                    row.update(bitmap_font=key, bitmap_character_size=text.m_CharacterSize,
                        bitmap_font_size=text.m_FontSize, bitmap_style=text.m_FontStyle,
                        bitmap_anchor=text.m_Anchor, bitmap_alignment=text.m_Alignment,
                        bitmap_line_spacing=text.m_LineSpacing, bitmap_tab_size=text.m_TabSize,
                        bitmap_rich_text=text.m_RichText,
                        bitmap_color=[text.m_Color.r,text.m_Color.g,text.m_Color.b,text.m_Color.a],
                        bitmap_sorting_order=renderer.m_SortingOrder)
                    texts.append(path)
                elif c.type.name=='MonoBehaviour':
                    typ = c.read(check_read=False).m_Script.read().m_ClassName
                    if typ in ('CharacterDamageUI','DamageNumberUI','DamageFloatSize'):
                        behaviours.append((typ,c,schemas.read(record(c))))
                    elif typ == 'GameSceneObjectOrderLayer':
                        raw = record(c).get_raw_data()
                        if len(raw) != 48:
                            raise ValueError('Unexpected original sorting layer layout')
                        group = ptr(dict(zip(('m_FileID','m_PathID'),struct.unpack_from('<iq',raw,32))),c)
                        if group.type.name != 'SortingGroup':
                            raise ValueError('Original sorting group binding differs')
                        group_data = group.read_typetree()
                        order_layers.append(dict(effectType=effect_type,
                            layerIndex=group_data['m_SortingLayer'],layerId=group_data['m_SortingLayerID'],
                            orderStart=struct.unpack_from('<i',raw,44)[0]))
            for child in transform.m_Children:
                walk(child.read().m_GameObject.deref(),path)

        walk(root)
        sorting_layers.extend(order_layers)
        if not texts:
            continue

        def bound(value, owner):
            if not value['m_PathID']:
                return ''
            obj = ptr(value,owner)
            return paths[(obj.assets_file.name,obj.path_id)]

        _, owner, cfg = next(value for value in behaviours if value[0]=='CharacterDamageUI')
        if cfg['mType'] != effect_type:
            raise ValueError('Configured damage effect type differs from prefab')
        _, number_owner, number_cfg = next(value for value in behaviours if value[0]=='DamageNumberUI')
        size = next(value[2]['mSize'] for value in behaviours if value[0]=='DamageFloatSize')
        clip_sources = {}
        def collect_clips(node):
            for animation in node.get('animations', []):
                clip_sources[(animation['owner'],animation['name'])] = (animation['source'],node)
            for child in node.get('children', []):
                collect_clips(child)
        collect_clips(prefab['root'])
        for row in rows:
            for animation in row.get('animations', []):
                if not animation['name'].startswith('ElementConfrontText_'):
                    continue
                source_clip, clip_root = clip_sources[(animation['owner'],animation['name'])]
                clip = record(next(obj for obj in apk.objects(source_clip['bundle'])
                    if obj.path_id==int(source_clip['path_id'])))
                tracks, duration, loop, unknown = mecanim_tracks(clip,clip_root,
                    scalar_material_property_names=('_SquenceRate',))
                if unknown:
                    raise ValueError('Unrestored original element animation bindings: '+str(unknown))
                decoded = dict(animation,tracks=tracks,duration=duration,loop=loop,unconverted_bindings=unknown)
                animation.update(normalize_hud_animation(decoded))
        prefabs.append(dict(effectType=effect_type, rootName=prefab['root']['name'], nodes=rows,
            numberPath=bound(number_cfg['mNumberText'],number_owner),
            breakPath=bound(cfg['mBreakResilienceGo'],owner), truePath=bound(cfg['mTrueDamageText'],owner),
            showTime=cfg['mShowTime'], height=cfg['mHeight'], sceneOffset=list(cfg['mSceneOffset'].values()),
            cameraScale=size, sortingOrderStart=order_layers[0]['layerIndex']*1000+order_layers[0]['orderStart'], sourceBundle=bundle,
            sourceBundleHash=apk.index[bundle], sourcePathId=str(root.path_id)))
    target = OUT/'world-damage.json'
    target.write_bytes((json.dumps(dict(manifest='854_156383', prefabs=prefabs,fonts=list(fonts.values()),sortingLayers=sorting_layers),ensure_ascii=True,indent=2)+'\n').encode())
    receipt = dict(manifest='854_156383', inputs=list(inputs.values()),
        world_source_sha256=digest(source_path), rules_sha256=digest(rules_path),
        generator_sha256=digest(__file__), animation_decoder_sha256=digest(ROOT/'scripts/official_animation.py'),
        language_sources=language_sources, effect_types=[p['effectType'] for p in prefabs],
        sorting_layers=sorting_layers,
        sorting_adapter='Use the existing scene-geometry mapping layerIndex*1000+sortingOrder. Keep the official layer ID/index and script order start in the receipt; numeric floats and the existing custom-action world float must participate in the same ordering as the restored environment.',
        outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=digest(p)) for p in sorted(OUT.iterdir()) if p.suffix in ('.json','.png','.fontsettings','.mat','.meta')],
        runtime=[dict(path=p,sha256=digest(ROOT/p)) for p in (
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudAnimationPlayer.cs',
            'unity/Assets/Game/Editor/Combat/OfficialBattleWorldDamageAssetTools.cs',
            'unity/Assets/Game/Resources/Opening/OpeningDamageBitmap.shader')],
        operation='Follow FightDamageSettings.EffectUIPoolPresents to the configured world-space prefabs. Retain their original hierarchy, native TextMesh bindings, bitmap atlas/glyph UV/advance, colors, transform, animations, lifetime, camera scale and stack height. Numeric effects only; tooltip-only effects and full combining remain separate.')
    (ROOT/'assets/reference/battle-hud/world-damage-source-manifest.json').write_bytes((json.dumps(receipt,ensure_ascii=True,indent=2)+'\n').encode())
    print('Restored configured numeric world effects:',receipt['effect_types'],'bitmap fonts:',len(fonts))


if __name__=='__main__':
    build()
