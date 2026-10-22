"""Original character overview bindings and referenced property row prefab."""
import hashlib,json,re,struct
from prepare_profession_ui import ProfessionExporter,sha
from prepare_map_regions import ROOT,ART,save
from map_timeline_assets import pointer
from official_resource_manifest import write_resources


def build():
    e=ProfessionExporter();bindings={};inputs=[];row=None;row_fields={}
    selected=['CharacterPanelPropPanel','CharacterDetailPropsDialogOverallContent','PlayerLevelText','PlayerExpSlider','PlayerSubRankDivisionText','PlayerSubRankImage','PlayerAnimationAppearance','Slider']
    for o in e.apk.objects('defaultpackage_assets_prefabs_ui_character.bundle'):
        if o.type.name!='MonoBehaviour' or e.mono(o)[0] not in selected:continue
        cls=e.mono(o)[0];path=e.object_path(o.read(check_read=False).m_GameObject)
        if not path.startswith('/CharacterPanel/'):continue
        if cls=='Slider':
            raw=o.get_raw_data();at=len(raw)-48
            if struct.unpack_from('<i',raw,len(raw)-4)[0]!=0:raise ValueError('Original character Slider has unexpected callbacks')
            def rect(offset):
                file_id,path_id=struct.unpack_from('<iq',raw,offset)
                return e.target(o,dict(m_FileID=file_id,m_PathID=path_id)) if path_id else None
            fields=dict(mFillRect=rect(at),mHandleRect=rect(at+12),mDirection=struct.unpack_from('<i',raw,at+24)[0],mMinValue=struct.unpack_from('<f',raw,at+28)[0],mMaxValue=struct.unpack_from('<f',raw,at+32)[0],mWholeNumbers=bool(raw[at+36]),mValue=struct.unpack_from('<f',raw,at+40)[0])
        else:fields=e.fields(o)
        inputs.append(dict(e.apk.provenance(o),component=cls,sha256=hashlib.sha256(o.get_raw_data()).hexdigest()))
        def convert(v):
            if isinstance(v,dict):
                if 'm_PathID' in v:return e.target(o,v) if v['m_PathID'] else None
                return {k:convert(x) for k,x in v.items()}
            if isinstance(v,list):return [convert(x) for x in v]
            return v
        bindings[cls]={'path':path,'fields':{k:convert(v) for k,v in fields.items() if k not in ['m_GameObject','m_Script','m_Name','m_Enabled']}}
        if cls=='CharacterDetailPropsDialogOverallContent':
            obj=pointer(fields['PropPrefab'],o);row=e.ui_node(obj)
            c=next(p.component.deref() for p in obj.read().m_Component if p.component.type.name=='MonoBehaviour' and e.mono(p.component.deref())[0]=='PlayerPropItem')
            row_fields={k:e.target(c,v) for k,v in e.fields(c).items() if isinstance(v,dict) and 'm_PathID' in v and k not in ['m_GameObject','m_Script']}
    if row is None:raise ValueError('Original character property row missing')
    rules=json.loads((ART/'rules.json').read_text(encoding='utf8'))
    rules.update(json.loads((ART/'fidelity-rules.json').read_text(encoding='utf8')))
    source=ROOT/'.tools/source-packages/official-code';common=source/'common-source/Common'
    enum=(common/'SubRank.cs').read_text(encoding='utf-8-sig');ids={name:index for index,name in enumerate(re.findall(r'\b(\w+)\s*,?',enum.split('{',1)[1].rsplit('}',1)[0]))}
    ext=(common/'EnumExt.cs').read_text(encoding='utf-8-sig').split('public static Rank ToRank(this SubRank subRank)',1)[1].split('default:',1)[0]
    rank_ids={name:index for index,name in enumerate(re.findall(r'\b(\w+)\s*,?',(common/'Rank.cs').read_text(encoding='utf-8-sig').split('{',1)[1].rsplit('}',1)[0]))}
    ranks={ids[name]:rank for cases,rank in re.findall(r'((?:\s*case SubRank\.\w+:)+)\s*return Rank\.(\w+);',ext) for name in re.findall(r'SubRank\.(\w+)',cases)}
    rank_data={};manifest_rows=[]
    from official_manifest import read_asset_manifest
    for n in e.apk.archive.namelist():
        if n.startswith('assets/yoo/') and n.endswith('_854_156383.v3.bytes'):manifest_rows.extend(read_asset_manifest(e.apk.archive.read(n)))
    for cfg in rules['player_subrank']:
        sid=cfg['SubRank'];rank=ranks[sid]
        sub='None' if sid in [0,1] else ['One','Two','Three','Four','Five','Six','Seven','Eight','Nine'][(sid-23)%9] if sid>=23 else ['Low','Middle','Hight'][(sid-2)%3]
        name=cfg['Icon'];asset=next(r for r in manifest_rows if r['path'].lower().endswith('/itemicon/characterrank/'+name.lower()+'.png'))
        spr=next(ptr for _,ptr in e.apk.container(asset['bundle']) if ptr.type.name=='Sprite' and ptr.read().m_Name==name)
        icon=e.sprite(spr)
        rank_data[str(sid)]=dict(rank=rank_ids[rank],name=e.text('Rank.'+rank),sub=e.text('SubRankSubLevel.'+sub) if sub!='None' else '',icon=icon)
    props=json.loads((ART/'character-properties.json').read_text(encoding='utf8'))['properties']
    texts={key:e.text(key) for key in ['player_exp_max_title','player_exp_max_desc1','player_exp_max_desc2','ui_profession_unlock_level_tip','function_limit_season']}
    season_names={str(cfg['Rank']):e.text('astrological_season_'+str(cfg['Season'])+'_name') for cfg in rules['astrological_season_config']}
    save(ART/'character-overview.json',dict(bindings=bindings,row=row,row_fields=row_fields,ranks=rank_data,properties=props,texts=texts,season_names=season_names))
    receipt=ROOT/'assets/reference/character-overview-source-manifest.json'
    save(receipt,dict(manifest='854_156383',inputs=inputs,resources=e.records,layout_fields=e.layout_inputs,language_sources=e.language_sources,schemas=e.schemas.records,
        code={n:sha(source/n) for n in ['client-source/CharacterPanelPropPanel.cs','client-source/CharacterDetailPropsDialogOverallContent.cs','client-source/PlayerPropItem.cs','client-source/PlayerLevelText.cs','client-source/PlayerExpSlider.cs','client-source/PlayerSubRankDivisionText.cs','client-source/PlayerSubRankImage.cs','common-source/Common/EnumExt.cs','common-source/Common/PlayerBlessLevelUtils.cs','common-source/Common/SystemLevelLimitInfoParser.cs','common-source/Common/LanguageUtils.cs']},
        outputs={'assets/official/character-overview.json':sha(ART/'character-overview.json')}))
    central=ROOT/'assets/reference/official-city/source-manifest.json';m=json.loads(central.read_text(encoding='utf8'));m['resources'].update(e.records);m.setdefault('explicit_data_resources',[]).append('character-overview.json');m['explicit_data_resources']=sorted(set(m['explicit_data_resources']));m['data_sha256']['character-overview.json']=sha(ART/'character-overview.json');write_resources(m);save(central,m)
    print('Original character overview',len(bindings),'bindings',len(rank_data),'ranks',len(e.records),'resources')


if __name__=='__main__':build()
