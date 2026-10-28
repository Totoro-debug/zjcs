"""Extract the exact native Model dissolve clock, retaining compiled payloads."""
from __future__ import annotations
import copy
from pathlib import Path
import json
from official_apk import ROOT, Apk
import UnityPy
from UnityPy.enums import BuildTarget
from UnityPy.helpers.Tpk import get_typetree_node
from prepare_unity_map13_prop_hierarchies import key, sha, write, encode
from prepare_unity_map13_prop_parts import meta

DEST=ROOT/'unity/Assets/Official/World/Map/13/NativeTreeModelAnimator'
RECEIPT=ROOT/'assets/reference/official-map/map13-native-tree-model-animator-source-manifest.json'


def build():
    apk=Apk.current();graph_path=ROOT/'unity/Assets/Official/World/Map/13/Source/native-entity-object-graph.json';graph=json.loads(graph_path.read_bytes())
    identity='cab-bf83731e5894550948fa63834ea3f4bd:1764703498556813374';indexed={key(o['cab'],o['path_id']):o for o in graph['objects']}
    source=indexed[identity];objects=apk.objects(source['input']);native=objects[0].assets_file;bundle=native.parent
    by_key={key(o.assets_file.name,o.path_id):o for o in objects};kept={};pending=[identity]
    def pointers(value):
        if isinstance(value,dict):
            if set(value)=={'m_FileID','m_PathID'}:yield value
            else:
                for item in value.values():yield from pointers(item)
        elif isinstance(value,(tuple,list)):
            for item in value:yield from pointers(item)
    while pending:
        current=pending.pop()
        if current in kept:continue
        obj=by_key[current]
        if obj.type.name not in ('AnimatorController','AnimationClip'):raise ValueError('Additional Model clock dependency needs migration')
        if sha(obj.get_raw_data())!=indexed[current]['payload_sha256']:raise ValueError('Original Model clock payload changed')
        kept[current]=obj
        for p in pointers(obj.read_typetree(check_read=True)):
            if not p['m_PathID']:continue
            if p['m_FileID']:raise ValueError('External Model clock dependency needs migration')
            pending.append(key(native.name,p['m_PathID']))
    if len(kept)!=2:raise ValueError('Original Model dissolve closure changed')
    records=[dict(key=sha(k.encode())[:24],identity=k,type=o.type.name,name=o.read_typetree()['m_Name'],assetName='__zjcs/map13/native/'+sha(k.encode())[:24],source=apk.provenance(o),original_payload_sha256=sha(o.get_raw_data())) for k,o in kept.items()]
    asset_bundle,=[o for o in objects if o.type.name=='AssetBundle'];body=copy.deepcopy(asset_bundle.read_typetree())
    body['m_PreloadTable']=[dict(m_FileID=0,m_PathID=o.path_id) for o in kept.values()]
    body['m_Container']=[(r['assetName'],dict(preloadIndex=0,preloadSize=len(kept),asset=dict(m_FileID=0,m_PathID=kept[r['identity']].path_id))) for r in records]
    if body['m_MainAsset']['asset']['m_PathID']:raise ValueError('Original bundle main asset requires migration')
    body['m_Dependencies']=[];asset_bundle.save_typetree(body)
    selected=[asset_bundle]+list(kept.values());old_types=list(native.types);types=[];type_ids={}
    for obj in selected:
        old=obj.type_id
        if old not in type_ids:type_ids[old]=len(types);types.append(old_types[old])
        obj.type_id=type_ids[old]
    native.objects={o.path_id:o for o in selected};native.types=types;native.ref_types=[];native.externals=[]
    native.target_platform=BuildTarget.StandaloneWindows64;native._m_target_platform=19;native._enable_type_tree=True
    for typ in native.types:
        if typ.node is None:typ.node=get_typetree_node(typ.class_id,native.version)
        if typ.type_dependencies is None:typ.type_dependencies=()
        for i,node in enumerate(typ.node.traverse()):
            node.m_Index=i
            for field in ('m_TypeFlags','m_RefTypeHash','m_MetaFlag'):setattr(node,field,getattr(node,field) or 0)
            node.m_Version=node.m_Version or 1
    bundle.files={native.name:native};raw=bundle.save(packer='lz4');loaded=UnityPy.load(raw)
    if len(loaded.objects)!=3:raise ValueError('Converted native Model clock inventory differs')
    for obj in loaded.objects:
        if int(obj.assets_file.target_platform)!=19:raise ValueError('Native Model clock target platform differs')
        if obj.type.name=='AssetBundle':
            if obj.read_typetree()!=body:raise ValueError('Native Model clock container differs')
            continue
        record=next(r for r in records if r['identity']==key(obj.assets_file.name,obj.path_id))
        if sha(obj.get_raw_data())!=record['original_payload_sha256']:raise ValueError('Compiled original Model clock payload changed')
    path=DEST/'model-animator.windows.bundle.bytes';write(path,raw);meta(DEST,folder=True);meta(path,'TextScriptImporter')
    data_path=DEST/'catalog.json';write(data_path,encode(dict(manifest=apk.version,sha256=sha(raw),entries=records)));meta(data_path,'TextScriptImporter')
    outputs=[path,data_path,Path(str(path)+'.meta'),Path(str(data_path)+'.meta'),Path(str(DEST)+'.meta')]
    inputs=[graph_path,Path(__file__).resolve()]
    write(RECEIPT,encode(dict(manifest=apk.version,mapClassId=13,method='Original Model AnimatorController and RONGjie AnimationClip stay byte-identical. Separate native animation closure retains all PPtrs internally, adds exact object lookup aliases, embeds source-version type trees and sets Windows64. Original source version is not rewritten; actual engine playback must be verified.',objects=records,source_unity_version=native.unity_version,target_platform=19,coverage='Model dissolve clock only; actual Model subtree and entity lifecycle remain separate.',inputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in inputs],outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in outputs])))
    print('Original Model native clock:',len(records),'objects,',len(raw),'bytes')


if __name__=='__main__':build()
