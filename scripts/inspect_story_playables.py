"""Inspect original story performance components before converting them."""
import collections, json
from prepare_map_regions import Regions, ART, ROOT, save

def main():
    stories=json.loads((ART/'adventure.json').read_text(encoding='utf8'))['stories']
    media=json.loads((ART/'map-story-media.json').read_text(encoding='utf8'))
    requested={p['mAssetGuid'] for s in stories.values() for n in s['nodes'].values() if n['_type']=='PlayPlayableNode' for p in n['PlayablesProxy'] if p['mAssetGuid']}
    e=Regions();result={};counts=collections.Counter()
    for guid in sorted(requested):
        row=media['assets'].get(guid)
        if row is None:result[guid]={'missing':True};continue
        objects=e.apk.objects(row['bundle']);components=[]
        for obj in objects:
            if obj.type.name!='MonoBehaviour':continue
            kind=obj.read(check_read=False).m_Script.read().m_ClassName
            if kind.startswith(('MyPlayable','Apply')):
                try:cfg=e.schemas.read(obj)
                except Exception as ex:cfg={'error':str(ex)}
                counts[kind]+=1;components.append(dict(kind=kind,id=obj.path_id,owner=obj.assets_file.name,config=cfg))
        result[guid]=dict(asset=row,components=components)
    save(ROOT/'.tools/story-playables-inspection.json',result)
    print(dict(counts));print('Guids',len(requested),'missing',sum(v.get('missing',False) for v in result.values()))

if __name__=='__main__':main()
