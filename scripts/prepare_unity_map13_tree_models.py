"""Restore both complete original tree Model subtrees, including apple anchors.

The enclosing EC entity, UI subtree, gameplay and Timeline are separate work;
no component inside the selected original Model subtree is omitted.
"""
from __future__ import annotations
import copy
import json
import re
from pathlib import Path
import yaml

from official_apk import ROOT
from prepare_unity_map13_prop_hierarchies import key, sha, write, encode
from prepare_unity_map13_prop_parts import meta, ref, dump

SOURCE=ROOT/'unity/Assets/Official/World/Map/13/Source'
DEST=ROOT/'unity/Assets/Game/Resources/Map13/Native/TreeModels'
RUNTIME=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap13EntityViews'
RECEIPT=ROOT/'assets/reference/official-map/map13-native-tree-models-source-manifest.json'


def build():
    hierarchy_path=SOURCE/'native-prop-hierarchies.json';attachment_path=SOURCE/'native-attachment-followers.json'
    h=json.loads(hierarchy_path.read_bytes());a=json.loads(attachment_path.read_bytes());records={o['key']:o for o in h['objects']}
    parts_proof=RECEIPT.with_name('map13-native-prop-parts-source-manifest.json');parts=json.loads(parts_proof.read_bytes())
    recipe_path=SOURCE/'native-prop-separators.json';parts_recipe=json.loads(recipe_path.read_bytes())
    for proof_path in [RECEIPT.with_name('map13-native-prop-hierarchies-source-manifest.json'),RECEIPT.with_name('map13-native-attachment-follower-source-manifest.json'),parts_proof]:
        proof=json.loads(proof_path.read_bytes())
        for output in proof['outputs']:
            # The geometry view is independently extended with application
            # tint in this module, so only source assets/prefabs are inputs.
            if '/Runtime/' in output['path']:continue
            if sha((ROOT/output['path']).read_bytes())!=output['sha256']:raise ValueError('Original tree source changed: '+output['path'])
    materials={key(m['cab'],m['path_id']):ROOT/m['output']['path'] for m in json.loads((SOURCE/'native-entity-material-bindings.json').read_bytes())['entries']}
    scripts={}
    for name in ['NativeMap13EntityModelBinding','NativeMap13PropFadeRenderer','NativeMap13SlicedSpriteBinding','NativeMap13TreeModelAnimatorLibrary']:
        scripts[name]=meta(RUNTIME/(name+'.cs'),'MonoImporter')
    meta(RUNTIME,folder=True);meta(RUNTIME/'Zjcs.Offline.World.NativeMap13EntityViews.asmdef','AssemblyDefinitionImporter')
    def script(path):return meta(ROOT/path,'MonoImporter')
    scripts['SpriteBinding']=script('unity/Assets/Game/Runtime/Offline/World/NativeMap13EntityAssets/NativeMap13EntitySpriteBinding.cs')
    scripts['SpriteRendererList']=script('unity/Assets/Game/Runtime/Official/StorySourceFade/SpriteRendererList.cs')
    scripts['AttachmentFollower']=script('unity/Assets/Game/Runtime/Official/World/Map13Attachment/AttachmentFollower.cs')
    light_templates=list((ROOT/'unity/Assets/Official/World/Map/13/NativeEnvironmentScene').rglob('*.prefab'))
    light_guid=None
    for p in light_templates:
        text=p.read_text(encoding='utf-8-sig')
        if 'm_LightType:' not in text:continue
        for body in re.split(r'(?m)^--- !u!\d+ &[-\d]+\n',text)[1:]:
            fields=yaml.safe_load(body)
            if fields and 'm_LightType' in fields.get('MonoBehaviour',{}):light_guid=fields['MonoBehaviour']['m_Script']['guid'];break
        if light_guid:break
    if not light_guid:raise ValueError('Original native URP Light2D script binding is unavailable')
    node_by_tf={n['transformKey']:n for n in a['nodes']};node_by_go={n['key']:n for n in a['nodes']}
    model_nodes=[n for n in a['nodes'] if n['name']=='Model'];outputs=[];models=[]
    for model in model_nodes:
        selected_go=[]
        def visit(node):
            selected_go.append(node['key'])
            for child in records[node['transformKey']]['references']:
                if child['field'].startswith('.m_Children['):visit(node_by_tf[key(child['cab'],child['path_id'])])
        visit(model)
        selected=[]
        for identity in selected_go:
            selected.append(records[identity]);selected.extend(records[key(p['cab'],p['path_id'])] for p in records[identity]['references'] if p['field'].startswith('.m_Component['))
        selected=list({r['key']:r for r in selected}.values());ids={r['key']:100001+i for i,r in enumerate(selected)}
        # Reuse the already verified native sub-prefab bodies for the independent
        # geometry adapters; relocate their exact local component references.
        source_renderer=next(r for r in selected if r['type']=='Spine.Unity.SkeletonMecanim')
        part=next(p for p in parts['prefabs'] if p['renderer']==source_renderer['key']);part_recipe=next(p for p in parts_recipe['separators'] if p['renderer']==source_renderer['key'])
        part_records=[]
        for node in part_recipe['nodes']:
            go=records[node['key']];part_records.append(go);part_records.extend(records[key(p['cab'],p['path_id'])] for p in go['references'] if p['field'].startswith('.m_Component['))
        part_records=list({r['key']:r for r in part_records}.values());old_ids={100001+i:r['key'] for i,r in enumerate(part_records)}
        template={}
        text=(ROOT/part['path']).read_text(encoding='utf-8-sig');tokens=re.split(r'(?m)^--- !u!(\d+) &(-?\d+)\n',text)
        def local(value):
            if isinstance(value,dict):
                if set(value)=={'fileID'} and value['fileID'] in old_ids:return ref(ids[old_ids[value['fileID']]])
                return {k:local(v) for k,v in value.items()}
            if isinstance(value,list):return [local(v) for v in value]
            return value
        for i in range(1,len(tokens),3):
            template[old_ids[int(tokens[i+1])]]=(int(tokens[i]),local(yaml.safe_load(tokens[i+2])))
        image_records=[r for r in selected if r['type'] in ('SpriteRenderer','SpriteMask')]
        image_ids={r['key']:200001+i for i,r in enumerate(image_records)};fade_id=300001;owner_id=300002
        follower_sources={f['key']:f for f in a['followers'] if f['key'] in ids}
        skeleton_go=key(source_renderer['cab'],source_renderer['fields']['m_GameObject']['m_PathID'])
        source_gofields=records[skeleton_go]['fields']
        native_classes=dict(GameObject=1,Transform=4,MeshRenderer=23,MeshFilter=33,Animator=95,SpriteRenderer=212,SpriteMask=331,SortingGroup=210)
        documents=[];controller_records=[];external_fade=[]
        def pointer(value,owner,field=''):
            if isinstance(value,dict):
                if set(value)=={'m_FileID','m_PathID'}:
                    if not value['m_PathID']:return ref()
                    references=[p for p in owner['references'] if p['field']==field]
                    if len(references)!=1:raise ValueError('Original Model pointer is not explicit: '+field)
                    identity=key(references[0]['cab'],references[0]['path_id'])
                    if owner['type']=='SpriteRendererList' and field=='.mSkeletonRenderer':
                        if identity!=source_renderer['key']:raise ValueError('Different original Model fade skeleton')
                        return ref(fade_id)
                    if identity in ids:return ref(ids[identity])
                    if owner['type']=='SpriteRendererList' and field.startswith('.FadeInCompleteActiveGO['):
                        if identity not in node_by_go:raise ValueError('Original external fade target is outside the verified hierarchy')
                        external_fade.append(dict(Owner=ref(ids[owner['key']]),Index=int(re.search(r'\[(\d+)\]$',field)[1]),GameObjectKey=identity))
                        # Existing original root is only a nonnull serialization
                        # sentinel for the source OnValidate diagnostic. The
                        # mandatory inactive Prepare replaces it with the exact
                        # external entity object before any runtime activation.
                        return ref(ids[model['key']])
                    if identity in materials:return ref(2100000,meta(materials[identity]),2)
                    if owner['key']==model['transformKey'] and field=='.m_Father':return ref()
                    if field=='.m_Controller':
                        controller_key=sha(identity.encode())[:24];path=ROOT/'unity/Assets/Official/World/Map/13/NativePropAnimators/Controllers'/(controller_key+'.controller')
                        controller_records.append(dict(Animator=ref(ids[owner['key']]),ControllerKey=controller_key))
                        if path.exists():return ref(9100000,meta(path),2)
                        clocks=json.loads((ROOT/'unity/Assets/Official/World/Map/13/NativeTreeModelAnimator/catalog.json').read_bytes())
                        if not any(r['key']==controller_key and r['type']=='AnimatorController' for r in clocks['entries']):raise ValueError('Original Model controller is not restored')
                        return ref()  # Exact native clock is injected before activation.
                    if field=='.m_Sprite' and owner['type'] in ('SpriteRenderer','SpriteMask'):return ref()
                    raise ValueError('Unbound original Model pointer: '+owner['type']+'/'+field+'/'+identity)
                return {k:pointer(v,owner,field+'.'+k) for k,v in value.items()}
            if isinstance(value,list):return [pointer(v,owner,field+'['+str(i)+']') for i,v in enumerate(value)]
            return value
        def mono(go_id,guid,enabled=1):return dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=ref(),m_PrefabInstance=ref(),m_PrefabAsset=ref(),m_GameObject=ref(go_id),m_Enabled=enabled,m_EditorHideFlags=0,m_Script=ref(11500000,guid),m_Name='',m_EditorClassIdentifier='')
        def document(class_id,file_id,kind,body):documents.append('--- !u!'+str(class_id)+' &'+str(file_id)+'\n'+dump({kind:body}))
        for record in selected:
            kind=record['type'];fields=record['fields'];base=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=ref(),m_PrefabInstance=ref(),m_PrefabAsset=ref())
            if kind in native_classes:
                body=pointer(fields,record)
                if kind=='GameObject':
                    if body.pop('m_Tag')!=0:raise ValueError('Original Model tag requires migration')
                    go=record['key'];components=body['m_Component']
                    components.extend(dict(component=ref(image_ids[r['key']])) for r in image_records if key(r['cab'],r['fields']['m_GameObject']['m_PathID'])==go)
                    if go==skeleton_go:components.append(dict(component=ref(fade_id)))
                    if go==model['key']:components.append(dict(component=ref(owner_id)))
                    body=dict(serializedVersion=6,m_Component=components,m_Layer=body['m_Layer'],m_Name=body['m_Name'],m_TagString='Untagged',m_Icon=ref(),m_NavMeshLayer=0,m_StaticEditorFlags=0,m_IsActive=body['m_IsActive'])
                elif kind=='Transform':body=dict(serializedVersion=2,**body)
                elif kind=='Animator':body=dict(serializedVersion=5,**body)
                base.update(body);class_id=native_classes[kind]
            elif record['key'] in template:
                class_id,body=template[record['key']];kind=next(iter(body));base=body[kind]
            elif kind in ('AttachmentFollower','SpriteRendererList','UnityEngine.Rendering.Universal.Light2D'):
                base=pointer({k:v for k,v in fields.items() if k!='m_Script'},record)
                base.update(m_ObjectHideFlags=0,m_CorrespondingSourceObject=ref(),m_PrefabInstance=ref(),m_PrefabAsset=ref(),m_EditorHideFlags=0,m_EditorClassIdentifier='',m_Script=ref(11500000,light_guid if kind.endswith('Light2D') else scripts[kind]))
                class_id=114;kind='MonoBehaviour'
            else:raise ValueError('Original Model component has no migration: '+kind)
            document(class_id,ids[record['key']],kind,base)
        for record in image_records:
            owner=key(record['cab'],record['fields']['m_GameObject']['m_PathID']);body=mono(ids[owner],scripts['SpriteBinding'])
            body.update(SourceKey=record['key'],Renderer=ref(ids[record['key']]) if record['type']=='SpriteRenderer' else ref(),Mask=ref(ids[record['key']]) if record['type']=='SpriteMask' else ref())
            document(114,image_ids[record['key']],'MonoBehaviour',body)
        body=mono(ids[skeleton_go],scripts['NativeMap13PropFadeRenderer']);body['Renderer']=ref(ids[source_renderer['key']]);document(114,fade_id,'MonoBehaviour',body)
        fade_owners=[r for r in selected if r['type']=='SpriteRendererList']
        if len(fade_owners)!=1:raise ValueError('Original tree Model fade owner inventory differs')
        body=mono(ids[model['key']],scripts['NativeMap13EntityModelBinding']);body.update(SourceKey=model['key'],ExternalFadeTargets=external_fade,Animators=controller_records,FadeTargets=[dict(Owner=ref(ids[fade_owners[0]['key']]),Renderer=ref(fade_id),Skeleton=ref(ids[source_renderer['key']]))],Skeletons=[ref(ids[source_renderer['key']])],Sprites=[ref(image_ids[r['key']]) for r in image_records]);document(114,owner_id,'MonoBehaviour',body)
        path=DEST/sha(model['key'].encode())[:24]/'Model.prefab';write(path,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(documents)).encode());meta(path.parent,folder=True);meta(path,'PrefabImporter');outputs.extend([path,Path(str(path)+'.meta'),Path(str(path.parent)+'.meta')])
        models.append(dict(source=model['key'],path=path.relative_to(ROOT).as_posix(),gameObjects=len(selected_go),original_components=len(selected)-len(selected_go),original_objects=len(selected),sprite_bindings=len(image_records),followers=len(follower_sources),source_renderer=source_renderer['key'],external_fade_targets=external_fade,original_keys=[r['key'] for r in selected]))
    if len(models)!=2 or sum(m['gameObjects'] for m in models)!=104:raise ValueError('Original tree Model inventory differs')
    meta(DEST,folder=True);outputs.append(Path(str(DEST)+'.meta'));outputs.append(Path(str(RUNTIME)+'.meta'))
    outputs.extend(p for p in RUNTIME.iterdir() if p.is_file())
    inputs=[hierarchy_path,attachment_path,recipe_path,parts_proof,SOURCE/'native-entity-material-bindings.json',RECEIPT.with_name('map13-native-tree-model-animator-source-manifest.json'),Path(__file__).resolve(),ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13SkeletonView.cs']+[ROOT/p['path'] for p in parts['prefabs']]
    reused=[]
    for name in ['map14-story-source-fade-runtime-source-manifest.json','map14-story-attachment-runtime-source-manifest.json','map14-story-follow-bone-runtime-source-manifest.json']:
        path=RECEIPT.with_name(name);proof=json.loads(path.read_bytes());inputs.append(path);reused.append(path.relative_to(ROOT).as_posix())
        for output in proof['outputs']:
            path=ROOT/output['path']
            if sha(path.read_bytes())!=output['sha256']:raise ValueError('Reused official application port changed: '+output['path'])
            inputs.append(path)
    for folder in ['StorySourceFade','StoryPlayables','StoryAttachment','StoryFollowBone']:
        inputs.extend(p for p in (ROOT/'unity/Assets/Game/Runtime/Official'/folder).iterdir() if p.is_file())
    write(RECEIPT,encode(dict(manifest=h['manifest'],mapClassId=13,method='Both original Model subtrees retain every original component, native field and original component order. Source image PPtrs are explicit pre-activation native Sprite bindings; original Skeleton rendering uses independent native geometry; original application SpriteRendererList reuses its migrated code and explicit actor-color boundary. Existing verified native geometry sub-prefab fields are relocated without reinterpretation.',models=models,
        coverage='Original tree Model subtrees only. Enclosing EC entity, UI, root transforms, state changes, Timeline, input/save and Windows Player remain pending.',reused_application_ports=reused,
        inputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in sorted(set(inputs))],outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in sorted(set(outputs))])))
    print(json.dumps(models,ensure_ascii=False)[:1400])


if __name__=='__main__':build()
