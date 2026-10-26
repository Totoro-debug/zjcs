"""Decode original anniversary map construction and festival shop tables."""
import hashlib
from pathlib import Path
from official_apk import Apk
from official_tables import Constructors, SOURCE
from prepare_official_city import Exporter
from prepare_map_regions import ART, REF, save

def build():
    apk=Apk(); decoder=Constructors(); tables={}; records={}
    for name, cls in [('anniversary_map_build','TbAnniversaryMapBuildInfo'),
                      ('anniversary_map_build_item','TbAnniversaryMapBuildItemInfo'),
                      ('activity_festival_shop','TbActivityFestivalShopInfo')]:
        obj=apk.asset('config_luban','/'+name+'.g.bytes')
        raw=obj.read().m_Script.encode('utf8','surrogateescape')
        tables[name]=decoder.table(raw,cls)
        records[name]=dict(apk.provenance(obj),bytes=len(raw),consumed_bytes=len(raw),
            sha256=hashlib.sha256(raw).hexdigest(),constructor=cls,
            constructor_sha256=hashlib.sha256((SOURCE/(cls+'.cs')).read_bytes()).hexdigest())
        print(name,len(tables[name]),flush=True)
    rules=__import__('json').loads((ART/'rules.json').read_text(encoding='utf8'))
    items={int(row['ClassId']):row for row in rules['item']}
    requested=set()
    for cfg in tables['anniversary_map_build_item']:
        for field in ['SubmitItemCountDict','SubmitRewardItems']:requested.update(map(int,cfg[field]))
    shop_ids={int(row['ShopId']) for row in tables['activity_festival_shop'] if int(row['ActivityId'])==217}
    goods_ids={int(id) for row in rules['shop'] if int(row['Id']) in shop_ids for id in row['FixedGoodsList']}
    requested.update(int(row['ItemId']) for row in rules['shop_goods'] if int(row['Id']) in goods_ids)
    exporter=Exporter(); icons={}; missing=[]; bundles={}
    for id in sorted(requested):
        icon=items[id]['RawIcon']; prefix='defaultpackage_assets_image_'+icon.rsplit('/',1)[0].replace('/','_').lower()
        asset_key='defaultpackage_assets_image_'+icon.replace('/','_').lower();found=None
        for bundle in exporter.apk.index:
            if not (bundle.startswith(prefix) or asset_key.startswith(bundle.removesuffix('.bundle')+'_')):continue
            if bundle not in bundles:
                try:bundles[bundle]={path.rsplit('/',1)[-1].rsplit('.',1)[0].lower():ptr for path,ptr in exporter.apk.container(bundle) if ptr.type.name=='Sprite'}
                except FileNotFoundError:bundles[bundle]={}
            found=bundles[bundle].get(icon.rsplit('/',1)[-1].lower())
            if found:break
        if found:icons[str(id)]=exporter.sprite(found)
        else:missing.append(dict(id=id,icon=icon))
    tables['icons']=icons
    save(ART/'map-anniversary.json',tables)
    save(REF/'anniversary-source-manifest.json',dict(manifest='854_156383',tables=records,
        script_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        resources=exporter.records,missing_icons=missing,
        outputs={'map-anniversary.json':hashlib.sha256((ART/'map-anniversary.json').read_bytes()).hexdigest()}))
    print('Anniversary icons',len(icons),'missing',missing,flush=True)

if __name__=='__main__':build()
