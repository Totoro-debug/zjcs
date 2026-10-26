"""Recover the configured element-feedback bindings, font and world materials."""
import hashlib
import io
import json
import struct
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.classes.PPtr import PPtr
from prepare_battle_world_shaders import shader_text
from prepare_battle_hud_materials import shader_text as ui_shader_text

OUT = ROOT/'unity/Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback'
SHADERS = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/Shaders'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def build():
    apk = Apk(manifest='854_156383')
    schemas = ScriptSchemas()
    # TMP's native serialization is read with the matching installed Unity/TMP
    # schema, with strict byte-consumption checks; no client method is replaced.
    tmp = ScriptSchemas()
    tmp.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    tmp.generator.load_local_dll_folder('D:/Tool/Unity/Editor/Data/Managed/UnityEngine')
    tmp.generator.load_local_dll_folder(str(ROOT/'unity/Library/ScriptAssemblies'))
    tmp.generator.load_dll(Path('D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll').read_bytes())
    tmp.generator.load_dll(Path('D:/Tool/Unity/Editor/Data/MonoBleedingEdge/lib/mono/4.5/mscorlib.dll').read_bytes())
    from refresh_current_localization import language
    import xxhash
    language_values, language_sources = language(ROOT/'.tools/source-packages/history/854_156383/WwiseBnk')
    OUT.mkdir(parents=True, exist_ok=True)
    inputs, textures, materials, shaders, fonts, bindings, sprite_records, sprites = {}, {}, {}, {}, {}, [], [], {}
    component_types = {}

    def record(obj):
        inputs[obj.assets_file.name+':'+str(obj.path_id)] = dict(apk.provenance(obj),
            sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return obj

    def pointer(value, owner):
        if not value['m_PathID']:
            return None
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    config_bundle = 'defaultpackage_assets_textmesh_pro_rawfonts_fontassetfontconfig.bundle'
    config_obj = next(p.deref() for path,p in apk.container(config_bundle)
        if path=='Assets/TextMesh Pro/RawFonts/FontAssetFontConfig.json')
    font_config = json.loads(record(config_obj).read().m_Script)

    def texture(obj):
        obj = record(obj)
        key = 'Texture_'+hashlib.sha256(obj.get_raw_data()).hexdigest()[:16]
        if key in textures:
            return key
        data = obj.read()
        buffer = io.BytesIO()
        data.image.save(buffer, format='PNG')
        target = OUT/(key+'.png')
        target.write_bytes(buffer.getvalue())
        settings = data.m_TextureSettings
        textures[key] = dict(key=key, width=data.m_Width, height=data.m_Height,
            filter=settings.m_FilterMode, wrapU=settings.m_WrapU, wrapV=settings.m_WrapV,
            aniso=settings.m_Aniso, bias=settings.m_MipBias, sRGB=data.m_ColorSpace==1)
        return key

    def material(obj):
        obj = record(obj)
        key = 'Material_'+hashlib.sha256(obj.get_raw_data()).hexdigest()[:16]
        if key in materials:
            return key
        data = obj.read()
        shader_obj = record(data.m_Shader.deref())
        shader = shader_obj.read()
        shader_name = shader.m_ParsedForm.m_Name
        names = {'Universal Render Pipeline/2D/Sprite-Unlit-Default': ('SpriteUnlit', ()),
                 'Universal Render Pipeline/2D/Sprite-Lit-Sequence': ('SpriteSequence', ('IsSqueueCycle','USE_SHAPE_LIGHT_TYPE_0')),
                 'Hidden/JSZX/TextMeshPro': ('TextMeshPro', ('UNITY_UI_CLIP_RECT','UNITY_UI_ALPHACLIP','OUTLINE_ON')),
                 'UI/EffectAdditive': ('HudEffectAdditive', None)}
        short, optional = names[shader_name]
        if shader_name not in shaders:
            if optional is None:
                _, translated, selected = ui_shader_text(shader, [])
                shader_target = SHADERS/'HudEffectAdditive.shader'
                if shader_target.read_text('utf-8') != translated:
                    raise ValueError('Existing UI/EffectAdditive translation differs')
                runtime_name = 'Zjcs/Combat/HudEffectAdditive'
            else:
                translated, selected = shader_text(shader, short, optional)
                shader_target = SHADERS/('World'+short+'.shader')
                shader_target.write_text(translated, encoding='utf-8', newline='\n')
                runtime_name = 'Zjcs/Combat/World'+short
            shaders[shader_name] = dict(name=shader_name, runtime=runtime_name,
                path=shader_target.relative_to(ROOT).as_posix(), sha256=sha(shader_target), selected=selected)
        texs = []
        for prop, env in data.m_SavedProperties.m_TexEnvs:
            tex = texture(env.m_Texture.deref()) if env.m_Texture.path_id else ''
            texs.append(dict(name=prop, texture=tex, scale=[env.m_Scale.x,env.m_Scale.y], offset=[env.m_Offset.x,env.m_Offset.y]))
        materials[key] = dict(key=key, name=data.m_Name, shader=shaders[shader_name]['runtime'],
            queue=data.m_CustomRenderQueue, keywords=data.m_ValidKeywords,
            floats=[dict(name=k,value=v) for k,v in data.m_SavedProperties.m_Floats],
            colors=[dict(name=k,rgba=[v.r,v.g,v.b,v.a]) for k,v in data.m_SavedProperties.m_Colors], textures=texs)
        return key

    def font(obj):
        obj = record(obj)
        key = 'Font_'+hashlib.sha256(obj.get_raw_data()).hexdigest()[:16]
        if key in fonts:
            return key
        data = tmp.read(obj)
        source = pointer(data['m_SourceFontFile'], obj)
        if source is not None:
            source_bytes = bytes(source.read().m_FontData)
        else:
            info = font_config['FontAssetInfoDict'][data['m_Name']]
            raw_bundle = 'defaultpackage_assets_textmesh_pro_rawfonts_'+Path(info['RawFontPath']).stem.lower()+'.bundle'
            raw_obj = next(p.deref() for path,p in apk.container(raw_bundle) if path==info['RawFontPath'])
            source_bytes = record(raw_obj).read().m_Script.encode('utf-8','surrogateescape')
            if len(source_bytes)!=info['Length'] or hashlib.md5(source_bytes).hexdigest()!=info['Hash']:
                raise ValueError('Original streaming font length/hash differs from its configuration')
        original_ttf = ROOT/'unity/Assets/Official/Commerce/Art/fonts/FZY4K.ttf'
        if hashlib.sha256(source_bytes).hexdigest() != sha(original_ttf):
            raise ValueError('The explicit local source font differs from the configured original')
        fonts[key] = dict(key=key, sourcePath=original_ttf.relative_to(ROOT/'unity').as_posix(), sourceSha256=sha(original_ttf),
            pointSize=data['m_FaceInfo']['m_PointSize'],
            name=data['m_Name'], faceInfo=data['m_FaceInfo'], glyphs=data['m_GlyphTable'], characters=data['m_CharacterTable'],
            atlasTextures=[texture(pointer(value,obj)) for value in data['m_AtlasTextures']],
            width=data['m_AtlasWidth'],height=data['m_AtlasHeight'],padding=data['m_AtlasPadding'],renderMode=data['m_AtlasRenderMode'],
            population=data['m_AtlasPopulationMode'],multiAtlas=bool(data['m_IsMultiAtlasTexturesEnabled']),
            clearOnBuild=bool(data['m_ClearDynamicDataOnBuild']),usedRects=data['m_UsedGlyphRects'],freeRects=data['m_FreeGlyphRects'],
            normalStyle=data['normalStyle'],normalSpacing=data['normalSpacingOffset'],boldStyle=data['boldStyle'],
            boldSpacing=data['boldSpacing'],italicStyle=data['italicStyle'],tabSize=data['tabSize'],
            material=material(pointer(data['material'],obj)))
        retained = ['m_FaceInfo','m_GlyphTable','m_CharacterTable','m_UsedGlyphRects','m_FreeGlyphRects',
                    'm_FontFeatureTable','normalStyle','normalSpacingOffset','boldStyle','boldSpacing','italicStyle','tabSize']
        fonts[key]['fontJson'] = json.dumps({name:data[name] for name in retained},ensure_ascii=True,separators=(',',':'))
        return key

    def sprite_asset(obj):
        obj = record(obj)
        key = 'Sprite_'+hashlib.sha256(obj.get_raw_data()).hexdigest()[:16]
        if key not in sprites:
            native = obj.read_typetree()
            rd = native['m_RD']
            vertex = rd['m_VertexData']
            if (rd['settingsRaw']!=0 or any(rd['textureRectOffset'].values())
                    or vertex['m_VertexCount']!=4 or vertex['m_Channels'][0]!={'stream':0,'offset':0,'format':0,'dimension':3}):
                raise ValueError('Restore this original Sprite packing/geometry explicitly')
            raw = vertex['m_DataSize']
            positions = [dict(zip(('x','y'),struct.unpack_from('<2f',raw,i*12))) for i in range(4)]
            indices = list(struct.unpack('<'+'H'*(len(rd['m_IndexBuffer'])//2),bytes(rd['m_IndexBuffer'])))
            sprites[key] = dict(key=key,texture=texture(pointer(rd['texture'],obj)),rect=native['m_Rect'],
                pivot=native['m_Pivot'],border=list(native['m_Border'].values()),pixelsPerUnit=native['m_PixelsToUnits'],vertices=positions,triangles=indices)
        return key

    world_path = ROOT/'unity/Assets/Official/Combat/Numbers/Resources/Combat/Numbers/world-damage.json'
    for prefab in json.loads(world_path.read_bytes())['prefabs']:
        root = next(p.deref() for path,p in apk.container(prefab['sourceBundle'])
            if p.type.name=='GameObject' and p.read().m_Name==prefab['rootName'])
        paths, behaviours, text_data, mat_bindings, effects, backgrounds = {}, [], {}, [], {}, []

        def walk(obj, parent=''):
            go = record(obj).read()
            path = parent+'/'+go.m_Name
            paths[(obj.assets_file.name,obj.path_id)] = path
            for component in go.m_Component:
                c = component.component.deref()
                paths[(c.assets_file.name,c.path_id)] = path
                if c.type.name in ('Transform','RectTransform'):
                    for child in record(c).read().m_Children:
                        walk(child.read().m_GameObject.deref(), path)
                elif c.type.name=='MonoBehaviour':
                    typ = c.read(check_read=False).m_Script.read().m_ClassName
                    if '/ElementConfrontText' in path:
                        component_types.setdefault(path,[]).append(typ)
                    if typ in ('CharacterDamageUI','ElementConfrontationUI'):
                        behaviours.append((typ,c,schemas.read(record(c))))
                    elif typ=='TextMeshPro':
                        cfg = tmp.read(record(c))
                        text_data[path] = dict(font=font(pointer(cfg['m_fontAsset'],c)),
                            material=material(pointer(cfg['m_sharedMaterial'],c)),
                            order=next(x.component.deref().read().m_SortingOrder for x in go.m_Component if x.component.type.name=='MeshRenderer'),
                            style=cfg['m_fontStyle'],weight=cfg['m_fontWeight'],size=cfg['m_fontSize'],
                            characterSpacing=cfg['m_characterSpacing'],wordSpacing=cfg['m_wordSpacing'],lineSpacing=cfg['m_lineSpacing'],
                            margin=list(cfg['m_margin'].values()))
                    elif typ=='TMPEffect':
                        cfg = schemas.read(record(c))
                        if any(cfg[name] for name in ('linearDodge','useVertexColor','RealtimeUpdate','outline2','underlay','glow','gray')):
                            raise ValueError('Restore this original TMPEffect branch explicitly: '+path)
                        effects[path] = dict(dilate=cfg['dilate'],outline=bool(cfg['outline']),
                            color=list(cfg['outlineColor'].values()),width=cfg['outlineWidth'])
                    elif typ=='DamageAutoSizeTextBack':
                        backgrounds.append((c,schemas.read(record(c))))
                elif c.type.name=='SpriteRenderer' and any(part in path for part in ('/ElementConfrontText/','/BreakResilience/')):
                    data = record(c).read()
                    mat_bindings.append(dict(path=path,material=material(data.m_Materials[0].deref()),
                        sprite=sprite_asset(data.m_Sprite.deref()),order=data.m_SortingOrder,
                        drawMode=data.m_DrawMode,size=[data.m_Size.x,data.m_Size.y],tileMode=data.m_SpriteTileMode))
                    sprite = record(data.m_Sprite.deref())
                    native = sprite.read_typetree()
                    sprite_records.append(dict(path=path, sprite=apk.provenance(sprite),rect=native['m_Rect'],
                        pivot=native['m_Pivot'],pixelsToUnits=native['m_PixelsToUnits'],renderData=native['m_RD']))

        walk(root)
        _, owner, cfg = next(value for value in behaviours if value[0]=='CharacterDamageUI')
        element_obj = pointer(cfg['mElementConfrontationUI'],owner)
        if element_obj is None:
            continue
        _, element_owner, element = next(value for value in behaviours if value[1]==element_obj)

        def path(value, context):
            obj = pointer(value,context)
            return '' if obj is None else paths[(obj.assets_file.name,obj.path_id)]

        controller = record(pointer(element['mAnimator'],element_owner).read().m_Controller.deref())
        native = controller.read_typetree()
        machine = native['m_Controller']['m_StateMachineArray'][0]['data']
        names = dict(native['m_TOS'])
        trigger_clips = []
        for row in machine['m_AnyStateTransitionConstantArray']:
            transition = row['data']
            conditions = transition['m_ConditionConstantArray']
            if transition['m_TransitionDuration'] != 0 or transition['m_HasExitTime'] or len(conditions)!=1:
                raise ValueError('Restore this original element Animator transition explicitly')
            state = machine['m_StateConstantArray'][transition['m_DestinationState']]['data']
            clip_index = state['m_BlendTreeConstantArray'][0]['data']['m_NodeArray'][0]['data']['m_ClipID']
            clip = pointer(native['m_AnimationClips'][clip_index],controller)
            trigger_clips.append(dict(trigger=names[conditions[0]['data']['m_EventID']],clip=clip.read().m_Name))
        keys = []
        for name in ('mElementDefenseKey','mElementConfrontWeak','mElementConfrontStrength','mElementConfrontImmunity'):
            key = element[name]
            keys.append(dict(key=key,text=language_values[xxhash.xxh64_intdigest(key.encode('utf8'))]))
        for text_path,value in text_data.items():
            value['effect'] = effects.get(text_path)
        background_bindings = [dict(backgroundPath=path(value['mBackground'],context),textPath=path(value['mText'],context),
            align=value['mAlignMode'],paddingX=value['mPaddingX'],paddingY=value['mPaddingY'],
            minWidth=value['mMinWidth'],maxWidth=value['mMaxWidth'],minHeight=value['mMinHeight'],maxHeight=value['mMaxHeight'])
            for context,value in backgrounds]
        bindings.append(dict(effectType=prefab['effectType'],rootName=prefab['rootName'],
            elementPath=path(cfg['mElementConfrontationUI'],owner),
            describePath=path(element['mDescribeText'],element_owner),bgPath=path(element['mBgGo'],element_owner),
            effectPaths=[path(element[name],element_owner) for name in ('mEffectImg','mEffectUIMesh','mEffectMeshRender')],
            hideObjects=[path(cfg[name],owner) for name in ('mNumberUi','DamageTypeImage','mBgImage') if cfg[name]['m_PathID']],
            hideRenderers=[path(cfg[name],owner) for name in ('mAddImage','mEffectSeq') if cfg[name]['m_PathID']],
            texts=[dict(path=key,**value) for key,value in text_data.items()],materials=mat_bindings,
            keys=keys,triggers=trigger_clips,backgrounds=background_bindings))
    payload = dict(manifest=apk.version,bindings=bindings,fonts=list(fonts.values()),materials=list(materials.values()),
        textures=list(textures.values()),sprites=list(sprites.values()))
    warm = ''.join(sorted(set(''.join(value['text'] for binding in bindings for value in binding['keys'])
        + ''.join(node['text_value'] for prefab in json.loads(world_path.read_bytes())['prefabs'] for node in prefab['nodes']))))
    for value in fonts.values():
        value['warmCharacters'] = warm
    target = OUT/'element-feedback.json'
    target.write_bytes((json.dumps(payload,ensure_ascii=True,indent=2)+'\n').encode())
    from read_official_native import NativeMethods
    native = NativeMethods(ROOT/'.tools/source-packages/commerce-native/libil2cpp-restored.bin',
        ROOT/'.tools/source-packages/official-code/global-metadata.dat')
    native_methods = []
    for name in ('TMPro.TMPEffect.DoSetup','TMPro.TMPEffect.<DoSetup>b__62_0','TMPro.TMP_FontAsset.get_atlasPadding'):
        address, index = native.by_name[name]
        end = min(value for value in native.by_address if value>address)
        native_methods.append(dict(name=name,metadata_index=index,address=hex(address),length=end-address,
            sha256=hashlib.sha256(native.image[address:end]).hexdigest()))
    official_code = []
    for version,directory in [('854_156383',ROOT/'.tools/source-packages/official-code'),
            ('871_156598',ROOT/'.tools/source-packages/current/871_156598/official-code')]:
        for name in ('ElementConfrontationUI','CharacterDamageUI','CharacterDamageUIManager','DamageAutoSizeTextBack'):
            path = directory/'client-source'/(name+'.cs')
            official_code.append(dict(manifest=version,path=path.relative_to(ROOT).as_posix(),sha256=sha(path)))
    runtime_paths = [
        'unity/Assets/Game/Runtime/Official/WorldUI/ElementConfrontationUI.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/DamageAutoSizeTextBack.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleElementFeedback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleElementFeedbackCatalog.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudAnimationPlayer.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleElementFeedbackAssetTools.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleElementFeedbackAcceptance.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleElementConfrontationAcceptance.cs',
        'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']
    # Raw Sprite geometry is evidence for the subsequent explicit native binding.
    receipt = dict(manifest=apk.version,inputs=list(inputs.values()),language_sources=language_sources,
        element_components=component_types,
        official_code=official_code,native_methods=native_methods,
        native_image_sha256=hashlib.sha256(native.image).hexdigest(),metadata_sha256=hashlib.sha256(native.metadata).hexdigest(),
        native_outline='TMPEffect.DoSetup normalizes outlineWidth by faceInfo.pointSize / fontSize / atlasPadding and clamps to [0,1]. Its callback enables OUTLINE_ON and writes the original outline color without linear conversion. Only the active single-outline branch of these six prefabs is ported; other active effect branches are rejected.',
        generator_sha256=sha(__file__),shader_generator_sha256=sha(ROOT/'scripts/prepare_battle_world_shaders.py'),
        world_source_sha256=sha(world_path),shaders=list(shaders.values()),sprites=sprite_records,
        outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in sorted(OUT.iterdir()) if p.is_file()],
        runtime=[dict(path=p,sha256=sha(ROOT/p)) for p in runtime_paths],
        operation='Recover and explicitly bind the six configured world element-feedback prefabs: original ElementConfrontationUI references/language keys, zero-duration Animator transitions and curves, native TMP font data/streaming TTF, single-outline effect, sprites/nine-slice/background autosizing, materials and selected original GLES programs translated to desktop HLSL. CharacterDamageUI retains break/element/true-damage precedence and hides the number on elemental immunity.',
        remaining='This binding does not complete every floating-text kind, merge/source rule, zero/hide-damage filter, timing dependency, audio or general TMPEffect branches.')
    (ROOT/'assets/reference/battle-hud/element-feedback-source-manifest.json').write_bytes((json.dumps(receipt,ensure_ascii=True,indent=2,default=lambda value:value.hex() if isinstance(value,bytes) else str(value))+'\n').encode())
    print('Recovered element feedback:',len(bindings),'prefabs,',len(fonts),'fonts,',len(materials),'materials.')


if __name__=='__main__':
    build()
