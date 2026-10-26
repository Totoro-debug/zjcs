"""Convert the official galaxy story books, dialog selection and image assets."""
import hashlib,json
from official_tables import Constructors,SOURCE
from official_manifest import read_asset_manifest
from prepare_map_regions import Regions,ART,REF,ROOT,save
from UnityPy.classes.PPtr import PPtr


def build():
    e=Regions();constructors=Constructors()
    obj=e.apk.asset('defaultpackage_assets_config_luban.bundle','/story_book.g.bytes')
    raw=obj.read().m_Script.encode('utf8','surrogateescape');books=constructors.table(raw,'TbStoryBookInfo');e.input(obj)
    native={r['path']:r for name in e.apk.archive.namelist() if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes') for r in read_asset_manifest(e.apk.archive.read(name))}
    settings={};images={}
    for row in native.values():
        if 'storydialogsetting' not in row['path'].lower():continue
        asset=e.apk.asset(row['bundle'],'/'+row['path'].rsplit('/',1)[-1]);e.input(asset)
        for cfg in e.schemas.read(asset).get('mStoryDialogConfigs',[]):settings[str(cfg['StoryId'])]=cfg['DialogPath']
    for path in sorted({r['ImagePath'] for r in books if r['ImagePath']}):
        candidates=[r for key,r in native.items() if key.lower().removesuffix('.png').endswith('/'+path.lower())]
        if len(candidates)!=1:raise ValueError((path,'Expected one original image',candidates))
        row=candidates[0];asset=e.apk.asset(row['bundle'],'/'+row['path'].rsplit('/',1)[-1]);e.input(asset)
        if asset.type.name=='Texture2D':
            name=row['path'].rsplit('/',1)[-1].rsplit('.',1)[0]
            sprites=[obj for obj in e.apk.objects(row['bundle']) if obj.type.name=='Sprite' and obj.read().m_Name==name]
            if len(sprites)!=1:raise ValueError((path,'Expected original Sprite subasset',len(sprites)))
            asset=sprites[0];e.input(asset)
        images[path]=e.sprite(PPtr(m_FileID=0,m_PathID=asset.path_id,assetsfile=asset.assets_file))
    panel=e.apk.asset('defaultpackage_assets_prefabs_ui_mapgalaxy.bundle','/MapGalaxyPanel.prefab')
    flight={};layers={};ship_states={};paths={}
    def visit(obj,parent=''):
        go=obj.read();path=parent+'/'+go.m_Name
        paths[(obj.assets_file.name,obj.path_id)]=path
        components=[p.component.deref() for p in go.m_Component]
        for c in components:paths[(c.assets_file.name,c.path_id)]=path
        t=next(c.read() for c in components if c.type.name in ['Transform','RectTransform'])
        for child in t.m_Children:visit(child.read().m_GameObject.deref(),path)
    visit(panel)
    def binding(value,owner):
        obj=PPtr(**value,assetsfile=owner.assets_file).deref()
        return paths[(obj.assets_file.name,obj.path_id)]
    for pointer in panel.read().m_Component:
        component=pointer.component.deref()
        if component.type.name!='MonoBehaviour':continue
        script=component.read(check_read=False).m_Script.read().m_ClassName
        if script!='MapGalaxyPanel':continue
        e.input(component);cfg=e.schemas.read(component)
        flight=dict(ready=cfg['mReadyChangeTime'],going=cfg['mChangeTime'],clip=cfg['mDefaultAnimKey'],
                    overrides={str(r['Key']):r['Value'] for r in cfg['mAccelerateAnimKeyDic']['mSerializeBuffer']})
        slots={name:binding(cfg[field],component) for name,field in [('background','mBgSlot'),('planet','mMapIconSlot')]}
        by_guid={r['guid']:r for r in native.values()}
        for row in cfg['mLayerPrefabProxies']['mSerializeBuffer']:
            entries=[]
            for item in row['Value']:
                asset=by_guid[item['AssetGuidProxy']['mAssetGuid']]
                entries.append(dict(prefab=asset['path'].rsplit('/',1)[-1].removesuffix('.prefab'),slot=binding(item['Slot'],component)))
            key=str(row['Key'])
            if key in layers and layers[key]!=entries:raise ValueError('Conflicting original galaxy layer bindings')
            layers[key]=entries
        state_by_layer={str(r['Key']):r['Value'] for r in cfg['mLayerShipStateDic']['mSerializeBuffer']}
        refs={r['rid']:r for r in cfg['references']['RefIds']}
        for ref in cfg['mAirshipStateSwitcher']['mStateConfigs']:
            state_cfg=refs[ref['rid']]['data']['mStateGameObjectData']['GoConfig']
            for row in state_cfg:
                ship_states[str(row['State']['mValue'])]=[binding(v,component) for v in row['GameObjects']]
    if not flight:raise ValueError('Original galaxy flight settings are absent')
    save(ART/'map-galaxy.json',dict(books={str(r['Id']):r for r in books},dialogs=settings,images=images,flight=flight,
                                 layers=layers,ship_states=ship_states,ship_state_by_layer=state_by_layer,slots=slots))
    save(REF/'galaxy-source-manifest.json',dict(manifest='854_156383',inputs=e.source_records,resources=e.records,schemas=e.schemas.records,
        constructor_sha256=hashlib.sha256((SOURCE/'TbStoryBookInfo.cs').read_bytes()).hexdigest(),
        outputs={'map-galaxy.json':hashlib.sha256((ART/'map-galaxy.json').read_bytes()).hexdigest()},
        operation='Original Luban story-book nodes, options and successors; original dialog paths, cropped Sprite images, per-GalaxyId prefab slots and airship state bindings'))
    print('Galaxy story-book nodes',len(books),'dialogs',len(settings),'images',len(images),flush=True)


if __name__=='__main__':build()
