"""Restore original no-match initialization and animation sound bindings on entry pages."""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import struct
import urllib.request

from current_component_schemas import CurrentSchemas
from official_apk import Apk
from official_source import OfficialSource
from prepare_dungeon_ui_bindings import relative_transform_path
from UnityPy.classes import PPtr

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT/'unity/Assets/Game/Resources/Dungeons/matching-panel-bindings.json'
RECEIPT = ROOT/'assets/reference/dungeon-ui/matching-panel-source-manifest.json'
RUNTIME = ROOT/'unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonEntryBindings.cs'
PREFABS = [('TravelNotesPanel','prefabs_ui_home'),
           ('DailyDungeonEntryPanel','prefabs_ui_dailydungeon'),('DailyDungeonEntryPanelItem','prefabs_ui_dailydungeon'),
           ('DailyDungeonDialog','prefabs_ui_dailydungeon'),('ArenaPanel','prefabs_ui_arena')]
LAYOUTS = ('GridLayoutGroup','VerticalLayoutGroup','HorizontalLayoutGroup','HorizontalLayoutGroupJSZX',
           'LayoutElement','LayoutElementClamper','ContentSizeFitter','RectMask2D')

def layout_record(obj, cls, fields, path, names):
    raw=obj.get_raw_data()
    row=dict(path='/'.join(map(str,path)),objects=names,type=cls,enabled=bool(fields['m_Enabled']))
    if cls=='GridLayoutGroup':
        if len(raw)!=84: raise ValueError('Review original grid layout size')
        values=struct.unpack_from('<7i4f2i',raw,32)
        row.update(padding=list(values[:4]),alignment=values[4],startCorner=values[5],startAxis=values[6],
            cellSize=list(values[7:9]),spacing=list(values[9:11]),constraint=values[11],constraintCount=values[12])
    elif cls in ('VerticalLayoutGroup','HorizontalLayoutGroup'):
        if len(raw)!=84: raise ValueError('Review original horizontal/vertical layout size')
        values=struct.unpack_from('<5if7i',raw,32)
        row.update(padding=list(values[:4]),alignment=values[4],layoutSpacing=values[5],flags=list(values[6:]))
    elif cls in ('HorizontalLayoutGroupJSZX','LayoutElement'):
        from prepare_character_creation_opening import decode_layout_component
        decoded={}
        if not decode_layout_component(cls,raw,decoded): raise ValueError('Unbound original layout')
        if cls=='LayoutElement':row['element']=decoded['layout_element']
        else:
            value=decoded['horizontal_layout']
            row.update(padding=value['padding'],alignment=value['alignment'],layoutSpacing=value['spacing'],
                spacingList=value['spacing_list'],flags=value['flags'])
    elif cls=='LayoutElementClamper':
        row['clamp']={key:fields[key] for key in ('mMaxPreferredWidth','mMaxPreferredHeight')}
    elif cls=='ContentSizeFitter':
        if len(raw)!=40: raise ValueError('Review original content fitter size')
        row['fit']=list(struct.unpack_from('<2i',raw,32))
    else:
        if len(raw)!=56: raise ValueError('Review original rectangle mask size')
        row.update(padding=list(struct.unpack_from('<4f',raw,32)),softness=list(struct.unpack_from('<2i',raw,48)))
    return row

def state_records(fields, name, nodes, *, sprite_proxies=False):
    refs={value['rid']:value for value in fields['references']['RefIds']}
    configs=fields[name]['mStateConfigs']
    if sprite_proxies:
        if {refs[v['rid']]['type']['class'] for v in configs}!={'StateGameObjectConfig','StateSpriteAssetProxyConfig'}:
            raise ValueError('Review original entry state configurations')
        configs=[v for v in configs if refs[v['rid']]['type']['class']=='StateGameObjectConfig']
    if len(configs)!=1:raise ValueError('Review original object state: '+name+' '+str([refs[v['rid']]['type'] for v in configs]))
    config=refs[configs[0]['rid']]
    if config['type']['class']!='StateGameObjectConfig':raise ValueError('Review original state config: '+name)
    rows=[]
    for value in config['data']['mStateGameObjectData']['GoConfig']:
        paths=[]
        for pointer in value['GameObjects']:
            if pointer['m_FileID']!=0:raise ValueError('Original state target is outside the prefab')
            paths.append('/'.join(map(str,nodes[pointer['m_PathID']][0])))
        rows.append(dict(state=value['State']['mValue'],objects=paths))
    return rows

def sha(raw): return hashlib.sha256(raw).hexdigest()

def file_record(path):
    raw=path.read_bytes()
    if path.suffix in ('.cs','.py'): raw=raw.replace(b'\r\n',b'\n')
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw), hashLineEndings='LF')


def native_hierarchy(path):
    """Read only original GameObject/Transform identities from the native export."""
    blocks = {int(identity): (int(kind), body) for kind, identity, body in re.findall(
        r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- !u!|\Z)', path.read_text(encoding='utf-8'), re.S)}
    names = {}
    for identity, (kind, body) in blocks.items():
        if kind != 1: continue
        value = re.search(r'\n  m_Name: (.*)', body)[1]
        names[identity] = (json.loads(value) if value.startswith('"') else
                           value[1:-1].replace("''", "'") if value.startswith("'") else value)
    nodes = {}
    for identity, (kind, body) in blocks.items():
        if kind not in (4, 224): continue
        game_object = int(re.search(r'm_GameObject: \{fileID: (-?\d+)', body)[1])
        parent = int(re.search(r'm_Father: \{fileID: (-?\d+)', body)[1])
        children = re.search(r'  m_Children:(.*?)(?=\n  \w)', body, re.S)[1]
        nodes[identity] = (names[game_object], parent, [int(value) for value in re.findall(r'fileID: (-?\d+)', children)])
    roots = [identity for identity, node in nodes.items() if node[1] == 0]
    if len(roots) != 1: raise ValueError('Expected one original native prefab root')
    result = {}
    def visit(identity, path=(), parents=()):
        name, _, children = nodes[identity]
        result['/'.join(map(str, path))] = list(parents + (name,))
        for index, child in enumerate(children): visit(child, path + (index,), parents + (name,))
    visit(roots[0])
    return result


def retained_arena(apk):
    # This shipped native panel and its Image/TMP bindings belong to 871. The
    # selected 884 panel adds group controls and cannot supply its layout paths.
    input_path = ROOT/'assets/reference/dungeon-ui/prefab-binding-source-manifest.json'
    receipt = json.loads(input_path.read_bytes())
    original = next(row['source'] for row in receipt['prefabs'] if row['name'] == 'ArenaPanel')
    if receipt['manifest'] != '871_156598' or original['manifest'] != '871_156598':
        raise ValueError('Review retained native ArenaPanel source identity')
    path = ROOT/'.tools/source-packages/historical/871_156598/defaultpackage_assets_prefabs_ui_arena.bundle'
    if not path.exists():
        raw = urllib.request.urlopen(original['url'], timeout=30).read()
        if (len(raw) != original['bundle_size'] or hashlib.md5(raw).hexdigest() != original['bundle_hash']
                or sha(raw) != original['bundle_sha256']):
            raise ValueError('Historical original arena bundle failed source verification')
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
    raw = path.read_bytes()
    if (len(raw) != original['bundle_size'] or hashlib.md5(raw).hexdigest() != original['bundle_hash']
            or sha(raw) != original['bundle_sha256']):
        raise ValueError('Retained original arena bundle failed source verification')
    bundle = apk.env.load_file(Apk.decode(raw), name='retained-871-arena.bundle')
    asset = next(value for value in bundle.files.values() if hasattr(value, 'container'))
    prefab = next(value.deref() for key, value in asset.container.items() if key.endswith('/ArenaPanel.prefab'))
    if str(prefab.path_id) != original['path_id']: raise ValueError('Historical arena root identity differs')
    native = ROOT/'unity/Assets/Game/Resources/Dungeons/UI/ArenaPanel.prefab'
    paths = {'/'.join(map(str, path)): list(names) for _, path, names, _ in relative_transform_path(prefab.read())}
    if paths != native_hierarchy(native): raise ValueError('Arena native hierarchy differs from its original source')
    return prefab, original, dict(source=original, cached_input=dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)),
        source_receipt=file_record(input_path), native=file_record(native), nodes=len(paths))

def build():
    if shutil.which('dotnet') is None:
        os.environ['PATH']=str(ROOT/'.tools/dotnet')+os.pathsep+os.environ.get('PATH','')
    apk=Apk.current()
    if apk.version!='884_156692': raise ValueError('Review matching source version before reproduction')
    schemas=CurrentSchemas()
    from official_manifest import read_asset_manifest
    from prepare_character_creation_opening import SpriteCache
    from prepare_dungeon_ui_bindings import sprite_binding, route_texts
    assets_by_guid={r["guid"]:r for r in read_asset_manifest(apk.source.manifest_bytes("DefaultPackage",apk.archive)) if r["guid"]}
    sprites=SpriteCache(apk)
    audio_receipt=ROOT/'assets/reference/official-map/map12-ui-audio-source-manifest.json'
    audio=json.loads(audio_receipt.read_bytes())
    package=next(p for p in audio['packages'] if 'ui_sfx_01' in p['name'])
    current=next(p for p in OfficialSource().manifest('WwiseBnk')['bundles'] if p['name']==package['name'])
    entry='assets/yoo/WwiseBnk/'+current['file']
    if entry in apk.archive.namelist():
        payload=apk.archive.read(entry)
        audio_source=dict(origin='apk_zip',entry=entry,apk_manifest=apk.source.apk_version)
    else:
        path=ROOT/'.tools/source-packages/WwiseBnk'/package['name']
        payload=path.read_bytes()
        audio_source=dict(origin='verified_cache',path=path.relative_to(ROOT).as_posix())
    if len(payload)!=current['size'] or hashlib.md5(payload).hexdigest()!=current['hash'] or sha(payload)!=package['sha256']:
        raise ValueError('Current original UI sound package differs from the converted PCM source')
    events={row['id']:row for row in audio['events'] if row['bank']['package']==package['name']}
    sources=[]; prefabs=[]; retained_sources=[]; travel_language_sources={}
    historical_asset = None; historical_source = None
    def source(obj,**fields):
        provenance = dict(historical_source, path_id=str(obj.path_id)) if obj.assets_file is historical_asset else apk.provenance(obj)
        sources.append(dict(source=provenance,payload_sha256=sha(obj.get_raw_data()),**fields))
    for name,bundle in PREFABS:
        historical_asset = None; historical_source = None
        if name == 'ArenaPanel':
            prefab, historical_source, retained = retained_arena(apk)
            historical_asset = prefab.assets_file
            retained_sources.append(retained)
        else:
            prefab=apk.asset(bundle,'/'+name+'.prefab')
        nodes=list(relative_transform_path(prefab.read()))
        by_id={go.object_reader.path_id:(path,names) for go,path,names,_ in nodes}
        row=dict(name=name,sourceManifest=historical_source['manifest'] if historical_source else apk.version,
                 contents=[],sounds=[],safeAreas=[])
        layouts=[]; initial_states=None; detail_states={}; travel_entries=[]
        source(prefab,prefab=name)
        for go,path,names,_ in nodes:
            for pair in go.m_Component:
                obj=pair.component.deref()
                if obj.type.name!='MonoBehaviour': continue
                cls=obj.read(check_read=False).m_Script.read().m_ClassName
                if cls not in ('GameMatchMiniPanel','AnimationSoundPlayer','SafeArea','DailyDungeonEntryPanel',
                               'DailyDungeonDetailContent','DailyDungeonUnlockContent','DemonBattleEntry','OldPlayEntryBtn')+LAYOUTS: continue
                if cls in ('DailyDungeonDetailContent','DailyDungeonUnlockContent') and name!='DailyDungeonDialog':continue
                fields=schemas.read(obj)
                source(obj,prefab=name,objects=names,fields=fields)
                if cls=='OldPlayEntryBtn':
                    button=PPtr(**fields['mEntryBtn'],assetsfile=obj.assets_file).deref()
                    button_fields=schemas.read(button)
                    button_path,button_names=by_id[button_fields['m_GameObject']['m_PathID']]
                    row['towerButton']=dict(path='/'.join(map(str,button_path)),objects=button_names)
                    source(button,prefab=name,objects=button_names,fields=button_fields)
                    continue
                if cls=='DemonBattleEntry':
                    refs={r['rid']:r for r in fields['references']['RefIds']}
                    entry=dict(type=cls,path='/'.join(map(str,path)),objects=names,
                        states=state_records(fields,'EntryStateSwitcher',by_id,sprite_proxies=True),
                        countStates=state_records(fields,'UnlockCountSwitcher',by_id),sprites=[],levels=[])
                    for ref in fields['EntryStateSwitcher']['mStateConfigs']:
                        config=refs[ref['rid']]
                        if config['type']['class']!='StateSpriteAssetProxyConfig':continue
                        for item in config['data']['mStateProxyData']['SpriteRendererConfigs']:
                            image=PPtr(**item['Img'],assetsfile=obj.assets_file).deref()
                            image_fields=schemas.read(image)
                            image_path,image_names=by_id[image_fields['m_GameObject']['m_PathID']]
                            source(image,prefab=name,objects=image_names,fields=image_fields)
                            for value in item['Proxy']['mSerializeBuffer']:
                                guid=value['Value']['mAssetGuid']; asset=assets_by_guid[guid]
                                target,=[p.deref() for key,p in apk.container(asset['bundle']) if key==asset['path'] and p.type.name=='Sprite']
                                sprite=sprite_binding(sprites,target.assets_file,0,target.path_id)
                                meta=ROOT/'unity/Assets/StreamingAssets/Dungeons/UI'/(sprite['file']+'.meta')
                                meta.write_text('\n'.join(line.rstrip() for line in meta.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
                                sprite['sourceGuid']=guid
                                entry['sprites'].append(dict(path='/'.join(map(str,image_path)),objects=image_names,
                                    state=value['Key']['mValue'],sprite=sprite))
                    for pointer in fields['DemonBattleLevelContents']:
                        component=PPtr(**pointer,assetsfile=obj.assets_file).deref(); value=schemas.read(component)
                        source(component,prefab=name,fields=value)
                        level={}
                        for key,field in [('title','DemonBattlNameText'),('layer','DemonBattleLeve1Text')]:
                            text=PPtr(**value[field],assetsfile=obj.assets_file).deref(); tf=schemas.read(text)
                            text_path,text_names=by_id[tf['m_GameObject']['m_PathID']]
                            level[key]='/'.join(map(str,text_path))
                            level[key+'Key']=tf['myKey']
                            source(text,prefab=name,objects=text_names,fields=tf)
                        entry['levels'].append(level)
                    travel_entries.append(entry)
                    continue
                if cls in LAYOUTS:
                    layouts.append(layout_record(obj,cls,fields,path,names))
                    continue
                if cls=='DailyDungeonDetailContent':
                    detail_states.update(detailLockStates=state_records(fields,'LockContentStateSwitcher',by_id),
                        previewStates=state_records(fields,'mPreviewStateSwitcher',by_id))
                    continue
                if cls=='DailyDungeonUnlockContent':
                    detail_states['catalogueButtonStates']=state_records(fields,'BtnStateSwitcher',by_id)
                    continue
                if cls=='DailyDungeonEntryPanel':
                    references={value['rid']:value for value in fields['references']['RefIds']}
                    configs=fields['DetailContentStateSwitcher']['mStateConfigs']
                    if len(configs)!=1:raise ValueError('Review original daily initial state')
                    config=references[configs[0]['rid']]
                    if config['type']['class']!='StateGameObjectConfig':raise ValueError('Review original daily state config')
                    initial_states=[]
                    for value in config['data']['mStateGameObjectData']['GoConfig']:
                        paths=[]
                        for pointer in value['GameObjects']:
                            if pointer['m_FileID']!=0:raise ValueError('Original state target is outside the prefab')
                            paths.append('/'.join(map(str,by_id[pointer['m_PathID']][0])))
                        initial_states.append(dict(state=value['State']['mValue'],objects=paths))
                    continue
                if cls=='SafeArea':
                    row['safeAreas'].append(dict(path='/'.join(map(str,path)),objects=names,enabled=bool(fields['m_Enabled']),
                        **{key:(bool(fields[key]) if key.startswith('Disable') else fields[key]) for key in ['PaddingTop','PaddingBottom','DisableTopAdapt','DisableBottomAdapt']}))
                    continue
                if cls=='GameMatchMiniPanel':
                    pointer=fields['mContentGo']
                    if pointer['m_FileID']!=0: raise ValueError('Matching content is outside its prefab')
                    target,target_names=by_id[pointer['m_PathID']]
                    row['contents'].append(dict(path='/'.join(map(str,target)),objects=target_names))
                else:
                    binding=dict(path='/'.join(map(str,path)),objects=names,enabled=bool(fields['m_Enabled']),events=[])
                    for event in fields['mEvents']:
                        obj_ref=PPtr(**event['WwiseObjectReference'],assetsfile=obj.assets_file).deref()
                        value=schemas.read(obj_ref)
                        source(obj_ref,fields=value)
                        rendered=events[value['id']]
                        if rendered['name']!=value['objectName'] or sha((ROOT/rendered['path']).read_bytes())!=rendered['sha256']:
                            raise ValueError('Original animation event differs from its converted local audio')
                        binding['events'].append(dict(id=value['id'],name=value['objectName'],resource=rendered['resource']))
                    row['sounds'].append(binding)
        if name in ('DailyDungeonEntryPanel','DailyDungeonDialog') and (len(row['contents'])!=1 or len(row['sounds'])!=1):
            raise ValueError('Original matching panel bindings are incomplete: '+name)
        if travel_entries:
            keys={level[key+'Key'] for entry in travel_entries for level in entry['levels'] for key in ('title','layer')}
            texts,travel_language_sources,missing=route_texts(apk,keys,keys)
            text_values={value['key']:value['value'] for value in texts}
            if missing:raise ValueError('Original travel entry language is missing')
            for entry in travel_entries:
                for level in entry['levels']:
                    for key in ('title','layer'):level[key+'Format']=text_values[level[key+'Key']]
        layout=dict(layouts=layouts,travelEntries=travel_entries,**detail_states)
        if initial_states is not None:layout['initialStates']=initial_states
        row['layoutJson']=json.dumps(layout,ensure_ascii=False,separators=(',',':'))
        prefabs.append(row)
    code=ROOT/'.tools/source-packages/current'/apk.version/'official-code/client-source/GameMatchMiniPanel.cs'
    text=code.read_text(encoding='utf-8-sig')
    if 'if (!mainPlayer.IsInGameMatch)' not in text or 'mContentGo.SetActive(false)' not in text:
        raise ValueError('Review the original unmatched visibility branch')
    OUTPUT.write_text(json.dumps(dict(manifest=apk.version,prefabs=prefabs),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    OUTPUT.with_name(OUTPUT.name+'.meta').write_text('fileFormatVersion: 2\nguid: 3c6bacaf46a749de95e0c140ec3509e8\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8',newline='\n')
    RECEIPT.parent.mkdir(parents=True,exist_ok=True)
    RECEIPT.write_text(json.dumps(dict(manifest=apk.version,apk_manifest=apk.source.apk_version,
        apk_sha256=apk.source.apk_sha256,travel_language_sources=travel_language_sources,
        sprite_sources=list(sprites.sources.values()),sources=sources,retained_native_sources=retained_sources,code_sources=[file_record(code),
        file_record(code.parents[1]/'launch-source/AnimationSoundPlayer.cs'),
        file_record(code.parents[1]/'framework-firstpass-source/SafeArea.cs'),
        file_record(code.with_name('DailyDungeonEntryPanel.cs'))],
        travel_entry_code_sources=[file_record(code.with_name(n)) for n in ('TravelNoteFirstPage.cs','GamePlayNoteEntryCollection.cs','GamePlayNoteEntry.cs','DemonBattleEntry.cs','DemonBattleLevelContent.cs','OldPlayEntryBtn.cs')],
        tower_entry_condition=dict(code_sources=[file_record(code.parents[1]/'common-source/Common/WorldMapProgressCondition.cs'),
            file_record(code.parents[1]/'common-source/Common/Formula.cs')],
            retained_rules=file_record(ROOT/'unity/Assets/StreamingAssets/Home/rules.json'),
            runtime=file_record(ROOT/'unity/Assets/Game/Runtime/Offline/Home/OfficialHomeRules.cs'),
            integration=file_record(ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs'),
            operation='Use the retained explicit personal function-limit conditions and real saved region progress; preserve original Int2Float times 100 truncation. Whole-map progress and later-tower achievement completion are not supplied by this region binding.'),
        detail_code_sources=[file_record(code.with_name('DailyDungeonDetailContent.cs')),
        file_record(code.with_name('DailyDungeonUnlockContent.cs')),
        file_record(code.parents[1]/'common-source/Common/PlayerDailyDungeonDataExt.cs')],
        audio_source=audio_source,audio_manifest=current,audio_package_sha256=sha(payload),
        reused_audio_receipt=file_record(audio_receipt),runtime=file_record(RUNTIME),
        reused_runtime=[file_record(ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/WorldAnimationSoundPlayer.cs'),
        file_record(ROOT/'unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonUiAudio.cs'),
        file_record(ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/WorldSafeArea.cs')],
        integration=file_record(ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Dungeons.cs'),
        shared_layout_runtime=file_record(ROOT/'unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonUiBindingRuntime.cs'),
        entry_navigation=dict(code_sources=[file_record(code.with_name('PublicBottomBar.cs')),
            file_record(code.parent/'UnityEngine.UI.Ext/TabGroup.cs')],
            integration=file_record(ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs'),
            operation='Keep the existing local UGUI tab input state synchronized with the selected tab, following PublicBottomBar/TabGroup exclusive selection; no layout or artwork changes.'),
        validation=[file_record(ROOT/'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.cs'),
        file_record(ROOT/'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.cs'),
        file_record(ROOT/'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.DungeonEntry.cs')],
        generator=file_record(Path(__file__).resolve()),output=file_record(OUTPUT),
        operation='Bind the original serialized AnimationSoundPlayer event list to the verified existing local PCM and restore GameMatchMiniPanel.RefreshUI/HideContent for entry pages without an active game-match task. Restore serialized original SafeArea fields after original transforms; preserve hierarchy and animation events.',
        limitations=['Active matchmaking requests, target changes, cancellation and countdown are not implemented by this initialization binding.',
            'ArenaPanel uses its verified retained 871_156598 native hierarchy and matching historical layout payloads. The selected 884 group controls are not applied to that older hierarchy; their complete UI migration remains pending.']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Verified original matching bindings:',[(p['name'],len(p['contents']),len(p['sounds'])) for p in prefabs])

if __name__=='__main__': build()
