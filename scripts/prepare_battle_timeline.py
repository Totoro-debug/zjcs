"""Extract the official scene timeline's renderers, materials and native Animator inputs."""
from __future__ import annotations
import hashlib
import io
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_animation import mecanim_tracks
from official_shader_programs import gles_programs
from official_unity_schema import ScriptSchemas
from refresh_current_ui_graphics import object_path
from UnityPy.classes.PPtr import PPtr

DEST = ROOT/'unity/Assets/Official/Combat/Timeline'
OUTPUT = DEST/'Resources/Combat/battle-timeline.json'
RECEIPT = ROOT/'assets/reference/battle-hud/timeline-source-manifest.json'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build():
    apk, schemas = Apk(), ScriptSchemas()
    inputs, textures, sprites, materials, shaders = {}, {}, {}, {}, {}
    source_path = ROOT/'unity/Assets/Official/Combat/Portraits/Resources/Combat/battle-portraits.json'
    foundation = json.loads(source_path.read_bytes())
    layers = json.loads((ROOT/'unity/Assets/Game/Resources/Combat/battle-scene-bindings.json').read_bytes())['layers']
    layer_orders = {row['uniqueID']:i for i,row in enumerate(layers)}

    def record(obj):
        info = dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
        inputs[(info['bundle'],info['path_id'])] = info
        return obj

    def identity(obj):
        return obj.assets_file.name+':'+str(obj.path_id)

    def texture(obj):
        record(obj)
        key = identity(obj)
        if key not in textures:
            raw = obj.read()
            if obj.type.name != 'Texture2D':
                raise ValueError('Timeline material requires an unsupported original texture: '+obj.type.name)
            filename = raw.m_Name+'-'+sha(obj.get_raw_data())[:12]+'.png'
            path = DEST/'Source/Textures'/filename
            path.parent.mkdir(parents=True,exist_ok=True)
            stream=io.BytesIO(); raw.image.save(stream,format='PNG'); path.write_bytes(stream.getvalue())
            textures[key] = dict(key=key,path=path.relative_to(ROOT/'unity').as_posix(),sha256=sha(path.read_bytes()),
                                 width=raw.m_Width,height=raw.m_Height,filter=raw.m_TextureSettings.m_FilterMode,
                                 wrapU=raw.m_TextureSettings.m_WrapU,wrapV=raw.m_TextureSettings.m_WrapV)
        return key

    def sprite(obj):
        record(obj)
        key=identity(obj)
        if key not in sprites:
            raw=obj.read()
            # Retain the original full texture and sprite rect so material UV panning
            # does not change when restoring assets independently of Android atlases.
            if raw.m_RD.texture.path_id:
                texture_key=texture(raw.m_RD.texture.deref())
            else:
                atlas=record(raw.m_SpriteAtlas.deref()).read()
                packed=next(v for k,v in atlas.m_RenderDataMap if k==raw.m_RenderDataKey)
                atlas_texture=record(packed.texture.deref()).read()
                if packed.alphaTexture.path_id:record(packed.alphaTexture.deref())
                image=raw.image
                if image.size!=(int(raw.m_Rect.width),int(raw.m_Rect.height)):
                    raise ValueError('Restore original atlas trim offsets for '+raw.m_Name+': '+str(image.size))
                texture_key=key+':unpacked'
                path=DEST/'Source/Textures'/(raw.m_Name+'-'+sha(obj.get_raw_data())[:12]+'.png')
                path.parent.mkdir(parents=True,exist_ok=True)
                stream=io.BytesIO();image.save(stream,format='PNG');path.write_bytes(stream.getvalue())
                textures[texture_key]=dict(key=texture_key,path=path.relative_to(ROOT/'unity').as_posix(),
                    sha256=sha(path.read_bytes()),width=image.width,height=image.height,
                    filter=atlas_texture.m_TextureSettings.m_FilterMode,
                    wrapU=atlas_texture.m_TextureSettings.m_WrapU,wrapV=atlas_texture.m_TextureSettings.m_WrapV,
                    operation='Decode the official atlas sprite, preserving its full original rect')
            sprites[key]=dict(key=key,name=raw.m_Name,texture=texture_key,
                rect=[getattr(raw.m_Rect,k) for k in ('x','y','width','height')],
                pivot=[raw.m_Pivot.x,raw.m_Pivot.y],border=[getattr(raw.m_Border,k) for k in 'xyzw'],ppu=raw.m_PixelsToUnits)
        return key

    def material(obj):
        record(obj)
        key=identity(obj)
        if key not in materials:
            raw=obj.read(); shader_obj=record(raw.m_Shader.deref()); shader=shader_obj.read(); shader_key=identity(shader_obj)
            if shader_key not in shaders:
                programs=gles_programs(shader,with_keywords=True)
                shader_folder=DEST/'Source/Shaders'/sha(shader_obj.get_raw_data())[:16]
                shader_folder.mkdir(parents=True,exist_ok=True)
                files=[]
                for index,program in enumerate(programs):
                    path=shader_folder/f'{index}.glsl.txt';path.write_bytes(program['source'].encode())
                    files.append(dict(path=path.relative_to(ROOT/'unity').as_posix(),sha256=sha(path.read_bytes()),
                                      keywords=program['keywords'],type=program['type']))
                shaders[shader_key]=dict(key=shader_key,name=shader.m_ParsedForm.m_Name,programs=files,
                    parsed=shader_obj.read_typetree()['m_ParsedForm'])
            props=raw.m_SavedProperties
            materials[key]=dict(key=key,name=raw.m_Name,shader=shader_key,queue=raw.m_CustomRenderQueue,
                keywords=raw.m_ValidKeywords,
                floats=[dict(name=k,value=v) for k,v in props.m_Floats],
                colors=[dict(name=k,value=[getattr(v,a) for a in 'rgba']) for k,v in props.m_Colors],
                textures=[dict(name=k,texture=texture(v.m_Texture.deref()) if v.m_Texture.path_id else '',
                               scale=[v.m_Scale.x,v.m_Scale.y],offset=[v.m_Offset.x,v.m_Offset.y]) for k,v in props.m_TexEnvs])
        return key

    def transition(wrapped,names):
        t=wrapped['data']
        return dict(destination=t['m_DestinationState'],duration=t['m_TransitionDuration'],fixedDuration=t['m_HasFixedDuration'],
            exitTime=t['m_ExitTime'],hasExitTime=t['m_HasExitTime'],offset=t['m_TransitionOffset'],
            canTransitionToSelf=t['m_CanTransitionToSelf'],interruptionSource=t['m_InterruptionSource'],
            orderedInterruption=t['m_OrderedInterruption'],conditions=[dict(mode=c['data']['m_ConditionMode'],
                parameter=names[c['data']['m_EventID']],threshold=c['data']['m_EventThreshold']) for c in t['m_ConditionConstantArray']])

    def binding(owner,ref):
        obj=PPtr(**ref,assetsfile=owner.assets_file).deref()
        go=obj if obj.type.name=='GameObject' else obj.read(check_read=False).m_GameObject.deref()
        return '/'.join(object_path(go)[0].split('/')[2:])

    prefabs=[];card_bindings={};scene_bindings={}
    for key,bundle in (('round_scene','prefabs_ui_fight_gameroundscene'),
                       ('round_character_scene','prefabs_ui_fight_gameroundcharacteritem_scene')):
        original=next(p for p in foundation['prefabs'] if p['key']==key)
        nodes=[dict(n,children=[]) for n in original['nodes']]
        for i,node in enumerate(nodes):
            node['path']='/'+node['name'] if node['parent']<0 else nodes[node['parent']]['path']+'/'+node['name']
            if node['parent']>=0:nodes[node['parent']]['children'].append(node)
        by_path={n['path']:n for n in nodes}
        controller=None; unsupported=[]
        for obj in apk.objects(bundle):
            if obj.type.name not in ('SpriteRenderer','MeshRenderer','Animator','MonoBehaviour','ParticleSystem','ParticleSystemRenderer'):
                continue
            raw=obj.read(check_read=False)
            if not raw.m_GameObject.path_id:continue
            go=raw.m_GameObject.read();tr=next(c.component.read() for c in go.m_Component if c.component.type.name in ('Transform','RectTransform'))
            names=[go.m_Name]
            while tr.m_Father.path_id:
                tr=tr.m_Father.read();names.insert(0,tr.m_GameObject.read().m_Name)
            path='/'+('/'.join(names))
            if path not in by_path:continue
            node=by_path[path]
            if obj.type.name in ('ParticleSystem','ParticleSystemRenderer'):
                record(obj);unsupported.append(dict(path=path,type=obj.type.name));continue
            if obj.type.name=='MonoBehaviour':
                name=raw.m_Script.read().m_ClassName
                if name=='GameRoundCharacterItemScene':
                    f=schemas.read(record(obj))
                    card_bindings.update({target:binding(obj,f[source]) for target,source in (
                        ('faction','FactionBgSr'),('mainPlayerEffect','MainPlayerEffect'),
                        ('monster','MonsterIconSr'),('monsterHurt','MonsterHurtSr'),('npcRoot','NpcIconGO'),('npc','NpcIconSr'))})
                if name=='GameRoundPortraitETC2SceneHandler':
                    f=schemas.read(record(obj))
                    card_bindings.update(playerRoot=binding(obj,f['PlayerIconGo']),player=binding(obj,f['HeadSr']))
                if name=='GameRoundScene':
                    f=schemas.read(record(obj))
                    scene_bindings.update(line=binding(obj,f['mLine']),slot=binding(obj,f['mIconRoot']))
                if name=='CircleSpriteRenderer':
                    f=schemas.read(record(obj))
                    node['circle']=dict(radius=f['Radius'],segments=f['Segments'],offset=[f['UVOffset'][a] for a in 'xy'],
                        color=[f['Color'][a] for a in 'rgba'],sprite='')
                    if f['Sprite']['m_PathID']:
                        node['circle']['sprite']=sprite(PPtr(**f['Sprite'],assetsfile=obj.assets_file).deref())
                continue
            record(obj)
            if obj.type.name in ('SpriteRenderer','MeshRenderer'):
                node['renderer']=dict(kind=obj.type.name,enabled=bool(raw.m_Enabled),
                    sortingLayerId=raw.m_SortingLayerID,order=layer_orders[raw.m_SortingLayerID&0xffffffff]*1000+raw.m_SortingOrder,
                    materials=[material(p.deref()) for p in raw.m_Materials])
                if obj.type.name=='SpriteRenderer':
                    node['renderer'].update(sprite=sprite(raw.m_Sprite.deref()) if raw.m_Sprite.path_id else '',
                        color=[getattr(raw.m_Color,a) for a in 'rgba'],size=[raw.m_Size.x,raw.m_Size.y],
                        drawMode=raw.m_DrawMode,flipX=raw.m_FlipX,flipY=raw.m_FlipY,
                        maskInteraction=raw.m_MaskInteraction,spriteSortPoint=raw.m_SpriteSortPoint)
            if obj.type.name=='Animator':
                ctrl=record(raw.m_Controller.deref());data=ctrl.read_typetree();names=dict(data['m_TOS']);con=data['m_Controller']
                if len(con['m_LayerArray'])!=1 or len(con['m_StateMachineArray'])!=1:raise ValueError('Restore original timeline controller layers')
                machine=con['m_StateMachineArray'][0]['data'];clips=[]
                for ptr in ctrl.read().m_AnimationClips:
                    clip_obj=record(ptr.deref());clip=clip_obj.read()
                    tracks,duration,loop,unknown=mecanim_tracks(clip_obj,nodes[0])
                    if unknown:raise ValueError('Unresolved timeline animation properties: '+str(unknown))
                    clips.append(dict(name=clip.m_Name,duration=duration,loop=loop,frameRate=clip.m_SampleRate,
                        tracks=[dict(path=t['path'][len(nodes[0]['path'])+1:],property=t['property'],
                                     keys=[dict(zip(('time','value','inTangent','outTangent'),k)) for k in t['keys']]) for t in tracks],
                        events=[dict(functionName=e.functionName,time=e.time,data=e.data,floatParameter=e.floatParameter,
                                     intParameter=e.intParameter,messageOptions=e.messageOptions) for e in clip.m_Events]))
                states=[]
                for wrapped in machine['m_StateConstantArray']:
                    s=wrapped['data'];leaves=[n['data'] for b in s['m_BlendTreeConstantArray'] for n in b['data']['m_NodeArray']]
                    if len(leaves)>1 or any(n['m_ChildIndices'] for n in leaves):raise ValueError('Restore original timeline blend tree')
                    clip_index=leaves[0]['m_ClipID'] if leaves else -1
                    states.append(dict(name=names[s['m_NameID']],clip=clip_index if clip_index<len(clips) else -1,
                        speed=s['m_Speed'],writeDefaultValues=s['m_WriteDefaultValues'],
                        transitions=[transition(t,names) for t in s['m_TransitionConstantArray']]))
                parameters=[]
                for p in con['m_Values']['data']['m_ValueArray']:
                    if p['m_Type']!=9:raise ValueError('Unexpected timeline parameter type')
                    parameters.append(dict(name=names[p['m_ID']],type=p['m_Type']))
                controller=dict(name=ctrl.read().m_Name,parameters=parameters,defaultState=machine['m_DefaultState'],
                    states=states,clips=clips,anyTransitions=[transition(t,names) for t in machine['m_AnyStateTransitionConstantArray']])
        for node in nodes:
            node['hasRenderer']='renderer' in node
            node['hasCircle']='circle' in node
        prefabs.append(dict(key=key,nodes=[{k:v for k,v in n.items() if k!='children'} for n in nodes],
                            hasController=controller is not None,controller=controller,unsupported=unsupported))
    scene_settings=next(c['fields'] for p in foundation['prefabs'] if p['key']=='round_scene' for c in p['components'] if c['type']=='GameRoundScene')
    card_settings=next(c['fields'] for p in foundation['prefabs'] if p['key']=='round_character_scene' for c in p['components'] if c['type']=='GameRoundCharacterItemScene')
    hud_source=json.loads((ROOT/'assets/reference/battle-hud/source-manifest.json').read_bytes())['prefabs']['main']
    game_round=next(record(o) for o in apk.objects(hud_source['bundle']) if o.type.name=='MonoBehaviour'
        and o.read(check_read=False).m_Script.read().m_ClassName=='GameRoundUI')
    round_fields=schemas.read(game_round)
    scene_prefab=PPtr(**round_fields['IconPrefabScene'],assetsfile=game_round.assets_file).deref()
    if scene_prefab.read().m_Name!='GameRoundCharacterItem_Scene':raise ValueError('The official HUD selects a different timeline prefab')
    record(scene_prefab)
    data=dict(manifest=apk.version,sceneSize=scene_settings['mSize'],z=scene_settings['mZValue'],deadTime=card_settings['DeadTime'],
              minDistance=round_fields['MinDist'],moveDuration=round_fields['IconMoveDuration'],
              cardBindings=card_bindings,sceneBindings=scene_bindings,anchor=binding(game_round,round_fields['IconBack']),
              prefabs=prefabs,textures=list(textures.values()),sprites=list(sprites.values()),
              materials=list(materials.values()),shaders=list(shaders.values()))
    OUTPUT.parent.mkdir(parents=True,exist_ok=True)
    OUTPUT.write_bytes((json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n').encode())
    def file_hashes(paths):
        return [dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in paths]
    official_code=ROOT/'.tools/source-packages/official-code/client-source'
    runtime=ROOT/'unity/Assets/Game/Runtime'
    RECEIPT.write_text(json.dumps(dict(manifest=apk.version,inputs=list(inputs.values()),
        foundation_input=dict(path=source_path.relative_to(ROOT).as_posix(),sha256=sha(source_path.read_bytes())),
        auxiliary_inputs=file_hashes([ROOT/'unity/Assets/Game/Resources/Combat/battle-scene-bindings.json',
                                     ROOT/'assets/reference/battle-hud/source-manifest.json']),
        official_code=file_hashes([official_code/(name+'.cs') for name in ('GameRoundUI','GameRoundScene',
            'GameRoundCharacterItemScene','GameRoundPortraitETC2SceneHandler','CircleSpriteRenderer')]),
        runtime=file_hashes([runtime/p for p in ('Official/WorldUI/WorldCircleSpriteRenderer.cs',
            'Presentation/Combat/OfficialBattleTimelineAssets.cs','Presentation/Combat/OfficialBattleSceneTimelineView.cs',
            'Presentation/Combat/OfficialBattleTimelineLifetime.cs','Presentation/Character/CharacterNativePortraitLease.cs',
            'Presentation/World/OfficialMap10GameRuntime.BattlePortrait.cs')]),
        shader_conversions=file_hashes(sorted((runtime/'Presentation/Combat/Shaders').glob('*.shader'))),
        generator_sha256=sha(Path(__file__).read_bytes()),output=OUTPUT.relative_to(ROOT).as_posix(),
        output_sha256=sha(OUTPUT.read_bytes()),textures=list(textures.values()),
        operation='Preserve original scene timeline Transform hierarchy, Sprite texture/rect/PPU/border, material properties, compiled GLES programs and native Animator tracks/states/AnyState transitions. Particle bindings remain explicitly listed for subsequent restoration.'),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Official scene timeline:',len(sprites),'sprites,',len(materials),'materials,',len(shaders),'shaders')


if __name__=='__main__':
    build()
