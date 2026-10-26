import sys,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--repository-root',type=Path,default=ROOT,help='Defaults to the standard scripts/ parent; useful for isolated candidate verification')
parser.add_argument('--output',type=Path,help='Private development output root, default output/map-transport-22081')
parser.add_argument('--reuse-manifest',type=Path,help='Optional exact locked pack inventory for deduplicating visual assets')
args=parser.parse_args()
ROOT=args.repository_root.resolve();OUT=(args.output or ROOT/'output/map-transport-22081').resolve();OUT.mkdir(parents=True,exist_ok=True)

sys.path.insert(0,str(ROOT/'scripts'))
from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_map_regions import transform
from map_timeline_assets import pointer
import numpy as np
a=Apk();schemas=ScriptSchemas()
index=a.asset('config_ec','/index.g.txt');dirs=json.loads(index.read().m_Script)
output={}
for id in [3001,6543,6549]:
    directory=next(k for k,v in dirs.items() if id in v)
    path='Assets/Prefabs/EcEntity/'+directory+'/'+str(id)+'.prefab'
    key='defaultpackage_'+path.rsplit('/',1)[0].replace('/','_').lower()
    bundle=sorted((b for b in a.index if key.startswith(b.removesuffix('.bundle'))),key=len,reverse=True)[0]
    root=a.asset(bundle,'/'+str(id)+'.prefab');records=[];transforms={};attached=[]
    def walk(obj,parent='',matrix=None):
        go=obj.read();name=parent+'/'+go.m_Name;cs=[c.component.deref() for c in go.m_Component]
        t=next(c for c in cs if c.type.name in ['Transform','RectTransform'])
        m=(matrix if matrix is not None else np.eye(4))@transform(t.read());transforms[t.path_id]=(name,m.tolist())
        for c in cs:
            if c.type.name=='MonoBehaviour':
                basic=c.read(check_read=False)
                if not basic.m_Script.path_id:continue
                kind=basic.m_Script.read().m_ClassName
                if kind in ['AttachAgent','ECAttachAgentView']:
                    cfg=schemas.read(c);attached.append((c,cfg))
                    records.append(dict(path=name,type=kind,data=cfg,source=a.provenance(c),raw_sha256=hashlib.sha256(c.get_raw_data()).hexdigest()))
        for child in t.read().m_Children:walk(child.read().m_GameObject.deref(),name,m)
    walk(root)
    anchors=[]
    for obj,cfg in attached:
        if 'myBoneNameList' in cfg:
            for name,ref in zip(cfg['myBoneNameList'],cfg['myBoneList']):
                if name=='CommonEffectSlot':
                    bone=pointer(ref,obj);anchors.append(dict(name=name,transform=transforms.get(bone.path_id) if bone else None,source=a.provenance(bone) if bone else None))
    output[str(id)]=dict(prefab=path,bundle=bundle,source=a.provenance(root),records=records,anchors=anchors)
    print(id,path,'anchors',anchors,flush=True)
(OUT/'anchors-source-manifest.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
