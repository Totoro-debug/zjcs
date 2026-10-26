"""Convert pinned whale island standalone-world entry and landing dialog."""
from pathlib import Path
import json
from official_apk import ROOT
from prepare_map_regions import Regions, save
from prepare_map_minimap_ui import MinimapExporterBase, digest
from prepare_home_decoration import components
from prepare_official_panels import unique_sibling_paths
from npc_travel_language import language

CLASSES={'AloneWorldMapEntryUIManager','AloneWorldMapEntryItemLoopView','AloneWorldMapEntryUIItem',
 'AloneWorldMapEntryItemContentNormal','AloneWorldTouchDialog','MapAloneWorldInfoUINormal','StoryNodeRegister','ShowRemainTimeInfo'}

def build():
    e=MinimapExporterBase();e.minimap_sprite_directory='assets/official/map16-alone-sprites'
    panels={};bindings={}
    paths=['UI/AloneWorldMap/AloneWorldMapEntryManager','UI/AloneWorldMap/AloneWorldMapEntryItemUI',
      'UI/AloneWorldMap/AloneWorldMapEntryItemContentMap16','UI/AloneWorldMap/AloneWorldTouchDialog',
      'Map/Map_16/Map16_DialogIcon']
    for path in paths:
        _,obj=Regions.locate(e,'Assets/Prefabs/'+path+'.prefab')
        name=path.rsplit('/',1)[-1];panels[name]=e.ui_node(obj);unique_sibling_paths(panels[name]);bindings[name]=[]
        for go,node_path,matrix,cs in components(e,obj):
            for c in cs:
                if c.type.name=='MonoBehaviour' and e.mono(c)[0] in CLASSES:
                    e.input(c);fields=e.schemas.read(c)
                    bindings[name].append(dict(path=node_path,script=e.mono(c)[0],fields=e.convert(fields,c)))
    cfg=next(r for r in json.loads((ROOT/'assets/official/adventure.json').read_bytes())['tables']['Map/map_world_alone'] if r['AloneMapEntryId']==16)
    from prepare_world_explore import group_icons
    bg=group_icons(e,[dict(Icon=cfg['BgIconPath'])])[cfg['BgIconPath']]
    e.keys.update(['alone_map_entry_name_16','alone_map_entry_desc_16','Mainland_16','TwoDayCountdown2','DayCountdown2'])
    e.keys.difference_update({'{0}','{0}%'})
    texts,languages=language(e.keys)
    missing=e.keys-set(texts)
    if missing:raise ValueError('Missing original alone-world language: '+str(missing))
    from official_tables import Constructors
    award_path=ROOT/'.tools/source-packages/official-data/def_award_handler.g.bytes'
    award=next(r for r in Constructors().table(award_path.read_bytes(),'TbDefAwardHandlerInfo') if r['SourceOperate']==222)
    if award!={'SourceOperate':222,'AwardStyle':2,'Title':''}:raise ValueError('Pinned alone-map award presentation changed')
    target=ROOT/'assets/official/map16-alone-ui.json'
    save(target,dict(manifest='854_156383',map=16,ui=panels,bindings=bindings,texts=texts,background=bg,award_handler=award))
    code_names=CLASSES|{'AloneWorldMapEntryItemContentCommonBase','AloneWorldTouchDialogBase','AloneWorldUtils','CultureTimeSpanArg','CanHandInItemNode','HandInItemNode','DefAwardHandler','AwardPanel','AwardUtils'}
    code={f'client-source/{n}.cs':digest(ROOT/'.tools/source-packages/official-code/client-source'/f'{n}.cs') for n in code_names if (ROOT/'.tools/source-packages/official-code/client-source'/f'{n}.cs').exists()}
    code['framework-firstpass-source/Framework/CultureTimeSpanArg.cs']=digest(ROOT/'.tools/source-packages/official-code/framework-firstpass-source/Framework/CultureTimeSpanArg.cs')
    code['def_award_handler.g.bytes']=digest(award_path)
    for name in ['SourceOperate','DefAwardHandlerInfo','EDefAwardStyle']:
        code[f'common-source/Common/{name}.cs']=digest(ROOT/'.tools/source-packages/official-code/common-source/Common'/f'{name}.cs')
    code['common-source/Common/Player.cs']=digest(ROOT/'.tools/source-packages/official-code/common-source/Common/Player.cs')
    save(ROOT/'assets/reference/official-map/alone-ui-16-source-manifest.json',dict(manifest='854_156383',map=16,inputs=e.inputs,resources=e.records,schemas=e.schemas.records,language_inputs=languages,code_inputs=code,outputs={target.relative_to(ROOT).as_posix():digest(target)},operation='Original private-map entry manager, whale card, landing dialog and managed state bindings'))
    print('Converted original whale island entry and landing dialog')

if __name__=='__main__':build()
