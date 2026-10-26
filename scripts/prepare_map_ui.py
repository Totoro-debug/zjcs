"""Convert the original in-map HUD, minimap, interaction and story UI prefabs."""
import hashlib
import json
import argparse
from prepare_official_panels import PanelExporter,unique_sibling_paths
from prepare_map_regions import save,ART,REF,ROOT

def story_animations(e,root,panel):
    """Retain the native IStoryPlayAnimation name, clip and object bindings."""
    from official_unity_schema import ScriptSchemas
    from UnityPy.classes.PPtr import PPtr
    schemas=ScriptSchemas();paths={};components=[]
    def visit(obj,parent=''):
        go=obj.read();path=parent+'/'+go.m_Name;paths[(obj.assets_file.name,obj.path_id)]=path
        cs=[c.component.deref() for c in go.m_Component]
        for c in cs:
            paths[(c.assets_file.name,c.path_id)]=path
            if c.type.name=='MonoBehaviour' and e.mono(c)[0]=='MapGalaxyPanelAnim':components.append(c)
        transform=next(c.read() for c in cs if c.type.name in ['Transform','RectTransform'])
        for child in transform.m_Children:visit(child.read().m_GameObject.deref(),path)
    visit(root)
    def binding(value,owner):
        if not value['m_PathID']:return ''
        obj=PPtr(**value,assetsfile=owner.assets_file).deref()
        return paths.get((obj.assets_file.name,obj.path_id),'')
    records={}
    for obj in components:
        raw=obj.get_raw_data();records[obj.assets_file.name+':'+str(obj.path_id)]=dict(e.apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest())
        for row in schemas.read(obj)['mUIData']['mSerializeBuffer']:
            cfg=row['Value'];duration=float(cfg['SpineAnimTime']);clip_path=binding(cfg['Animation'],obj)
            if cfg['Animation']['m_PathID']:
                animation=PPtr(**cfg['Animation'],assetsfile=obj.assets_file).read()
                for pointer in animation.m_Animations:
                    if pointer.path_id and pointer.read().m_Name==cfg['AnimationKey']:
                        duration+=float(e.clip(pointer.deref(),clip_path)['duration']);break
            panel.setdefault('story_animations',{}).setdefault(row['Key'],[]).append(dict(
                path=clip_path,clip=cfg['AnimationKey'],delay=float(cfg['SpineAnimTime']),duration=duration,
                close=bool(cfg['IsStopCloseGo']),active=[dict(path=binding(item['Key'],obj),value=bool(item['Value'])) for item in cfg['ActiveGameObjects']['mSerializeBuffer']],
                spine_path=binding(cfg['GripAnimSequence'],obj),spine_sequence=cfg['AnimDataList']))
    return records


def build():
    parser=argparse.ArgumentParser();parser.add_argument('--groups',nargs='+');args=parser.parse_args()
    e=PanelExporter();panels={};sources={};failures={};animation_inputs={}
    selected={
        'map_world':['WorldPanel','WorldCanvas','SwitchMap'],
        'map_minimap':['MiniMapFullScreenPanel','MiniMapMainRegionDescItem','MiniMapBranchRegionDescItem'],
        'map_common':['MapEntityListMenu','MapEntityListMenuItem','MapNotifyUI','MonsterHpBarForMap','LostGoddessActiveDialog'],
        'map_explore':['ExploreConfirmDialog','ExploreDetailDialog'],
        'story':None,
        'minigame':None,
        'dailydungeon':None,
        'task':None,
        'map_puzzlestage':None,
        'map_hideandseek':None,
        'hideandseek':None,
        'activitymusicfestival':None,
        'worldmap':None,
        'roadblock':None,
        'award':None,
        'elementlink':None,
        'energycircuit':None,
        'doublekitchen':None,
        'mapgalaxy':None,
        'storybook':None,
    }
    for bundle in e.apk.index:
        if bundle.startswith('defaultpackage_assets_prefabs_ui_map_'):
            selected[bundle.removeprefix('defaultpackage_assets_prefabs_ui_').removesuffix('.bundle')]=None
    models=json.loads((ART/'map-models/catalog.json').read_text(encoding='utf8'))
    for model in models['models'].values():
        for driver in model.get('views',[]):
            for ref in driver.get('references',{}).get('RefIds',[]):
                window=ref.get('data',{}).get('mWindowName','')
                if not window:continue
                asset_key='defaultpackage_assets_prefabs_ui_'+window.replace('/','_').lower()+'.prefab'
                candidates=sorted((b for b in e.apk.index if asset_key.startswith(b.removesuffix('.bundle'))),key=len,reverse=True)
                if candidates:selected[candidates[0].removeprefix('defaultpackage_assets_prefabs_ui_').removesuffix('.bundle')]=None
    if args.groups:
        selected={group:None for group in args.groups}
        panels=json.loads((ART/'map-ui.json').read_text(encoding='utf8'))
        previous=json.loads((REF/'ui-source-manifest.json').read_text(encoding='utf8'))
        sources=previous['prefabs'];failures=previous.get('failures',{})
    for group,names in selected.items():
        bundle='defaultpackage_assets_prefabs_ui_'+group+'.bundle'
        if bundle not in e.apk.index:continue
        for path,ptr in e.apk.container(bundle):
            name=path.rsplit('/',1)[-1].removesuffix('.prefab')
            if (names is not None and name not in names) or ptr.type.name!='GameObject':continue
            key=name if name not in panels or sources.get(name,{}).get('path')==path else group+'/'+name
            panel=e.ui(bundle,path)
            if panel['root'] is None:
                sources[name]=dict(e.apk.provenance(ptr.deref()),path=path,kind='world-space anchor');continue
            unique_sibling_paths(panel['root']);panels[key]=panel['root']
            if name=='MapGalaxyPanel':animation_inputs.update(story_animations(e,ptr.deref(),panel['root']))
            sources[key]=dict(e.apk.provenance(ptr.deref()),path=path);print(key,flush=True)
    save(ART/'map-ui.json',panels)
    resources=previous['resources'] if args.groups else {};resources.update(e.records)
    if args.groups:animation_inputs={**previous.get('animation_inputs',{}),**animation_inputs}
    save(REF/'ui-source-manifest.json',dict(manifest='854_156383',prefabs=sources,resources=resources,animation_inputs=animation_inputs,failures=failures,output_sha256=hashlib.sha256((ART/'map-ui.json').read_bytes()).hexdigest()))
    path=ROOT/'assets/reference/official-city/source-manifest.json';m=json.loads(path.read_text(encoding='utf8'));m['resources'].update(e.records);m['data_sha256']['map-ui.json']=hashlib.sha256((ART/'map-ui.json').read_bytes()).hexdigest();save(path,m)
    __import__("official_resource_manifest").write_resources(m)


if __name__=='__main__':build()
