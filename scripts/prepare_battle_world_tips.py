"""Restore the configured native immunity, dodge and miss world-space prefabs."""
import hashlib
import json
from pathlib import Path
import struct
import sys

sys.path.insert(0,str(Path(__file__).resolve().parent))
from official_apk import Apk,ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.classes.PPtr import PPtr
from prepare_unity_battle_assets import normalize_world_float_node,localize_hud_node,normalize_hud_animation
from official_animation import mecanim_tracks

OUTPUT=ROOT/'unity/Assets/Game/Resources/Combat/world-tips.json'
RECEIPT=ROOT/'assets/reference/battle-hud/world-tips-source-manifest.json'


def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def save(path,value):
    path.write_bytes((json.dumps(value,ensure_ascii=True,indent=2)+'\n').encode())


def refresh_runtime():
    receipt=json.loads(RECEIPT.read_bytes())
    names=[
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleWorldTips.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleElementFeedback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleWorldTipsAcceptance.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleHudAssetTools.cs',
        'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']
    receipt['runtime']=[dict(path=n,sha256=sha(ROOT/n)) for n in names]
    receipt['generator_sha256']=sha(__file__)
    receipt['output_sha256']=sha(OUTPUT)
    shared_paths={row['path'] for row in receipt['shared_assets']}
    for row in receipt['shared_assets']:row['sha256']=sha(ROOT/row['path'])
    receipt['derived_font_cache']='The shared dynamic font may cache the three original localized words using the already-bound official FZY4K TTF. Glyph outlines and metrics are generated from that exact font; no replacement font or artwork is introduced.'
    save(RECEIPT,receipt)
    for name in ['damage-display','damage-visibility','element-feedback','world-damage']:
        path=ROOT/'assets/reference/battle-hud'/(name+'-source-manifest.json')
        data=json.loads(path.read_bytes())
        for row in data.get('runtime',[]):
            if row['path'] in names:row['sha256']=sha(ROOT/row['path'])
        for row in data.get('outputs',[]):
            if row['path'] in shared_paths:row['sha256']=sha(ROOT/row['path'])
        path.write_bytes((json.dumps(data,ensure_ascii=name in ['element-feedback','world-damage'],indent=2)+'\n').encode())


def build():
    apk,schemas,tmp=Apk(manifest='854_156383'),ScriptSchemas(),ScriptSchemas()
    tmp.generator=TypeTreeGenerator('2022.3.57f1',generator='AssetsTools')
    tmp.generator.load_local_dll_folder('D:/Tool/Unity/Editor/Data/Managed/UnityEngine')
    tmp.generator.load_local_dll_folder(str(ROOT/'unity/Library/ScriptAssemblies'))
    for name in ['D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll',
                 'D:/Tool/Unity/Editor/Data/MonoBleedingEdge/lib/mono/4.5/mscorlib.dll']:
        tmp.generator.load_dll(Path(name).read_bytes())
    from refresh_current_localization import language
    import xxhash
    values,language_sources=language(ROOT/'.tools/source-packages/history/854_156383/WwiseBnk')
    source_path=ROOT/'assets/official/battle-hud/world-ui.json'
    rules_path=ROOT/'assets/official/battle-hud/rules.json'
    source=json.loads(source_path.read_bytes())
    settings=json.loads(rules_path.read_bytes())['damage_settings']
    shared=ROOT/'unity/Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback'
    inputs,shared_assets,bindings={},{},[]

    def record(obj):
        inputs[obj.assets_file.name+':'+str(obj.path_id)]=dict(apk.provenance(obj),sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return obj

    def ptr(value,owner):
        return record(PPtr(**value,assetsfile=owner.assets_file).deref()) if value['m_PathID'] else None

    for typ in [4,8,9]:
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
        paths,texts,effects,behaviours,orders={},[],{},[],[]

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
                if c.type.name!='MonoBehaviour':continue
                kind=c.read(check_read=False).m_Script.read().m_ClassName
                if kind=='TextMeshPro':
                    cfg=tmp.read(record(c));keys={}
                    if any(cfg.get(k) for k in ['m_enableAutoSizing','m_isRightToLeft','m_enableVertexGradient']):
                        raise ValueError('Restore this original TMP layout branch before binding')
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
                        lineSpacing=cfg['m_lineSpacing'],margin=list(cfg['m_margin'].values())))
                elif kind=='TMPEffect':
                    cfg=schemas.read(record(c))
                    if any(cfg[k] for k in ['linearDodge','useVertexColor','RealtimeUpdate','outline2','underlay','glow','gray']):
                        raise ValueError('Recover this original TMP effect branch before binding')
                    effects[path]=dict(dilate=cfg['dilate'],outline=bool(cfg['outline']),width=cfg['outlineWidth'],color=list(cfg['outlineColor'].values()))
                elif kind in ['CharacterDamageUI','DamageFloatSize']:
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
        tip=ptr(cfg['mTipUi'],owner);tip_path=paths[(tip.assets_file.name,tip.path_id)]
        size=next(v[2]['mSize'] for v in behaviours if v[0]=='DamageFloatSize')
        clips={}

        def collect(node):
            for animation in node.get('animations',[]):clips[(animation['owner'],animation['name'])]=(animation['source'],node)
            for child in node.get('children',[]):collect(child)

        collect(original['root'])
        for row in rows:
            for animation in row.get('animations',[]):
                evidence,clip_root=clips[(animation['owner'],animation['name'])]
                clip=record(next(o for o in apk.objects(evidence['bundle']) if o.path_id==int(evidence['path_id'])))
                tracks,duration,loop,unknown=mecanim_tracks(clip,clip_root)
                if unknown:raise ValueError('Unrestored original world tip animation: '+str(unknown))
                animation.update(normalize_hud_animation(dict(animation,tracks=tracks,duration=duration,loop=loop,unconverted_bindings=unknown)))
        prefab=dict(effectType=typ,rootName=original['root']['name'],nodes=rows,textPath=tip_path,
            showTime=cfg['mShowTime'],height=cfg['mHeight'],sceneOffset=list(cfg['mSceneOffset'].values()),
            cameraScale=size,sortingOrderStart=orders[0])
        bindings.append(dict(key='damage_'+original['root']['name'].removesuffix('_Scene'),prefab=prefab,texts=texts))
    OUTPUT.write_bytes((json.dumps(dict(manifest=apk.version,bindings=bindings),ensure_ascii=True,indent=2)+'\n').encode())
    receipt=dict(manifest=apk.version,inputs=list(inputs.values()),language_sources=language_sources,
        world_source_sha256=sha(source_path),rules_sha256=sha(rules_path),generator_sha256=sha(__file__),
        shared_assets=list(shared_assets.values()),output_path=OUTPUT.relative_to(ROOT).as_posix(),output_sha256=sha(OUTPUT),
        operation='Follow the actual three configured DamageScene prefabs. Restore native transforms/RectTransforms, TMP fields and original font/material identities, single-outline TMPEffect, localized text, full clip bindings, sorting, camera scale, lifetime, height and scene offset. Reuse existing native assets only when their raw official identities match.')
    receipt['official_code']=[]
    for version,directory in [('854_156383',ROOT/'.tools/source-packages/official-code'),
            ('871_156598',ROOT/'.tools/source-packages/current/871_156598/official-code')]:
        receipt['official_code'].append(dict(manifest=version,files=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p))
            for p in [directory/n for n in ['client-source/CharacterDamageUI.cs','client-source/CharacterDamageUIManager.cs',
                'client-source/DamageFloatSize.cs','framework-firstpass-source/MultiLanguageText.cs']]]))
    RECEIPT.write_bytes((json.dumps(receipt,ensure_ascii=False,indent=2)+'\n').encode())
    refresh_runtime()
    print('Restored original world tip types:',[v['prefab']['effectType'] for v in bindings])


if __name__=='__main__':
    if sys.argv[1:]==['--refresh-runtime']:refresh_runtime()
    elif sys.argv[1:]:raise SystemExit('Use no arguments to extract, or --refresh-runtime after validation.')
    else:build()
