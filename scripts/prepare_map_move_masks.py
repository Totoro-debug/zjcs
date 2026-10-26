"""Keep native per-prefab movement overrides used when approaching obstacles."""
import hashlib
import json
from prepare_map_regions import Regions, ART, REF, save

def build():
    e=Regions()
    index_obj=e.apk.asset('config_ec','/index.g.txt')
    e.input(index_obj)
    index=json.loads(index_obj.read().m_Script)
    templates=json.loads((ART/'map-entities.json').read_text(encoding='utf8'))['templates']
    paths={str(id):'Assets/Prefabs/EcEntity/'+folder+'/'+str(id)+'.prefab' for folder,ids in index.items() for id in ids if str(id) in templates}
    masks={};failures={}
    for path,full in paths.items():
        try:
            bundle,root=e.locate(full)
            def walk(go):
                components=[c.component.deref() for c in go.read().m_Component]
                for obj in components:
                    if obj.type.name!='MonoBehaviour' or obj.read(check_read=False).m_Script.m_PathID==0 or e.mono(obj)[0]!='OverlapGridMoverOverrideMoveMask':continue
                    value=e.schemas.read(obj);e.input(obj)
                    masks[path]=value['mOverrideWalkableGroundTypes']
                transform=next((c.read() for c in components if c.type.name in ['Transform','RectTransform']),None)
                if transform:
                    for child in transform.m_Children:walk(child.read().m_GameObject.deref())
            walk(root)
        except Exception as ex:failures[path]=str(ex)
    output=ART/'map-move-masks.json';save(output,masks)
    save(REF/'move-masks-source-manifest.json',dict(manifest='854_156383',inputs=e.source_records,schemas=e.schemas.records,failures=failures,models=len(masks),outputs={'map-move-masks.json':hashlib.sha256(output.read_bytes()).hexdigest()}))
    print('Native movement masks',len(masks),'failures',len(failures))

if __name__=='__main__':build()
