"""Restore configured native world-space battle tip prefabs."""
import hashlib
import json
import re
from pathlib import Path
import struct
import sys
import uuid

sys.path.insert(0,str(Path(__file__).resolve().parent))
from official_apk import Apk,ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.classes.PPtr import PPtr
from prepare_unity_battle_assets import normalize_world_float_node,localize_hud_node,normalize_hud_animation
from official_animation import mecanim_tracks
from prepare_unity_map11_ui import animation_record

OUTPUT=ROOT/'unity/Assets/Game/Resources/Combat/world-tips.json'
RECEIPT=ROOT/'assets/reference/battle-hud/world-tips-source-manifest.json'


def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def save(path,value,ensure_ascii=True):
    content=(json.dumps(value,ensure_ascii=ensure_ascii,indent=2)+'\n').encode()
    temporary=path.with_name(path.name+'.'+uuid.uuid4().hex+'.tmp')
    try:
        temporary.write_bytes(content)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def refresh_runtime():
    receipt=json.loads(RECEIPT.read_bytes())
    names=[
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleWorldTips.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageFloat.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleElementFeedback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleElementFeedbackCatalog.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudAnimationPlayer.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleWorldTipsAssetTools.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleWorldTipsAcceptance.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleHudAssetTools.cs',
        'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs',
        'scripts/test_battle_status_disperse.ps1']
    receipt['runtime']=[dict(path=n,sha256=sha(ROOT/n)) for n in names]
    receipt['generator_sha256']=sha(__file__)
    receipt['output_sha256']=sha(OUTPUT)
    shared_paths={row['path'] for row in receipt['shared_assets']}
    for row in receipt['shared_assets']:row['sha256']=sha(ROOT/row['path'])
    receipt['derived_font_cache']='The shared dynamic font may cache the original localized words using the already-bound official FZY4K TTF. Glyph outlines and metrics are generated from that exact font; no replacement font or artwork is introduced.'
    receipt['native_sprites']=[]
    for sprite in json.loads(OUTPUT.read_bytes()).get('spriteAssets',[]):
        path=ROOT/'unity/Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback'/(sprite['key']+'.asset')
        for output in [path,Path(str(path)+'.meta')]:
            if output.exists():receipt['native_sprites'].append(dict(path=output.relative_to(ROOT).as_posix(),sha256=sha(output)))
    receipt['disperse_notification']='User-confirmed local wiring: emit once when one disperse operation removes at least one status. Probability failure or no removable status emits nothing. This supplies the missing server call to FightDisperseModification, not a recovered server implementation.'
    receipt['anchor_binding']='Port CharacterDamageUI.UpdateAttach/UpdateOffset and CharacterDamageUIManager.UpdateDamageUIOffset: sample the actual entity transform when creating or stacking a float; flatten local Z. DamageFloatSize retains execution order 101, camera reference/size caching, and LateUpdate scaling independently of scaled animation time. Existing no-view grid fallback and full entity/event scheduling are still incomplete.'
    save(RECEIPT,receipt)
    for name in ['damage-display','damage-visibility','element-feedback','world-damage','element-confrontation']:
        path=ROOT/'assets/reference'/('battle-rules' if name=='element-confrontation' else 'battle-hud')/(name+'-source-manifest.json')
        data=json.loads(path.read_bytes())
        for row in data.get('runtime',[]):
            if row['path'] in names:row['sha256']=sha(ROOT/row['path'])
        for row in data.get('outputs',[]):
            if row['path'] in shared_paths:row['sha256']=sha(ROOT/row['path'])
        save(path,data,ensure_ascii=name in ['element-feedback','world-damage'])



def apk_language(apk):
    # Read the original language bytes directly; extracted historical views are disposable.
    names=('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
           'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile')
    by_name={row['name']:row for row in apk.source.manifest('WwiseBnk',apk.archive)['bundles']}
    result,records={},[]
    for name in names:
        entry=by_name[name];path='assets/yoo/WwiseBnk/'+entry['file']
        raw=apk.archive.read(path)
        if len(raw)!=entry['size'] or hashlib.md5(raw).hexdigest()!=entry['hash']:
            raise ValueError('Original APK language differs from its pinned manifest: '+name)
        count=struct.unpack_from('<i',raw)[0];start=4+count*12
        if count<0 or start>len(raw):raise ValueError('Original language index is out of bounds')
        for i in range(count):
            identity,offset=struct.unpack_from('<Qi',raw,4+12*i);position=start+offset
            if offset<0 or position+2>len(raw):raise ValueError('Original language entry is out of bounds')
            size=struct.unpack_from('<H',raw,position)[0]
            if position+2+size>len(raw):raise ValueError('Original language value is out of bounds')
            result[identity]=raw[position+2:position+2+size].decode('utf8')
        records.append(dict(path=apk.source.apk.relative_to(ROOT).as_posix(),rawfile=name,
            sha256=hashlib.sha256(raw).hexdigest(),entries=count,
            source=dict(origin='apk_zip',apk_sha256=apk.source.apk_sha256,path=path,bundle_hash=entry['hash'])))
    return result,records


def build():
    apk,schemas,tmp=Apk(manifest='854_156383',reuse_current_cache=True),ScriptSchemas(),ScriptSchemas()
    tmp.generator=TypeTreeGenerator('2022.3.57f1',generator='AssetsTools')
    tmp.generator.load_local_dll_folder('D:/Tool/Unity/Editor/Data/Managed/UnityEngine')
    tmp.generator.load_local_dll_folder(str(ROOT/'unity/Library/ScriptAssemblies'))
    for name in ['D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll',
                 'D:/Tool/Unity/Editor/Data/MonoBleedingEdge/lib/mono/4.5/mscorlib.dll']:
        tmp.generator.load_dll(Path(name).read_bytes())
    import xxhash
    values,language_sources=apk_language(apk)
    source_path=ROOT/'assets/official/battle-hud/world-ui.json'
    rules_path=ROOT/'assets/official/battle-hud/rules.json'
    source=json.loads(source_path.read_bytes())
    settings=json.loads(rules_path.read_bytes())['damage_settings']
    shared=ROOT/'unity/Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback'
    inputs,shared_assets,bindings,sprite_assets={},{},[],{}

    def record(obj):
        inputs[obj.assets_file.name+':'+str(obj.path_id)]=dict(apk.provenance(obj),sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return obj

    def ptr(value,owner):
        return record(PPtr(**value,assetsfile=owner.assets_file).deref()) if value['m_PathID'] else None

    for typ in [4,8,9,10,11,12]:
        original=source['prefabs'][settings['EffectUIPoolPresents'][typ]['EffectUIPrefabProxy']['mAssetGuid']]
        localize_hud_node(original['root'],lambda key:values.get(xxhash.xxh64_intdigest(key.encode())))
        rows=[];normalize_world_float_node(original['root'],-1,rows)
        by_path={r['path']:r for r in rows}
        def localize(node):
            key=node.get('localization_key')
            if key:by_path[node['path']]['text_value']=values[xxhash.xxh64_intdigest(key.encode())]
            for child in node.get('children',[]):localize(child)
        localize(original['root'])
        bundle=original['source']['bundle']
        root=next(p.deref() for path,p in apk.container(bundle) if p.type.name=='GameObject' and p.read().m_Name==original['root']['name'])
        paths,texts,effects,behaviours,orders,sprite_bindings={},[],{},[],[],[]

        def walk(obj,parent=''):
            go=record(obj).read();path=parent+'/'+go.m_Name;paths[(obj.assets_file.name,obj.path_id)]=path
            components=[c.component.deref() for c in go.m_Component]
            tr=next(c for c in components if c.type.name in ('Transform','RectTransform'))
            native=record(tr).read();row=by_path[path]
            row['local_position']=[native.m_LocalPosition.x,native.m_LocalPosition.y,native.m_LocalPosition.z]
            row['scale']=[native.m_LocalScale.x,native.m_LocalScale.y,native.m_LocalScale.z]
            if tr.type.name=='RectTransform':
                row.update(uses_rect_transform=True,anchor_min=[native.m_AnchorMin.x,native.m_AnchorMin.y],
                    anchor_max=[native.m_AnchorMax.x,native.m_AnchorMax.y],anchored_position=[native.m_AnchoredPosition.x,native.m_AnchoredPosition.y],
                    size_delta=[native.m_SizeDelta.x,native.m_SizeDelta.y],pivot=[native.m_Pivot.x,native.m_Pivot.y])
            for c in components:
                paths[(c.assets_file.name,c.path_id)]=path
                if c.type.name=='Animation':
                    animation=record(c).read()
                    row['default_animation']=animation.m_Animation.read().m_Name if animation.m_Animation.path_id else ''
                    row['animation_play_automatically']=bool(animation.m_PlayAutomatically)
                    for ref in animation.m_Animations:
                        clip=record(ref.deref())
                        converted=animation_record(apk,clip,dict(path=path))
                        if converted['unknown'] or converted['events']:raise ValueError('Restore the remaining original legacy animation branches')
                        row['animations'].append(normalize_hud_animation(dict(converted,owner=path)))
                elif c.type.name=='SpriteRenderer':
                    native_sprite=record(c).read();obj=record(native_sprite.m_Sprite.deref())
                    key='Sprite_'+hashlib.sha256(obj.get_raw_data()).hexdigest()[:16]
                    data=obj.read_typetree();rd=data['m_RD'];vertex=rd['m_VertexData']
                    if data['m_SpriteAtlas']['m_PathID']:
                        atlas=ptr(data['m_SpriteAtlas'],obj)
                        packed=next(v for k,v in atlas.read_typetree()['m_RenderDataMap'] if k==data['m_RenderDataKey'])
                        for field in ['texture','alphaTexture']:ptr(packed[field],atlas)
                    if (any(rd['textureRectOffset'].values()) or rd['textureRect']!=data['m_Rect']
                            or vertex['m_VertexCount']!=4 or vertex['m_Channels'][0]!={'stream':0,'offset':0,'format':0,'dimension':3}):
                        raise ValueError('Restore this original world-tip Sprite geometry explicitly')
                    source_file=row['image_sprite_file']
                    target=ROOT/'unity/Assets/Official/Combat/HUD/Sprites'/source_file.rsplit('/',1)[-1]
                    from PIL import Image
                    original_image=obj.read().image.convert('RGBA')
                    if original_image.tobytes()!=Image.open(target).convert('RGBA').tobytes():raise ValueError('Original Sprite pixels differ from the bound HUD texture')
                    sprite_assets[key]=dict(key=key,sourceFile=source_file,sourcePath=target.relative_to(ROOT).as_posix(),sourceSha256=sha(target),
                        rect=data['m_Rect'],pivot=data['m_Pivot'],border=list(data['m_Border'].values()),pixelsPerUnit=data['m_PixelsToUnits'],
                        vertices=[dict(zip(('x','y'),struct.unpack_from('<2f',vertex['m_DataSize'],i*12))) for i in range(4)],
                        triangles=list(struct.unpack('<'+'H'*(len(rd['m_IndexBuffer'])//2),bytes(rd['m_IndexBuffer']))))
                    material=record(native_sprite.m_Materials[0].deref());material_key='Material_'+hashlib.sha256(material.get_raw_data()).hexdigest()[:16]
                    material_path=shared/(material_key+'.mat')
                    if not material_path.is_file():raise ValueError('Extract the original world-tip Sprite material: '+material_key)
                    shared_assets[material_key]=dict(path=material_path.relative_to(ROOT).as_posix(),sha256=sha(material_path),original=apk.provenance(material))
                    sprite_bindings.append(dict(path=path,material=material_key,sprite=key,order=native_sprite.m_SortingOrder,
                        drawMode=native_sprite.m_DrawMode,tileMode=native_sprite.m_SpriteTileMode,size=[native_sprite.m_Size.x,native_sprite.m_Size.y]))
                if c.type.name!='MonoBehaviour':continue
                kind=c.read(check_read=False).m_Script.read().m_ClassName
                if kind=='TextMeshPro':
                    cfg=tmp.read(record(c));keys={}
                    if any(cfg.get(k) for k in ['m_enableAutoSizing','m_isRightToLeft']):
                        raise ValueError('Restore this original TMP layout branch before binding: '+path)
                    if cfg.get('m_fontColorGradientPreset',{}).get('m_PathID'):
                        raise ValueError('Resolve the original TMP gradient preset before binding: '+path)
                    row['text_word_wrap']=bool(cfg['m_enableWordWrapping'])
                    row['text_overflow']=cfg['m_overflowMode']
                    row['text_color']=list(cfg['m_fontColor'].values())
                    for field,prefix,extension in [('m_fontAsset','Font_','.asset'),('m_sharedMaterial','Material_','.mat')]:
                        asset=ptr(cfg[field],c);key=prefix+hashlib.sha256(asset.get_raw_data()).hexdigest()[:16]
                        target=shared/(key+extension)
                        if not target.is_file():raise ValueError('Extract this original text asset before binding: '+key)
                        shared_assets[key]=dict(path=target.relative_to(ROOT).as_posix(),sha256=sha(target),original=apk.provenance(asset))
                        keys[field]=key
                    renderer=next(x for x in components if x.type.name=='MeshRenderer')
                    order=record(renderer).read().m_SortingOrder
                    texts.append(dict(path=path,font=keys['m_fontAsset'],material=keys['m_sharedMaterial'],
                        order=order,style=cfg['m_fontStyle'],weight=cfg['m_fontWeight'],size=cfg['m_fontSize'],
                        characterSpacing=cfg['m_characterSpacing'],wordSpacing=cfg['m_wordSpacing'],
                        lineSpacing=cfg['m_lineSpacing'],margin=list(cfg['m_margin'].values()),
                        enableGradient=bool(cfg['m_enableVertexGradient']),
                        gradient={k:list(cfg['m_fontColorGradient'][k].values()) for k in ['topLeft','topRight','bottomLeft','bottomRight']} ))
                elif kind=='TMPEffect':
                    cfg=schemas.read(record(c))
                    if any(cfg[k] for k in ['linearDodge','useVertexColor','RealtimeUpdate','outline2','underlay','glow','gray']):
                        raise ValueError('Recover this original TMP effect branch before binding')
                    effects[path]=dict(dilate=cfg['dilate'],outline=bool(cfg['outline']),width=cfg['outlineWidth'],color=list(cfg['outlineColor'].values()))
                elif kind in ['CharacterDamageUI','DamageFloatSize','DamageCustomActionUI']:
                    behaviours.append((kind,c,schemas.read(record(c))))
                elif kind=='GameSceneObjectOrderLayer':
                    raw=record(c).get_raw_data()
                    if len(raw)!=48:raise ValueError('Original sorting-layer layout changed')
                    group=ptr(dict(zip(('m_FileID','m_PathID'),struct.unpack_from('<iq',raw,32))),c)
                    cfg=group.read_typetree();orders.append(cfg['m_SortingLayer']*1000+struct.unpack_from('<i',raw,44)[0])
            for child in native.m_Children:walk(child.read().m_GameObject.deref(),path)

        walk(root)
        if len(texts)!=1 or len(orders)!=1:raise ValueError('Unexpected original world tip hierarchy')
        for text in texts:text['effect']=effects.get(text['path'])
        _,owner,cfg=next(v for v in behaviours if v[0]=='CharacterDamageUI')
        if cfg['mType']!=typ:raise ValueError('Original world tip type differs from configured pool')
        tip=ptr(cfg['mTipUi'],owner)
        if tip is None and typ not in [10,12]:raise ValueError('Original damage tip reference is absent')
        # Init(entity,onEnd) keeps these static texts as authored; it never calls UpdateText/UpdateColor.
        tip_path=paths[(tip.assets_file.name,tip.path_id)] if tip else texts[0]['path']
        size=next(v[2]['mSize'] for v in behaviours if v[0]=='DamageFloatSize')
        clips={}

        def collect(node):
            for animation in node.get('animations',[]):clips[(animation['owner'],animation['name'])]=(animation['source'],node)
            for child in node.get('children',[]):collect(child)

        collect(original['root'])
        for row in rows:
            for animation in row.get('animations',[]):
                if (animation['owner'],animation['name']) not in clips:continue # Legacy clips were decoded directly above.
                evidence,clip_root=clips[(animation['owner'],animation['name'])]
                clip=record(next(o for o in apk.objects(evidence['bundle']) if o.path_id==int(evidence['path_id'])))
                tracks,duration,loop,unknown=mecanim_tracks(clip,clip_root)
                if unknown:raise ValueError('Unrestored original world tip animation: '+str(unknown))
                animation.update(normalize_hud_animation(dict(animation,tracks=tracks,duration=duration,loop=loop,unconverted_bindings=unknown)))
        prefab=dict(effectType=typ,rootName=original['root']['name'],nodes=rows,textPath=tip_path,
            showTime=cfg['mShowTime'],height=cfg['mHeight'],sceneOffset=list(cfg['mSceneOffset'].values()),
            cameraScale=size,sortingOrderStart=orders[0])
        if typ==11:
            _,action_owner,action_config=next(v for v in behaviours if v[0]=='DamageCustomActionUI')
            icon=ptr(action_config['mIcon'],action_owner)
            if icon is None:raise ValueError('Original custom-action icon reference is absent')
            prefab['iconPath']=paths[(icon.assets_file.name,icon.path_id)]
            enum_path=ROOT/'.tools/source-packages/official-code/common-source/Common/EStatusCustomAction.cs'
            names=re.findall(r'^\s*(None|Immunity|Unflinching|CDModify),?\s*$',enum_path.read_text(encoding='utf-8'),re.M)
            if names!=['None','Immunity','Unflinching','CDModify']:raise ValueError('Original custom action enum changed')
            prefab['actions']=[]
            for value,name in enumerate(names):
                key='EStatusCustomAction.'+name
                text=values.get(xxhash.xxh64_intdigest(key.encode()))
                if text:prefab['actions'].append(dict(action=value,key=key,text=text))
            if len(prefab['actions'])!=3:raise ValueError('Original custom action localized words are incomplete')
        bindings.append(dict(key='damage_'+original['root']['name'].removesuffix('_Scene'),prefab=prefab,texts=texts,sprites=sprite_bindings))
    # Reuse the verified native extraction, then recover the records its old nonempty-playable filter omitted.
    catalog_path=ROOT/'assets/official/battle-animation/catalog.json'
    catalog_receipt=ROOT/'assets/reference/battle-animation/source-manifest.json'
    previous=json.loads(catalog_receipt.read_bytes())
    if sha(catalog_path)!=previous['output_sha256'] or previous['unconverted']:raise ValueError('Original skill prefab extraction is not verified')
    replay={int(k):v['show_replay'] for k,v in json.loads(catalog_path.read_bytes())['skills'].items()}
    required=set()
    for name in ['battle-rules','skill-rules']:
        for key,components in json.loads((ROOT/('unity/Assets/Official/Combat/'+name+'.json')).read_bytes())['entities'].items():
            if 'FightSkillComponent' in components:required.add(int(key))
    missing=required-set(replay)
    roots=missing|{v//100*100 for v in missing}|{v//10*10 for v in missing}
    for bundle in apk.index:
        match=re.search(r'_fightskill_(?:move_)?(\d+)\.bundle$',bundle)
        if not match or int(match[1]) not in roots:continue
        for obj in apk.objects(bundle):
            if obj.type.name!='MonoBehaviour':continue
            native=obj.read(check_read=False)
            if native.m_Script.read().m_ClassName!='MyFightSkill':continue
            identity=int(native.m_GameObject.read().m_Name)
            if identity not in missing:continue
            replay[identity]=bool(schemas.read(record(obj))['ShowSkillReplay'])
    if required-set(replay):raise ValueError('Original skill replay flags are missing: '+str(sorted(required-set(replay))))
    save(OUTPUT,dict(manifest=apk.version,bindings=bindings,spriteAssets=list(sprite_assets.values()),
        replaySkills=[dict(entity=k,show=replay[k]) for k in sorted(required)]))
    receipt=dict(manifest=apk.version,inputs=list(inputs.values()),language_sources=language_sources,
        world_source_sha256=sha(source_path),rules_sha256=sha(rules_path),generator_sha256=sha(__file__),
        shared_assets=list(shared_assets.values()),output_path=OUTPUT.relative_to(ROOT).as_posix(),output_sha256=sha(OUTPUT),
        operation='Follow the actual configured DamageScene prefabs. Restore native transforms/RectTransforms, TMP fields and original font/material identities, single-outline TMPEffect, localized text, full clip bindings, sorting, camera scale, lifetime, height and scene offset. Reuse existing native assets only when their raw official identities match.')
    receipt['cache_policy']='Read the original APK first. A retained current cloud file is reusable only when its bytes satisfy the requested 854 manifest MD5 and size plus its own source-receipt SHA256. Changed bundles are never substituted.'
    receipt['extraction_tools']=[dict(path='scripts/'+name,sha256=sha(ROOT/'scripts'/name)) for name in ['official_apk.py','official_source.py','official_cloud_cache.py','official_unity_schema.py']]
    receipt['official_code']=[]
    receipt['replay_catalog']=dict(path=catalog_path.relative_to(ROOT).as_posix(),sha256=sha(catalog_path),
        source_manifest_sha256=sha(catalog_receipt),recovered_omitted_ids=sorted(missing),count=len(required),hidden_count=sum(not replay[v] for v in required))
    for version,directory in [('854_156383',ROOT/'.tools/source-packages/official-code'),
            ('871_156598',ROOT/'.tools/source-packages/current/871_156598/official-code')]:
        receipt['official_code'].append(dict(manifest=version,files=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p))
            for p in [directory/n for n in ['client-source/CharacterDamageUI.cs','client-source/CharacterDamageUIManager.cs',
                'client-source/DamageFloatSize.cs','client-source/DamageCustomActionUI.cs','client-source/MyFightSkill.cs','framework-firstpass-source/MultiLanguageText.cs',
                'common-source/Common/FightDisperseModification.cs','common-source/Common/FightRoleFightComponent.cs',
                'common-source/Common/FightSkillReplayModification.cs','common-source/Common/EReplaySkillType.cs','common-source/Common/EStatusCustomAction.cs']]]))
    save(RECEIPT,receipt)
    refresh_runtime()
    print('Restored original world tip types:',[v['prefab']['effectType'] for v in bindings])


if __name__=='__main__':
    if sys.argv[1:]==['--refresh-runtime']:refresh_runtime()
    elif sys.argv[1:]:raise SystemExit('Use no arguments to extract, or --refresh-runtime after validation.')
    else:build()
