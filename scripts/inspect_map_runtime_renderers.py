"""Check native ground and partition prefabs for runtime-generated water meshes."""
import json
from prepare_map_regions import Regions, ART, REF, save


def build():
    exporter=Regions();checked={};maps={};failures={}
    def inspect(path):
        if path in checked:return checked[path]
        bundle,root=exporter.locate(path);count=0
        def visit(obj,active=True):
            nonlocal count
            go=obj.read();active=active and bool(go.m_IsActive)
            components=[c.component.deref() for c in go.m_Component]
            for c in components:
                if active and c.type.name=='MonoBehaviour' and exporter.mono(c)[0]=='FordingSpriteRenderer':
                    data=exporter.schemas.read(c);exporter.input(c)
                    if data['m_Enabled'] and data['Sprite']['m_PathID']:count+=1
            transform=next(c.read() for c in components if c.type.name in ['Transform','RectTransform'])
            for child in transform.m_Children:visit(child.read().m_GameObject.deref(),active)
        visit(root);checked[path]=count
        return count
    for cfg in json.loads((ART/'adventure.json').read_text(encoding='utf8'))['tables']['Map/map']:
        mid=str(cfg['ClassId']);paths={}
        full=('Assets/Partition/'+cfg['MapGroundBarePath'] if cfg['MapGroundBarePath'] else 'Assets/Prefabs/Map/'+cfg['MapGroundPath'])+'.prefab'
        try:
            paths[full]='ground';bundle,root=exporter.locate(full)
            if cfg['MapGroundBarePath']:
                partition=next(o for o in exporter.apk.objects(bundle) if o.type.name=='MonoBehaviour' and exporter.mono(o)[0]=='PartitionData')
                data=exporter.schemas.read(partition)
                for entry in data['SceneChunks']+data['Extensions']:paths[entry['Res']+'.prefab']='base'
                for rid,region in enumerate(data.get('RegionDatas') or []):
                    if not region:continue
                    for layer,definition in enumerate(region.get('LayerDatas') or []):
                        if not definition:continue
                        for entry in (definition.get('Scenes') or [])+(definition.get('Extensions') or []):paths[entry['Res']+'.prefab']=f'region/{rid}/{layer}'
            found={path:{'renderers':inspect(path),'scope':scope} for path,scope in paths.items() if inspect(path)}
            maps[mid]=found
            print('Runtime ground scan',mid,'prefabs',len(paths),'fording',len(found),flush=True)
        except Exception as ex:failures[mid]=type(ex).__name__+': '+str(ex)
    save(REF/'ground-runtime-renderer-audit.json',{'manifest':'854_156383','maps':maps,'prefabs':checked,'failures':failures,'inputs':exporter.source_records})
    print('Ground maps',len(maps),'unique prefabs',len(checked),'affected',sum(bool(v)for v in maps.values()),'failures',len(failures),flush=True)
    if failures:raise RuntimeError(failures)


if __name__=='__main__':build()
