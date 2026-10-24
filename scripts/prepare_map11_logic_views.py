"""Prove map 11's class 6533 is a logic stage with no original display binding."""
import hashlib,json,re
from prepare_map_regions import Regions,ART,REF,ROOT,save
from official_tables import Constructors
from official_manifest import read_asset_manifest


def build():
    e=Regions();c=Constructors()
    mapping={m[2]:m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',c.source('Tables'))}
    obj=e.apk.asset('defaultpackage_assets_config_luban_map.bundle','/Map/entity_map_display.g.bytes');e.input(obj)
    raw=obj.read().m_Script.encode('utf8','surrogateescape')
    rows=c.table(raw,mapping['Map/entity_map_display'])
    matches=[r for r in rows if int(r['EntityClassId'])==6533]
    names=[];manifests={}
    for name in e.apk.archive.namelist():
        if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes'):
            blob=e.apk.archive.read(name);manifests[name]=hashlib.sha256(blob).hexdigest()
            names.extend(r['path'] for r in read_asset_manifest(blob) if re.search(r'(^|/)6533(?:_\d+)?_view\.prefab$',r['path']))
    source=json.loads((REF/'entities-source-manifest.json').read_text(encoding='utf8'))
    selected=source['selected']['0/6533'];record=source['inputs'][selected['path']]
    native=e.apk.asset(record['bundle'],'/'+selected['path'].rsplit('/',1)[-1]);e.input(native)
    assert hashlib.sha256(native.read().m_Script.encode('utf8','surrogateescape')).hexdigest()==record['sha256']
    template=json.loads((ART/'map-entities.json').read_text(encoding='utf8'))['templates']['6533']
    assert not matches and not names and int(template['WorldStageComponent']['StageWorldType'])==208
    assert not any('View' in name or 'Model' in name or 'Display' in name for name in template)
    placements=json.loads((ART/'regions/11.json').read_text(encoding='utf8'))['placement']['entities']
    entities=[int(v['id']) for v in placements if int(v['entity'])==6533]
    catalog={'manifest':'854_156383','map':11,'classes':{'6533':{'empty_idle_states':[0,1],'entities':entities,'display_path':'','reason':'Original logic stage has no display-table binding, no class view prefab, and no visual component; its results spawn another entity and update its paired stage.'}}}
    save(ART/'map11-logic-views.json',catalog)
    save(REF/'logic-11-source-manifest.json',{'manifest':'854_156383','map':11,'class':6533,'display_table':{'source':e.apk.provenance(obj),'raw_sha256':hashlib.sha256(raw).hexdigest(),'decoded_rows':len(rows),'matching_rows':matches,'constructor':mapping['Map/entity_map_display']},'five_package_asset_manifest_sha256':manifests,'matching_class_view_assets':names,'template_source':dict(record,path=selected['path']),'template':template,'entities':entities,'inputs':e.source_records,'outputs':{'assets/official/map11-logic-views.json':hashlib.sha256((ART/'map11-logic-views.json').read_bytes()).hexdigest()},'classification':'Source-defined logic-only stage. This is not a missing official bitmap or a general exception for undrawn entities.'})
    print('Map 11 logic view: class 6533; original display rows',len(matches),'view assets',len(names),'entities',entities)


if __name__=='__main__':build()
