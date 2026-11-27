"""Prepare an original item icon and metadata in an isolated source project for native treasure UI verification."""
import argparse,json,hashlib,shutil
from pathlib import Path
from official_source import ROOT,OfficialSource
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_treasure_icon_native import TreasureIconBinding,prepare_cache,BUNDLE
from prepare_current_partner_native import PartnerNativeRecovery,documents
from native_spine_assets import guid
from npc_travel_language import language
from home_native_assets import write_staged_assets
from validation_workspace import require_temporary_unity_project

def prepare_texts(work):
    if not work.resolve().is_relative_to(ROOT/'output/validation'):raise ValueError('Use owned temporary validation outputs')
    proof=json.loads((ROOT/'assets/reference/treasure-icon-native-source-manifest.json').read_bytes())
    script=guid(ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/WorldMultiLanguageText.cs.meta')
    keys={'treasure_common_unactivate','{0}'}
    for name,expected in proof['outputs'].items():
        if not name.endswith('.prefab'):continue
        path=ROOT/name
        if hashlib.sha256(path.read_bytes()).hexdigest()!=expected:raise ValueError('Original icon prefab changed')
        for doc in documents(path.read_text('utf8')).values():
            fields=doc.get('MonoBehaviour',{})
            if fields.get('m_Script',{}).get('guid')==script and fields.get('myKey'):keys.add(fields['myKey'])
    texts,sources=language(keys,OfficialSource())
    data=json.loads((work/'probe.json').read_bytes())
    data['audio']=proof['audio']
    data['texts']=[dict(Key=key,Value=value) for key,value in sorted(texts.items())];data['language']=sources
    (work/'probe.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf8')

def prepare(project,work):
    project=require_temporary_unity_project(project);work=work.resolve()
    if not work.is_relative_to(ROOT/'output/validation'):raise ValueError('Use owned temporary validation outputs')
    recovery=PartnerNativeRecovery(native_cache=prepare_cache(),export_name='native-export',bundle=BUNDLE)
    try:
        binding=TreasureIconBinding(work,recovery=recovery,guid_prefix='zjcs:treasure-icon-validation:')
        code=recovery.apk.source.prepared_path/'official-code';constructors=Constructors(code)
        source=recovery.apk.asset('defaultpackage_assets_config_luban.bundle','/item.g.bytes')
        raw=source.read().m_Script.encode('utf8','surrogateescape');rows=constructors.table(raw,'TbItemInfo')
        row,=[r for r in rows if r['ClassId']==61001]
        if row['ItemType']!=305 or row['IconEffect']:raise ValueError('Reinspect original icon probe dependencies')
        manifest=recovery.apk.source.manifest_bytes('DefaultPackage',recovery.apk.archive)
        record,=[r for r in read_asset_manifest(manifest) if r['address']=='Assets/Image/'+row['RawIcon']]
        sprite,=[p.deref() for name,p in recovery.apk.container(record['bundle']) if name.casefold()==record['path'].casefold() and p.deref().type.name=='Sprite']
        pointer=binding.original_asset(sprite);index=0
        while index<len(binding.queue):
            _,_,data=binding.queue[index]
            if isinstance(data,dict):updated=binding.replace(data);data.clear();data.update(updated)
            index+=1
        staged=work/'probe-assets';outputs=write_staged_assets(binding.queue,staged)
        destination=project/'Assets/TreasureIconValidationProbe';destination.mkdir(parents=True,exist_ok=True)
        for name in outputs:
            target=destination/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(staged/name,target)
        payload=dict(item=row,sprite=pointer,source=recovery.apk.provenance(source),table_sha256=hashlib.sha256(raw).hexdigest(),
            manifest_asset=record,resources=binding.proof,outputs=outputs)
        (work/'probe.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n','utf8')
    finally:recovery.apk.close()
    prepare_texts(work)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path);p.add_argument('--work',type=Path,required=True);p.add_argument('--texts-only',action='store_true')
    a=p.parse_args()
    if a.texts_only:prepare_texts(a.work)
    else:prepare(a.project,a.work)
